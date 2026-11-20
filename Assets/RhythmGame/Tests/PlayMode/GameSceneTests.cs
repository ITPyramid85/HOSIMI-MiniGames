using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RhythmGame.Tests
{
    public sealed class GameSceneTests
    {
        private string originalPlayer;

        [UnitySetUp]
        public IEnumerator OpenGameFromMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            originalPlayer = JsonUtility.ToJson(GameManager.Instance.Data.Player);
            Object.FindFirstObjectByType<MainMenuController>().StartGame();
            float timeout = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "Game" && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
            Assert.AreEqual(GameRunState.Ready, Object.FindFirstObjectByType<GameController>().State);
        }

        [UnityTearDown]
        public IEnumerator RestorePlayer()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            if (originalPlayer != null)
            {
                JsonUtility.FromJsonOverwrite(originalPlayer, GameManager.Instance.Data.Player);
                GameManager.Instance.Save();
            }
        }

        [UnityTest]
        public IEnumerator CountdownPauseResumeAndExitPreserveManagerAndDoNotAwardCoins()
        {
            GameManager manager = GameManager.Instance;
            GameController game = Object.FindFirstObjectByType<GameController>();
            Assert.IsTrue(game.GetComponentInChildren<MiniGameGuide>(true).gameObject.activeInHierarchy);
            Assert.IsFalse(game.transform.Find("Modal/Card/Body").gameObject.activeSelf);
            game.StartRun(); yield return new WaitForSecondsRealtime(.15f);
            game.TogglePause(); double time = game.SongTime;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(time, game.SongTime);
            game.TogglePause(); Assert.AreEqual(GameRunState.Playing, game.State);
            game.BackToMenu(); yield return new WaitForSecondsRealtime(.3f);
            Assert.AreSame(manager, GameManager.Instance);
            Assert.AreEqual(originalPlayer, JsonUtility.ToJson(manager.Data.Player));
        }

        [UnityTest]
        public IEnumerator EarlyPressDoesNotScoreAndBeginnerLightAccepts700MsReaction()
        {
            GameController game = Object.FindFirstObjectByType<GameController>();
            GameManager.Instance.Data.SetInputOffset(0);
            game.StartRun();
            yield return WaitForSong(game, 1.9);
            game.Press(5); game.Release(5);
            Assert.AreEqual(0, game.Session.JudgedCount);
            yield return WaitForLight(game);
            Assert.IsTrue(game.IsPadLit(5));
            double shown = game.SongTime;
            yield return WaitForSong(game, shown + .7);
            var area = game.GetComponentsInChildren<GameInputArea>().Single(p => p.PadIndex == 5);
            var input = new PointerEventData(EventSystem.current) { pointerId = 11, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(area.gameObject, input, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(area.gameObject, input, ExecuteEvents.pointerUpHandler);
            Assert.AreEqual(1, game.Session.JudgedCount); Assert.AreEqual(1, game.Session.Combo);
            Assert.AreEqual(100, game.Session.Health);
            Assert.AreEqual("GOOD", game.transform.Find("Layout/Track/Judgement").GetComponent<Text>().text);
            Assert.AreEqual("0 / 10", game.transform.Find("Layout/Health").GetComponent<Text>().text);
            Assert.AreEqual("MISS", game.transform.Find("Layout/HealthCaption").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator FullSongUsesSingleLightsThreeStagesThenRetriesFresh()
        {
            GameController game = Object.FindFirstObjectByType<GameController>();
            yield return new WaitForSecondsRealtime(1.5f);
            Capture("casual-guide-preview.png");
            var chart = GameManager.Instance.Data.Config.Chart(GameController.DefaultChartId);
            BeatmapData map = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text);
            double offset = GameManager.Instance.Data.Player.inputOffsetMs / 1000;
            game.StartRun();
            foreach (JudgementPoint point in map.Compile(chart))
            {
                yield return WaitForSong(game, point.Time + offset);
                yield return WaitForLight(game);
                Assert.IsTrue(game.IsPadLit(point.Pad));
                Assert.AreEqual(1, Enumerable.Range(0, 8).Count(p => game.IsPadLit(p)));
                if (point.Index == 1) { game.TogglePause(); game.TogglePause(); Capture("casual-play-preview.png"); }
                // Capturing a frame may take time; ordinary test reactions wait 150 ms.
                if (point.Index != 1) yield return WaitForSong(game, game.SongTime + .15);
                game.Press(point.Pad); game.Release(point.Pad);
            }
            float timeout = Time.realtimeSinceStartup + 6;
            while (game.State == GameRunState.Playing && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(GameRunState.Result, game.State);
            Assert.IsTrue(game.Result.Cleared); Assert.AreEqual(41, game.Result.Perfect);
            Assert.AreEqual(0, game.Result.Great);
            string body = game.transform.Find("Modal/Card/Body").GetComponent<Text>().text;
            StringAssert.DoesNotContain("HP", body); StringAssert.DoesNotContain("GREAT", body);
            Capture("casual-result-preview.png");
            PlaySession previous = game.Session;
            game.StartRun(); yield return null;
            Assert.AreNotSame(previous, game.Session); Assert.AreEqual(100, game.Session.Health);
            Assert.AreEqual(0, game.Session.JudgedCount);
            Assert.AreEqual(8, game.GetComponentsInChildren<GameInputArea>(true).Length);
        }

        [UnityTest]
        public IEnumerator PauseFreezesVisibleCueAndHoldingCannotScoreAnotherBeat()
        {
            GameController game = Object.FindFirstObjectByType<GameController>();
            GameManager.Instance.Data.SetInputOffset(0); game.StartRun();
            yield return WaitForLight(game);
            game.TogglePause(); double time = game.SongTime;
            yield return new WaitForSecondsRealtime(1);
            Assert.AreEqual(time, game.SongTime);
            game.TogglePause(); Assert.IsTrue(game.IsPadLit(5));
            game.Press(5);
            Assert.AreEqual(1, game.Session.JudgedCount);
            yield return WaitForSong(game, 4.1);
            game.Press(5);
            Assert.AreEqual(1, game.Session.JudgedCount);
            Assert.IsTrue(game.IsPadLit(6));
            game.Press(6); game.Release(6); game.Release(5);
            Assert.AreEqual(2, game.Session.JudgedCount);
        }

        [UnityTest]
        public IEnumerator TenMissesEndRunAndBarNeverDisplaysHP()
        {
            GameController game = Object.FindFirstObjectByType<GameController>();
            game.StartRun(); float timeout = Time.realtimeSinceStartup + 30;
            while (game.State == GameRunState.Playing && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(GameRunState.Result, game.State);
            Assert.AreEqual(10, game.Result.Miss); Assert.AreEqual(0, game.Result.RemainingHealth);
            Assert.AreEqual(0, game.Result.TotalCoins);
            Assert.AreEqual("10 / 10", game.transform.Find("Layout/Health").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator KeyboardTargetsTheLitPadAndWrongPadDoesNotStealIt()
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                GameController game = Object.FindFirstObjectByType<GameController>();
                GameManager.Instance.Data.SetInputOffset(0); game.StartRun();
                yield return WaitForLight(game);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
                yield return null; yield return null;
                Assert.AreEqual(0, game.Session.JudgedCount);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X, Key.Z));
                yield return null; yield return null;
                Assert.AreEqual(1, game.Session.JudgedCount);
                Assert.AreEqual(1, game.Session.Combo);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        private static IEnumerator WaitForLight(GameController game)
        {
            float timeout = Time.realtimeSinceStartup + 7;
            while (!game.ButtonIsLit && game.State == GameRunState.Playing && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(game.ButtonIsLit);
        }

        private static IEnumerator WaitForSong(GameController game, double target)
        {
            float timeout = Time.realtimeSinceStartup + 10;
            while (game.SongTime < target && game.State == GameRunState.Playing && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(GameRunState.Playing, game.State);
            Assert.GreaterOrEqual(game.SongTime, target);
        }

        internal static void Capture(string name)
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Camera camera = Camera.main;
            var target = new RenderTexture(1920, 1080, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", name), texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            target.Release(); Object.Destroy(target); Object.Destroy(texture);
        }
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class MochiTests
    {
        private string originalPlayer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            originalPlayer = JsonUtility.ToJson(GameManager.Instance.Data.Player);
            GameManager.Instance.Data.SetLanguage(GameLanguage.SimplifiedChinese);
        }

        // Rounds pay real drumsticks into the save; put the player back afterwards.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RhythmGameData data = GameManager.Instance.Data;
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
            yield return null;
        }

        private static IEnumerator LoadFromMenu()
        {
            Object.FindFirstObjectByType<MainMenuController>().OpenMiniGames();
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "MiniGames" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Object.FindFirstObjectByType<MiniGamesMenu>().transform.Find("Game_mochi").GetComponent<Button>().onClick.Invoke();
            while (SceneManager.GetActiveScene().name != "Mochi" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual("Mochi", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator OnlyPadEightIsJudgedAndTapsAnywhereCount()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<MochiController>();
            Assert.AreEqual(MiniGameState.Ready, game.State);
            Image[] diagram = game.transform.Find("Modal/Card/PadDiagram").GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Pad")).ToArray();
            Assert.AreEqual(Palette.Sun, diagram.Single(i => i.name == "Pad8").color, "The prompt lights pad 8.");
            Assert.AreEqual(1, diagram.Count(i => i.color == Palette.Sun));
            StringAssert.Contains("8", game.transform.Find("Modal/Card/Body").GetComponent<Text>().text);
            yield return new WaitForSecondsRealtime(1.5f);
            GameSceneTests.Capture("mochi-prompt-preview.png");

            game.StartRound(1);
            for (int pad = 0; pad < 7; pad++) game.Press(pad);
            Assert.AreEqual(0, game.Score.Miss, "Other pads are neither judged nor penalised (plan §3).");
            ExecuteEvents.Execute(game.transform.Find("Field/TouchArea").gameObject,
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerDownHandler);
            Assert.AreEqual(1, game.Score.Miss, "A tap on the field is pad 8; with no flip near, it is a miss.");

            float deadline = Time.realtimeSinceStartup + 5;
            while (game.SongTime < 1.85 && Time.realtimeSinceStartup < deadline) yield return null;
            RectTransform arm = (RectTransform)game.transform.Find("Field/Arm");
            Assert.Greater(arm.anchoredPosition.x, -150, "Grandma's hand is reaching in ahead of the first flip.");
            GameSceneTests.Capture("mochi-play-preview.png");
            game.FinishRound();
            Assert.AreEqual(MiniGameState.Result, game.State);
        }

        [UnityTest]
        public IEnumerator PerfectPlayClearsStageTenWithTheBonusAndKeepsTheRecord()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<MochiController>();
            game.StartRound(3);
            double offset = AudioLatencyMonitor.EnsureInstance().EffectiveOffsetMs / 1000;
            long before = GameManager.Instance.Data.Player.coins;
            foreach (BeatCue cue in game.Judge.Cues.ToArray())
                if (cue.Kind != CueKind.Forbidden) game.PressAt(cue.Pad, cue.Time + offset);
            game.AdvanceTo(MochiChart.RunSeconds + 1);
            Assert.AreEqual(MiniGameState.Result, game.State, "Surviving the minute opens the clear dialog.");
            Assert.IsTrue(game.Result.Cleared);
            Assert.AreEqual(1, game.Result.Stage);
            Assert.AreEqual(DrumstickReward.For(MochiChart.RunSeconds, 1, true), game.Result.Drumsticks);
            Assert.AreEqual(before + game.Result.Drumsticks, GameManager.Instance.Data.Player.coins, "Paid into the shared balance.");
            Assert.AreEqual(1, GameManager.Instance.Data.Player.FindMiniGame(game.GameId).bestStage);
            string body = game.transform.Find("Modal/Card/Body").GetComponent<Text>().text;
            StringAssert.Contains("1:00", body, "Result: time played,");
            StringAssert.Contains(game.Result.Drumsticks.ToString("N0"), body, "and drumsticks (client reply v5 §1-3).");
            yield return new WaitForSecondsRealtime(1f);
            GameSceneTests.Capture("mochi-result-preview.png");

            game.transform.Find("Modal/Card/PrimaryButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(MiniGameState.Playing, game.State, "Play again starts straight away.");
            Assert.AreEqual(1, game.Stage.Number, "Each run starts again at stage 1.");
            game.FinishRound();
        }

        [UnityTest]
        public IEnumerator MissingEverythingEndsTheRunWhenTheLivesRunOut()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<MochiController>();
            game.StartRound(5);
            game.AdvanceTo(60);
            Assert.AreEqual(MiniGameState.Result, game.State);
            Assert.IsFalse(game.Result.Cleared);
            Assert.AreEqual(0, game.Score.Lives);
            Assert.AreEqual(1, game.Result.Stage);
            Assert.Less(game.PlayedSeconds, 60, "The run ends at the last miss, not where the clock was moved to.");
        }
    }
}
#endif

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
    public sealed class WhackAMoleTests
    {
        private string originalPlayer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            originalPlayer = JsonUtility.ToJson(GameManager.Instance.Data.Player);
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
            Object.FindFirstObjectByType<MiniGamesMenu>().transform.Find("Game_whack").GetComponent<Button>().onClick.Invoke();
            while (SceneManager.GetActiveScene().name != "WhackAMole" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual("WhackAMole", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator PromptShowsTheStageOnePadsAndTouchesAreJudged()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<WhackAMoleController>();
            Assert.AreEqual(MiniGameState.Ready, game.State);
            Image[] diagram = game.transform.Find("Modal/Card/PadDiagram").GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Pad")).ToArray();
            Assert.AreEqual(3, diagram.Count(i => i.color == Palette.Sun), "Stage 1 lights the bottom three pads.");

            game.StartRound(1);
            Assert.AreEqual(MiniGameState.Playing, game.State);
            var hole = game.transform.Find("Field/Hole4").gameObject;
            ExecuteEvents.Execute(hole, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerDownHandler);
            Assert.AreEqual(1, game.Score.Miss, "Whack-a-mole judges every pad: an empty hole is a miss.");

            float deadline = Time.realtimeSinceStartup + 5;
            while (game.SongTime < 2.05 && Time.realtimeSinceStartup < deadline) yield return null;
            BeatCue first = game.Judge.Cues[0];
            Assert.Greater(game.transform.Find("Field/Hole" + (first.Pad + 1) + "/Mouth/Mole").GetComponent<RectTransform>().anchoredPosition.y, -128,
                "The first mole has risen by its beat.");
            GameSceneTests.Capture("whack-preview.png");
            game.FinishRound();
            Assert.AreEqual(MiniGameState.Result, game.State);
        }

        [UnityTest]
        public IEnumerator PerfectPlayClearsStageTenWithTheBonusAndKeepsTheRecord()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<WhackAMoleController>();
            game.StartRound(3);
            double offset = AudioLatencyMonitor.EnsureInstance().EffectiveOffsetMs / 1000;
            long before = GameManager.Instance.Data.Player.coins;
            foreach (BeatCue cue in game.Judge.Cues.ToArray())
                if (cue.Kind != CueKind.Forbidden) game.PressAt(cue.Pad, cue.Time + offset);
            game.AdvanceTo(StageTable.ClearSeconds + 1);
            Assert.AreEqual(MiniGameState.Result, game.State, "Surviving to the end of stage 10 finishes the run.");
            Assert.IsTrue(game.Result.Cleared);
            Assert.AreEqual(10, game.Result.Stage);
            Assert.AreEqual(DrumstickReward.For(StageTable.ClearSeconds, 10, true), game.Result.Drumsticks);
            Assert.AreEqual(before + game.Result.Drumsticks, GameManager.Instance.Data.Player.coins, "Paid into the shared balance.");
            Assert.AreEqual(10, GameManager.Instance.Data.Player.FindMiniGame(game.GameId).bestStage);
            string body = game.transform.Find("Modal/Card/Body").GetComponent<Text>().text;
            StringAssert.Contains("8:00", body, "Result: time played,");
            StringAssert.Contains("10", body, "stage reached");
            StringAssert.Contains(game.Result.Drumsticks.ToString("N0"), body, "and drumsticks (client reply v5 §1-3).");
            yield return new WaitForSecondsRealtime(1f);
            GameSceneTests.Capture("whack-result-preview.png");

            game.transform.Find("Modal/Card/PrimaryButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(MiniGameState.Playing, game.State, "Play again starts straight away.");
            Assert.AreEqual(1, game.Stage.Number, "Each run starts again at stage 1.");
            game.FinishRound();
        }

        [UnityTest]
        public IEnumerator MissingEverythingEndsTheRunWhenTheLivesRunOut()
        {
            yield return LoadFromMenu();
            var game = Object.FindFirstObjectByType<WhackAMoleController>();
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

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class DrivingNailTests
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RhythmGameData data = GameManager.Instance.Data;
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuOpensNailDrivingAndHitsDrainTheCounter()
        {
            Object.FindFirstObjectByType<MainMenuController>().OpenMiniGames();
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "MiniGames" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Object.FindFirstObjectByType<MiniGamesMenu>().transform.Find("Game_nails").GetComponent<Button>().onClick.Invoke();
            while (SceneManager.GetActiveScene().name != "DrivingNail" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual("DrivingNail", SceneManager.GetActiveScene().name);

            var game = Object.FindFirstObjectByType<DrivingNailController>();
            Assert.AreEqual(MiniGameState.Ready, game.State);
            Image[] diagram = game.transform.Find("Modal/Card/PadDiagram").GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Pad")).ToArray();
            Assert.AreEqual(Palette.Sun, diagram.Single(i => i.name == "Pad7").color);
            StringAssert.Contains("7", game.transform.Find("Modal/Card/Body").GetComponent<Text>().text);

            game.StartRound(1);
            Text count = game.transform.Find("Field/Count").GetComponent<Text>();
            deadline = Time.realtimeSinceStartup + 5;
            while (game.SongTime < 1.6 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual("1 / 1", count.text, "One strike on the first nail.");
            StringAssert.Contains("剩余", game.transform.Find("Field/Remaining").GetComponent<Text>().text);
            double offset = AudioLatencyMonitor.EnsureInstance().EffectiveOffsetMs / 1000;
            game.PressAt(game.Judge.Cues[0].Pad, game.Judge.Cues[0].Time + offset);
            Assert.AreEqual("0 / 1", count.text, "One rimshot seats the nail.");
            Assert.AreEqual(MiniGameScore.MaxHalves, game.Score.Halves);
            game.FinishRound();

            game.StartRound(2);
            deadline = Time.realtimeSinceStartup + 5;
            while (game.SongTime < 1.6 && Time.realtimeSinceStartup < deadline) yield return null;
            BeatCue first = game.Judge.Cues[0];
            game.AdvanceTo(first.Time + first.GoodMs / 1000 + .05);
            Assert.AreEqual(MiniGameScore.MaxHalves - 1, game.Score.Halves, "Missing the beat bends the nail and costs half a heart.");
            Assert.AreEqual("1 / 1", count.text, "The next nail is waiting, not already struck.");
            BeatCue next = game.Judge.Cues.First(c => c.Variant != first.Variant);
            Assert.AreEqual(first.Time + NailChart.RecoverBeats * NailChart.BeatSeconds, next.Time, .05, "The next nail waits, so the player can strike it.");
            game.AdvanceTo(next.Time - .05);
            Assert.AreEqual(MiniGameScore.MaxHalves - 1, game.Score.Halves, "The bent nail's other strikes are not further misses.");
            game.FinishRound();
            Assert.AreEqual(MiniGameState.Result, game.State);
            yield return null;
        }
    }
}
#endif

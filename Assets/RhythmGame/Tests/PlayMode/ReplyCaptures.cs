#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    // Korean screenshots for the reply to the client's v5 review, written to <project>/Showcase/v2/ko.
    // Explicit: unity test <project> --mode PlayMode --filter RhythmGame.Tests.ReplyCaptures
    [Explicit, Category("Showcase")]
    public sealed class ReplyCaptures
    {
        private const string Folder = "Showcase/v2/ko";
        private string originalPlayer;
        private RhythmGameData data;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", Folder));
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            data = GameManager.Instance.Data;
            originalPlayer = JsonUtility.ToJson(data.Player);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(PlayerData.Create(data.Config)), data.Player);
            data.Player.coins = 1500;
            data.Player.miniGames.Add(new MiniGameRecord { gameId = "mochi", bestStage = 6, bestSeconds = 262, plays = 4, totalDrumsticks = 210 });
            data.SetLanguage(GameLanguage.Korean);
            data.Save();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
        }

        private static IEnumerator Shot(string name, float settle)
        {
            yield return new WaitForSecondsRealtime(settle);
            GameSceneTests.Capture(Folder + "/" + name + ".png");
        }

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureReplyScreens()
        {
            yield return Load("MainMenu");
            yield return Shot("01-main", 2.5f);

            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenSettings();
            yield return Shot("02-settings", .8f);
            menu.GetComponentInChildren<SettingsPopup>().Close();

            yield return Load("MiniGames");
            yield return Shot("03-minigames", .6f);

            yield return Load("Mochi");
            var mochi = Object.FindFirstObjectByType<MochiController>();
            yield return Shot("04-mochi-prompt", 1.5f);
            mochi.StartRound(7);
            float deadline = Time.realtimeSinceStartup + 6;
            while (mochi.SongTime < 1.85 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Shot("05-mochi-play", 0);
            double offset = AudioLatencyMonitor.EnsureInstance().EffectiveOffsetMs / 1000;
            foreach (BeatCue cue in mochi.Judge.Cues.ToArray())
                if (cue.Kind != CueKind.Forbidden) mochi.PressAt(cue.Pad, cue.Time + offset);
            mochi.AdvanceTo(StageTable.ClearSeconds + 1);
            yield return Shot("06-mochi-clear", 1.5f);

            yield return Load("WhackAMole");
            var whack = Object.FindFirstObjectByType<WhackAMoleController>();
            whack.StartRound(9);
            deadline = Time.realtimeSinceStartup + 6;
            while (whack.SongTime < 2.02 && Time.realtimeSinceStartup < deadline) yield return null;
            whack.Press(whack.Judge.Cues[0].Pad);
            yield return Shot("07-whack-play", .05f);
            // Hit the first few, then stop: the lives run out and the run ends where the player got to.
            foreach (BeatCue cue in whack.Judge.Cues.Where(c => c.Time < 100 && c.Kind != CueKind.Forbidden).ToArray())
                whack.PressAt(cue.Pad, cue.Time + offset);
            whack.AdvanceTo(400);
            yield return Shot("08-whack-over", 1.5f);
        }
    }
}
#endif

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
    // Screenshots for the client review deck, written to <project>/Showcase. Explicit: run on demand with
    // unity test <project> --mode PlayMode --filter RhythmGame.Tests.ShowcaseCaptures
    [Explicit, Category("Showcase")]
    public sealed class ShowcaseCaptures
    {
        private string originalPlayer, folder;
        private RhythmGameData data;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            data = GameManager.Instance.Data;
            originalPlayer = JsonUtility.ToJson(data.Player);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(PlayerData.Create(data.Config)), data.Player);
            data.Player.coins = 1500;
            data.Save();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
            AudioLatencyMonitor.EnsureInstance().EditorOutputOverride = null;
        }

        private IEnumerator Shot(string name, float settle = .8f)
        {
            yield return new WaitForSecondsRealtime(settle);
            GameSceneTests.Capture(folder + "/" + name + ".png");
        }

        private static IEnumerator Menu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureEveryReviewScreen() { return CaptureAll(GameLanguage.SimplifiedChinese, "Showcase"); }

        // Korean deck for the Korean client: same screens, game language switched first.
        [UnityTest]
        public IEnumerator CaptureEveryReviewScreenInKorean() { return CaptureAll(GameLanguage.Korean, "Showcase/ko"); }

        private IEnumerator CaptureAll(GameLanguage language, string target)
        {
            folder = target;
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", folder));
            data.SetLanguage(language);
            yield return Menu();
            yield return Shot("01-main-menu", 2.5f);

            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenCharacters();
            CharacterPopup characters = menu.GetComponentInChildren<CharacterPopup>();
            yield return Shot("02-character-owned");
            characters.Next();
            yield return Shot("03-character-locked", .6f);
            characters.PurchaseOrEquip();
            yield return Shot("04-character-got", .6f);
            characters.Close();

            yield return Menu();
            AudioLatencyMonitor monitor = AudioLatencyMonitor.EnsureInstance();
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenSettings();
            menu.GetComponentInChildren<SettingsPopup>().OpenLatency();
            LatencyCalibrationPopup settings = menu.GetComponentInChildren<LatencyCalibrationPopup>();
            yield return Shot("05-bluetooth-idle");
            settings.transform.Find("Card/TestButton").GetComponent<Button>().onClick.Invoke();
            LatencyCalibration test = settings.Calibration;
            for (int beat = 2; beat < 12; beat++) settings.TapAt(test.FirstBeatTime + beat * test.IntervalSeconds + .175 + (beat % 3 - 1) * .012);
            settings.transform.Find("Card/ApplyResultButton").GetComponent<Button>().onClick.Invoke();
            yield return Shot("06-bluetooth-result", .3f);
            settings.Close();

            SceneManager.LoadScene("WhackAMole");
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "WhackAMole" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Shot("07-whack-prompt", 2f);
            var game = Object.FindFirstObjectByType<WhackAMoleController>();
            game.StartRound(3);
            deadline = Time.realtimeSinceStartup + 6;
            while (game.SongTime < 2.02 && Time.realtimeSinceStartup < deadline) yield return null;
            game.Press(game.Judge.Cues[0].Pad);
            yield return Shot("08-whack-play", .05f);
            double offset = monitor.EffectiveOffsetMs / 1000;
            foreach (BeatCue cue in game.Judge.Cues.ToArray())
                if (cue.Kind != CueKind.Forbidden && !game.Judge.IsResolved(cue)) game.PressAt(cue.Pad, cue.Time + offset);
            game.AdvanceTo(StageTable.ClearSeconds + 1);
            yield return Shot("09-whack-result", 1.5f);
        }
    }
}
#endif

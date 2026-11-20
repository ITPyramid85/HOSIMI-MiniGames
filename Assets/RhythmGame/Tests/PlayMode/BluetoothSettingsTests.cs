#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class BluetoothSettingsTests
    {
        private string original;
        private AudioLatencyMonitor monitor;
        private RhythmGameData data;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            data = GameManager.Instance.Data; original = JsonUtility.ToJson(data.Player);
            data.SetBluetoothCompensation(false, 0); data.SetInputOffset(-25);
            monitor = AudioLatencyMonitor.EnsureInstance();
            monitor.EditorOutputOverride = AudioOutputKind.Other; monitor.Refresh();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            JsonUtility.FromJsonOverwrite(original, data.Player); data.Save();
            monitor.EditorOutputOverride = null; monitor.Refresh();
        }
        private LatencyCalibrationPopup Open()
        {
            // Calibration now lives one level down, behind the settings screen's Bluetooth entry.
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenSettings();
            menu.GetComponentInChildren<SettingsPopup>().OpenLatency();
            return Object.FindFirstObjectByType<LatencyCalibrationPopup>();
        }
        private static Button Button(LatencyCalibrationPopup popup, string name) { return popup.transform.Find("Card/" + name).GetComponent<Button>(); }
        private static Slider Slider(LatencyCalibrationPopup popup) { return popup.GetComponentInChildren<Slider>(); }

        [UnityTest]
        public IEnumerator SavedDelayAppliesOnlyOnBluetoothAndCancelDiscardsDraft()
        {
            var popup = Open(); Button(popup, "EnableBluetoothButton").onClick.Invoke(); Slider(popup).value = 30;
            Button(popup, "SaveButton").onClick.Invoke();
            Assert.IsFalse(popup.IsOpen);
            Assert.AreEqual(-25, monitor.EffectiveOffsetMs);
            var loaded = RhythmGameData.Open(data.SavePath);
            Assert.IsTrue(loaded.Settings.bluetoothCompensationEnabled); Assert.AreEqual(300, loaded.Settings.bluetoothDelayMs);
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            Assert.AreEqual(275, monitor.EffectiveOffsetMs);
            popup = Open(); Slider(popup).value = 90; Button(popup, "EnableBluetoothButton").onClick.Invoke(); popup.Close();
            Assert.AreEqual(300, data.Settings.bluetoothDelayMs); Assert.IsTrue(data.Settings.bluetoothCompensationEnabled);
            monitor.EditorOutputOverride = AudioOutputKind.Other; monitor.Refresh();
            Assert.AreEqual(-25, monitor.EffectiveOffsetMs); Assert.AreEqual(300, data.Settings.bluetoothDelayMs);
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            Assert.AreEqual(275, monitor.EffectiveOffsetMs);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TapTestMeasuresDelayAndApplyOnlyFillsDraftUntilSaved()
        {
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            var popup = Open();
            Assert.IsFalse(Button(popup, "ApplyResultButton").interactable);
            Button(popup, "TestButton").onClick.Invoke();
            Assert.IsTrue(popup.IsTesting);
            LatencyCalibration test = popup.Calibration;
            for (int beat = 2; beat < 12; beat++) popup.TapAt(test.FirstBeatTime + beat * test.IntervalSeconds + .175);
            Assert.IsFalse(popup.IsTesting, "Ten valid taps finish the test.");
            Assert.AreEqual("175 ms", popup.transform.Find("Card/ResultValue").GetComponent<Text>().text);
            Assert.IsTrue(Button(popup, "ApplyResultButton").interactable);
            Button(popup, "ApplyResultButton").onClick.Invoke();
            Assert.AreEqual(20, Slider(popup).value, "175 ms measured minus the -25 ms input offset.");
            Assert.IsFalse(data.Settings.bluetoothCompensationEnabled, "Applying must not save by itself.");
            Button(popup, "SaveButton").onClick.Invoke();
            Assert.IsTrue(data.Settings.bluetoothCompensationEnabled); Assert.AreEqual(200, data.Settings.bluetoothDelayMs);
            Assert.AreEqual(175, monitor.EffectiveOffsetMs);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClosingDuringTestStopsMetronome()
        {
            var popup = Open();
            Button(popup, "TestButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(popup.GetComponentInChildren<AudioSource>().isPlaying || popup.IsTesting);
            Button(popup, "CloseButton").onClick.Invoke();
            Assert.IsFalse(popup.IsTesting);
            Assert.IsFalse(popup.GetComponentInChildren<AudioSource>().isPlaying);
        }

        [UnityTest]
        public IEnumerator ThreeLanguagesShowStatusAndButtonsAdjustAndResetDraft()
        {
            foreach (var language in new[] { GameLanguage.SimplifiedChinese, GameLanguage.English, GameLanguage.Korean })
            {
                data.SetLanguage(language);
                monitor.EditorOutputOverride = AudioOutputKind.BluetoothConnected; monitor.Refresh();
                var popup = Open();
                Button(popup, "PlusButton").onClick.Invoke(); Assert.AreEqual(1, Slider(popup).value);
                Button(popup, "MinusButton").onClick.Invoke(); Assert.AreEqual(0, Slider(popup).value);
                Slider(popup).value = 100; Button(popup, "ResetButton").onClick.Invoke(); Assert.AreEqual(0, Slider(popup).value);
                Slider(popup).value = 30; Button(popup, "EnableBluetoothButton").onClick.Invoke();
                yield return null;
                Assert.IsNotEmpty(popup.transform.Find("Card/OutputStatus").GetComponent<Text>().text);
                GameSceneTests.Capture("bluetooth-settings-" + language + ".png");
                popup.Close();
            }
        }

        [UnityTest]
        public IEnumerator DisconnectDuringLitCuePausesAndResumePreservesJudgement()
        {
            data.SetBluetoothCompensation(true, 300);
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            yield return SceneManager.LoadSceneAsync("Game"); yield return null;
            var game = Object.FindFirstObjectByType<GameController>(); game.StartRun();
            float timeout = Time.realtimeSinceStartup + 8;
            while (game.SongTime < 2.1 && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsFalse(game.IsPadLit(5), "Positive Bluetooth delay must move light later.");
            while (!game.IsPadLit(5) && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(game.IsPadLit(5));
            double before = game.SongTime;
            monitor.EditorOutputOverride = AudioOutputKind.Other; monitor.Refresh();
            Assert.AreEqual(GameRunState.Paused, game.State);
            Assert.AreEqual(before - .3, game.SongTime, .03);
            Assert.AreEqual(-25, monitor.EffectiveOffsetMs);
            Assert.AreEqual(300, data.Settings.bluetoothDelayMs);
            Assert.AreEqual(0, game.Session.JudgedCount);
            double frozen = game.SongTime; yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(frozen, game.SongTime);
            game.TogglePause(); Assert.IsTrue(game.IsPadLit(5));
            game.Press(5); game.Release(5);
            Assert.AreEqual(1, game.Session.JudgedCount); Assert.AreEqual(100, game.Session.Health);
            Assert.AreEqual("PERFECT", game.transform.Find("Layout/Track/Judgement").GetComponent<Text>().text);
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            Assert.AreEqual(GameRunState.Paused, game.State); Assert.AreEqual(275, monitor.EffectiveOffsetMs);
            game.TogglePause(); Assert.AreEqual(GameRunState.Playing, game.State);
            Assert.AreEqual(1, game.Session.JudgedCount);
        }
    }
}
#endif

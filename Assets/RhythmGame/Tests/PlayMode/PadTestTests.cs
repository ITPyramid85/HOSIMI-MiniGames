using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class PadTestTests
    {
        private string original;
        private RhythmGameData data;
        private DrumInput drum;
        private AudioLatencyMonitor monitor;
        private PadTestPopup popup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            data = GameManager.Instance.Data; original = JsonUtility.ToJson(data.Player);
            data.SetLanguage(GameLanguage.SimplifiedChinese);
            data.SetInputOffset(-25);
            drum = DrumInput.EnsureInstance();
            monitor = AudioLatencyMonitor.EnsureInstance();
            monitor.EditorOutputOverride = AudioOutputKind.Other; monitor.Refresh();
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenSettings();
            menu.GetComponentInChildren<SettingsPopup>().OpenPadTest(false);
            popup = menu.GetComponentInChildren<PadTestPopup>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            drum.SetStatus(DrumConnection.None, ""); drum.Dispatch();
            monitor.EditorOutputOverride = null; monitor.Refresh();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            JsonUtility.FromJsonOverwrite(original, data.Player); data.Save();
        }

        private Transform Card(string name) { return popup.transform.Find("Card/" + name); }
        private void Connect() { drum.SetStatus(DrumConnection.Bluetooth, "BLE_MIDI"); drum.Dispatch(); }

        [UnityTest]
        public IEnumerator StruckPadsLightUpAndAreLoggedWithVelocity()
        {
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(Strings.T("pad.unsupported"), Card("Connection/ConnectionText").GetComponent<Text>().text,
                "The editor has no MIDI source; on-screen pads still work.");
            Assert.IsFalse(Card("ScanButton").GetComponent<Button>().interactable);
            drum.Inject(43, 96);      // PAD8 tom 3
            drum.Inject(36, 1);       // kick pedal
            drum.Dispatch();
            yield return null;
            Assert.AreEqual(2, popup.Log.Count);
            Assert.AreEqual(DrumKit.KickPedal, popup.Log[0].Input, "Newest first.");
            Assert.AreEqual(PadTestPopup.Colour(7), Card("Drum/Pad8").GetComponent<Image>().color, "The struck pad lights up.");
            Assert.AreEqual(PadTestPopup.Colour(DrumKit.KickPedal), Card("PedalKick").GetComponent<Image>().color);
            Assert.AreEqual("底鼓", Card("Hits/Name0").GetComponent<Text>().text);
            Assert.AreEqual("踏板", Card("Hits/Value0").GetComponent<Text>().text);
            Assert.AreEqual("8 桶鼓3", Card("Hits/Name1").GetComponent<Text>().text);
            Assert.AreEqual("力度 96", Card("Hits/Value1").GetComponent<Text>().text);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreNotEqual(PadTestPopup.Colour(7), Card("Drum/Pad8").GetComponent<Image>().color, "And goes out again.");
        }

        [UnityTest]
        public IEnumerator OnScreenPadsFeedTheSamePath()
        {
            Card("Drum/Pad6").GetComponent<CalibrationTapPad>().OnPointerDown(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
            drum.Dispatch();
            yield return null;
            Assert.AreEqual(5, popup.Log[0].Input, "PAD6 snare.");
        }

        [UnityTest]
        public IEnumerator CalibrationNeedsTheDrumAndAppliesWhileItIsConnected()
        {
            Assert.IsFalse(Card("Calibration/CalibrateButton").GetComponent<Button>().interactable);
            popup.StartTest();
            Assert.IsFalse(popup.IsTesting, "No drum, no calibration: a touch measurement would be wrong for the pads.");

            Connect();
            yield return null;
            StringAssert.Contains("已连接", Card("Connection/ConnectionText").GetComponent<Text>().text);
            Assert.IsTrue(Card("Calibration/CalibrateButton").GetComponent<Button>().interactable);
            popup.StartTest();
            Assert.IsTrue(popup.IsTesting);
            LatencyCalibration test = popup.Calibration;
            for (int beat = 2; beat < 12; beat++) popup.TapAt(test.FirstBeatTime + beat * test.IntervalSeconds + .040 + (beat % 2) * .004);
            Assert.IsFalse(popup.IsTesting, "Ten strikes finish it.");
            Assert.AreEqual(42, data.Settings.PadOffsetMs(false));
            Assert.AreEqual(0, data.Settings.PadOffsetMs(true), "Bluetooth speaker route is kept apart.");
            Assert.AreEqual(42, RhythmGameData.Open(data.SavePath).Settings.padOffsetMs, "Saved.");
            StringAssert.Contains("42 ms", Card("Calibration/Offset").GetComponent<Text>().text);
            monitor.Refresh();
            Assert.AreEqual(42, monitor.EffectiveOffsetMs, "Games use the pad offset while the drum is connected.");

            drum.SetStatus(DrumConnection.None, ""); drum.Dispatch();
            monitor.Refresh();
            Assert.AreEqual(-25, monitor.EffectiveOffsetMs, "Touch play keeps its own offset.");
        }

        [UnityTest]
        public IEnumerator BluetoothSpeakerRouteIsCalibratedSeparately()
        {
            monitor.EditorOutputOverride = AudioOutputKind.Bluetooth; monitor.Refresh();
            Connect();
            yield return null;
            popup.StartTest();
            LatencyCalibration test = popup.Calibration;
            for (int beat = 2; beat < 12; beat++) popup.TapAt(test.FirstBeatTime + beat * test.IntervalSeconds + .230);
            Assert.AreEqual(230, data.Settings.PadOffsetMs(true));
            Assert.AreEqual(0, data.Settings.PadOffsetMs(false));
            monitor.Refresh();
            Assert.AreEqual(230, monitor.EffectiveOffsetMs);
        }

        [UnityTest]
        public IEnumerator EveryLanguageLabelsTheScreen()
        {
            foreach (GameLanguage language in PlayerSettings.DisplayOrder)
            {
                popup.Close();
                data.SetLanguage(language);
                popup.Show();
                yield return null;
                Assert.IsNotEmpty(Card("Title").GetComponent<Text>().text);
                Assert.IsNotEmpty(Card("Connection/ConnectionText").GetComponent<Text>().text);
                Assert.AreEqual(Strings.Get("pad.start", language), Card("Calibration/CalibrateButton").GetComponentInChildren<Text>().text);
            }
        }
    }
}

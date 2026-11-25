using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class SettingsScreenTests
    {
        private string originalPlayer;
        private RhythmGameData data;
        private MainMenuController menu;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            data = GameManager.Instance.Data;
            originalPlayer = JsonUtility.ToJson(data.Player);
            menu = Object.FindFirstObjectByType<MainMenuController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        private SettingsPopup Open()
        {
            menu.transform.Find("Menu/SettingsButton").GetComponent<Button>().onClick.Invoke();
            return menu.GetComponentInChildren<SettingsPopup>();
        }
        private static Transform Card(SettingsPopup popup, string name) { return popup.transform.Find("Card/" + name); }
        private static string Label(SettingsPopup popup, string name) { return Card(popup, name).GetComponentInChildren<Text>().text; }

        [UnityTest]
        public IEnumerator FourLanguagesSwitchTheWholeMenuAndAreSaved()
        {
            data.SetLanguage(GameLanguage.SimplifiedChinese);
            SettingsPopup popup = Open();
            Assert.AreEqual("设置", Card(popup, "Title").GetComponent<Text>().text);
            string[] names = { "简体中文", "日本語", "English", "한국어" };
            string[] codes = { "zh", "ja", "en", "ko" };
            for (int i = 0; i < 4; i++) Assert.AreEqual(names[i], Label(popup, "Language_" + codes[i]));
            Assert.AreEqual(Palette.Sun, Card(popup, "Language_zh").GetComponent<Image>().color, "Current language is highlighted.");

            Card(popup, "Language_ja").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(GameLanguage.Japanese, data.Settings.language);
            Assert.AreEqual(GameLanguage.Japanese, RhythmGameData.Open(data.SavePath).Settings.language, "Saved straight away.");
            Assert.AreEqual("設定", Card(popup, "Title").GetComponent<Text>().text);
            Assert.AreEqual(Palette.Sun, Card(popup, "Language_ja").GetComponent<Image>().color);
            Assert.AreEqual("リズムゲーム", menu.transform.Find("Menu/StartGameButton").GetComponentInChildren<Text>().text,
                "The menu behind relabels without reopening.");
            yield return new WaitForSecondsRealtime(.6f);
            GameSceneTests.Capture("settings-ja-preview.png");

            Card(popup, "Language_ko").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("설정", Card(popup, "Title").GetComponent<Text>().text);
            Card(popup, "Language_en").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("Rhythm Game", menu.transform.Find("Menu/StartGameButton").GetComponentInChildren<Text>().text);
            popup.Close();
        }

        [UnityTest]
        public IEnumerator VolumesApplyLiveAndSaveWhenTheScreenCloses()
        {
            data.SetVolumes(1, 1, 1);
            var music = new GameObject("TestMusic").AddComponent<AudioSource>();
            AudioChannel.Bind(music, AudioKind.Music);
            AudioSource effects = AudioChannel.Create(null, "TestEffects", AudioKind.Effects);
            Assert.AreEqual(1f, music.volume);

            SettingsPopup popup = Open();
            Card(popup, "MusicSlider").GetComponent<Slider>().value = 4;
            Card(popup, "EffectsSlider").GetComponent<Slider>().value = 0;
            Assert.AreEqual(.4f, data.Settings.musicVolume, 1e-6);
            Assert.AreEqual(.4f, music.volume, 1e-6, "Playing sources follow at once.");
            Assert.AreEqual(0f, effects.volume);
            Assert.AreEqual("40%", Card(popup, "MusicValue").GetComponent<Text>().text);
            Assert.AreEqual(1f, RhythmGameData.Open(data.SavePath).Settings.musicVolume, "Not written while dragging.");
            yield return new WaitForSecondsRealtime(.6f);
            GameSceneTests.Capture("settings-preview.png");

            popup.Close();
            PlayerSettings saved = RhythmGameData.Open(data.SavePath).Settings;
            Assert.AreEqual(.4f, saved.musicVolume, 1e-6);
            Assert.AreEqual(0f, saved.soundEffectsVolume);
            Object.Destroy(music.gameObject); Object.Destroy(effects.gameObject);
        }

        [UnityTest]
        public IEnumerator DrumTestOpensOnTopAndReturnsToSettings()
        {
            SettingsPopup popup = Open();
            foreach (string row in new[] { "PadButton", "LatencyButton" })
            {
                Card(popup, row).GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(popup.PadTestOpen, row);
                PadTestPopup test = menu.GetComponentInChildren<PadTestPopup>();
                Assert.IsTrue(test.IsOpen);
                Assert.IsFalse(test.IsTesting, "No drum connected, so the latency row does not start calibrating.");
                Assert.IsTrue(popup.IsOpen, "Settings stays underneath.");
                test.Close();
                Assert.IsFalse(popup.PadTestOpen);
                Assert.IsTrue(popup.IsOpen);
            }
            popup.Close();
            yield return null;
            CanvasGroup group = menu.transform.Find("Menu").GetComponent<CanvasGroup>();
            Assert.IsTrue(group.interactable && group.blocksRaycasts);
        }
    }
}

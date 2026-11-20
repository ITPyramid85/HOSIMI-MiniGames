using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class MainMenuTests
    {
        [UnitySetUp]
        public IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsPrefabIsReusedAndModalRestoresMenu()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            Assert.AreEqual(8, menu.GetComponentsInChildren<Button>().Length, "3 modes, 2 secondary, 3 small (client reply v5 §4).");
            menu.OpenSettings();
            menu.OpenSettings();
            yield return null;
            SettingsPopup[] popups = menu.GetComponentsInChildren<SettingsPopup>(true);
            Assert.AreEqual(1, popups.Length);
            Assert.IsTrue(popups[0].IsOpen);
            CanvasGroup group = menu.transform.Find("Menu").GetComponent<CanvasGroup>();
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
            popups[0].Close();
            Assert.IsFalse(popups[0].IsOpen);
            Assert.IsTrue(group.interactable && group.blocksRaycasts);
            menu.OpenSettings();
            Assert.AreSame(popups[0], menu.GetComponentInChildren<SettingsPopup>());
            popups[0].Close();
        }

        [UnityTest]
        public IEnumerator PopupAnimatesClosedThenDeactivatesAndCanReopenMidClose()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenSettings();
            SettingsPopup popup = menu.GetComponentInChildren<SettingsPopup>();
            float deadline = Time.realtimeSinceStartup + 2;
            while (popup.GetComponent<PopupTransition>().IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(Vector3.one, popup.transform.Find("Card").localScale, "Open animation ends at full size.");
            popup.Close();
            Assert.IsTrue(popup.gameObject.activeSelf, "Closing animation keeps the popup visible briefly.");
            menu.OpenSettings();
            Assert.IsTrue(popup.IsOpen, "Reopening mid-close is allowed.");
            popup.Close();
            deadline = Time.realtimeSinceStartup + 2;
            while (popup.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(popup.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator IntroLeavesButtonsAtTheirLayoutPositions()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu.GetComponent<MainMenuAnimator>());
            yield return new WaitForSecondsRealtime(1.5f);
            var start = (RectTransform)menu.transform.Find("Menu/StartGameButton");
            Assert.AreEqual(new Vector2(300, 76), start.anchoredPosition);
            Assert.IsFalse(menu.transform.Find("Wipe/Bar0").gameObject.activeSelf, "Reveal shutters are hidden after the intro.");
            Assert.IsNotEmpty(menu.BubbleText, "The guide character greets the player.");
            Assert.IsNotNull(menu.transform.Find("Menu/Guide").GetComponent<Image>().sprite, "The guide is shown large.");
            Assert.IsTrue(menu.transform.Find("Music").GetComponent<AudioSource>().isPlaying, "The menu has BGM (client reply v5 §4-3).");
        }

        [Test]
        public void FrameRateIsNotLeftAtTheMobileDefault()
        {
            int fps = GameManager.ConfigureFrameRate();
            Assert.AreEqual(fps, Application.targetFrameRate);
            Assert.That(fps, Is.InRange(60, 120), "Android defaults to 30 fps when targetFrameRate is unset.");
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [UnityTest]
        public IEnumerator ManagerAndDataSurviveSceneReloadWithoutDuplicates()
        {
            GameManager manager = GameManager.Instance;
            Assert.IsNotNull(manager);
            Assert.IsTrue(manager.IsReady, manager.InitializationError);
            RhythmGameData data = manager.Data;
            Object.Instantiate(Resources.Load<GameManager>("RhythmGame/GameManager"));
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.AreSame(manager, GameManager.Instance);
            Assert.AreSame(data, GameManager.Instance.Data);
            Assert.AreEqual(1, Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator ShopPlaceholderDoesNotChangeSceneOrData()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            string before = JsonUtility.ToJson(GameManager.Instance.Data.Player);
            menu.transform.Find("Menu/StoreButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(before, JsonUtility.ToJson(GameManager.Instance.Data.Player));
            Assert.IsNull(menu.GetComponentInChildren<SettingsPopup>());
            Assert.IsNull(menu.GetComponentInChildren<CharacterPopup>());
            Assert.AreEqual(Strings.T("menu.coming_soon"), menu.BubbleText, "Not-yet-built entries answer in the guide's bubble.");
        }

        [UnityTest]
        public IEnumerator OfficialLogoIsTheSameInEveryLanguage()
        {
            RhythmGameData data = GameManager.Instance.Data;
            GameLanguage original = data.Settings.language;
            try
            {
                foreach (GameLanguage language in System.Enum.GetValues(typeof(GameLanguage)))
                {
                    data.SetLanguage(language);
                    yield return SceneManager.LoadSceneAsync("MainMenu");
                    yield return null;
                    Image logo = Object.FindFirstObjectByType<MainMenuController>().transform.Find("Menu/Logo").GetComponent<Image>();
                    Assert.AreEqual("logo_black", logo.sprite.name, language + ": the official Hos!m! wordmark, not text.");
                }
            }
            finally { data.SetLanguage(original); }
        }
    }
}

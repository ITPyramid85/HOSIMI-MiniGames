using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class LoginTests
    {
        private string originalPlayer;
        private RhythmGameData data;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameManager.EnsureInstance();
            data = GameManager.Instance.Data;
            originalPlayer = JsonUtility.ToJson(data.Player);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
            data.Save();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        private static IEnumerator OpenLogin()
        {
            yield return SceneManager.LoadSceneAsync("Login");
            yield return null;
        }

        private static Transform Find(string name)
        {
            return Object.FindFirstObjectByType<LoginController>().transform.Find("Login/" + name);
        }

        private static IEnumerator WaitForScene(string scene)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != scene && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
        }

        [Test]
        public void LoginIsTheFirstScene()
        {
            Assert.AreEqual("Login", System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)));
        }

        [UnityTest]
        public IEnumerator NewPlayerStartsAsGuestAndLandsOnTheMainMenu()
        {
            data.Player.account = new AccountInfo();
            data.SetLanguage(GameLanguage.Korean);
            yield return OpenLogin();
            Assert.IsTrue(Find("GuestButton").gameObject.activeSelf);
            Assert.IsTrue(Find("GoogleButton").gameObject.activeSelf);
            Assert.IsFalse(Find("StartButton").gameObject.activeSelf);
            Assert.AreEqual("게스트로 시작", Find("GuestButton").GetComponentInChildren<Text>().text);
            Assert.IsNotNull(Find("Logo").GetComponent<Image>().sprite, "Official wordmark.");

            Find("GuestButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(data.Account.IsSignedIn);
            Assert.IsTrue(RhythmGameData.Open(data.SavePath).Account.IsGuest, "Saved straight away.");
            yield return WaitForScene("MainMenu");
        }

        [UnityTest]
        public IEnumerator GoogleSaysComingSoonAndStaysOnTheLoginScreen()
        {
            data.Player.account = new AccountInfo();
            data.SetLanguage(GameLanguage.English);
            yield return OpenLogin();
            Find("GoogleButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(data.Account.IsSignedIn);
            StringAssert.Contains("coming soon", Find("Status").GetComponent<Text>().text);
            Assert.AreEqual("Login", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ReturningPlayerSeesOneStartButtonWithTheirId()
        {
            data.Player.account = new AccountInfo();
            data.SignIn(AccountProvider.Guest, null);
            string id = data.Account.userId;
            data.SetLanguage(GameLanguage.SimplifiedChinese);
            yield return OpenLogin();
            Assert.IsFalse(Find("GuestButton").gameObject.activeSelf);
            Assert.IsTrue(Find("StartButton").gameObject.activeSelf);
            StringAssert.Contains(data.Account.ShortId, Find("Account").GetComponent<Text>().text);
            Find("StartButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(id, data.Account.userId);
            yield return WaitForScene("MainMenu");
        }

        [UnityTest]
        public IEnumerator LanguageCanBePickedBeforeSigningIn()
        {
            data.Player.account = new AccountInfo();
            data.SetLanguage(GameLanguage.SimplifiedChinese);
            yield return OpenLogin();
            Assert.AreEqual("游客开始", Find("GuestButton").GetComponentInChildren<Text>().text);
            Find("Language_ja").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(GameLanguage.Japanese, data.Settings.language);
            Assert.AreEqual("ゲストではじめる", Find("GuestButton").GetComponentInChildren<Text>().text);
            Assert.AreEqual(Palette.Lift(Palette.Sun), Find("Language_ja").GetComponent<Image>().color);
        }
    }
}

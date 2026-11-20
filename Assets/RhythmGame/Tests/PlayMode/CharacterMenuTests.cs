using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmGame.Tests
{
    public sealed class CharacterMenuTests
    {
        private string originalPlayer;
        private bool deliversHere;
        private RhythmGameData data;
        private MainMenuController menu;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            data = GameManager.Instance.Data;
            originalPlayer = JsonUtility.ToJson(data.Player);
            deliversHere = CharacterPopup.DeliversHere;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(PlayerData.Create(data.Config)), data.Player);
            data.Save();
            menu = Object.FindFirstObjectByType<MainMenuController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            CharacterPopup.DeliversHere = deliversHere;
            if (originalPlayer != null)
            {
                JsonUtility.FromJsonOverwrite(originalPlayer, data.Player);
                data.Save();
            }
        }

        private CharacterPopup Open()
        {
            menu.transform.Find("Menu/CharacterButton").GetComponent<Button>().onClick.Invoke();
            return menu.GetComponentInChildren<CharacterPopup>();
        }
        // Controls live at different depths (world panel, story card), so look them up by name.
        private static Transform Child(CharacterPopup popup, string name)
        {
            foreach (Transform child in popup.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            throw new AssertionException("Missing " + name);
        }
        private static Button Button(CharacterPopup popup, string name) { return Child(popup, name).GetComponent<Button>(); }
        private static Text Text(CharacterPopup popup, string name) { return Child(popup, name).GetComponent<Text>(); }
        private static string ButtonText(CharacterPopup popup, string name) { return Child(popup, name).GetComponentInChildren<Text>().text; }
        private static Color PortraitColor(CharacterPopup popup) { return Child(popup, "Portrait").GetComponent<Image>().color; }

        private static IEnumerator WaitForSlide(CharacterPopup popup)
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (popup.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(popup.IsAnimating);
            Assert.AreEqual(Vector2.zero, ((RectTransform)Child(popup, "Stage")).anchoredPosition);
        }

        [UnityTest]
        public IEnumerator PagesFlipThroughSixCharactersWithWorldAndStory()
        {
            CharacterPopup popup = Open();
            Assert.AreEqual("whale", popup.SelectedCharacterId, "Opens on the guide character.");
            Assert.AreEqual("角色 1 / 6", Text(popup, "Title").text);
            StringAssert.Contains("持有鼓棒", Text(popup, "Balance").text);
            StringAssert.Contains("天空鲸", Text(popup, "CharacterName").text);
            StringAssert.Contains("无重力世界", Text(popup, "WorldLabel").text);
            StringAssert.Contains("次元", Text(popup, "Story").text);
            Assert.AreEqual(Color.white, PortraitColor(popup), "Owned characters are in colour.");
            Assert.AreEqual(6, Child(popup, "Dots").GetComponentsInChildren<Image>().Length);
            yield return new WaitForSecondsRealtime(.6f);   // past the open animation
            GameSceneTests.Capture("character-preview.png");

            Button(popup, "NextButton").onClick.Invoke();
            Assert.AreEqual("turtle", popup.SelectedCharacterId);
            Assert.AreEqual("角色 2 / 6", Text(popup, "Title").text);
            yield return WaitForSlide(popup);
            Assert.AreNotEqual(Color.white, PortraitColor(popup), "Unowned characters are silhouettes.");
            Assert.IsFalse(Button(popup, "ClaimButton").interactable, "Claiming needs the character first.");
            StringAssert.Contains("请先获得角色", ButtonText(popup, "ClaimButton"));
            Assert.IsTrue(Button(popup, "MerchButton").interactable, "Merch is always available.");
            GameSceneTests.Capture("character-locked-preview.png");

            Button(popup, "PreviousButton").onClick.Invoke();
            Button(popup, "PreviousButton").onClick.Invoke();
            Assert.AreEqual("squirrel", popup.SelectedCharacterId, "Paging wraps around.");
            yield return WaitForSlide(popup);
            foreach (CharacterConfig character in data.Config.characters)
            {
                popup.SelectCharacter(character.id);
                Assert.IsNotNull(Child(popup, "Portrait").GetComponent<Image>().sprite, character.id + " portrait");
                Assert.IsNotEmpty(Text(popup, "Story").text, character.id + " story");
            }
            popup.Close();
        }

        [UnityTest]
        public IEnumerator PurchaseEquipPersistAndShowInGame()
        {
            data.Player.coins = 2000; data.Save(); // Isolated test fixture; restored in TearDown.
            CharacterPopup popup = Open();
            popup.SelectCharacter("turtle");
            Button(popup, "PrimaryButton").onClick.Invoke();
            Assert.AreEqual(800, data.Player.coins);
            Assert.AreEqual(1, data.Player.FindCharacter("turtle").level);
            Assert.AreEqual("whale", data.Player.equippedCharacterId, "Getting a character does not silently equip it.");
            Assert.IsTrue(Button(popup, "ClaimButton").interactable, "Owning the character opens the figure claim.");
            Assert.AreEqual(Color.white, PortraitColor(popup));
            StringAssert.Contains("装备", ButtonText(popup, "PrimaryButton"));
            Button(popup, "PrimaryButton").onClick.Invoke();
            Assert.AreEqual("turtle", data.Player.equippedCharacterId);
            Assert.AreEqual(800, data.Player.coins, "Equipping must not purchase a second time.");
            Assert.AreEqual(2, data.Player.characters.Count);
            Assert.IsFalse(Button(popup, "PrimaryButton").interactable, "Already the guide.");
            Button(popup, "PrimaryButton").onClick.Invoke();
            Assert.AreEqual(800, data.Player.coins);

            CharacterPopup.DeliversHere = false;
            Button(popup, "ClaimButton").onClick.Invoke();
            Assert.AreEqual("目前仅支持韩国配送", Text(popup, "Status").text);
            Button(popup, "MerchButton").onClick.Invoke();
            Assert.AreEqual("目前仅支持韩国配送", Text(popup, "Status").text);
            Assert.AreEqual(800, data.Player.coins, "Neither button spends drumsticks.");

            RhythmGameData loaded = RhythmGameData.Open(data.SavePath);
            Assert.AreEqual("turtle", loaded.Player.equippedCharacterId);
            Assert.AreEqual(800, loaded.Player.coins);
            yield return WaitForSlide(popup);
            GameSceneTests.Capture("character-owned-preview.png");
            popup.Close();
            string summary = menu.transform.Find("Menu/Balance/BalanceText").GetComponent<Text>().text;
            StringAssert.Contains("800", summary); StringAssert.DoesNotContain("Lv.", summary);
            menu.StartGame();
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "Game" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            GameController game = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(game);
            Image portrait = game.transform.Find("Layout/CharacterCard/Portrait").GetComponent<Image>();
            Assert.IsTrue(portrait.enabled);
            Assert.AreEqual("turtle", portrait.sprite.name, "The game shows the equipped character's art.");
            game.StartRun();
            Assert.AreEqual("turtle", game.Session.CharacterId);
            Assert.AreEqual(GameRunState.Playing, game.State);
        }

        [UnityTest]
        public IEnumerator NotEnoughDrumsticksCannotSpendOrChangeOwnership()
        {
            CharacterPopup popup = Open();
            popup.SelectCharacter("squirrel");
            string before = JsonUtility.ToJson(data.Player);
            Assert.IsFalse(Button(popup, "PrimaryButton").interactable);
            StringAssert.Contains("鼓棒不足", ButtonText(popup, "PrimaryButton"));
            Button(popup, "PrimaryButton").onClick.Invoke();
            Button(popup, "ClaimButton").onClick.Invoke();
            Assert.AreEqual(before, JsonUtility.ToJson(data.Player));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalIsReusedBlocksOtherActionsAndReturnsFocus()
        {
            CharacterPopup popup = Open();
            menu.OpenCharacters(); menu.OpenSettings(); menu.StartGame();
            yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, menu.GetComponentsInChildren<CharacterPopup>(true).Length);
            Assert.IsNull(menu.GetComponentInChildren<SettingsPopup>());
            var group = menu.transform.Find("Menu").GetComponent<CanvasGroup>();
            Assert.IsFalse(group.interactable || group.blocksRaycasts);
            popup.SelectCharacter("lion");
            StringAssert.Contains("软软狮", Text(popup, "CharacterName").text);
            popup.Close();
            Assert.IsTrue(group.interactable && group.blocksRaycasts);
            Assert.AreEqual("CharacterButton", EventSystem.current.currentSelectedGameObject.name);
            menu.OpenCharacters();
            Assert.AreSame(popup, menu.GetComponentInChildren<CharacterPopup>());
            popup.Close();
            menu.OpenSettings();
            Assert.IsNotNull(menu.GetComponentInChildren<SettingsPopup>());
            menu.OpenCharacters();
            Assert.IsFalse(popup.IsOpen);
        }

        [UnityTest]
        public IEnumerator CharacterPanelSupportsAllThreeLanguages()
        {
            GameLanguage[] languages = { GameLanguage.SimplifiedChinese, GameLanguage.English, GameLanguage.Korean };
            string[] titles = { "角色 3 / 6", "CHARACTER 3 / 6", "캐릭터 3 / 6" };
            string[] names = { "软软狮", "Squishy Lion", "말랑사자" };
            for (int i = 0; i < languages.Length; i++)
            {
                data.SetLanguage(languages[i]);
                CharacterPopup popup = Open();
                popup.SelectCharacter("lion");
                Assert.AreEqual(titles[i], Text(popup, "Title").text);
                StringAssert.Contains(names[i], Text(popup, "CharacterName").text);
                Assert.IsNotEmpty(Text(popup, "Story").text);
                yield return WaitForSlide(popup);
                GameSceneTests.Capture("character-language-" + i + ".png");
                popup.Close();
            }
        }
    }
}

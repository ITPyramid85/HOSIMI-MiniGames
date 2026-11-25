using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Home screen (client reply v5 §4): three big modes (drum lessons, rhythm game, drum mini-games),
    // two secondary buttons (characters with the shop, records), three small ones (settings, contact,
    // official store), drumsticks pinned top right, and the guide character shown large with a
    // one-line speech bubble over the menu BGM.
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button lessonButton, startButton, miniGamesButton;
        [SerializeField] private Button characterButton, recordsButton;
        [SerializeField] private Button settingsButton, inquiryButton, storeButton;
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private SettingsPopup settingsPrefab;
        [SerializeField] private CharacterPopup characterPrefab;
        [SerializeField] private Text balanceText, statusText, bubbleText;
        [SerializeField] private Image guidePortrait;
        [SerializeField] private AudioSource music;
        [SerializeField] private string storeUrl;   // official store page; empty until the client provides it
        private SettingsPopup settingsInstance;
        private CharacterPopup characterInstance;
        private Button popupOpener;
        private bool loading;
        private Coroutine typing;
        private bool PopupOpen { get { return (settingsInstance != null && settingsInstance.IsOpen) ||
            (characterInstance != null && characterInstance.IsOpen); } }

        private void Awake()
        {
            GameManager.EnsureInstance();
            lessonButton.onClick.AddListener(ComingSoon);
            startButton.onClick.AddListener(StartGame);
            miniGamesButton.onClick.AddListener(OpenMiniGames);
            characterButton.onClick.AddListener(OpenCharacters);
            recordsButton.onClick.AddListener(ComingSoon);
            settingsButton.onClick.AddListener(OpenSettings);
            inquiryButton.onClick.AddListener(ComingSoon);
            storeButton.onClick.AddListener(OpenStore);
            if (music != null) AudioChannel.Bind(music, AudioKind.Music);
            if (GameManager.Instance.IsReady) GameManager.Instance.Data.SettingsChanged += Relabel;
            Relabel();
        }

        private void Start()
        {
            if (music != null) { music.loop = true; music.Play(); }
            Say(Strings.T("menu.hello_" + Random.Range(1, 4)));
            if (EventSystem.current != null && !PopupOpen)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        // Also runs when the language or volume is changed from the settings screen.
        private void Relabel()
        {
            SetLabel(lessonButton, Strings.T("menu.lessons"));
            SetLabel(startButton, Strings.T("menu.rhythm_game"));
            SetLabel(miniGamesButton, Strings.T("menu.mini_games"));
            SetLabel(characterButton, Strings.T("menu.characters_shop"));
            SetLabel(recordsButton, Strings.T("menu.records"));
            SetLabel(settingsButton, Strings.T("menu.settings"));
            SetLabel(inquiryButton, Strings.T("menu.inquiry"));
            SetLabel(storeButton, Strings.T("menu.official_store"));
            MenuTypography typography = GetComponentInParent<MenuTypography>();
            if (typography != null) typography.Apply();
            RefreshStatus();
        }

        private static void SetLabel(Button button, string text)
        {
            button.GetComponentInChildren<Text>().text = text;
        }

        // The guide character's one-line speech bubble (client reply v5 §4-2).
        public void Say(string line)
        {
            if (typing != null) StopCoroutine(typing);
            BubbleText = line;
            bubbleText.text = line;
            typing = StartCoroutine(PixelTween.Typewrite(bubbleText, 30));
        }

        // The whole line, even while it is still being typed out.
        public string BubbleText { get; private set; }

        private void ComingSoon() { Say(Strings.T("menu.coming_soon")); }

        private void OpenStore()
        {
            if (string.IsNullOrEmpty(storeUrl)) ComingSoon();
            else Application.OpenURL(storeUrl);
        }

        public void OpenSettings()
        {
            if (loading || PopupOpen) return;
            popupOpener = settingsButton;
            if (settingsInstance == null)
            {
                settingsInstance = Instantiate(settingsPrefab, popupRoot, false);
                settingsInstance.Closed += RestoreMenu;
            }
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            settingsInstance.transform.SetAsLastSibling();
            settingsInstance.Show();
        }

        public void OpenCharacters()
        {
            if (loading || PopupOpen || !GameManager.Instance.IsReady) return;
            popupOpener = characterButton;
            if (characterInstance == null)
            {
                characterInstance = Instantiate(characterPrefab, popupRoot, false);
                characterInstance.Closed += RestoreMenu;
            }
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            characterInstance.transform.SetAsLastSibling();
            characterInstance.Show();
        }

        private void RestoreMenu()
        {
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
            RefreshStatus();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(popupOpener.gameObject);
        }

        private void RefreshStatus()
        {
            GameManager manager = GameManager.Instance;
            if (!manager.IsReady)
            {
                statusText.text = Strings.T("menu.unable_to_load_save_please_restart_the_g");
                return;
            }
            RhythmGameData data = manager.Data;
            balanceText.text = Strings.T("menu.balance") + data.Player.coins.ToString("N0");
            CharacterConfig guide = data.Config.Character(data.Player.equippedCharacterId);
            guidePortrait.sprite = CharacterArt.Portrait(guide);
            guidePortrait.enabled = guidePortrait.sprite != null;
            statusText.text = data.HasUnsavedChanges ? Strings.T("menu.unsaved_changes_retry_saving_in_characte") : "";
        }

        public void StartGame() { LoadScene("Game"); }
        public void OpenMiniGames() { LoadScene("MiniGames"); }

        private void LoadScene(string scene)
        {
            if (loading || PopupOpen || !GameManager.Instance.IsReady) return;
            loading = true;
            menuGroup.interactable = false;
            SceneTransition.Load(scene);
        }

        private void OnDestroy()
        {
            if (settingsInstance != null) settingsInstance.Closed -= RestoreMenu;
            if (characterInstance != null) characterInstance.Closed -= RestoreMenu;
            if (GameManager.Instance != null && GameManager.Instance.IsReady) GameManager.Instance.Data.SettingsChanged -= Relabel;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // First screen (client reply v5 §5-2). Until the account server is up, "start as guest" signs in
    // on the device straight away; the Google button is in place and says it is coming soon. A player
    // who already signed in sees one start button. Language can be picked here before anything else.
    public sealed class LoginController : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";

        // Layout (reference canvas): with the Apple button the guest button moves down a row.
        public const float GoogleY = 36, AppleY = -72, GuestY = -176;
        private const float GuestYWithoutApple = -84;
        // Apple sign-in is offered on iOS only (client reply v6 §4-2); tests and screenshots can force it.
        public static bool? ShowAppleOverride;
        public static bool ShowApple { get { return ShowAppleOverride ?? Application.platform == RuntimePlatform.IPhonePlayer; } }

        [SerializeField] private Button googleButton, appleButton, guestButton, startButton;
        [SerializeField] private Button[] languageButtons;   // PlayerSettings.DisplayOrder
        [SerializeField] private Text tagline, accountText, statusText, noteText;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private AudioSource music;
        private string statusKey;
        private bool loading;

        public bool IsLoading { get { return loading; } }

        private void Awake()
        {
            GameManager.EnsureInstance();
            googleButton.onClick.AddListener(() => SignIn(AccountProvider.Google));
            appleButton.onClick.AddListener(() => SignIn(AccountProvider.Apple));
            guestButton.onClick.AddListener(() => SignIn(AccountProvider.Guest));
            startButton.onClick.AddListener(Continue);
            for (int i = 0; i < languageButtons.Length; i++)
            {
                GameLanguage language = PlayerSettings.DisplayOrder[i];
                languageButtons[i].onClick.AddListener(() => SetLanguage(language));
            }
            if (music != null) AudioChannel.Bind(music, AudioKind.Music);
            Refresh();
        }

        private void Start()
        {
            if (music != null) { music.loop = true; music.Play(); }
        }

        private static RhythmGameData Data { get { return GameManager.Instance.IsReady ? GameManager.Instance.Data : null; } }

        public void SignIn(AccountProvider provider)
        {
            if (loading || Data == null) return;
            statusKey = null;
            Data.SignIn(provider, result =>
            {
                if (result.Ok) Enter();
                else { statusKey = result.ErrorKey; Refresh(); }
            });
        }

        // Returning player: refresh the sign-in time and go in.
        public void Continue() { SignIn(Data != null ? Data.Account.provider : AccountProvider.Guest); }

        private void SetLanguage(GameLanguage language)
        {
            if (Data == null) return;
            Data.SetLanguage(language);
            Refresh();
        }

        private void Enter()
        {
            if (loading) return;
            loading = true;
            if (group != null) group.interactable = false;
            SceneTransition.Load(MenuScene);
        }

        private void Refresh()
        {
            RhythmGameData data = Data;
            bool signedIn = data != null && data.Account.IsSignedIn;
            googleButton.gameObject.SetActive(!signedIn);
            appleButton.gameObject.SetActive(!signedIn && ShowApple);
            guestButton.gameObject.SetActive(!signedIn);
            float guestY = ShowApple ? GuestY : GuestYWithoutApple;
            Move(guestButton.transform, guestY);
            Move(noteText.transform, guestY - 54);   // two lines at most, between the guide characters
            noteText.gameObject.SetActive(!signedIn);
            startButton.gameObject.SetActive(signedIn);
            SetLabel(googleButton, Strings.T("login.google"));
            SetLabel(appleButton, Strings.T("login.apple"));
            SetLabel(guestButton, Strings.T("login.guest"));
            SetLabel(startButton, Strings.T("login.start"));
            tagline.text = Strings.T("login.tagline");
            noteText.text = Strings.T("login.local_note");
            accountText.text = !signedIn ? "" :
                Strings.T(data.Account.IsGuest ? "login.guest_account" : "login.google_account") + " · ID " + data.Account.ShortId;
            statusText.text = data == null ? Strings.T("menu.unable_to_load_save_please_restart_the_g")
                : string.IsNullOrEmpty(statusKey) ? "" : Strings.T(statusKey);
            for (int i = 0; i < languageButtons.Length; i++)
            {
                GameLanguage language = PlayerSettings.DisplayOrder[i];
                SetLabel(languageButtons[i], Strings.T("language." + PlayerSettings.CodeOf(language).Substring(0, 2)));
                bool selected = data != null && language == data.Settings.language;
                languageButtons[i].GetComponent<Image>().color = Palette.Lift(selected ? Palette.Sun : Palette.Well);
            }
            MenuTypography typography = GetComponent<MenuTypography>();
            if (typography != null) typography.Apply();
        }

        private static void Move(Transform target, float y)
        {
            var rect = (RectTransform)target;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private static void SetLabel(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Settings (client reply v5 §5): drum-pad connection, sound (BGM, effects), language (zh / ja / en /
    // ko), Google account, latency calibration and contact. Every control writes straight to
    // PlayerSettings through RhythmGameData: language saves at once; volumes apply live while dragging
    // and are saved when the screen closes. Pad connection and latency open the drum test (PadTestPopup);
    // Google sign-in and contact are placeholders.
    public sealed class SettingsPopup : MonoBehaviour
    {
        private const int VolumeSteps = 10;   // sliders move in 10% steps
        [SerializeField] private Button closeButton, padButton, googleButton, latencyButton, inquiryButton;
        [SerializeField] private Button appleButton, deleteAccountButton;   // client reply v6 §3-5
        [SerializeField] private Button[] languageButtons;   // same order as PlayerSettings.DisplayOrder
        [SerializeField] private Slider musicSlider, effectsSlider;
        [SerializeField] private Text title, padLabel, musicLabel, effectsLabel, languageLabel, googleLabel, latencyLabel, inquiryLabel,
            musicValue, effectsValue, status;
        [SerializeField] private PadTestPopup padTestPrefab;
        [SerializeField] private LatencyCalibrationPopup latencyPrefab;
        private RhythmGameData data;
        private PopupTransition transition;
        private PadTestPopup padTest;
        private LatencyCalibrationPopup latency;
        private AudioSource tick;
        private static AudioClip tickClip;
        private float lastTick;
        private bool refreshing;
        private string message = "";
        public event Action Closed;
        public bool IsOpen { get { return transition.IsOpen; } }
        public bool PadTestOpen { get { return padTest != null && padTest.IsOpen; } }
        public bool LatencyOpen { get { return latency != null && latency.IsOpen; } }

        private void Awake()
        {
            data = GameManager.EnsureInstance().Data;
            transition = PopupTransition.For(gameObject);
            closeButton.onClick.AddListener(Close);
            padButton.onClick.AddListener(() => OpenPadTest(false));
            latencyButton.onClick.AddListener(() => OpenPadTest(true));
            appleButton.gameObject.SetActive(LoginController.ShowApple);
            foreach (Button placeholder in new[] { googleButton, appleButton, deleteAccountButton, inquiryButton })
                placeholder.onClick.AddListener(() => { message = Strings.T("settings.coming_soon"); Refresh(); });
            for (int i = 0; i < languageButtons.Length; i++)
            {
                GameLanguage language = PlayerSettings.DisplayOrder[i];
                languageButtons[i].onClick.AddListener(() => SetLanguage(language));
            }
            foreach (Slider slider in new[] { musicSlider, effectsSlider })
            {
                slider.minValue = 0; slider.maxValue = VolumeSteps; slider.wholeNumbers = true;
                slider.onValueChanged.AddListener(_ => OnVolumeChanged(slider));
            }
            tick = AudioChannel.Create(transform, "Tick", AudioKind.Effects);
            data.SettingsChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (data != null) data.SettingsChanged -= Refresh;
        }

        private void OnEnable()
        {
            message = "";
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Show() { transition.Show(); }

        public void SetLanguage(GameLanguage language)
        {
            if (!isActiveAndEnabled) return;
            data.SetLanguage(language);   // saves and raises SettingsChanged, which relabels this and the menu
            GetComponent<MenuTypography>().Apply();   // each language has its own readable font
            Refresh();
        }

        private void OnVolumeChanged(Slider changed)
        {
            if (refreshing) return;
            data.PreviewVolumes(data.Settings.masterVolume, musicSlider.value / VolumeSteps, effectsSlider.value / VolumeSteps);
            // A tick at the new level previews the effects volume; the menu BGM previews music by itself.
            if (changed == effectsSlider && Time.unscaledTime - lastTick > .08f)
            {
                lastTick = Time.unscaledTime;
                tick.PlayOneShot(TickClip());
            }
        }

        private void Refresh()
        {
            PlayerSettings settings = data.Settings;
            title.text = Strings.T("settings.title");
            padLabel.text = Strings.T("settings.pad");
            musicLabel.text = Strings.T("settings.bgm");
            effectsLabel.text = Strings.T("settings.effects_volume");
            languageLabel.text = Strings.T("settings.language");
            googleLabel.text = Strings.T("settings.google");
            latencyLabel.text = Strings.T("settings.latency");
            inquiryLabel.text = Strings.T("settings.inquiry");
            status.text = string.IsNullOrEmpty(message) ? Strings.T("settings.auto_saved") : message;
            Label(closeButton, Strings.T("settings.close"));
            DrumInput drum = DrumInput.EnsureInstance();
            Label(padButton, Strings.T(drum.IsConnected ? "settings.pad_connected" : "settings.pad_connect"));
            Label(googleButton, Strings.T("settings.google_sign_in"));
            Label(appleButton, Strings.T("settings.apple_sign_in"));
            Label(deleteAccountButton, Strings.T("settings.delete_account"));
            Label(latencyButton, Strings.T("settings.latency_open"));
            Label(inquiryButton, Strings.T("settings.inquiry_open"));
            for (int i = 0; i < languageButtons.Length; i++)
            {
                GameLanguage language = PlayerSettings.DisplayOrder[i];
                Label(languageButtons[i], Strings.T("language." + PlayerSettings.CodeOf(language).Substring(0, 2)));
                bool selected = language == settings.language;
                languageButtons[i].GetComponent<Image>().color = Palette.Lift(selected ? Palette.Sun : Palette.Well);
            }
            refreshing = true;
            musicSlider.value = Mathf.Round(settings.musicVolume * VolumeSteps);
            effectsSlider.value = Mathf.Round(settings.soundEffectsVolume * VolumeSteps);
            refreshing = false;
            musicValue.text = Percent(settings.musicVolume);
            effectsValue.text = Percent(settings.soundEffectsVolume);
        }

        private static string Percent(float volume) { return Mathf.RoundToInt(volume * 100) + "%"; }
        private static void Label(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }

        // The drum test opens on top of this screen and hands control back when it closes; from the
        // latency row it goes straight into calibration when the drum is connected.
        public void OpenPadTest(bool calibrate)
        {
            if (!IsOpen || PadTestOpen) return;
            if (padTest == null)
            {
                padTest = Instantiate(padTestPrefab, transform.parent, false);
                padTest.Closed += () =>
                {
                    Refresh();
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(padButton.gameObject);
                };
            }
            padTest.transform.SetAsLastSibling();
            if (calibrate) padTest.ShowAndCalibrate(); else padTest.Show();
        }

        // Bluetooth tap calibration. The latency row opens the drum test instead; play-mode tests call this directly.
        public void OpenLatency()
        {
            if (!IsOpen || LatencyOpen || latencyPrefab == null) return;
            if (latency == null) latency = Instantiate(latencyPrefab, transform.parent, false);
            latency.transform.SetAsLastSibling();
            latency.Show();
        }

        private void Update()
        {
            if (!IsOpen || PadTestOpen || LatencyOpen) return;
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (PadTestOpen) padTest.Close();
            if (LatencyOpen) latency.Close();
            // Volumes were previewed in memory; persist them once here.
            if (data.HasUnsavedChanges)
            {
                try { data.Save(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            transition.Hide();
            if (Closed != null) Closed();
        }

        // 60 ms soft click, generated once.
        private static AudioClip TickClip()
        {
            if (tickClip != null) return tickClip;
            int rate = AudioSettings.outputSampleRate, length = rate * 60 / 1000;
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-t * 60) * .6f;
            }
            tickClip = AudioClip.Create("SettingsTick", length, 1, rate, false);
            tickClip.SetData(samples, 0);
            return tickClip;
        }
    }
}

using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Bluetooth latency calibration, opened from the settings screen.
    public sealed class LatencyCalibrationPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Text title;
        [SerializeField] private Text message, delayText, statusText;
        [SerializeField] private Slider delaySlider;
        [SerializeField] private Button enableButton, minusButton, plusButton, resetButton, saveButton;
        [Header("Tap test")]
        [SerializeField] private Button testButton, applyButton;
        [SerializeField] private CalibrationTapPad tapPad;
        [SerializeField] private Image beatLight;
        [SerializeField] private Text lightCaption, tapPrompt, tapLog, resultHeading, resultValue, resultDetail;
        private static readonly Color LightOff = Palette.Well;
        private static readonly Color LightOn = Palette.Mint;
        private static readonly Color PadIdle = Palette.Sun;
        private static readonly Color PadPressed = Palette.Coral;
        private const float FlashSeconds = .12f;
        private static AudioClip clickClip;
        private RhythmGameData data;
        private AudioLatencyMonitor latency;
        private AudioSource metronome;
        private bool draftEnabled, testing, wasWarmingUp;
        private float padFlashUntil;
        private Image padImage;
        private double? suggestedDelayMs;
        private PopupTransition transition;
        public event Action Closed;
        public bool IsOpen { get { return transition.IsOpen; } }
        public LatencyCalibration Calibration { get; private set; }
        public bool IsTesting { get { return testing; } }

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            enableButton.onClick.AddListener(() => { draftEnabled = !draftEnabled; RefreshLabels(); });
            delaySlider.onValueChanged.AddListener(value => RefreshLabels());
            minusButton.onClick.AddListener(() => delaySlider.value -= 1);
            plusButton.onClick.AddListener(() => delaySlider.value += 1);
            resetButton.onClick.AddListener(() => delaySlider.value = 0);
            saveButton.onClick.AddListener(SaveAndClose);
            testButton.onClick.AddListener(() => { if (testing) FinishTest(); else StartTest(); });
            applyButton.onClick.AddListener(ApplyMeasurement);
            tapPad.Tapped += Tap;
            padImage = tapPad.GetComponent<Image>();
            metronome = AudioChannel.Create(transform, "Metronome", AudioKind.Effects);
            metronome.loop = true;
            transition = PopupTransition.For(gameObject);
        }

        public void Show() { transition.Show(); }

        private void OnEnable()
        {
            data = GameManager.EnsureInstance().Data;
            latency = AudioLatencyMonitor.EnsureInstance();
            latency.Changed += RefreshLabels;
            draftEnabled = data.Settings.bluetoothCompensationEnabled;
            delaySlider.SetValueWithoutNotify((float)data.Settings.bluetoothDelayMs / 10);
            Calibration = null; suggestedDelayMs = null; testing = false;
            title.text = Strings.T("settings.bluetooth_latency_test");
            message.text = Strings.T("settings.put_on_your_headset_press_start_and_tap");
            resultHeading.text = Strings.T("settings.result");
            SetLabel(closeButton, Strings.T("settings.cancel"));
            SetLabel(saveButton, Strings.T("settings.save_close"));
            SetLabel(resetButton, Strings.T("settings.reset"));
            SetLabel(applyButton, Strings.T("settings.use_result"));
            Text tapLabel = tapPad.GetComponentInChildren<Text>();
            tapLabel.text = Strings.T("settings.tap");
            tapLabel.color = Palette.Ink;   // the pad is Sun yellow
            beatLight.color = LightOff;
            RefreshLabels();
            RefreshTest();
            GetComponent<MenuTypography>().Apply();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(testButton.gameObject);
        }

        private double DraftDelayMs { get { return delaySlider.value * 10; } }

        // Mirrors AudioLatencyMonitor.EffectiveOffsetMs, but with the unsaved draft.
        private double PreviewOffsetMs
        {
            get { return data.Player.inputOffsetMs + (draftEnabled && latency.UsesBluetooth ? DraftDelayMs : 0); }
        }

        private void RefreshLabels()
        {
            if (latency == null) return;
            SetLabel(enableButton, draftEnabled ? Strings.T("settings.bluetooth_compensation_on") :
                Strings.T("settings.bluetooth_compensation_off"));
            delayText.text = DraftDelayMs.ToString("0") + " ms";
            string route = latency.Output == AudioOutputKind.Bluetooth ? Strings.T("settings.bluetooth_audio_in_use") :
                latency.Output == AudioOutputKind.BluetoothConnected ? Strings.T("settings.bluetooth_headset_connected_estimated_ro") :
                latency.Output == AudioOutputKind.Other ? Strings.T("settings.normal_output_bluetooth_offset_inactive") :
                Strings.T("settings.output_unknown_test_detection_on_a_phone");
            double activeBluetooth = data.Settings.BluetoothCompensationMs(latency.UsesBluetooth);
            statusText.text = route + "  ·  " + Strings.T("settings.active_offset") + activeBluetooth.ToString("0") + " ms";
            if (latency.Output == AudioOutputKind.BluetoothConnected)
                statusText.text += "\n" + Strings.T("settings.if_using_the_speaker_turn_this_off_and_s");
            lightCaption.text = Strings.T("settings.light_delayed_by") + PreviewOffsetMs.ToString("0") + " ms";
        }

        public void StartTest()
        {
            if (clickClip == null) clickClip = CreateClickClip(LatencyCalibration.DefaultIntervalSeconds);
            metronome.Stop();
            metronome.clip = clickClip;
            double start = AudioSettings.dspTime + .5;
            Calibration = new LatencyCalibration(start);
            suggestedDelayMs = null;
            metronome.PlayScheduled(start);
            testing = wasWarmingUp = true;
            RefreshTest();
        }

        public void FinishTest()
        {
            if (!testing) return;
            testing = false;
            metronome.Stop();
            beatLight.color = LightOff;
            suggestedDelayMs = Calibration.HasResult
                ? LatencyCalibration.SuggestBluetoothDelayMs(Calibration.MedianMs, data.Player.inputOffsetMs) : (double?)null;
            RefreshTest();
        }

        private void Tap()
        {
            padFlashUntil = Time.unscaledTime + .08f;
            if (testing) TapAt(AudioSettings.dspTime);
            else tapPrompt.text = Strings.T("settings.press_start_first");
        }

        // Audio-clock entry point; also lets tests feed exact tap times.
        public void TapAt(double dspTime)
        {
            if (!testing || Calibration.RecordTap(dspTime) == null) return;
            if (Calibration.IsComplete) FinishTest(); else RefreshTest();
        }

        private void ApplyMeasurement()
        {
            if (!suggestedDelayMs.HasValue) return;
            draftEnabled = true;
            delaySlider.value = (float)(suggestedDelayMs.Value / 10);
            RefreshLabels();
        }

        private void RefreshTest()
        {
            SetLabel(testButton, testing ? Strings.T("settings.stop") :
                Calibration != null ? Strings.T("settings.test_again") : Strings.T("settings.start_test"));
            applyButton.interactable = !testing && suggestedDelayMs.HasValue;
            tapLog.text = FormatOffsets();
            if (testing)
            {
                tapPrompt.text = metronome.volume < .05f ? Strings.T("settings.sound_effects_are_muted") :
                    Calibration.IsWarmingUp(AudioSettings.dspTime) ? Strings.T("settings.listen_for_two_beats") :
                    Strings.T("settings.tap_on_each_click") + Calibration.TapCount + " / " + Calibration.TargetTaps;
                resultValue.text = "-- ms";
                resultDetail.text = Strings.T("settings.testing");
                return;
            }
            if (Calibration == null)
            {
                tapPrompt.text = Strings.T("settings.press_start_when_ready");
                resultValue.text = "-- ms";
                resultDetail.text = Strings.T("settings.tap_to_the_sound_not_the_light");
                return;
            }
            if (!suggestedDelayMs.HasValue)
            {
                tapPrompt.text = Strings.T("settings.not_enough_taps");
                resultValue.text = "-- ms";
                resultDetail.text = Strings.T("settings.at_least") + LatencyCalibration.MinimumTaps + Strings.T("settings.valid_taps_are_needed_try_again");
                return;
            }
            tapPrompt.text = Strings.T("settings.done");
            resultValue.text = Calibration.MedianMs.ToString("0") + " ms";
            string detail = Strings.T("settings.spread") + Calibration.SpreadMs.ToString("0") + " ms · " + Calibration.TapCount + Strings.T("settings.taps") +
                "\n" + Strings.T("settings.suggested_bluetooth_offset") + suggestedDelayMs.Value.ToString("0") + " ms";
            if (Calibration.IsUnstable) detail += "\n" + Strings.T("settings.inconsistent_taps_try_again");
            else if (!latency.UsesBluetooth) detail += "\n" + Strings.T("settings.not_on_bluetooth_for_reference");
            resultDetail.text = detail;
        }

        private string FormatOffsets()
        {
            if (Calibration == null || Calibration.TapCount == 0) return "";
            var text = new StringBuilder();
            foreach (double offset in Calibration.OffsetsMs) text.Append(offset >= 0 ? "+" : "").Append(offset.ToString("0")).Append("  ");
            return text.ToString().TrimEnd();
        }

        private void SaveAndClose()
        {
            try
            {
                data.SetBluetoothCompensation(draftEnabled, DraftDelayMs);
                if (data.HasUnsavedChanges) data.Save();
                latency.Refresh();
                Close();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                latency.Refresh();
                statusText.text = Strings.T("settings.could_not_save_please_retry");
            }
        }
        private static void SetLabel(Button button, string value) { if (button != null) button.GetComponentInChildren<Text>().text = value; }

        private void OnDisable()
        {
            if (latency != null) latency.Changed -= RefreshLabels;
            if (metronome != null) metronome.Stop();
            testing = false;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                if (testing) FinishTest(); else Close();
            }
            // Desktop / editor debugging: space or gamepad south acts as the tap pad.
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) Tap();
            padImage.color = Time.unscaledTime < padFlashUntil ? PadPressed : PadIdle;
            if (!testing) return;
            // The light follows the game's offset, so it lines up with the click once calibrated.
            double since = Calibration.SinceBeat(AudioSettings.dspTime - PreviewOffsetMs / 1000);
            beatLight.color = since >= 0 && since < FlashSeconds ? Color.Lerp(LightOn, LightOff, (float)(since / FlashSeconds)) : LightOff;
            if (Calibration.IsWarmingUp(AudioSettings.dspTime) != wasWarmingUp) { wasWarmingUp = !wasWarmingUp; RefreshTest(); }
        }

        // One beat of audio: a short decaying 1.5 kHz click, then silence. Looped with PlayScheduled,
        // beat k starts exactly k * interval after the scheduled start on the DSP clock.
        private static AudioClip CreateClickClip(double intervalSeconds)
        {
            int rate = AudioSettings.outputSampleRate;
            int length = (int)Math.Round(intervalSeconds * rate);
            var samples = new float[length];
            int clickLength = Math.Min(length, rate * 30 / 1000);
            for (int i = 0; i < clickLength; i++)
            {
                float t = (float)i / rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 1500 * t) * Mathf.Exp(-t * 160) * .9f;
            }
            AudioClip clip = AudioClip.Create("CalibrationClick", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (testing) { testing = false; metronome.Stop(); beatLight.color = LightOff; }
            transition.Hide();
            if (Closed != null) Closed();
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Close);
            if (tapPad != null) tapPad.Tapped -= Tap;
        }
    }
}

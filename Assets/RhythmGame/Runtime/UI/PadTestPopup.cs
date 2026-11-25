using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Drum test (replaces the Bluetooth-earphone test): shows whether the G3003 drum is connected, lights
    // the pad or pedal that was struck with its velocity, and calibrates the delay by striking pads along
    // to a click. The result is stored per audio route (phone speaker vs Bluetooth speaker such as the
    // drum's own "EDRUM") and used by every game while the drum is connected.
    // On-screen pads and the keyboard (Q E A S D Z X C, V / B pedals) feed the same path for trying it
    // without a drum; calibration needs the real drum.
    public sealed class PadTestPopup : MonoBehaviour
    {
        public const int LogRows = 4;
        private const float LitSeconds = .15f;
        private static readonly Key[] Keys = { Key.Q, Key.E, Key.A, Key.S, Key.D, Key.Z, Key.X, Key.C, Key.V, Key.B };
        private static readonly Color Idle = new Color32(84, 80, 98, 255);
        private static readonly Color PedalIdle = new Color32(84, 80, 98, 255);

        [SerializeField] private Button closeButton, scanButton, calibrateButton;
        [SerializeField] private Image connectionDot;
        [SerializeField] private Text title, connectionText, audioText, hitsTitle, calibrationTitle, calibrationHint, offsetText, promptText;
        [SerializeField] private Image[] faces;              // DrumKit inputs 0-9: eight pad faces, then the two pedals
        [SerializeField] private Text[] faceLabels;
        [SerializeField] private Image[] logSwatches, logBars;
        [SerializeField] private Text[] logNames, logValues;
        [SerializeField] private float barWidth = 120;

        private RhythmGameData data;
        private DrumInput drum;
        private AudioLatencyMonitor latency;
        private PopupTransition transition;
        private AudioSource metronome;
        private static AudioClip clickClip;
        private readonly float[] litUntil = new float[DrumKit.InputCount];
        private readonly List<DrumHit> log = new List<DrumHit>();
        private bool testing, wasWarmingUp;
        private string result = "";

        public event Action Closed;
        public bool IsOpen { get { return transition.IsOpen; } }
        public bool IsTesting { get { return testing; } }
        public LatencyCalibration Calibration { get; private set; }
        public IReadOnlyList<DrumHit> Log { get { return log; } }

        private void Awake()
        {
            transition = PopupTransition.For(gameObject);
            closeButton.onClick.AddListener(Close);
            scanButton.onClick.AddListener(() => { drum.Scan(); Refresh(); });
            calibrateButton.onClick.AddListener(() => { if (testing) FinishTest(); else StartTest(); });
            for (int i = 0; i < faces.Length; i++)
            {
                int input = i;
                CalibrationTapPad touch = faces[i].GetComponent<CalibrationTapPad>();
                if (touch != null) touch.Tapped += () => drum.Inject(DrumKit.Notes[input], 100);
            }
            metronome = AudioChannel.Create(transform, "Metronome", AudioKind.Effects);
            metronome.loop = true;
        }

        private void OnEnable()
        {
            data = GameManager.EnsureInstance().Data;
            drum = DrumInput.EnsureInstance();
            latency = AudioLatencyMonitor.EnsureInstance();
            drum.Hit += OnHit;
            drum.StatusChanged += Refresh;
            latency.Changed += Refresh;
            testing = false; Calibration = null; result = "";
            log.Clear();
            Refresh();
            GetComponent<MenuTypography>().Apply();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(calibrateButton.gameObject);
        }

        private void OnDisable()
        {
            if (drum != null) { drum.Hit -= OnHit; drum.StatusChanged -= Refresh; }
            if (latency != null) latency.Changed -= Refresh;
            if (metronome != null) metronome.Stop();
            testing = false;
        }

        public void Show() { transition.Show(); }

        // Opens straight into calibration when the drum is connected (settings' latency row).
        public void ShowAndCalibrate()
        {
            Show();
            if (drum.IsConnected) StartTest();
        }

        // Lights one pad or pedal for a while (a hit uses LitSeconds).
        public void Flash(int input, float seconds = LitSeconds) { litUntil[input] = Time.unscaledTime + seconds; }

        private void OnHit(DrumHit hit)
        {
            Flash(hit.Input);
            log.Insert(0, hit);
            if (log.Count > LogRows) log.RemoveAt(LogRows);
            RefreshLog();
            if (testing) TapAt(AudioSettings.dspTime);
        }

        // Audio-clock entry for a calibration strike; also lets tests feed exact times.
        public void TapAt(double dspTime)
        {
            if (!testing) return;
            if (Calibration.RecordTap(dspTime) != null && Calibration.IsComplete) FinishTest();
            else RefreshPrompt();
        }

        public void StartTest()
        {
            if (!drum.IsConnected) { Refresh(); return; }
            if (clickClip == null) clickClip = CreateClickClip(LatencyCalibration.DefaultIntervalSeconds);
            metronome.Stop();
            metronome.clip = clickClip;
            double start = AudioSettings.dspTime + .5;
            Calibration = new LatencyCalibration(start);
            metronome.PlayScheduled(start);
            testing = wasWarmingUp = true;
            result = "";
            Refresh();
        }

        public void FinishTest()
        {
            if (!testing) return;
            testing = false;
            metronome.Stop();
            if (!Calibration.HasResult) result = Strings.T("pad.not_enough");
            else
            {
                double measured = Math.Max(PlayerSettings.MinPadOffsetMs, Math.Min(PlayerSettings.MaxPadOffsetMs, Calibration.MedianMs));
                try
                {
                    data.SetPadOffset(latency.UsesBluetooth, measured);
                    latency.Refresh();
                    result = Strings.T("pad.done") + Math.Round(measured).ToString("0") + " ms · " + Strings.T("pad.spread") + Calibration.SpreadMs.ToString("0") + " ms";
                    if (Calibration.IsUnstable) result += "\n" + Strings.T("pad.unstable");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    result = Strings.T("settings.could_not_save_please_retry");
                }
            }
            Refresh();
        }

        private void Refresh()
        {
            if (data == null) return;
            title.text = Strings.T("pad.title");
            SetLabel(closeButton, Strings.T("settings.close"));
            SetLabel(scanButton, Strings.T(drum.Scanning ? "pad.scanning_short" : "pad.scan"));
            scanButton.interactable = drum.Supported && !drum.IsConnected && !drum.Scanning;
            string name = string.IsNullOrEmpty(drum.DeviceName) ? "" : " (" + drum.DeviceName + ")";
            connectionText.text =
                drum.Connection == DrumConnection.Bluetooth ? Strings.T("pad.connected_bluetooth") + name :
                drum.Connection == DrumConnection.Usb ? Strings.T("pad.connected_usb") + name :
                drum.Scanning ? Strings.T("pad.scanning") :
                drum.BluetoothOff ? Strings.T("pad.bluetooth_off") :
                !drum.Supported ? Strings.T("pad.unsupported") : Strings.T("pad.not_connected");
            connectionDot.color = drum.IsConnected ? new Color32(90, 186, 120, 255) : drum.Scanning ? (Color)Palette.Orange : (Color)new Color32(190, 186, 200, 255);
            audioText.text = Strings.T(latency.UsesBluetooth ? "pad.audio_bluetooth" : "pad.audio_phone");
            hitsTitle.text = Strings.T("pad.hits");
            calibrationTitle.text = Strings.T("pad.calibration");
            calibrationHint.text = Strings.T(drum.IsConnected ? "pad.calibration_hint" : "pad.needs_drum");
            offsetText.text = Strings.T("pad.current_offset") + data.Settings.PadOffsetMs(latency.UsesBluetooth).ToString("0") + " ms";
            SetLabel(calibrateButton, Strings.T(testing ? "pad.stop" : "pad.start"));
            calibrateButton.interactable = testing || drum.IsConnected;
            for (int i = 0; i < faceLabels.Length; i++)
                if (DrumKit.IsPedal(i)) faceLabels[i].text = Strings.T(DrumKit.NameKey(i));
            RefreshPrompt();
            RefreshLog();
        }

        private void RefreshPrompt()
        {
            if (!testing) { promptText.text = result; return; }
            promptText.text = metronome.volume < .05f ? Strings.T("pad.muted") :
                Calibration.IsWarmingUp(AudioSettings.dspTime) ? Strings.T("pad.listen") :
                Strings.T("pad.hit_on_beat") + Calibration.TapCount + " / " + Calibration.TargetTaps;
        }

        private void RefreshLog()
        {
            for (int row = 0; row < logNames.Length; row++)
            {
                bool used = row < log.Count;
                logSwatches[row].enabled = used;
                logBars[row].enabled = used;
                if (!used)
                {
                    logNames[row].text = row == 0 ? Strings.T("pad.hits_empty") : "";
                    logValues[row].text = "";
                    continue;
                }
                DrumHit hit = log[row];
                Color color = Colour(hit.Input);
                logSwatches[row].color = color;
                logBars[row].color = color;
                logNames[row].text = (DrumKit.IsPedal(hit.Input) ? "" : (hit.Input + 1) + " ") + Strings.T(DrumKit.NameKey(hit.Input));
                logValues[row].text = DrumKit.IsPedal(hit.Input) ? Strings.T("pad.pedal") : Strings.T("pad.velocity") + hit.Velocity;
                RectTransform bar = logBars[row].rectTransform;
                bar.sizeDelta = new Vector2(PixelTween.Snap(barWidth * hit.Velocity / 127f, PixelTween.Pixel), bar.sizeDelta.y);
            }
        }

        public static Color Colour(int input) { return Palette.Rainbow[input % Palette.Rainbow.Length]; }

        private void Update()
        {
            if (!IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { if (testing) FinishTest(); else Close(); }
                for (int i = 0; i < Keys.Length; i++)
                    if (keyboard[Keys[i]].wasPressedThisFrame) drum.Inject(DrumKit.Notes[i], 100);
            }
            for (int i = 0; i < faces.Length; i++)
            {
                bool lit = Time.unscaledTime < litUntil[i];
                faces[i].color = lit ? Colour(i) : DrumKit.IsPedal(i) ? PedalIdle : Idle;
                if (faceLabels[i] != null) faceLabels[i].color = lit ? Palette.Ink : Palette.Panel;
            }
            if (testing && Calibration.IsWarmingUp(AudioSettings.dspTime) != wasWarmingUp) { wasWarmingUp = !wasWarmingUp; RefreshPrompt(); }
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (testing) { testing = false; metronome.Stop(); }
            transition.Hide();
            if (Closed != null) Closed();
        }

        private static void SetLabel(Button button, string value) { button.GetComponentInChildren<Text>().text = value; }

        // One beat: a short decaying 1.5 kHz click, then silence; looped with PlayScheduled so beat k
        // starts exactly k * interval after the start on the DSP clock.
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
            AudioClip clip = AudioClip.Create("PadCalibrationClick", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

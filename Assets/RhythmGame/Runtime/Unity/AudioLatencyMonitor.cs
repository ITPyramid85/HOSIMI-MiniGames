using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RhythmGame
{
    public enum AudioOutputKind { Unknown, Other, Bluetooth, BluetoothConnected }

    [DefaultExecutionOrder(-900)]
    public sealed class AudioLatencyMonitor : MonoBehaviour
    {
        public AudioOutputKind Output { get; private set; }
        public bool UsesBluetooth { get { return Output == AudioOutputKind.Bluetooth || Output == AudioOutputKind.BluetoothConnected; } }
        public double EffectiveOffsetMs { get; private set; }
        public event Action Changed;
        private bool initialized;
        private float nextPoll;
#if UNITY_EDITOR
        public AudioOutputKind? EditorOutputOverride { get; set; }
#endif
        public static AudioLatencyMonitor EnsureInstance()
        {
            var manager = GameManager.EnsureInstance();
            var monitor = manager.GetComponent<AudioLatencyMonitor>();
            if (monitor == null) monitor = manager.gameObject.AddComponent<AudioLatencyMonitor>();
            monitor.Refresh();
            return monitor;
        }
        private void OnEnable() { AudioSettings.OnAudioConfigurationChanged += OnAudioChanged; }
        private void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= OnAudioChanged; }
        private void OnAudioChanged(bool deviceChanged) { Refresh(); }
        private void OnApplicationFocus(bool focused) { if (focused) Refresh(); }
        private void OnApplicationPause(bool paused) { if (!paused) Refresh(); }
        private void Update() { if (Time.unscaledTime >= nextPoll) Refresh(); }

        public void Refresh()
        {
            nextPoll = Time.unscaledTime + .5f;
            if (GameManager.Instance == null || !GameManager.Instance.IsReady) return;
            AudioOutputKind output = ReadOutput();
#if UNITY_EDITOR
            if (EditorOutputOverride.HasValue) output = EditorOutputOverride.Value;
#endif
            var data = GameManager.Instance.Data;
            bool bluetooth = output == AudioOutputKind.Bluetooth || output == AudioOutputKind.BluetoothConnected;
            // With the drum connected, the pad measurement already covers output and input; otherwise
            // (touch play) the touch offset plus the Bluetooth headphone compensation applies.
            bool drum = DrumInput.Instance != null && DrumInput.Instance.IsConnected;
            double effective = drum ? data.Settings.PadOffsetMs(bluetooth)
                : data.Player.inputOffsetMs + data.Settings.BluetoothCompensationMs(bluetooth);
            bool changed = initialized && (output != Output || Math.Abs(effective - EffectiveOffsetMs) > .000001);
            Output = output; EffectiveOffsetMs = effective; initialized = true;
            if (changed && Changed != null) Changed();
        }

        private static AudioOutputKind ReadOutput()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var audio = activity.Call<AndroidJavaObject>("getSystemService", "audio"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 33)
                    {
                        using (var builder = new AndroidJavaObject("android.media.AudioAttributes$Builder"))
                        using (var usage = builder.Call<AndroidJavaObject>("setUsage", 14)) // USAGE_GAME
                        using (var attributes = usage.Call<AndroidJavaObject>("build"))
                        using (var devices = audio.Call<AndroidJavaObject>("getAudioDevicesForAttributes", attributes))
                        {
                            int count = devices.Call<int>("size");
                            if (count == 0) return AudioOutputKind.Unknown;
                            for (int i = 0; i < count; i++)
                                using (var device = devices.Call<AndroidJavaObject>("get", i))
                                    if (IsBluetoothType(device.Call<int>("getType"))) return AudioOutputKind.Bluetooth;
                            return AudioOutputKind.Other;
                        }
                    }
                    // API 23-32 exposes available outputs, not the media route. Mark this
                    // explicitly as a connection-based estimate; no scan or Bluetooth permission.
                    AndroidJavaObject[] outputs = audio.Call<AndroidJavaObject[]>("getDevices", 2);
                    bool connected = false;
                    try { foreach (var device in outputs) if (IsBluetoothType(device.Call<int>("getType"))) connected = true; }
                    finally { foreach (var device in outputs) device.Dispose(); }
                    return connected ? AudioOutputKind.BluetoothConnected : AudioOutputKind.Other;
                }
            }
            catch (Exception) { return AudioOutputKind.Unknown; }
#elif UNITY_IOS && !UNITY_EDITOR
            try { return (AudioOutputKind)RhythmBluetoothOutput(); }
            catch (Exception) { return AudioOutputKind.Unknown; }
#else
            return AudioOutputKind.Unknown;
#endif
        }
        public static bool IsBluetoothType(int type)
        {
            // SCO, A2DP, hearing aid, LE headset, LE speaker, LE broadcast.
            return type == 7 || type == 8 || type == 23 || type == 26 || type == 27 || type == 30;
        }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RhythmBluetoothOutput();
#endif
    }
}

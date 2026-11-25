using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    public enum DrumConnection { None = 0, Usb = 1, Bluetooth = 2 }

    // The physical drum (G3003) as an input device. MIDI arrives on a plugin thread; notes are queued and
    // handed out on the main thread before the games' Update, as DrumHit events in DrumKit numbering.
    // Android: Plugins/Android/DrumMidi.androidlib (USB MIDI at start-up, Bluetooth MIDI on Scan()).
    // Other platforms have no MIDI source yet; Inject() lets tests and on-screen pads feed the same path.
    [DefaultExecutionOrder(-850)]
    public sealed class DrumInput : MonoBehaviour
    {
        public static DrumInput Instance { get; private set; }
        public DrumConnection Connection { get; private set; }
        public string DeviceName { get; private set; }
        public bool IsConnected { get { return Connection != DrumConnection.None; } }
        public bool Scanning { get; private set; }
        public bool BluetoothOff { get; private set; }
        public bool Supported { get { return backend != null; } }
        public event Action<DrumHit> Hit;
        public event Action StatusChanged;

        private readonly object gate = new object();
        private readonly Queue<DrumHit> pending = new Queue<DrumHit>();
        private bool statusDirty;
        private DrumConnection nextConnection;
        private string nextName = "";
        private bool nextScanning, nextBluetoothOff;
        private IDrumMidiBackend backend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        public static DrumInput EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameManager manager = GameManager.EnsureInstance();
            DrumInput input = manager.GetComponent<DrumInput>();
            if (input == null) input = manager.gameObject.AddComponent<DrumInput>();
            return input;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DeviceName = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            backend = AndroidDrumMidi.TryStart(this);
#endif
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (backend != null) backend.Stop();
            Instance = null;
        }

        // Looks for the drum over Bluetooth MIDI (asks for the Bluetooth permission the first time).
        public void Scan()
        {
            if (backend != null) backend.Scan();
        }

        // Thread-safe: a MIDI note-on from any source.
        public void Inject(int note, int velocity)
        {
            int input = DrumKit.FromMidiNote(note);
            if (input < 0 || velocity <= 0) return;
            if (DrumKit.IsPedal(input)) velocity = DrumKit.PedalVelocity;
            lock (gate) pending.Enqueue(new DrumHit { Input = input, Velocity = Math.Min(127, velocity), Note = note });
        }

        // Thread-safe: connection state from the plugin (or tests).
        public void SetStatus(DrumConnection connection, string deviceName, bool scanning = false, bool bluetoothOff = false)
        {
            lock (gate)
            {
                nextConnection = connection; nextName = deviceName ?? "";
                nextScanning = scanning; nextBluetoothOff = bluetoothOff;
                statusDirty = true;
            }
        }

        private void Update() { Dispatch(); }

        // Hands queued hits and status changes to listeners; Update calls it, tests may call it directly.
        public void Dispatch()
        {
            bool changed = false;
            DrumHit[] hits = null;
            lock (gate)
            {
                if (statusDirty)
                {
                    changed = Connection != nextConnection || DeviceName != nextName || Scanning != nextScanning || BluetoothOff != nextBluetoothOff;
                    Connection = nextConnection; DeviceName = nextName; Scanning = nextScanning; BluetoothOff = nextBluetoothOff;
                    statusDirty = false;
                }
                if (pending.Count > 0) { hits = pending.ToArray(); pending.Clear(); }
            }
            if (changed && StatusChanged != null) StatusChanged();
            if (hits == null || Hit == null) return;
            foreach (DrumHit hit in hits) Hit(hit);
        }
    }

    internal interface IDrumMidiBackend
    {
        void Scan();
        void Stop();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Bridge to com.hosimi.drum.DrumMidi (android.media.midi). Its callbacks run on the plugin's thread.
    internal sealed class AndroidDrumMidi : AndroidJavaProxy, IDrumMidiBackend
    {
        private const int StatusNone = 0, StatusUsb = 1, StatusBluetooth = 2, StatusScanning = 3, StatusNotFound = 4, StatusBluetoothOff = 5;
        private const int ScanSeconds = 12;
        private readonly DrumInput sink;
        private AndroidJavaObject plugin;
        private bool connected;

        private AndroidDrumMidi(DrumInput sink) : base("com.hosimi.drum.DrumMidi$Listener") { this.sink = sink; }

        public static IDrumMidiBackend TryStart(DrumInput sink)
        {
            try
            {
                var bridge = new AndroidDrumMidi(sink);
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    bridge.plugin = new AndroidJavaObject("com.hosimi.drum.DrumMidi", activity, bridge);
                if (!bridge.plugin.Call<bool>("start")) return null;   // no MIDI support on this phone
                // Reconnect to the drum over Bluetooth by itself when the permission was given before.
                if (BluetoothPermitted()) bridge.plugin.Call("scanBluetooth", ScanSeconds);
                return bridge;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Drum MIDI unavailable: " + e.Message);
                return null;
            }
        }

        private static int SdkLevel()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) return version.GetStatic<int>("SDK_INT");
        }

        private static string[] BluetoothPermissions()
        {
            return SdkLevel() >= 31
                ? new[] { "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT" }
                : new[] { "android.permission.ACCESS_FINE_LOCATION" };
        }

        private static bool BluetoothPermitted()
        {
            foreach (string permission in BluetoothPermissions())
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission)) return false;
            return true;
        }

        public void Scan()
        {
            if (connected) return;
            if (BluetoothPermitted()) { plugin.Call("scanBluetooth", ScanSeconds); return; }
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => { if (BluetoothPermitted()) plugin.Call("scanBluetooth", ScanSeconds); };
            callbacks.PermissionDenied += _ => sink.SetStatus(DrumConnection.None, "");
            UnityEngine.Android.Permission.RequestUserPermissions(BluetoothPermissions(), callbacks);
        }

        public void Stop()
        {
            if (plugin == null) return;
            try { plugin.Call("stop"); } catch (Exception) { }
            plugin.Dispose();
            plugin = null;
        }

        // Called from Java.
        public void onNote(int note, int velocity) { sink.Inject(note, velocity); }

        public void onStatus(int status, string name)
        {
            connected = status == StatusUsb || status == StatusBluetooth;
            sink.SetStatus(status == StatusUsb ? DrumConnection.Usb : status == StatusBluetooth ? DrumConnection.Bluetooth : DrumConnection.None,
                connected ? name : "", status == StatusScanning, status == StatusBluetoothOff);
        }
    }
#endif
}

package com.hosimi.drum;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanFilter;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Context;
import android.media.midi.MidiDevice;
import android.media.midi.MidiDeviceInfo;
import android.media.midi.MidiManager;
import android.media.midi.MidiOutputPort;
import android.media.midi.MidiReceiver;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.ParcelUuid;

import java.io.IOException;
import java.util.Collections;
import java.util.UUID;

// Reads the HOSIMI drum (G3003) through android.media.midi: USB MIDI devices are opened as soon as
// they appear; Bluetooth MIDI ("BLE_MIDI") is found by scanning for the standard BLE-MIDI service and
// opened through MidiManager. Note-ons go to the Listener (implemented in C# by AndroidJavaProxy).
// The caller must hold BLUETOOTH_SCAN + BLUETOOTH_CONNECT (Android 12+) or location (older) before
// scanBluetooth(); USB needs no runtime permission.
public final class DrumMidi {
    public interface Listener {
        void onNote(int note, int velocity);
        void onStatus(int status, String name);
    }

    public static final int STATUS_NONE = 0, STATUS_USB = 1, STATUS_BLUETOOTH = 2, STATUS_SCANNING = 3,
        STATUS_NOT_FOUND = 4, STATUS_BLUETOOTH_OFF = 5;
    private static final ParcelUuid MIDI_SERVICE = new ParcelUuid(UUID.fromString("03B80E5A-EDE8-4B33-A751-6CE34EC4C700"));

    private final Context context;
    private final Listener listener;
    private final HandlerThread thread = new HandlerThread("DrumMidi");
    private Handler handler;
    private MidiManager midi;
    private MidiDevice device;
    private MidiDeviceInfo deviceInfo;
    private MidiOutputPort port;
    private BluetoothLeScanner scanner;
    private boolean scanning;

    public DrumMidi(Context context, Listener listener) {
        this.context = context.getApplicationContext();
        this.listener = listener;
    }

    public boolean start() {
        if (!context.getPackageManager().hasSystemFeature("android.software.midi")) return false;
        midi = (MidiManager) context.getSystemService(Context.MIDI_SERVICE);
        if (midi == null) return false;
        thread.start();
        handler = new Handler(thread.getLooper());
        midi.registerDeviceCallback(deviceCallback, handler);
        handler.post(() -> { for (MidiDeviceInfo info : midi.getDevices()) open(info); });
        return true;
    }

    public void stop() {
        if (handler == null) return;
        handler.post(() -> {
            stopScan();
            midi.unregisterDeviceCallback(deviceCallback);
            close();
            thread.quitSafely();
        });
    }

    // Looks for the drum over Bluetooth for the given time, then gives up with STATUS_NOT_FOUND.
    public void scanBluetooth(final int seconds) {
        if (handler == null) return;
        handler.post(() -> {
            if (device != null || scanning) return;
            BluetoothManager manager = (BluetoothManager) context.getSystemService(Context.BLUETOOTH_SERVICE);
            BluetoothAdapter adapter = manager != null ? manager.getAdapter() : null;
            if (adapter == null || !adapter.isEnabled()) { listener.onStatus(STATUS_BLUETOOTH_OFF, ""); return; }
            scanner = adapter.getBluetoothLeScanner();
            if (scanner == null) { listener.onStatus(STATUS_BLUETOOTH_OFF, ""); return; }
            ScanFilter filter = new ScanFilter.Builder().setServiceUuid(MIDI_SERVICE).build();
            ScanSettings settings = new ScanSettings.Builder().setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY).build();
            try {
                scanner.startScan(Collections.singletonList(filter), settings, scanCallback);
            } catch (SecurityException e) {
                listener.onStatus(STATUS_NONE, "");
                return;
            }
            scanning = true;
            listener.onStatus(STATUS_SCANNING, "");
            handler.postDelayed(() -> {
                if (!scanning) return;
                stopScan();
                if (device == null) listener.onStatus(STATUS_NOT_FOUND, "");
            }, seconds * 1000L);
        });
    }

    private void stopScan() {
        if (!scanning || scanner == null) return;
        scanning = false;
        try { scanner.stopScan(scanCallback); } catch (SecurityException | IllegalStateException ignored) { }
    }

    private final ScanCallback scanCallback = new ScanCallback() {
        @Override public void onScanResult(int callbackType, final ScanResult result) {
            handler.post(() -> {
                if (!scanning || device != null) return;
                stopScan();
                final BluetoothDevice found = result.getDevice();
                try {
                    midi.openBluetoothDevice(found, opened -> {
                        if (opened == null) { listener.onStatus(STATUS_NOT_FOUND, ""); return; }
                        attach(opened, opened.getInfo());
                    }, handler);
                } catch (SecurityException e) {
                    listener.onStatus(STATUS_NONE, "");
                }
            });
        }

        @Override public void onScanFailed(int errorCode) {
            handler.post(() -> { scanning = false; listener.onStatus(STATUS_NOT_FOUND, ""); });
        }
    };

    private final MidiManager.DeviceCallback deviceCallback = new MidiManager.DeviceCallback() {
        @Override public void onDeviceAdded(MidiDeviceInfo info) { open(info); }

        @Override public void onDeviceRemoved(MidiDeviceInfo info) {
            if (deviceInfo != null && info.getId() == deviceInfo.getId()) {
                close();
                listener.onStatus(STATUS_NONE, "");
            }
        }
    };

    // USB (and already-paired Bluetooth) devices that send MIDI out to us.
    private void open(MidiDeviceInfo info) {
        if (device != null || info.getOutputPortCount() == 0) return;
        if (info.getType() == MidiDeviceInfo.TYPE_VIRTUAL) return;
        midi.openDevice(info, opened -> { if (opened != null) attach(opened, info); }, handler);
    }

    private void attach(MidiDevice opened, MidiDeviceInfo info) {
        if (device != null) { closeQuietly(opened); return; }
        MidiOutputPort output = opened.openOutputPort(0);
        if (output == null) { closeQuietly(opened); return; }
        device = opened;
        deviceInfo = info;
        port = output;
        port.connect(receiver);
        Bundle properties = info.getProperties();
        String name = properties.getString(MidiDeviceInfo.PROPERTY_NAME);
        if (name == null) name = properties.getString(MidiDeviceInfo.PROPERTY_PRODUCT);
        listener.onStatus(info.getType() == MidiDeviceInfo.TYPE_BLUETOOTH ? STATUS_BLUETOOTH : STATUS_USB, name == null ? "" : name);
    }

    private void close() {
        if (port != null) {
            port.disconnect(receiver);
            try { port.close(); } catch (IOException ignored) { }
            port = null;
        }
        closeQuietly(device);
        device = null;
        deviceInfo = null;
    }

    private static void closeQuietly(MidiDevice d) {
        if (d == null) return;
        try { d.close(); } catch (IOException ignored) { }
    }

    // Channel-voice parser with running status; only note-on with velocity > 0 is reported.
    private final MidiReceiver receiver = new MidiReceiver() {
        private int status, first, count;

        @Override public void onSend(byte[] data, int offset, int length, long timestamp) {
            for (int i = offset; i < offset + length; i++) {
                int b = data[i] & 0xFF;
                if (b >= 0xF8) continue;                 // real-time bytes may appear anywhere
                if (b >= 0x80) { status = b < 0xF0 ? b : 0; count = 0; continue; }
                if (status == 0) continue;
                int kind = status & 0xF0;
                int needed = kind == 0xC0 || kind == 0xD0 ? 1 : 2;
                if (count == 0) { first = b; count = 1; }
                else { count = 0; if (kind == 0x90 && b > 0) listener.onNote(first, b); continue; }
                if (needed == 1) count = 0;
            }
        }
    };
}

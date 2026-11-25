using System;

namespace RhythmGame
{
    // Persisted values: do not reorder or reuse these numbers.
    public enum GameLanguage { SimplifiedChinese = 0, English = 1, Korean = 2, Japanese = 3 }

    [Serializable]
    public sealed class PlayerSettings
    {
        public GameLanguage language = GameLanguage.SimplifiedChinese;
        // Linear gain: 0 = silent, 1 = full volume. UI sliders can display value * 100%.
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float soundEffectsVolume = 1f;
        // Optional v2 fields: missing fields in older saves mean disabled / zero.
        public bool bluetoothCompensationEnabled;
        public double bluetoothDelayMs;
        public const double MaxBluetoothDelayMs = 1000;
        // Drum (G3003) delay measured by striking the pads: sound out + pad in, per audio route, because
        // the drum's own Bluetooth speaker ("EDRUM") is far slower than the phone speaker. Optional v3
        // fields: older saves read 0 = not measured.
        public double padOffsetMs;
        public double padOffsetBluetoothAudioMs;
        public const double MinPadOffsetMs = -300, MaxPadOffsetMs = 1000;

        public double PadOffsetMs(bool bluetoothAudio) { return bluetoothAudio ? padOffsetBluetoothAudioMs : padOffsetMs; }

        public static void ValidatePadOffset(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < MinPadOffsetMs || value > MaxPadOffsetMs)
                throw new ArgumentOutOfRangeException("padOffsetMs");
        }

        public double BluetoothCompensationMs(bool bluetoothOutput)
        {
            return bluetoothCompensationEnabled && bluetoothOutput ? bluetoothDelayMs : 0;
        }

        public static void ValidateBluetoothDelay(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > MaxBluetoothDelayMs)
                throw new ArgumentOutOfRangeException("bluetoothDelayMs");
        }

        public string LanguageCode { get { return CodeOf(language); } }

        // Order the settings screen lists languages in (zh, ja, en, ko); enum values are the save format.
        public static readonly GameLanguage[] DisplayOrder =
            { GameLanguage.SimplifiedChinese, GameLanguage.Japanese, GameLanguage.English, GameLanguage.Korean };

        // Column header used for this language in the string table.
        public static string CodeOf(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.SimplifiedChinese: return "zh-Hans";
                case GameLanguage.English: return "en";
                case GameLanguage.Korean: return "ko";
                case GameLanguage.Japanese: return "ja";
                default: throw new InvalidOperationException("Unsupported language.");
            }
        }

        // For direct AudioSource volume integration. Do not apply master gain again.
        public float EffectiveMusicVolume { get { return masterVolume * musicVolume; } }
        public float EffectiveSoundEffectsVolume { get { return masterVolume * soundEffectsVolume; } }

        public static void ValidateLanguage(GameLanguage value)
        {
            if (!Enum.IsDefined(typeof(GameLanguage), value)) throw new ArgumentOutOfRangeException("language");
        }

        public static void ValidateVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException("volume", "Volume must be a finite number between 0 and 1.");
        }

        public void Validate()
        {
            ValidateLanguage(language);
            ValidateVolume(masterVolume);
            ValidateVolume(musicVolume);
            ValidateVolume(soundEffectsVolume);
            ValidateBluetoothDelay(bluetoothDelayMs);
            ValidatePadOffset(padOffsetMs);
            ValidatePadOffset(padOffsetBluetoothAudioMs);
        }
    }
}

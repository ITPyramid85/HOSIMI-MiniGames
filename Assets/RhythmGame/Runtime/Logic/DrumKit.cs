namespace RhythmGame
{
    // One strike from the drum: which input (0-7 pads, 8 hi-hat pedal, 9 kick) and how hard.
    public struct DrumHit
    {
        public int Input;
        public int Velocity;   // 1-127 from the pads; pedals have none and report DrumKit.PedalVelocity
        public int Note;       // MIDI note it came from
    }

    // Layout of the client's drum, the G3003 portable kit (manual 2025-08-27): eight velocity pads and
    // two pedals, sending MIDI over Bluetooth LE ("BLE_MIDI") or USB. Input numbers follow the manual's
    // PAD1-PAD8, so pad index 7 is PAD8 TOM3 (the mochi game's pad).
    public static class DrumKit
    {
        public const int PadCount = 8;
        public const int HiHatPedal = 8, KickPedal = 9;
        public const int InputCount = 10;
        public const int PedalVelocity = 127;

        // Main note per input, as printed in the manual; the hi-hat pad also sends 42 when closed.
        public static readonly int[] Notes = { 49, 51, 46, 47, 45, 38, 37, 43, 44, 36 };

        public static bool IsPedal(int input) { return input == HiHatPedal || input == KickPedal; }

        // MIDI note -> input, or -1 for notes this kit does not send.
        public static int FromMidiNote(int note)
        {
            switch (note)
            {
                case 49: return 0;   // PAD1 crash
                case 51: return 1;   // PAD2 ride
                case 46: case 42: return 2;   // PAD3 hi-hat, open / closed by the left pedal
                case 47: return 3;   // PAD4 tom 1
                case 45: return 4;   // PAD5 tom 2
                case 38: return 5;   // PAD6 snare
                case 37: return 6;   // PAD7 snare cross-stick
                case 43: return 7;   // PAD8 tom 3
                case 44: return HiHatPedal;
                case 36: return KickPedal;
                default: return -1;
            }
        }

        public static string NameKey(int input) { return "pad.name_" + input; }
    }
}

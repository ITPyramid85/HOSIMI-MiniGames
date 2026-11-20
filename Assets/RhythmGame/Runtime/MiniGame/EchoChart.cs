using System;

namespace RhythmGame
{
    // Game 8. The rival plays a whole phrase, the player answers it from memory, and a right answer
    // adds one beat. Three stages, each with its own pads, opening phrase and number of rounds.
    // Seven failures end the run, whichever stage it is in.
    public static class EchoChart
    {
        public const double Bpm = 100;
        public const int MaxFails = 7;
        public static double BeatSeconds { get { return 60.0 / Bpm; } }

        // Keyboard index, matching MiniGameController pads. Display number is index + 1.
        private static readonly int[] BottomAndMiddle = { 3, 4, 5, 7 };          // S D Z C
        private static readonly int[] WithLeftTom = { 2, 3, 4, 5, 7 };           // + A
        private static readonly int[] WithCymbals = { 0, 1, 2, 3, 4, 5, 7 };     // + Q E

        public sealed class EchoStage
        {
            public int Number;
            public int StartLength;
            public int Rounds;
            public int[] Pads;
            // The phrase after the last round of the stage.
            public int EndLength { get { return StartLength + Rounds - 1; } }
        }

        public static readonly EchoStage[] Stages = {
            new EchoStage { Number = 1, StartLength = 2, Rounds = 7, Pads = BottomAndMiddle },
            new EchoStage { Number = 2, StartLength = 4, Rounds = 5, Pads = WithLeftTom },
            new EchoStage { Number = 3, StartLength = 5, Rounds = 5, Pads = WithCymbals } };

        public static int[] AllPads { get { return WithCymbals; } }
        public static int Count { get { return Stages.Length; } }

        public static EchoStage Get(int number)
        {
            return Stages[Math.Min(Stages.Length, Math.Max(1, number)) - 1];
        }

        public static int[] PadsIn(int number) { return Get(number).Pads; }

        // A fresh phrase of exactly this length. draw is how many phrases this stage has already used,
        // so the next round is a new sequence rather than the last one with a beat stuck on the end.
        public static int[] Phrase(int seed, int stage, int length, int draw)
        {
            EchoStage step = Get(stage);
            if (length < 1 || length > step.EndLength) throw new ArgumentOutOfRangeException("length");
            var random = new Random(unchecked(seed * 397 + step.Number * 131 + draw * 977));
            var notes = new int[length];
            for (int i = 0; i < notes.Length; i++) notes[i] = step.Pads[random.Next(step.Pads.Length)];
            return notes;
        }
    }

    // The phrase the rival plays and the player answers. A correct echo grows it by one beat.
    public sealed class EchoMemory
    {
        private int[] notes;
        public int Length;
        public int Best;
        public int Cursor;
        public bool Listening = true;

        public EchoMemory(int[] notes, int startLength)
        {
            if (notes == null || notes.Length == 0) throw new ArgumentException("Empty phrase.");
            if (startLength < 1 || startLength > notes.Length) throw new ArgumentOutOfRangeException("startLength");
            this.notes = notes;
            Length = startLength;
        }

        public int Current { get { return notes[Cursor]; } }
        public bool AtFullLength { get { return Length >= notes.Length; } }

        // The next beat of the rival's phrase. Listening ends after the last beat.
        public int Hear()
        {
            int pad = notes[Cursor++];
            if (Cursor >= Length)
            {
                Listening = false;
                Cursor = 0;
            }
            return pad;
        }

        // True when this pad continues the echo. grown is set when the whole phrase was right.
        public bool Answer(int pad, out bool grown)
        {
            grown = false;
            if (Listening || pad != notes[Cursor]) return false;
            Cursor++;
            if (Cursor < Length) return true;
            Best = Math.Max(Best, Length);
            Cursor = 0;
            Listening = true;
            grown = true;
            return true;
        }

        // The next round is its own phrase. A miss keeps the one the player just heard.
        public void Use(int[] phrase)
        {
            if (phrase == null || phrase.Length == 0) throw new ArgumentException("Empty phrase.");
            notes = phrase;
            Length = phrase.Length;
            Cursor = 0;
            Listening = true;
        }

        public void Fail()
        {
            Cursor = 0;
            Listening = true;
        }
    }
}

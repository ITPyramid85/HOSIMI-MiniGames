using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Conveyor of firewood on one pad. A thick log is two beats in a row. A watermelon is a bonus,
    // a stone must not be hit. The axe turns gold after a streak of clean chops.
    public static class WoodChart
    {
        public const double Bpm = 120, FirstCue = 2, RunSeconds = 120;
        // The gap matches the controller's three-beat ride, so the next piece enters as the last one leaves the board.
        public const int Pad = 7, GoldAt = 2, GapBeats = 3;

        public static double BeatSeconds { get { return 60 / Bpm; } }
        public static int[] Pads(int stage) { return new[] { Pad }; }

        public static List<BeatCue> Generate(int seed)
        {
            var random = new Random(seed);
            double beat = BeatSeconds, last = RunSeconds - 2;
            Stage window = StageTable.Get(1);
            var cues = new List<BeatCue>();
            int piece = 0;
            for (double time = FirstCue; time <= last + 1e-9; piece++)
            {
                int hits;
                CueKind kind = Roll(random, window.Number, out hits);
                for (int h = 0; h < hits; h++)
                {
                    cues.Add(new BeatCue
                    {
                        Index = cues.Count, Time = time, Pad = Pad, Kind = kind, Variant = piece,
                        PerfectMs = window.PerfectMs, GoodMs = window.GoodMs
                    });
                    time += beat;
                }
                time += GapBeats * beat;
            }
            return cues;
        }

        // Stones ride the opening belt too, and stay the minority. Thick logs and watermelons do as well.
        private static CueKind Roll(Random random, int stage, out int hits)
        {
            double roll = random.NextDouble();
            double stone = Math.Min(.2, .15 + (stage - 1) * .006);
            const double melon = .12, thick = .2;
            hits = 1;
            if (roll < stone) return CueKind.Forbidden;
            if (roll < stone + melon) return CueKind.Bonus;
            if (roll < stone + melon + thick) { hits = 2; return CueKind.Normal; }
            return CueKind.Normal;
        }
    }
}

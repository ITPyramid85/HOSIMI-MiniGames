using System.Collections.Generic;

namespace RhythmGame
{
    // Seven heats forge one sword, and swords follow one another for the whole 8-minute run.
    // Each glow in a sword is shorter than the last. Hammering blows are pad 2; the finish is crash 1.
    public static class SmithChart
    {
        public const double Bpm = 120, FirstCue = 2;
        public const int CrashPad = 0, StackPad = 1, HeatCount = 7, LegendaryAt = 7;
        private static readonly double[] Perfect = { 110, 100, 90, 80, 70, 60, 50 };
        private static readonly double[] Glow = { 280, 250, 220, 190, 160, 130, 100 };

        public static double BeatSeconds { get { return 60 / Bpm; } }
        // Shown on the start dialog as pads 1 and 2.
        public static int[] Pads(int stage) { return new[] { CrashPad, StackPad }; }
        public static string StageKey(int forged)
        {
            if (forged >= LegendaryAt) return "smith.legendary";
            if (forged <= 0) return "smith.stage1";
            return "smith.stage" + forged;
        }

        public static List<BeatCue> Generate(int seed)
        {
            var cues = new List<BeatCue>();
            double time = FirstCue, last = StageTable.ClearSeconds - 2;
            for (int heat = 0; time <= last; heat++)
            {
                int step = heat % HeatCount;
                cues.Add(new BeatCue
                {
                    Index = cues.Count, Time = time, Pad = step == HeatCount - 1 ? CrashPad : StackPad, Variant = heat,
                    PerfectMs = Perfect[step], GoodMs = Glow[step]
                });
                time += BeatSeconds * 3;
            }
            return cues;
        }
    }
}

using System.Collections.Generic;

namespace RhythmGame
{
    // Game 7. Two clubs alternate on the middle-row toms: pad 4 (S) then pad 5 (D), then pad 4 again.
    // The wait after each blow is shorter than the wait before it, down to half a beat.
    public static class FabricChart
    {
        public const double Bpm = 120, FirstCue = 2;
        public const int LeftPad = 3, RightPad = 4;   // display pads 4 and 5
        // The opening blow waits two beats. Every blow after that waits a little less, down to half a beat.
        private const double SlowBeats = 2, FastBeats = .5;

        public static double BeatSeconds { get { return 60 / Bpm; } }
        public static int[] Pads(int stage) { return new[] { LeftPad, RightPad }; }

        public static List<BeatCue> Generate(int seed)
        {
            var cues = new List<BeatCue>();
            double time = FirstCue;
            bool left = true;
            double last = StageTable.ClearSeconds - 1;
            while (time <= last + 1e-9)
            {
                Stage stage = StageTable.At(time);
                cues.Add(new BeatCue
                {
                    Index = cues.Count,
                    Time = time,
                    Pad = left ? LeftPad : RightPad,
                    PerfectMs = stage.PerfectMs,
                    GoodMs = stage.GoodMs
                });
                time += GapSeconds(time);
                left = !left;
            }
            return cues;
        }

        // Shrinks for the whole run, so a later blow is always sooner than an earlier one.
        private static double GapSeconds(double time)
        {
            double along = System.Math.Max(0, System.Math.Min(1, time / StageTable.ClearSeconds));
            return (SlowBeats + (FastBeats - SlowBeats) * along) * BeatSeconds;
        }
    }
}

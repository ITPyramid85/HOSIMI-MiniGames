using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Plan §4-01 rice-cake pounding: Grandma flips the rice cake at irregular intervals; strike pad 8
    // (tom 3) on each flip. Surviving the whole 8 minutes opens the clear dialog. Level 1's lift stays
    // lenient and later levels quicken on Mochi's own ladder. Rainbow rice cakes are bonus.
    // The shared countdown is beats 0..3 (3, 2, 1, GO). The next two beats are only for listening.
    // The first flip, and the first beat that can be judged, is the third beat after GO.
    public static class MochiChart
    {
        public const double Bpm = 120;
        public const int PrepBeats = 2;
        public const int Pad = 7;   // pad 8, tom 3
        public const double RunSeconds = StageTable.ClearSeconds;
        public const double LevelOneSeconds = 150;
        // Level 1's window covers the whole slow lift, so a strike while the cake is coming up still lands.
        // Later stages narrow, but each one stays wider than the stage after it.
        private static readonly double[] Perfect = { 400, 320, 260, 210, 170, 140, 110, 90, 70, 55 };
        private static readonly double[] Good = { 900, 720, 580, 460, 360, 280, 220, 170, 130, 100 };
        // Level 1 holds for 2:30. Later stages keep a gentler climb than the shared clock.
        private static readonly double[] Starts = { 0, 150, 185, 220, 260, 305, 370, 405, 435, 455 };
        // Gaps in beats per level. Level 1 is a little quicker than a 3-second lift.
        private static readonly double[][] Gaps = {
            new[] { 5, 4.5, 4 }, new double[] { 5, 4, 4 }, new[] { 4, 3.5, 3 }, new[] { 3, 2.5, 2 }, new[] { 2.5, 2, 1.5 },
            new[] { 2, 1.5, 1 }, new[] { 1.5, 1, .5 }, new[] { 1, .5, .5 }, new[] { .5, 1, .5 }, new[] { .5, .5, .5 } };
        private static readonly double[] FeintChance = { 0, 0, 0, 0, 0, .08, .10, .12, .15, .18 };
        private const double RainbowChance = .08;

        public static double BeatSeconds { get { return 60.0 / Bpm; } }
        // Beat index 6: two beats after the GO beat.
        public static double FirstCue { get { return (4 + PrepBeats) * BeatSeconds; } }
        // Presses before this are the listen beats, not misses. A wide level-1 window must not open during them.
        public static double JudgementOpens
        {
            get
            {
                double byWindow = FirstCue - StageOf(1).GoodMs / 1000.0;
                double afterListenBeats = 5 * BeatSeconds + .02;
                return byWindow > afterListenBeats ? byWindow : afterListenBeats;
            }
        }
        public static int[] Pads(int stage) { return new[] { Pad }; }

        public static Stage StageOf(int number)
        {
            if (number < 1 || number > Starts.Length) throw new ArgumentOutOfRangeException("number");
            int i = number - 1;
            return new Stage { Number = number, StartSeconds = Starts[i], PerfectMs = Perfect[i], GoodMs = Good[i] };
        }

        public static Stage StageAt(double seconds)
        {
            int number = 1;
            while (number < Starts.Length && seconds >= Starts[number]) number++;
            return StageOf(number);
        }

        public static double ProgressIn(double seconds)
        {
            return Math.Max(0, Math.Min(1, seconds / RunSeconds));
        }

        public static List<BeatCue> Generate(int seed)
        {
            var random = new Random(seed);
            var cues = new List<BeatCue>();
            double time = FirstCue, last = RunSeconds - 1;
            while (time <= last + 1e-9)
            {
                Stage stage = StageAt(time);
                int i = stage.Number - 1;
                double roll = random.NextDouble();
                CueKind kind = roll < FeintChance[i] ? CueKind.Forbidden : roll < FeintChance[i] + RainbowChance ? CueKind.Bonus : CueKind.Normal;
                cues.Add(new BeatCue { Index = cues.Count, Time = time, Pad = Pad, Kind = kind, PerfectMs = stage.PerfectMs, GoodMs = stage.GoodMs });
                double gap = Gaps[i][random.Next(Gaps[i].Length)];
                // A feint is always followed by at least a full beat, so the real flip reads clearly.
                if (kind == CueKind.Forbidden) gap = Math.Max(gap, 1);
                time += gap * BeatSeconds;
            }
            return cues;
        }
    }
}

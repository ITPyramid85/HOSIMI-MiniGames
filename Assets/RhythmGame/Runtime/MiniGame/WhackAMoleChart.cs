using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Plan §4-02: one mole per beat from the hole matching a pad. An endless run (client reply v5 §1)
    // starts on the bottom three pads, widens to the middle row and then all eight, and speeds up;
    // gold moles are bonus, flower moles must not be hit, later stages add quick pairs and disguises.
    public static class WhackAMoleChart
    {
        public const double Bpm = 120, FirstCue = 2, ShowSeconds = 1;
        public const int Disguises = 2;   // visual variants 1..2 (hat, glasses)
        // Indexed by stage - 1.
        private static readonly double[] StepBeats = { 2, 2, 1.5, 1.5, 1, 1, 1, 1, 1, 1 };
        private static readonly double[] FlowerChance = { 0, 0, 0, .08, .10, .12, .14, .15, .16, .18 };
        private static readonly double[] PairChance = { 0, 0, 0, 0, .05, .10, .15, .20, .25, .30 };
        private const double GoldChance = .08, DisguiseChance = .35;
        private const int DisguiseFrom = 6;

        public static double BeatSeconds { get { return 60 / Bpm; } }

        public static int[] Pads(int stage)
        {
            return stage <= 2 ? new[] { 5, 6, 7 } : stage <= 4 ? new[] { 2, 3, 4, 5, 6, 7 } : new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        }

        public static List<BeatCue> Generate(int seed)
        {
            var random = new Random(seed);
            double beat = BeatSeconds, last = StageTable.ClearSeconds - 1;
            var cues = new List<BeatCue>();
            int lastPad = -1;
            for (double time = FirstCue; time <= last + 1e-9;)
            {
                Stage stage = StageTable.At(time);
                int i = stage.Number - 1;
                lastPad = Add(cues, random, stage, time, lastPad);
                double step = beat * StepBeats[i];
                // A quick second mole half a beat later, then a breather.
                if (random.NextDouble() < PairChance[i] && time + beat / 2 <= last)
                {
                    lastPad = Add(cues, random, StageTable.At(time + beat / 2), time + beat / 2, lastPad, normalOnly: true);
                    step += beat;
                }
                time += step;
            }
            return cues;
        }

        private static int Add(List<BeatCue> cues, Random random, Stage stage, double time, int lastPad, bool normalOnly = false)
        {
            int[] pads = Pads(stage.Number);
            int pad;
            do pad = pads[random.Next(pads.Length)]; while (pad == lastPad);
            double roll = random.NextDouble();
            CueKind kind = normalOnly ? CueKind.Normal : roll < GoldChance ? CueKind.Bonus :
                roll < GoldChance + FlowerChance[stage.Number - 1] ? CueKind.Forbidden : CueKind.Normal;
            int variant = kind == CueKind.Normal && stage.Number >= DisguiseFrom && random.NextDouble() < DisguiseChance ? 1 + random.Next(Disguises) : 0;
            cues.Add(new BeatCue { Index = cues.Count, Time = time, Pad = pad, Kind = kind, Variant = variant,
                PerfectMs = stage.PerfectMs, GoodMs = stage.GoodMs });
            return pad;
        }
    }
}

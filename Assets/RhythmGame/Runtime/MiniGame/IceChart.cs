using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Eight fish pass under the ice. Later ones are faster. Big ones take two strikes. Some only pretend to stop.
    public static class IceChart
    {
        public const double Bpm = 120, FirstCue = 2.5;
        // Drum pads are numbered from 1. Pad 4 is the left ice, pad 5 the middle, pad 8 the right.
        public const int Left = 3, Center = 4, Right = 7, SpeciesCount = 6;
        public static readonly string[] Species = { "perch", "trout", "carp", "pike", "salmon", "char" };
        // The fish only travels between the three tiles: center, right, center, left, and back.
        private static readonly int[] Route = { Center, Right, Center, Left };
        // Species 3 and up are the big ones. Feints are single forbidden cues.
        private static readonly int[] SpeciesPlan = { 0, 1, 2, 3, 0, 1, 4, 5 };
        private static readonly bool[] FeintPlan = { false, false, true, false, false, true, false, false };

        public static double BeatSeconds { get { return 60 / Bpm; } }
        public static bool IsBig(int species) { return species >= 3; }
        public static int SpeciesOf(int variant) { return variant / 100; }
        public static int FishOf(int variant) { return variant % 100; }
        public static string SpeciesId(int variant) { return Species[SpeciesOf(variant)]; }
        public static int[] Pads(int stage) { return new[] { Left, Center, Right }; }

        public static int HoleIndex(int pad)
        {
            if (pad == Left) return 0;
            if (pad == Center) return 1;
            if (pad == Right) return 2;
            return -1;
        }

        public static List<BeatCue> Generate(int seed)
        {
            var cues = new List<BeatCue>();
            double time = FirstCue;
            for (int fish = 0; fish < SpeciesPlan.Length; fish++)
            {
                int species = SpeciesPlan[fish];
                int hole = Route[fish % Route.Length];
                int variant = species * 100 + fish;
                // Half-window is 0.3s, so the fish's 0.6s stay is exactly the hittable time.
                double good = 300;
                double perfect = good * .45;
                if (FeintPlan[fish])
                    cues.Add(Cue(cues.Count, time, hole, CueKind.Forbidden, variant, perfect, good));
                else if (IsBig(species))
                {
                    double gap = good / 1000 * 2;
                    cues.Add(Cue(cues.Count, time, hole, CueKind.Normal, variant, perfect, good));
                    cues.Add(Cue(cues.Count, time + gap, hole, CueKind.Normal, variant, perfect, good));
                    time += gap;
                }
                else
                    cues.Add(Cue(cues.Count, time, hole, CueKind.Normal, variant, perfect, good));
                time += Math.Max(1.35, 2.5 - fish * .12);
            }
            return cues;
        }

        private static BeatCue Cue(int index, double time, int pad, CueKind kind, int variant, double perfect, double good)
        {
            return new BeatCue
            {
                Index = index, Time = time, Pad = pad, Kind = kind, Variant = variant,
                PerfectMs = perfect, GoodMs = good
            };
        }
    }
}

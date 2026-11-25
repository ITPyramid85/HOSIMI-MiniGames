using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Nails keep coming for two minutes, one strike each, all on stage 1. One miss bends that nail; the next still comes.
    // Two seated light the chair, three the desk, and five the house. The two minutes then end the run.
    public static class NailChart
    {
        public const double Bpm = 120, FirstCue = 2, RunSeconds = 120;
        public const int Pad = 6, NailCount = 5, Hits = 1, ChairAt = 2, DeskAt = 3, HouseAt = 5;
        public const int RecoverBeats = 4;
        public enum Piece { Chair, Desk, House }

        public static double BeatSeconds { get { return 60 / Bpm; } }
        public static int[] Pads(int stage) { return new[] { Pad }; }
        public static string PieceKey(Piece piece) { return piece == Piece.Chair ? "nails.chair" : piece == Piece.Desk ? "nails.desk" : "nails.house"; }

        public static List<BeatCue> Generate(int seed)
        {
            var cues = new List<BeatCue>();
            double beat = BeatSeconds;
            double time = FirstCue;
            double gap = 2 * beat;
            double last = RunSeconds - 2;
            for (int nail = 0; time <= last; nail++)
            {
                for (int h = 0; h < Hits; h++)
                {
                    Stage window = StageTable.Get(1);
                    cues.Add(new BeatCue { Index = cues.Count, Time = time, Pad = Pad, Variant = nail, PerfectMs = window.PerfectMs, GoodMs = window.GoodMs });
                    time += gap;
                }
                time += beat;
            }
            return cues;
        }

        // The next nail is the next variant, not a leftover hit of a nail that already bent.
        public static BeatCue NextNail(IList<BeatCue> cues, int variant)
        {
            if (cues == null) return null;
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].Variant == variant + 1) return cues[i];
            return null;
        }

        // The next nail waits a few beats. Starting it on the very next beat made the hammer strike before the player could.
        public static void PullForward(IList<BeatCue> cues, BeatCue missed, double beat)
        {
            if (cues == null || missed == null) return;
            BeatCue next = NextNail(cues, missed.Variant);
            if (next == null) return;
            double shift = next.Time - (missed.Time + RecoverBeats * beat);
            if (shift <= .02) return;
            double start = next.Time;
            for (int i = 0; i < cues.Count; i++)
            {
                BeatCue cue = cues[i];
                if (cue.Variant <= missed.Variant) continue;
                if (cue.Time + .000001 < start) continue;
                cue.Time -= shift;
                Stage window = StageTable.Get(1);
                cue.PerfectMs = window.PerfectMs;
                cue.GoodMs = window.GoodMs;
            }
        }
    }
}

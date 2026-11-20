using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Shared by every mini-game. A cue is "hit this pad on this beat"; the art decides how it looks.
    public enum CueKind { Normal, Bonus, Forbidden }
    public enum HitGrade { Perfect, Good, Miss }

    public enum CueOutcome
    {
        Hit,              // a Normal/Bonus cue struck in time (see Grade)
        Missed,           // a Normal/Bonus cue that passed unstruck
        ForbiddenHit,     // struck a cue that must not be hit
        ForbiddenAvoided, // a must-not-hit cue passed untouched
        EmptyHit          // struck a pad with no cue (only when every pad is judged)
    }

    [Serializable]
    public sealed class BeatCue
    {
        public int Index;
        public double Time;     // seconds on the music clock
        public int Pad;         // 0..7, pad number - 1
        public CueKind Kind;
        public int Variant;     // game grouping (a disguise, one nail). The judge itself ignores it.
        // This cue's windows; 0 uses the judge's defaults. Endless runs tighten them by stage.
        public double PerfectMs, GoodMs;
    }

    public sealed class CueResult
    {
        public BeatCue Cue;     // null for EmptyHit
        public int Pad;
        public CueOutcome Outcome;
        public HitGrade Grade;
        public double OffsetMs; // positive = late; NaN when there was no strike
    }

    // Beat-based judging: the music beat is the reference, not the moment a cue became visible.
    // Wrong pads are ignored unless judgeAllPads is set (whack-a-mole judges every pad).
    public sealed class BeatJudge
    {
        private readonly List<BeatCue> cues;
        private readonly bool[] resolved;
        private readonly double perfectSeconds, goodSeconds, offsetSeconds;
        private readonly bool judgeAllPads;
        private int firstOpen;
        public event Action<CueResult> Judged;

        public BeatJudge(IList<BeatCue> cues, double perfectMs, double goodMs, double offsetMs, bool judgeAllPads)
        {
            if (cues == null || !(perfectMs > 0) || !(goodMs >= perfectMs) || !GameConfig.IsFinite(offsetMs))
                throw new ArgumentException("Invalid judge setup.");
            this.cues = new List<BeatCue>(cues);
            this.cues.Sort((a, b) => a.Time.CompareTo(b.Time));
            for (int i = 0; i < this.cues.Count; i++)
                if (!GameConfig.IsFinite(this.cues[i].Time) || this.cues[i].Pad < 0 || this.cues[i].Pad >= BeatmapData.PadCount)
                    throw new ArgumentException("Invalid cue.");
            resolved = new bool[this.cues.Count];
            perfectSeconds = perfectMs / 1000;
            goodSeconds = goodMs / 1000;
            offsetSeconds = offsetMs / 1000;
            this.judgeAllPads = judgeAllPads;
        }

        public IReadOnlyList<BeatCue> Cues { get { return cues; } }
        public double GoodWindowSeconds { get { return goodSeconds; } }
        public bool IsResolved(BeatCue cue) { return resolved[cues.IndexOf(cue)]; }
        public bool AllResolved { get { return firstOpen >= cues.Count; } }

        // time is the raw music clock; the calibration offset is removed here, once.
        public void Press(int pad, double time)
        {
            if (pad < 0 || pad >= BeatmapData.PadCount || !GameConfig.IsFinite(time)) return;
            AdvanceTo(time);
            double t = time - offsetSeconds;
            int best = -1;
            double bestError = double.MaxValue;
            for (int i = firstOpen; i < cues.Count && cues[i].Time - Good(cues[i]) <= t; i++)
            {
                if (resolved[i] || cues[i].Pad != pad) continue;
                double error = Math.Abs(t - cues[i].Time);
                if (error <= Good(cues[i]) && error < bestError) { best = i; bestError = error; }
            }
            if (best < 0)
            {
                if (judgeAllPads) Emit(null, pad, CueOutcome.EmptyHit, HitGrade.Miss, double.NaN);
                return;
            }
            BeatCue cue = cues[best];
            double offsetMs = (t - cue.Time) * 1000;
            Resolve(best);
            if (cue.Kind == CueKind.Forbidden) Emit(cue, pad, CueOutcome.ForbiddenHit, HitGrade.Miss, offsetMs);
            else Emit(cue, pad, CueOutcome.Hit, bestError <= Perfect(cue) ? HitGrade.Perfect : HitGrade.Good, offsetMs);
        }

        // Settles every cue whose window has closed.
        public void AdvanceTo(double time)
        {
            double t = time - offsetSeconds;
            for (int i = firstOpen; i < cues.Count && cues[i].Time + Good(cues[i]) < t; i++)
            {
                if (resolved[i]) continue;
                Resolve(i);
                BeatCue cue = cues[i];
                if (cue.Kind == CueKind.Forbidden) Emit(cue, cue.Pad, CueOutcome.ForbiddenAvoided, HitGrade.Perfect, double.NaN);
                else Emit(cue, cue.Pad, CueOutcome.Missed, HitGrade.Miss, double.NaN);
            }
        }

        // Windows only narrow as a run goes on, so cue time plus window stays in order for the scans above.
        private double Good(BeatCue cue) { return cue.GoodMs > 0 ? cue.GoodMs / 1000 : goodSeconds; }
        private double Perfect(BeatCue cue) { return cue.PerfectMs > 0 ? cue.PerfectMs / 1000 : perfectSeconds; }

        // Drops the rest of a ruined nail without judging it. One miss bends the nail; the leftover hits are not further misses.
        public void Abandon(System.Predicate<BeatCue> match)
        {
            if (match == null) return;
            for (int i = 0; i < cues.Count; i++)
            {
                if (!resolved[i] && match(cues[i])) resolved[i] = true;
            }
            while (firstOpen < cues.Count && resolved[firstOpen]) firstOpen++;
        }

        private void Resolve(int index)
        {
            resolved[index] = true;
            while (firstOpen < cues.Count && resolved[firstOpen]) firstOpen++;
        }

        private void Emit(BeatCue cue, int pad, CueOutcome outcome, HitGrade grade, double offsetMs)
        {
            if (Judged != null) Judged(new CueResult { Cue = cue, Pad = pad, Outcome = outcome, Grade = grade, OffsetMs = offsetMs });
        }
    }
}

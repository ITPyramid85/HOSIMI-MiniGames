using System;

namespace RhythmGame
{
    // Pure gameplay input/scheduling layer. All times are audio-song seconds, never accumulated frame time.
    public sealed class RhythmInputJudge
    {
        private readonly PlaySession session;
        private readonly JudgementPoint[] points;
        private readonly bool[] resolved;
        private readonly double offsetSeconds;
        private readonly double earlyWindowSeconds;
        private readonly double lateWindowSeconds;
        private double currentTime = double.NegativeInfinity;
        public bool IsHeld { get; private set; }
        public event Action<HitResult> Judged;

        public RhythmInputJudge(PlaySession session, JudgementPoint[] points, RulesConfig rules, double calibrationMs)
        {
            if (session == null || points == null || points.Length != session.NoteCount)
                throw new ArgumentException("Session and beatmap must agree.");
            this.session = session;
            this.points = points;
            resolved = new bool[points.Length];
            offsetSeconds = calibrationMs / 1000;
            earlyWindowSeconds = (rules.GetJudgement(Judgement.Good).windowMs + RulesConfig.TimingToleranceMs) / 1000;
            lateWindowSeconds = (rules.LateGoodWindowMs + RulesConfig.TimingToleranceMs) / 1000;
        }

        public bool IsResolved(int index) { return resolved[index]; }

        public void AdvanceTo(double time)
        {
            if (!GameConfig.IsFinite(time) || time < currentTime - 0.0000001)
                throw new ArgumentOutOfRangeException("time", "Song time must be finite and monotonic.");
            currentTime = Math.Max(time, currentTime);
            if (session.IsFinished) return;
            foreach (JudgementPoint point in points)
            {
                if (resolved[point.Index]) continue;
                double target = point.Time + offsetSeconds;
                if (point.IsHoldTick)
                {
                    if (time < target) break;
                    Resolve(point.Index, IsHeld, offsetSeconds * 1000);
                }
                else
                {
                    if (time <= target + lateWindowSeconds) break;
                    Resolve(point.Index, false, 0);
                }
                if (session.IsFinished) break;
            }
        }

        public bool Press(double time)
        {
            AdvanceTo(time); // Resolve crossed hold ticks using the state BEFORE this input edge.
            IsHeld = true;
            return HitNearest(time, false);
        }

        public bool Swipe(double time)
        {
            AdvanceTo(time);
            return HitNearest(time, true);
        }

        public void Release(double time)
        {
            AdvanceTo(time);
            IsHeld = false;
        }

        private bool HitNearest(double time, bool swipe)
        {
            if (session.IsFinished) return false;
            JudgementPoint best = null;
            double bestDistance = double.MaxValue;
            foreach (JudgementPoint point in points)
            {
                if (resolved[point.Index] || point.IsHoldTick || (point.Kind == RhythmNoteKind.Swipe) != swipe) continue;
                double error = time - point.Time - offsetSeconds;
                double distance = Math.Abs(error);
                if (error >= -earlyWindowSeconds && error <= lateWindowSeconds && distance < bestDistance)
                { best = point; bestDistance = distance; }
            }
            if (best == null) return false; // Empty/early input is ignored, never consumed as a future note.
            Resolve(best.Index, true, (time - best.Time) * 1000);
            return true;
        }

        private void Resolve(int index, bool hit, double rawOffsetMs)
        {
            HitResult result = hit ? session.SubmitTiming(index, rawOffsetMs) : session.SubmitMiss(index);
            resolved[index] = true;
            if (Judged != null) Judged(result);
        }
    }
}

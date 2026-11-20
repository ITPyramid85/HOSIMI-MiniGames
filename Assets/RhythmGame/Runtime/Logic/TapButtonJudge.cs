using System;

namespace RhythmGame
{
    // Reuses the chart's compiled points as individual button taps.
    public sealed class TapButtonJudge
    {
        private readonly PlaySession session;
        private readonly JudgementPoint[] points;
        public const double PerfectReactionMs = 200;
        public const double GreatReactionMs = 280;
        public const double GoodReactionMs = 350;
        private readonly double calibrationSeconds;
        private double currentTime = double.NegativeInfinity;
        private double litAt = double.NaN;
        private int next;
        public bool IsHeld { get; private set; }
        public JudgementPoint NextPoint { get { return session.IsFinished || next >= points.Length ? null : points[next]; } }
        public double NextTime { get { return NextPoint == null ? double.PositiveInfinity : NextPoint.Time + calibrationSeconds; } }
        public event Action<HitResult> Judged;

        public TapButtonJudge(PlaySession session, JudgementPoint[] points, double calibrationMs)
        {
            if (session == null || points == null || points.Length != session.NoteCount)
                throw new ArgumentException("Session and beatmap must agree.");
            this.session = session;
            this.points = points;
            calibrationSeconds = calibrationMs / 1000;
        }

        private double Deadline { get { return (double.IsNaN(litAt) ? NextTime : litAt) + (GoodReactionMs + RulesConfig.TimingToleranceMs) / 1000; } }
        public bool IsLit(double time) { return NextPoint != null && !double.IsNaN(litAt) && time >= litAt && time <= Deadline; }

        // Called by the view on the frame it lights the button. Input alone cannot start a cue.
        public void Present(double time)
        {
            AdvanceTo(time);
            if (NextPoint != null && double.IsNaN(litAt) && time >= NextTime) litAt = time;
        }

        public void AdvanceTo(double time)
        {
            if (!GameConfig.IsFinite(time) || time < currentTime - .0000001)
                throw new ArgumentOutOfRangeException("time", "Song time must be finite and monotonic.");
            currentTime = Math.Max(currentTime, time);
            while (NextPoint != null && time > Deadline) Resolve(false, time);
        }

        public bool Press(double time)
        {
            AdvanceTo(time);
            if (IsHeld) return false;
            IsHeld = true;
            if (!IsLit(time)) return false;
            Resolve(true, time);
            return true;
        }

        public void Release(double time) { AdvanceTo(time); IsHeld = false; }

        private void Resolve(bool hit, double time)
        {
            JudgementPoint point = points[next++];
            double elapsedMs = (time - litAt) * 1000;
            Judgement rating = elapsedMs <= PerfectReactionMs + RulesConfig.TimingToleranceMs ? Judgement.Perfect :
                elapsedMs <= GreatReactionMs + RulesConfig.TimingToleranceMs ? Judgement.Great : Judgement.Good;
            litAt = double.NaN;
            HitResult result = hit ? session.Submit(point.Index, rating) : session.SubmitMiss(point.Index);
            if (Judged != null) Judged(result);
        }
    }
}

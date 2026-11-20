using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Every pad owns its cue, held state and visible-light reaction clock.
    public sealed class PadInputJudge
    {
        public const int MaxLitPads = 2;
        private readonly PlaySession session;
        private readonly Queue<JudgementPoint>[] queues = new Queue<JudgementPoint>[BeatmapData.PadCount];
        private readonly double[] litAt = new double[BeatmapData.PadCount];
        private readonly bool[] held = new bool[BeatmapData.PadCount];
        private double calibrationSeconds;

        // Called only while paused. Rebase song position by the returned delta as well,
        // so an already visible cue retains exactly the same reaction time remaining.
        public double ChangeCalibrationWhilePaused(double milliseconds)
        {
            if (!GameConfig.IsFinite(milliseconds)) throw new ArgumentOutOfRangeException("milliseconds");
            double delta = milliseconds / 1000 - calibrationSeconds;
            calibrationSeconds = milliseconds / 1000;
            currentTime += delta;
            for (int pad = 0; pad < litAt.Length; pad++)
                if (!double.IsNaN(litAt[pad])) litAt[pad] += delta;
            Array.Clear(held, 0, held.Length);
            return delta;
        }
        private double currentTime = double.NegativeInfinity;
        public event Action<HitResult> Judged;

        public PadInputJudge(PlaySession session, JudgementPoint[] points, double calibrationMs)
        {
            if (session == null || points == null || session.NoteCount != points.Length ||
                !GameConfig.IsFinite(calibrationMs)) throw new ArgumentException("Invalid session or beatmap.");
            this.session = session;
            calibrationSeconds = calibrationMs / 1000;
            for (int i = 0; i < queues.Length; i++)
            {
                queues[i] = new Queue<JudgementPoint>();
                litAt[i] = double.NaN;
            }
            foreach (JudgementPoint point in points)
            {
                CheckPad(point.Pad);
                queues[point.Pad].Enqueue(point);
            }
        }

        private static void CheckPad(int pad)
        {
            if (pad < 0 || pad >= BeatmapData.PadCount) throw new ArgumentOutOfRangeException("pad");
        }
        public JudgementPoint NextPoint(int pad)
        {
            CheckPad(pad);
            return session.IsFinished || queues[pad].Count == 0 ? null : queues[pad].Peek();
        }
        public double NextTime(int pad)
        {
            var point = NextPoint(pad);
            return point == null ? double.PositiveInfinity : point.Time + calibrationSeconds;
        }
        private double Deadline(int pad)
        {
            return (double.IsNaN(litAt[pad]) ? NextTime(pad) : litAt[pad]) +
                ((session.IsCasualMode && NextPoint(pad) != null ? CasualMode.GoodWindowMs(NextPoint(pad).Time) :
                    TapButtonJudge.GoodReactionMs) + RulesConfig.TimingToleranceMs) / 1000;
        }
        public bool IsLit(int pad, double time)
        {
            // Reconstructing the DSP anchor on resume can round a few ULPs below
            // the displayed frame's time. Use the same tolerance as AdvanceTo;
            // an unpresented (dark) cue still cannot be hit.
            return NextPoint(pad) != null && !double.IsNaN(litAt[pad]) && time >= litAt[pad] - .0000001 && time <= Deadline(pad);
        }
        public void Present(double time)
        {
            AdvanceTo(time);
            int litCount = 0;
            for (int pad = 0; pad < queues.Length; pad++)
                if (IsLit(pad, time)) litCount++;
            // A delayed frame can overlap adjacent waves. Keep at most two visible,
            // presenting the earliest remaining cue when a finger slot becomes free.
            while (litCount < (session.IsCasualMode ? 1 : MaxLitPads))
            {
                int next = -1;
                for (int pad = 0; pad < queues.Length; pad++)
                {
                    if (NextPoint(pad) == null || !double.IsNaN(litAt[pad]) || time < NextTime(pad)) continue;
                    if (next < 0 || NextPoint(pad).Index < NextPoint(next).Index) next = pad;
                }
                if (next < 0) break;
                litAt[next] = time;
                litCount++;
            }
        }
        public void AdvanceTo(double time)
        {
            if (!GameConfig.IsFinite(time) || time < currentTime - .0000001)
                throw new ArgumentOutOfRangeException("time", "Song time must be finite and monotonic.");
            currentTime = Math.Max(currentTime, time);
            // Resolve skipped cues by deadline, then stable chart index; stop immediately on death.
            while (!session.IsFinished)
            {
                int expired = -1;
                for (int pad = 0; pad < queues.Length; pad++)
                {
                    if (NextPoint(pad) == null || time <= Deadline(pad)) continue;
                    if (expired < 0 || Deadline(pad) < Deadline(expired) ||
                        (Deadline(pad) == Deadline(expired) && NextPoint(pad).Index < NextPoint(expired).Index))
                        expired = pad;
                }
                if (expired < 0) break;
                Resolve(expired, false, time);
            }
        }
        public bool Press(int pad, double time)
        {
            CheckPad(pad);
            AdvanceTo(time);
            if (held[pad]) return false;
            held[pad] = true;
            if (!IsLit(pad, time)) return false;
            Resolve(pad, true, time);
            return true;
        }
        public void Release(int pad, double time)
        {
            CheckPad(pad);
            AdvanceTo(time);
            held[pad] = false;
        }
        public void ReleaseAll(double time)
        {
            AdvanceTo(time);
            Array.Clear(held, 0, held.Length);
        }
        private void Resolve(int pad, bool hit, double time)
        {
            var point = queues[pad].Dequeue();
            double ms = (time - litAt[pad]) * 1000;
            Judgement rating = ms <= TapButtonJudge.PerfectReactionMs + RulesConfig.TimingToleranceMs ? Judgement.Perfect :
                ms <= TapButtonJudge.GreatReactionMs + RulesConfig.TimingToleranceMs ? Judgement.Great : Judgement.Good;
            if (session.IsCasualMode)
                rating = ms <= CasualMode.PerfectWindowMs(point.Time) + RulesConfig.TimingToleranceMs ? Judgement.Perfect : Judgement.Good;
            litAt[pad] = double.NaN;
            HitResult result = hit ? session.Submit(point.Index, rating) : session.SubmitMiss(point.Index);
            if (Judged != null) Judged(result);
        }
    }
}

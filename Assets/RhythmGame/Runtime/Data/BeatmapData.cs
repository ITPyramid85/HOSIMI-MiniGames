using System;
using System.Collections.Generic;

namespace RhythmGame
{
    public enum RhythmNoteKind { Tap = 0, Hold = 1, Swipe = 2 }

    [Serializable]
    public sealed class BeatmapNote
    {
        public RhythmNoteKind kind;
        public int pad; // 0..7; omitted in legacy single-button charts = pad 0.
        public double time;
        public double duration;
        public double tickInterval;
    }

    public sealed class JudgementPoint
    {
        public int Index { get; internal set; }
        public int NoteIndex { get; internal set; }
        public int Pad { get; internal set; }
        public double Time { get; internal set; }
        public bool IsHoldTick { get; internal set; }
        public RhythmNoteKind Kind { get; internal set; }
    }

    [Serializable]
    public sealed class BeatmapData
    {
        public const int PadCount = 8;
        public string chartId;
        public double bpm;
        public double duration;
        public double travelSeconds;
        public BeatmapNote[] notes;

        public JudgementPoint[] Compile(ChartConfig chart)
        {
            if (chart == null || chart.id != chartId || notes == null || notes.Length == 0 ||
                !GameConfig.IsFinite(bpm) || bpm <= 0 || !GameConfig.IsFinite(duration) || duration <= 0 ||
                !GameConfig.IsFinite(travelSeconds) || travelSeconds < 0.5 || travelSeconds > 10)
                throw new InvalidOperationException("Invalid beatmap header.");
            var points = new List<JudgementPoint>();
            var previousEnd = new double[PadCount];
            for (int pad = 0; pad < PadCount; pad++) previousEnd[pad] = -1;
            double previousTime = -1;
            for (int i = 0; i < notes.Length; i++)
            {
                BeatmapNote note = notes[i];
                if (note == null || !Enum.IsDefined(typeof(RhythmNoteKind), note.kind) ||
                    note.pad < 0 || note.pad >= PadCount ||
                    !GameConfig.IsFinite(note.time) || note.time < 0 || note.time < previousTime ||
                    note.time - previousEnd[note.pad] < 0.25 ||
                    !GameConfig.IsFinite(note.duration) || !GameConfig.IsFinite(note.tickInterval))
                    throw new InvalidOperationException("Invalid or overlapping note: " + i);
                int ticks = 0;
                if (note.kind == RhythmNoteKind.Hold)
                {
                    if (note.duration <= 0 || note.tickInterval < 0.1 || note.duration > duration)
                        throw new InvalidOperationException("Invalid hold: " + i);
                    double count = note.duration / note.tickInterval;
                    if (count < 1 || count > 10000 || Math.Abs(count - Math.Round(count)) > 0.000001)
                        throw new InvalidOperationException("Hold duration must be a multiple of its tick interval.");
                    ticks = (int)Math.Round(count);
                }
                else if (note.duration != 0 || note.tickInterval != 0)
                    throw new InvalidOperationException("Only holds have duration/ticks.");
                previousTime = note.time;
                previousEnd[note.pad] = note.time + note.duration;
                if (previousEnd[note.pad] + 0.5 > duration) throw new InvalidOperationException("Audio needs a tail after the last note.");
                for (int tick = 0; tick <= ticks; tick++)
                {
                    points.Add(new JudgementPoint { NoteIndex = i, Kind = note.kind, Pad = note.pad,
                        Time = note.time + tick * note.tickInterval, IsHoldTick = tick > 0 });
                }
            }
            if (points.Count != chart.noteCount) throw new InvalidOperationException("Beatmap judgement count differs from chart config.");
            // Stable order even when other pads have notes during a legacy hold.
            points.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.NoteIndex.CompareTo(b.NoteIndex));
            for (int i = 0; i < points.Count; i++) points[i].Index = i;
            return points.ToArray();
        }
    }
}

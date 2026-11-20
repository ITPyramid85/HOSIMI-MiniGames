using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Tap-along latency test: beats are heard at firstBeat + k * interval on the audio clock.
    // Each tap is matched to the beat it most plausibly follows; the median offset is the
    // total output + touch delay the player perceives on the current audio route.
    public sealed class LatencyCalibration
    {
        public const double DefaultIntervalSeconds = .8;   // 75 BPM: slow enough to tell 300+ ms of lag apart.
        public const int DefaultWarmupBeats = 2;
        public const int DefaultTargetTaps = 10;
        public const int MinimumTaps = 6;
        // Window relative to a beat: a tap up to 200 ms early or 600 ms late belongs to it.
        public const double EarlyWindowMs = 200;
        public const double UnstableSpreadMs = 45;

        private readonly double firstBeat, interval;
        private readonly int warmupBeats, targetTaps;
        private readonly List<double> offsets = new List<double>();
        private readonly HashSet<long> tappedBeats = new HashSet<long>();

        public LatencyCalibration(double firstBeatTime, double intervalSeconds = DefaultIntervalSeconds,
            int warmupBeats = DefaultWarmupBeats, int targetTaps = DefaultTargetTaps)
        {
            if (!GameConfig.IsFinite(firstBeatTime) || !GameConfig.IsFinite(intervalSeconds) || intervalSeconds <= EarlyWindowMs / 1000 * 2 ||
                warmupBeats < 0 || targetTaps < MinimumTaps) throw new ArgumentException("Invalid calibration timing.");
            firstBeat = firstBeatTime; interval = intervalSeconds;
            this.warmupBeats = warmupBeats; this.targetTaps = targetTaps;
        }

        public double FirstBeatTime { get { return firstBeat; } }
        public double IntervalSeconds { get { return interval; } }
        public int TargetTaps { get { return targetTaps; } }
        public IReadOnlyList<double> OffsetsMs { get { return offsets; } }
        public int TapCount { get { return offsets.Count; } }
        public bool IsComplete { get { return offsets.Count >= targetTaps; } }
        public bool HasResult { get { return offsets.Count >= MinimumTaps; } }
        public bool IsWarmingUp(double time) { return BeatIndex(time) < warmupBeats; }
        public long BeatIndex(double time) { return (long)Math.Floor((time - firstBeat) / interval); }

        // Seconds since the most recent beat, or a negative value before the first beat.
        public double SinceBeat(double time)
        {
            if (time < firstBeat) return time - firstBeat;
            return time - (firstBeat + BeatIndex(time) * interval);
        }

        // Returns the accepted offset in ms, or null for warm-up, duplicate or stray taps.
        public double? RecordTap(double time)
        {
            if (IsComplete || !GameConfig.IsFinite(time)) return null;
            long beat = (long)Math.Floor((time - firstBeat + EarlyWindowMs / 1000) / interval);
            if (beat < warmupBeats || !tappedBeats.Add(beat)) return null;
            double offset = (time - (firstBeat + beat * interval)) * 1000;
            offsets.Add(offset);
            return offset;
        }

        public double MedianMs
        {
            get
            {
                if (!HasResult) throw new InvalidOperationException("Not enough taps.");
                return Median(offsets);
            }
        }

        // Median absolute deviation: how consistently the player tapped, in ms.
        public double SpreadMs
        {
            get
            {
                double median = MedianMs;
                var deviations = new List<double>(offsets.Count);
                foreach (double offset in offsets) deviations.Add(Math.Abs(offset - median));
                return Median(deviations);
            }
        }

        public bool IsUnstable { get { return SpreadMs > UnstableSpreadMs; } }

        // The game applies inputOffset + bluetoothDelay on Bluetooth, so only the remainder is stored.
        public static double SuggestBluetoothDelayMs(double measuredMs, double inputOffsetMs)
        {
            double value = Math.Round((measuredMs - inputOffsetMs) / 10, MidpointRounding.AwayFromZero) * 10;
            return Math.Max(0, Math.Min(PlayerSettings.MaxBluetoothDelayMs, value));
        }

        private static double Median(List<double> values)
        {
            var sorted = new List<double>(values);
            sorted.Sort();
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }
}

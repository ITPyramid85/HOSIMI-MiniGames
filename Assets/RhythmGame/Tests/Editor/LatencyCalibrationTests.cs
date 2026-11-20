using System;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class LatencyCalibrationTests
    {
        [TestCase(0)] [TestCase(180)] [TestCase(-120)] [TestCase(450)]
        public void ConsistentTapsMeasureTheirOffset(double lagMs)
        {
            var test = new LatencyCalibration(10);
            for (int beat = 2; beat < 12; beat++) Assert.IsNotNull(test.RecordTap(10 + beat * test.IntervalSeconds + lagMs / 1000));
            Assert.IsTrue(test.IsComplete);
            Assert.AreEqual(lagMs, test.MedianMs, .001);
            Assert.AreEqual(0, test.SpreadMs, .001);
            Assert.IsFalse(test.IsUnstable);
        }

        [Test]
        public void WarmupDuplicateAndPostCompletionTapsAreIgnored()
        {
            var test = new LatencyCalibration(0);
            Assert.IsTrue(test.IsWarmingUp(.1));
            Assert.IsNull(test.RecordTap(.2), "First beat is warm-up.");
            Assert.IsNull(test.RecordTap(.8 + .2), "Second beat is warm-up.");
            Assert.IsNotNull(test.RecordTap(1.6 + .2));
            Assert.IsNull(test.RecordTap(1.6 + .25), "Second tap on the same beat.");
            for (int beat = 3; beat < 12; beat++) test.RecordTap(beat * .8 + .2);
            Assert.AreEqual(10, test.TapCount);
            Assert.IsNull(test.RecordTap(12 * .8 + .2));
            Assert.AreEqual(10, test.TapCount);
        }

        [Test]
        public void MedianRejectsOneStrayTapAndSpreadFlagsSloppyTapping()
        {
            var steady = new LatencyCalibration(0);
            double[] lag = { 200, 205, 195, 200, 580, 200, 210, 190 };
            for (int i = 0; i < lag.Length; i++) steady.RecordTap((i + 2) * .8 + lag[i] / 1000);
            Assert.IsTrue(steady.HasResult); Assert.IsFalse(steady.IsComplete);
            Assert.AreEqual(200, steady.MedianMs, .001);
            Assert.IsFalse(steady.IsUnstable);

            var sloppy = new LatencyCalibration(0);
            double[] wild = { 50, 300, 120, 400, -100, 250 };
            for (int i = 0; i < wild.Length; i++) sloppy.RecordTap((i + 2) * .8 + wild[i] / 1000);
            Assert.IsTrue(sloppy.IsUnstable);
        }

        [Test]
        public void TooFewTapsHaveNoResult()
        {
            var test = new LatencyCalibration(0);
            for (int beat = 2; beat < 7; beat++) test.RecordTap(beat * .8);
            Assert.IsFalse(test.HasResult);
            Assert.Throws<InvalidOperationException>(() => { double unused = test.MedianMs; });
        }

        [TestCase(200, -25, 230)] [TestCase(175, -25, 200)] [TestCase(30, 50, 0)] [TestCase(5000, 0, 1000)] [TestCase(184, 0, 180)]
        public void SuggestedDelayRemovesGeneralOffsetAndStaysInSavedRange(double measured, double inputOffset, double expected)
        {
            double value = LatencyCalibration.SuggestBluetoothDelayMs(measured, inputOffset);
            Assert.AreEqual(expected, value);
            Assert.DoesNotThrow(() => PlayerSettings.ValidateBluetoothDelay(value));
        }
    }
}

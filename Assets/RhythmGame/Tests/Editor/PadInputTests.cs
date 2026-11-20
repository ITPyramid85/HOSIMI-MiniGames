using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class PadInputTests
    {
        private GameConfig config;
        private BeatmapData map;
        private JudgementPoint[] points;
        private ProgressionService service;
        private PlaySession session;
        private PadInputJudge judge;

        [SetUp]
        public void SetUp()
        {
            config = RhythmGameData.LoadDefaultConfig();
            var chart = config.Chart("training_01_pads");
            map = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text);
            points = map.Compile(chart);
            service = new ProgressionService(config, PlayerData.Create(config));
            session = service.BeginPlay(chart.id);
            judge = new PadInputJudge(session, points, 0);
        }

        private void HitBefore(double time)
        {
            foreach (var group in points.Where(p => p.Time < time).GroupBy(p => p.Time))
            {
                judge.Present(group.Key);
                foreach (var point in group) { judge.Press(point.Pad, group.Key); judge.Release(point.Pad, group.Key); }
            }
        }

        [Test]
        public void DemoUsesAllEightPadsWithAtMostTwoLightsAndNoRapidRepeats()
        {
            Assert.AreEqual(30, points.Length);
            Assert.AreEqual(8, points.Select(p => p.Pad).Distinct().Count());
            Assert.IsTrue(points.GroupBy(p => p.Time).Any(g => g.Count() == 2));
            Assert.IsTrue(points.GroupBy(p => p.Time).All(g => g.Count() <= PadInputJudge.MaxLitPads));
            // Include the complete reaction windows, not just identical timestamps.
            foreach (var point in points)
                Assert.LessOrEqual(points.Where(p => p.Time <= point.Time &&
                    p.Time + TapButtonJudge.GoodReactionMs / 1000 >= point.Time).Select(p => p.Pad).Distinct().Count(), 2);
            foreach (var pad in points.GroupBy(p => p.Pad))
            {
                double previous = -1;
                foreach (var point in pad)
                {
                    Assert.GreaterOrEqual(point.Time - previous, 1);
                    Assert.IsFalse(point.IsHoldTick);
                    previous = point.Time;
                }
            }
        }

        [Test]
        public void OverlappingWavesNeverDisplayMoreThanTwoPadsAndWaitingCueCanStillBeHit()
        {
            map.notes[1].time = 2;
            map.notes[2].time = 2.25;
            points = map.Compile(config.Chart(map.chartId));
            judge = new PadInputJudge(session, points, 0);
            judge.Present(2);
            judge.Present(2.25);
            Assert.IsTrue(judge.IsLit(0, 2.25));
            Assert.IsTrue(judge.IsLit(6, 2.25));
            Assert.IsFalse(judge.IsLit(5, 2.25));
            judge.Press(0, 2.26);
            judge.Present(2.26);
            Assert.IsTrue(judge.IsLit(5, 2.3));
            Assert.IsTrue(judge.IsLit(6, 2.3));
            Assert.IsFalse(judge.IsLit(0, 2.3));
            Assert.IsTrue(judge.Press(5, 2.3));
            Assert.AreEqual(2, session.Combo);
        }

        [Test]
        public void ChordCanBeHitInReverseOrderWithoutReleasingFirstFinger()
        {
            HitBefore(4);
            judge.Present(4);
            Assert.IsTrue(judge.IsLit(5, 4));
            Assert.IsTrue(judge.IsLit(7, 4));
            Assert.IsTrue(judge.Press(7, 4.1));
            Assert.IsFalse(judge.Press(7, 4.1));
            Assert.IsTrue(judge.IsLit(5, 4.1));
            Assert.IsTrue(judge.Press(5, 4.1));
            Assert.AreEqual(4, session.Combo);
            Assert.AreEqual(100, session.Health);
        }

        [Test]
        public void OneMissInChordDoesNotCancelTheOtherPadOrCountTwice()
        {
            HitBefore(4);
            judge.Present(4);
            judge.Press(7, 4.1);
            judge.AdvanceTo(4.351);
            Assert.AreEqual(4, session.JudgedCount);
            Assert.AreEqual(90, session.Health);
            judge.AdvanceTo(4.9);
            Assert.AreEqual(90, session.Health);
        }

        [Test]
        public void DarkPadAndEarlyInputCannotConsumeOrAutomaticallyHitAnotherPad()
        {
            Assert.IsFalse(judge.Press(0, 1.9));
            judge.Present(2);
            Assert.IsFalse(judge.Press(0, 2.1));
            Assert.IsFalse(judge.Press(7, 2.1));
            Assert.AreEqual(0, session.JudgedCount);
            judge.Release(0, 2.1);
            Assert.IsTrue(judge.Press(0, 2.1));
        }

        [TestCase(0, Judgement.Perfect)]
        [TestCase(200, Judgement.Perfect)]
        [TestCase(200.001, Judgement.Great)]
        [TestCase(280, Judgement.Great)]
        [TestCase(280.001, Judgement.Good)]
        [TestCase(350, Judgement.Good)]
        public void IndependentReactionWindowsStartAtDisplayedFrame(double ms, Judgement expected)
        {
            judge = new PadInputJudge(session, points, 125);
            judge.Present(2.141);
            HitResult hit = null;
            judge.Judged += result => hit = result;
            Assert.IsTrue(judge.Press(0, 2.141 + ms / 1000));
            Assert.AreEqual(expected, hit.Judgement);
        }

        [Test]
        public void AllPadsSettleThroughExistingScoringAndSaveValidation()
        {
            HitBefore(43);
            var result = service.Complete(session);
            Assert.AreEqual(30, result.Perfect);
            Assert.AreEqual(30, result.MaxCombo);
            Assert.IsTrue(result.AllPerfect);
            JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(service.Player)).Validate(config);
        }

        [Test]
        public void MissesStopAtDeathAndUnshownExpiredLightsAreNeverPresented()
        {
            judge.Present(42);
            Assert.AreEqual(10, session.JudgedCount);
            Assert.AreEqual(0, session.Health);
            for (int pad = 0; pad < 8; pad++) Assert.IsFalse(judge.IsLit(pad, 42));
        }

        [Test]
        public void PausingReleasesAllPadsWithoutRestartingTheirVisibleClock()
        {
            HitBefore(4);
            judge.Present(4);
            judge.Press(7, 4.1);
            judge.ReleaseAll(4.15);
            judge.Present(4.15);
            HitResult hit = null;
            judge.Judged += result => hit = result;
            Assert.IsTrue(judge.Press(5, 4.3));
            Assert.AreEqual(Judgement.Good, hit.Judgement);
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void InvalidPadIsRejected(int pad)
        {
            map.notes[0].pad = pad;
            Assert.Throws<InvalidOperationException>(() => map.Compile(config.Chart(map.chartId)));
        }

        [Test]
        public void SamePadCollisionIsRejectedButSimultaneousDifferentPadsAreValid()
        {
            Assert.DoesNotThrow(() => map.Compile(config.Chart(map.chartId)));
            map.notes[3].pad = map.notes[2].pad;
            Assert.Throws<InvalidOperationException>(() => map.Compile(config.Chart(map.chartId)));
        }
    }
}

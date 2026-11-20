using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class TapButtonTests
    {
        private GameConfig config;
        private JudgementPoint[] points;
        private ProgressionService service;
        private PlaySession session;
        private TapButtonJudge judge;

        [SetUp]
        public void SetUp()
        {
            config = RhythmGameData.LoadDefaultConfig();
            var map = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>("RhythmGame/Charts/training_01").text);
            points = map.Compile(config.Chart("training_01"));
            service = new ProgressionService(config, PlayerData.Create(config));
            session = service.BeginPlay("training_01");
            judge = new TapButtonJudge(session, points, 0);
        }

        [Test]
        public void AllExistingPointsBecomeSeparateTapsWithSameScore()
        {
            foreach (var point in points)
            {
                judge.Present(point.Time);
                Assert.IsTrue(judge.IsLit(point.Time));
                Assert.AreEqual(point.Index, judge.NextPoint.Index);
                Assert.IsTrue(judge.Press(point.Time));
                Assert.IsFalse(judge.IsLit(point.Time));
                Assert.IsFalse(judge.Press(point.Time));
                judge.Release(point.Time);
            }
            var result = service.Complete(session);
            Assert.AreEqual(46, result.Perfect);
            Assert.AreEqual(46, result.MaxCombo);
            Assert.AreEqual(100, result.RemainingHealth);
            Assert.IsTrue(result.AllPerfect);
        }

        [Test]
        public void HoldingAcrossOldHoldTicksDoesNotScoreAndRequiresRelease()
        {
            foreach (var point in points)
            {
                if (point.Time >= 9) break;
                judge.Present(point.Time); judge.Press(point.Time); judge.Release(point.Time);
            }
            judge.Present(9);
            judge.Press(9);
            int before = session.JudgedCount;
            judge.Present(9.5);
            Assert.AreEqual(before, session.JudgedCount);
            Assert.IsTrue(judge.IsLit(9.5));
            Assert.IsFalse(judge.Press(9.5));
            judge.AdvanceTo(9.851);
            Assert.AreEqual(before + 1, session.JudgedCount);
            Assert.AreEqual(90, session.Health);
            Assert.AreEqual(0, session.Combo);
            judge.Release(9.9);
            judge.Present(10);
            Assert.IsTrue(judge.Press(10));
            Assert.AreEqual(1, session.Combo);
        }

        [TestCase(0, Judgement.Perfect)]
        [TestCase(150, Judgement.Perfect)]
        [TestCase(200, Judgement.Perfect)]
        [TestCase(200.001, Judgement.Great)]
        [TestCase(280, Judgement.Great)]
        [TestCase(280.001, Judgement.Good)]
        [TestCase(350, Judgement.Good)]
        public void ReactionWindowsStartOnDisplayedFrameWithoutDoubleApplyingCalibration(double reactionMs, Judgement expected)
        {
            service.Abort(session);
            service.SetInputOffset(125);
            session = service.BeginPlay("training_01");
            judge = new TapButtonJudge(session, points, 125);
            // The first rendered frame is 16 ms after the scheduled cue.
            const double shown = 2.141;
            Assert.AreEqual(2.125, judge.NextTime);
            judge.Present(shown);
            HitResult hit = null;
            judge.Judged += value => hit = value;
            Assert.IsTrue(judge.Press(shown + reactionMs / 1000));
            Assert.AreEqual(expected, hit.Judgement);
        }

        [TestCase(1.98)]
        [TestCase(2.0)]
        public void ClickingBeforeDisplayedLightNeverConsumesThePoint(double time)
        {
            // Includes the old pre-beat Perfect and a press before rendering on the beat itself.
            Assert.IsFalse(judge.IsLit(time));
            Assert.IsFalse(judge.Press(time));
            Assert.AreEqual(0, session.JudgedCount);
            judge.Release(time);
            judge.Present(2.016);
            Assert.IsTrue(judge.Press(2.166));
            Assert.AreEqual(1, session.Combo);
        }

        [Test]
        public void DisplayedCueExpiresAfter350MillisecondsAndCannotHitDarkNextCue()
        {
            judge.Present(2.016);
            HitResult hit = null;
            judge.Judged += value => hit = value;
            Assert.IsFalse(judge.Press(2.366001));
            Assert.AreEqual(Judgement.Miss, hit.Judgement);
            Assert.AreEqual(90, session.Health);
            Assert.IsFalse(judge.IsLit(2.366001));
            Assert.AreEqual(1, session.JudgedCount);
        }

        [Test]
        public void EmptyPressCannotBeHeldIntoNextLight()
        {
            Assert.IsFalse(judge.Press(1));
            judge.Present(2);
            Assert.IsTrue(judge.IsLit(2));
            Assert.IsFalse(judge.Press(2));
            Assert.AreEqual(0, session.JudgedCount);
            judge.Release(2);
            Assert.IsTrue(judge.Press(2));
        }

        [Test]
        public void SingleTapChartHasNoBurstsAndPreservesLegacyRecords()
        {
            foreach (var point in points)
            {
                judge.Present(point.Time);
                judge.Press(point.Time); judge.Release(point.Time);
            }
            service.Complete(session);
            ChartRecord legacy = service.Player.FindRecord("training_01");
            long legacyScore = legacy.bestScore;

            var chart = config.Chart("training_01_single");
            var map = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text);
            var singles = map.Compile(chart);
            Assert.AreEqual(30, singles.Length);
            for (int i = 0; i < singles.Length; i++)
            {
                Assert.IsFalse(singles[i].IsHoldTick);
                Assert.AreEqual(RhythmNoteKind.Tap, singles[i].Kind);
                if (i > 0) Assert.GreaterOrEqual(singles[i].Time - singles[i - 1].Time, 1);
            }
            foreach (double removedTime in new[] { 9.5, 10, 10.5, 11, 16.5, 17, 17.5, 18,
                23.5, 24, 24.5, 25, 29.5, 30, 30.5, 31 })
                Assert.IsFalse(System.Array.Exists(singles, p => p.Time == removedTime));

            session = service.BeginPlay(chart.id);
            judge = new TapButtonJudge(session, singles, 0);
            foreach (var point in singles)
            {
                judge.Present(point.Time);
                Assert.IsTrue(judge.Press(point.Time + .15));
                judge.Release(point.Time + .15);
            }
            var result = service.Complete(session);
            Assert.AreEqual(30, result.Perfect);
            Assert.IsTrue(result.AllPerfect);
            Assert.AreEqual(46, legacy.maxCombo);
            Assert.AreEqual(legacyScore, legacy.bestScore);
            JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(service.Player)).Validate(config);
        }

        [Test]
        public void NoInputFailsAfterTenMissesIncludingFormerHoldTicks()
        {
            judge.AdvanceTo(42);
            Assert.AreEqual(10, session.JudgedCount);
            Assert.IsTrue(session.HasFailed);
            Assert.IsFalse(judge.IsLit(42));
            Assert.IsFalse(judge.Press(42));
            Assert.AreEqual(0, service.Complete(session).TotalCoins);
        }
    }
}

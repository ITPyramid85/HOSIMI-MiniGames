using System;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class GameplayTests
    {
        private GameConfig config;
        private BeatmapData map;
        private JudgementPoint[] points;
        private ProgressionService service;
        private PlaySession session;
        private RhythmInputJudge judge;

        [SetUp]
        public void SetUp()
        {
            config = RhythmGameData.LoadDefaultConfig();
            map = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>("RhythmGame/Charts/training_01").text);
            points = map.Compile(config.Chart("training_01"));
            service = new ProgressionService(config, PlayerData.Create(config));
            session = service.BeginPlay("training_01");
            judge = new RhythmInputJudge(session, points, config.rules, 0);
        }

        private void PlayThrough(double end)
        {
            foreach (BeatmapNote note in map.notes)
            {
                if (note.time > end) break;
                if (note.kind == RhythmNoteKind.Swipe) judge.Swipe(note.time); else judge.Press(note.time);
                judge.Release(note.time + note.duration + .01);
            }
        }

        [Test]
        public void CompleteMixedChartScoresEveryUnitExactlyOnce()
        {
            Assert.AreEqual(46, points.Length);
            PlayThrough(42);
            PlayResult result = service.Complete(session);
            Assert.AreEqual(46, result.Perfect);
            Assert.AreEqual(46, result.MaxCombo);
            Assert.AreEqual(100, result.RemainingHealth);
            Assert.IsTrue(result.AllPerfect);
            Assert.Greater(result.TotalCoins, 0);
            Assert.Throws<InvalidOperationException>(() => service.Complete(session));
        }

        [Test]
        public void TapCannotClearSwipeAndSwipeCannotClearTap()
        {
            Assert.IsFalse(judge.Swipe(2));
            Assert.IsTrue(judge.Press(2));
            Assert.IsFalse(judge.Press(2));
            judge.Press(3); judge.Press(4); judge.Press(5);
            Assert.IsFalse(judge.Swipe(6));
            Assert.IsTrue(judge.Press(6));
            Assert.IsFalse(judge.Press(7));
            Assert.IsTrue(judge.Swipe(7));
            Assert.IsFalse(judge.Swipe(7));
            Assert.AreEqual(6, session.JudgedCount);
        }

        [Test]
        public void EarlyTapDoesNotConsumeFutureNote()
        {
            Assert.IsFalse(judge.Press(1)); judge.Release(1.1);
            Assert.AreEqual(0, session.JudgedCount);
            Assert.IsTrue(judge.Press(2)); Assert.AreEqual(1, session.Combo);
        }

        [TestCase(-200, Judgement.Good)]
        [TestCase(-80, Judgement.Great)]
        [TestCase(-40, Judgement.Perfect)]
        [TestCase(40, Judgement.Perfect)]
        [TestCase(80, Judgement.Great)]
        [TestCase(150, Judgement.Good)]
        [TestCase(200, Judgement.Good)]
        [TestCase(350, Judgement.Good)]
        public void EarlyAndLateTimingEdgesAreInclusive(double ms, Judgement expected)
        {
            HitResult hit = null;
            judge.Judged += value => hit = value;
            Assert.IsTrue(judge.Press(2 + ms / 1000));
            Assert.AreEqual(expected, hit.Judgement);
            Assert.AreEqual(100, session.Health);
        }

        [TestCase(RhythmNoteKind.Tap)]
        [TestCase(RhythmNoteKind.Hold)]
        [TestCase(RhythmNoteKind.Swipe)]
        public void AllNoteHeadsCanBeHitBelowLineUntilLateWindowEnds(RhythmNoteKind kind)
        {
            BeatmapNote note = Array.Find(map.notes, n => n.kind == kind);
            foreach (BeatmapNote previous in map.notes)
            {
                if (previous == note) break;
                if (previous.kind == RhythmNoteKind.Swipe) judge.Swipe(previous.time); else judge.Press(previous.time);
                judge.Release(previous.time + previous.duration + .01);
            }
            int index = Array.FindIndex(points, p => p.Time == note.time);
            judge.AdvanceTo(note.time + .15);
            Assert.IsFalse(judge.IsResolved(index));
            HitResult hit = null;
            judge.Judged += value => hit = value;
            double lateTime = note.time + config.rules.LateGoodWindowMs / 1000;
            bool accepted = kind == RhythmNoteKind.Swipe ? judge.Swipe(lateTime) : judge.Press(lateTime);
            Assert.IsTrue(accepted);
            Assert.AreEqual(index, hit.NoteIndex);
            Assert.AreEqual(Judgement.Good, hit.Judgement);
            if (kind == RhythmNoteKind.Hold)
            {
                judge.AdvanceTo(note.time + note.tickInterval);
                Assert.AreEqual(Judgement.Perfect, hit.Judgement);
                Assert.AreEqual(1, session.Combo);
            }
        }

        [TestCase(-150)]
        [TestCase(0)]
        [TestCase(150)]
        public void MissOccursOnlyAfterCalibratedLateWindowAndCannotBeScoredTwice(double offset)
        {
            service.Abort(session); service.SetInputOffset(offset);
            session = service.BeginPlay("training_01");
            judge = new RhythmInputJudge(session, points, config.rules, offset);
            double deadline = 2 + offset / 1000 + config.rules.LateGoodWindowMs / 1000;
            judge.AdvanceTo(deadline);
            Assert.AreEqual(0, session.JudgedCount);
            judge.AdvanceTo(deadline + .001);
            Assert.AreEqual(1, session.JudgedCount);
            Assert.AreEqual(90, session.Health);
            Assert.IsFalse(judge.Press(deadline + .002));
            Assert.AreEqual(1, session.JudgedCount);
        }

        [TestCase(RhythmNoteKind.Tap)]
        [TestCase(RhythmNoteKind.Hold)]
        [TestCase(RhythmNoteKind.Swipe)]
        public void WholeNoteCanStayBelowLineFor140MsAndStillBeHit(RhythmNoteKind kind)
        {
            BeatmapNote note = Array.Find(map.notes, n => n.kind == kind);
            int noteIndex = Array.IndexOf(map.notes, note);
            int first = Array.FindIndex(points, p => p.NoteIndex == noteIndex);
            int last = Array.FindLastIndex(points, p => p.NoteIndex == noteIndex);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RhythmGame/Prefabs/RhythmNote.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                RhythmNoteView view = instance.GetComponent<RhythmNoteView>();
                view.Initialize(note, first, last);
                judge.AdvanceTo(note.time + .2);
                view.Render(note.time + .2, map.travelSeconds, judge);
                Assert.IsTrue(instance.transform.Find("Head").gameObject.activeSelf);
                var corners = new Vector3[4];
                ((RectTransform)instance.transform.Find("Head")).GetWorldCorners(corners);
                Assert.Less(corners[1].y, -173, "The TOP edge must be below the line, not just the center.");
                judge.AdvanceTo(note.time + .34);
                view.Render(note.time + .34, map.travelSeconds, judge);
                Assert.IsTrue(instance.transform.Find("Head").gameObject.activeSelf);
                Assert.IsFalse(judge.IsResolved(first));
                HitResult hit = null;
                judge.Judged += value => hit = value;
                if (kind == RhythmNoteKind.Swipe) Assert.IsTrue(judge.Swipe(note.time + .34));
                else Assert.IsTrue(judge.Press(note.time + .34));
                Assert.AreEqual(Judgement.Good, hit.Judgement);
                Assert.AreEqual(first, hit.NoteIndex);
                view.Render(note.time + .34, map.travelSeconds, judge);
                if (kind == RhythmNoteKind.Hold)
                    Assert.AreEqual(-173, ((RectTransform)instance.transform).anchoredPosition.y);
                else Assert.IsFalse(instance.transform.Find("Head").gameObject.activeSelf);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void LateExtensionDoesNotMakeEarlyInputMoreLenient()
        {
            Assert.IsFalse(judge.Press(1.7));
            Assert.AreEqual(0, session.JudgedCount);
            judge.Release(1.71);
            Assert.IsTrue(judge.Press(2.3));
            Assert.AreEqual(1, session.JudgedCount);
            Assert.AreEqual(40, session.Score);
            Assert.AreEqual(0, session.Combo);
        }

        [Test]
        public void MissingLateWindowKeepsLegacyTimingAndInvalidValuesAreRejected()
        {
            config.rules.lateGoodWindowMs = 0;
            config.Validate();
            Assert.AreEqual(200, config.rules.LateGoodWindowMs);
            Assert.AreEqual(Judgement.Miss, config.rules.Judge(201, 0));
            foreach (double invalid in new[] { -1, 100, double.NaN, double.PositiveInfinity })
            {
                config.rules.lateGoodWindowMs = invalid;
                Assert.Throws<InvalidOperationException>(() => config.Validate());
            }
        }

        [Test]
        public void ReleasedHoldMissesCheckpointBreaksComboAndCanBeRegrabbed()
        {
            PlayThrough(8);
            judge.Press(9); judge.Release(9.6); // Head and first tick earned.
            Assert.AreEqual(9, session.Combo);
            judge.AdvanceTo(10);
            Assert.AreEqual(90, session.Health); Assert.AreEqual(0, session.Combo);
            judge.Press(10.1); judge.AdvanceTo(11);
            Assert.AreEqual(2, session.Combo); Assert.AreEqual(12, session.JudgedCount);
        }

        [Test]
        public void PressAfterFrameHitchDoesNotRetroactivelyHoldPastTicks()
        {
            PlayThrough(8); judge.Press(9); judge.Release(9.1);
            judge.Press(10.6); // Three checkpoints passed with no finger down.
            Assert.AreEqual(70, session.Health); Assert.AreEqual(0, session.Combo);
            judge.AdvanceTo(11); Assert.AreEqual(1, session.Combo);
        }

        [Test]
        public void NoInputStopsAtTenMissesAndAwardsNoCoins()
        {
            judge.AdvanceTo(42);
            Assert.IsTrue(session.HasFailed); Assert.AreEqual(10, session.JudgedCount);
            PlayResult result = service.Complete(session);
            Assert.AreEqual(10, result.Miss); Assert.AreEqual(36, result.UnplayedNotes);
            Assert.AreEqual(0, result.TotalCoins);
            Assert.IsFalse(judge.Press(43));
        }

        [TestCase(150)]
        [TestCase(-150)]
        public void CalibrationAppliesToAllKindsAndHoldCheckpoints(double offset)
        {
            service.Abort(session); service.SetInputOffset(offset);
            session = service.BeginPlay("training_01");
            judge = new RhythmInputJudge(session, points, config.rules, offset);
            foreach (BeatmapNote note in map.notes)
            {
                double t = note.time + offset / 1000;
                if (note.kind == RhythmNoteKind.Swipe) judge.Swipe(t); else judge.Press(t);
                judge.Release(t + note.duration + .001);
            }
            Assert.IsTrue(service.Complete(session).AllPerfect);
        }

        [Test]
        public void InvalidOrOverlappingChartFailsBeforePlaying()
        {
            map.notes[1].time = 2.1;
            Assert.Throws<InvalidOperationException>(() => map.Compile(config.Chart("training_01")));
            map.notes[1].time = 3; map.notes[7].duration = 1.1;
            Assert.Throws<InvalidOperationException>(() => map.Compile(config.Chart("training_01")));
        }

        [Test]
        public void TimeCannotGoBackwardsOrBecomeNonFinite()
        {
            judge.AdvanceTo(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.AdvanceTo(.9));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.AdvanceTo(double.NaN));
        }
    }
}

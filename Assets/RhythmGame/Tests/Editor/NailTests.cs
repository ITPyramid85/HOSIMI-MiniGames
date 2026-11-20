using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class NailTests
    {
        [Test]
        public void NailDrivingIsTheThirdMenuGame()
        {
            Assert.AreEqual("nails", MiniGamesMenu.Games[2].Id);
            Assert.AreEqual("DrivingNail", MiniGamesMenu.Games[2].Scene);
        }

        [Test]
        public void TheJobIsFiveNailsOfOneHit()
        {
            List<BeatCue> cues = NailChart.Generate(0);
            Assert.AreEqual(NailChart.FirstCue, cues[0].Time, .001);
            Assert.AreEqual(NailChart.NailCount, cues.Select(c => c.Variant).Distinct().Count());
            Assert.IsTrue(cues.All(c => c.Pad == NailChart.Pad));
            for (int nail = 0; nail < NailChart.NailCount; nail++)
                Assert.AreEqual(NailChart.Hits, cues.Count(c => c.Variant == nail), "Nail " + nail);
            Assert.AreEqual(2, NailChart.ChairAt);
            Assert.AreEqual(3, NailChart.DeskAt);
            Assert.AreEqual(5, NailChart.HouseAt);
        }

        [Test]
        public void ASecondBendStillBringsTheThirdNail()
        {
            List<BeatCue> cues = NailChart.Generate(1);
            BeatCue first = cues[0];
            NailChart.PullForward(cues, first, NailChart.BeatSeconds);
            BeatCue second = NailChart.NextNail(cues, first.Variant);
            Assert.AreEqual(first.Time + NailChart.RecoverBeats * NailChart.BeatSeconds, second.Time, .02);
            NailChart.PullForward(cues, second, NailChart.BeatSeconds);
            BeatCue third = NailChart.NextNail(cues, second.Variant);
            Assert.AreEqual(second.Time + NailChart.RecoverBeats * NailChart.BeatSeconds, third.Time, .02, "Leftover hits of a bent nail must not block the one after it.");
            Assert.AreEqual(NailChart.Hits, cues.Count(c => c.Variant == third.Variant));
        }

        [Test]
        public void OneMissDropsTheRestOfThatNailWithoutJudgingThem()
        {
            List<BeatCue> cues = NailChart.Generate(1);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            BeatCue first = cues[0];
            judge.AdvanceTo(first.Time + first.GoodMs / 1000 + .02);
            Assert.AreEqual(CueOutcome.Missed, results.Single().Outcome);
            judge.Abandon(cue => cue.Variant == first.Variant);
            double end = cues.Where(c => c.Variant == first.Variant).Max(c => c.Time) + 1;
            judge.AdvanceTo(end);
            Assert.AreEqual(1, results.Count(r => r.Outcome == CueOutcome.Missed), "A bent nail costs one life, not one per remaining hit.");
        }

        [Test]
        public void EveryStrikeOnTheBeatSeatsEveryNail()
        {
            List<BeatCue> cues = NailChart.Generate(4);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            foreach (BeatCue cue in cues) judge.Press(cue.Pad, cue.Time);
            judge.AdvanceTo(StageTable.ClearSeconds + 1);
            Assert.AreEqual(0, results.Count(r => r.Outcome == CueOutcome.Missed));
            Assert.AreEqual(cues.Count, results.Count(r => r.Outcome == CueOutcome.Hit));
            Assert.IsTrue(judge.AllResolved);
        }
    }
}

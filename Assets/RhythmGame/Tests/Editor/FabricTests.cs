using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class FabricTests
    {
        [Test]
        public void FabricBeatingIsTheSeventhMenuGame()
        {
            Assert.AreEqual("laundry", MiniGamesMenu.Games[6].Id);
            Assert.AreEqual("FabricBeating", MiniGamesMenu.Games[6].Scene);
            CollectionAssert.AreEqual(new[] { FabricChart.LeftPad, FabricChart.RightPad }, FabricChart.Pads(1));
        }

        [Test]
        public void ClubsAlternateAndTheGapsShrink()
        {
            List<BeatCue> cues = FabricChart.Generate(1);
            Assert.Greater(cues.Count, 100);
            Assert.AreEqual(FabricChart.FirstCue, cues[0].Time, .001);
            Assert.AreEqual(FabricChart.LeftPad, cues[0].Pad, "The first blow is the left club, pad 4.");
            Assert.AreEqual(FabricChart.RightPad, cues[1].Pad, "The next blow is the right club, pad 5.");
            for (int i = 1; i < cues.Count; i++)
            {
                Assert.AreNotEqual(cues[i].Pad, cues[i - 1].Pad, "Left and right never repeat.");
                Assert.GreaterOrEqual(cues[i].Time - cues[i - 1].Time, FabricChart.BeatSeconds / 2 - 1e-9);
                Stage stage = StageTable.At(cues[i].Time);
                Assert.AreEqual(stage.GoodMs, cues[i].GoodMs);
                Assert.AreEqual(stage.PerfectMs, cues[i].PerfectMs);
            }
            int firstMinute = cues.Count(c => c.Time < 60);
            int lastMinute = cues.Count(c => c.Time >= 420);
            Assert.Greater(lastMinute, firstMinute * 2, "Stage 10 beats far more often than the opening.");
        }

        [Test]
        public void APerfectPlayerLandsEveryBlow()
        {
            List<BeatCue> cues = FabricChart.Generate(4);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            foreach (BeatCue cue in cues) judge.Press(cue.Pad, cue.Time);
            judge.AdvanceTo(cues[cues.Count - 1].Time + 1);
            Assert.AreEqual(cues.Count, results.FindAll(r => r.Outcome == CueOutcome.Hit).Count);
            Assert.AreEqual(0, results.FindAll(r => r.Outcome == CueOutcome.Missed).Count);
        }

        [Test]
        public void TheSamePadTwiceIsAnEmptyStrike()
        {
            List<BeatCue> cues = FabricChart.Generate(2);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            BeatCue first = cues[0];
            judge.Press(first.Pad, first.Time);
            Assert.AreEqual(CueOutcome.Hit, results[0].Outcome);
            results.Clear();
            judge.Press(first.Pad, first.Time + .05);
            Assert.AreEqual(CueOutcome.EmptyHit, results[0].Outcome, "The same club again is not the next blow.");
            Assert.AreEqual(first.Pad, results[0].Pad);
        }
    }
}

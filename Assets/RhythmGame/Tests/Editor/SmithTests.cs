using System.Collections.Generic;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class SmithTests
    {
        [Test]
        public void SmithyIsTheFifthMenuGame()
        {
            Assert.AreEqual("smith", MiniGamesMenu.Games[4].Id);
            Assert.AreEqual("Smithy", MiniGamesMenu.Games[4].Scene);
        }

        [Test]
        public void SevenHeatsAndTheGlowShrinksEveryBlow()
        {
            List<BeatCue> cues = SmithChart.Generate(0);
            Assert.AreEqual(SmithChart.HeatCount, cues.Count);
            Assert.AreEqual(SmithChart.FirstCue, cues[0].Time, .001);
            CollectionAssert.AreEqual(new[] { SmithChart.CrashPad, SmithChart.StackPad }, SmithChart.Pads(1));
            Assert.AreEqual(SmithChart.StackPad, cues[0].Pad, "Hammering blows are pad 2.");
            Assert.AreEqual(SmithChart.CrashPad, cues[cues.Count - 1].Pad, "The finishing blow is crash 1.");
            Assert.IsTrue(cues.GetRange(0, cues.Count - 1).TrueForAll(c => c.Pad == SmithChart.StackPad));
            Assert.AreEqual(7, SmithChart.LegendaryAt);
            for (int i = 1; i < cues.Count; i++)
            {
                Assert.Less(cues[i].GoodMs, cues[i - 1].GoodMs, "Each glow is shorter than the last.");
                Assert.Greater(cues[i].Time, cues[i - 1].Time);
                Assert.AreEqual(i, cues[i].Variant);
            }
        }

        [Test]
        public void ABlowInsideTheGlowHitsAndAColdBlowDoesNot()
        {
            List<BeatCue> cues = SmithChart.Generate(1);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            BeatCue first = cues[0];
            judge.Press(first.Pad, first.Time - first.GoodMs / 1000.0 - .05);
            Assert.AreEqual(CueOutcome.EmptyHit, results[0].Outcome, "Before the iron glows, the blow is cold.");
            results.Clear();
            judge.Press(first.Pad, first.Time);
            Assert.AreEqual(CueOutcome.Hit, results[0].Outcome, "The glowing beat takes the blow.");
            Assert.AreEqual(HitGrade.Perfect, results[0].Grade);
        }

        [Test]
        public void EveryHotBlowForgesTheWholeSword()
        {
            List<BeatCue> cues = SmithChart.Generate(4);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            foreach (BeatCue cue in cues) judge.Press(cue.Pad, cue.Time);
            judge.AdvanceTo(cues[cues.Count - 1].Time + 1);
            Assert.AreEqual(cues.Count, results.FindAll(r => r.Outcome == CueOutcome.Hit).Count);
            Assert.AreEqual(0, results.FindAll(r => r.Outcome == CueOutcome.Missed).Count);
            Assert.AreEqual("smith.legendary", SmithChart.StageKey(SmithChart.LegendaryAt));
        }
    }
}

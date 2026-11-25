using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class WoodTests
    {
        [Test]
        public void ChoppingWoodIsTheFourthMenuGame()
        {
            Assert.AreEqual("wood", MiniGamesMenu.Games[3].Id);
            Assert.AreEqual("ChoppingWood", MiniGamesMenu.Games[3].Scene);
        }

        [Test]
        public void TheBeltMixesLogsMelonsAndStones()
        {
            List<BeatCue> cues = WoodChart.Generate(1);
            Assert.AreEqual(7, WoodChart.Pad, "The start prompt lights pad 8.");
            Assert.AreEqual(WoodChart.FirstCue, cues[0].Time, .001);
            Assert.IsTrue(cues.All(c => c.Pad == WoodChart.Pad));
            Assert.IsTrue(cues.Any(c => c.Kind == CueKind.Bonus), "A watermelon is a bonus chop.");
            Assert.IsTrue(cues.Any(c => c.Kind == CueKind.Forbidden), "A stone must not be hit.");
            Assert.IsFalse(cues.Where(c => c.Kind != CueKind.Normal).GroupBy(c => c.Variant).Any(g => g.Count() != 1));
            var thick = cues.Where(c => c.Kind == CueKind.Normal).GroupBy(c => c.Variant).First(g => g.Count() == 2).OrderBy(c => c.Time).ToArray();
            Assert.AreEqual(WoodChart.BeatSeconds, thick[1].Time - thick[0].Time, .001, "A thick log is two beats in a row.");
            Assert.AreEqual(2, WoodChart.GoldAt);
            Assert.IsTrue(cues.Any(c => c.Kind == CueKind.Forbidden && c.Time < WoodChart.RunSeconds), "A stone shows up during the two minutes.");
            Assert.IsTrue(cues.All(c => c.Time < WoodChart.RunSeconds), "Every piece is inside the two minutes.");
            Assert.Greater(cues[cues.Count - 1].Time, WoodChart.RunSeconds - 5, "Pieces come right up to the end.");
            Stage stage1 = StageTable.Get(1);
            Assert.IsTrue(cues.All(c => c.PerfectMs == stage1.PerfectMs && c.GoodMs == stage1.GoodMs), "The belt never enters stage 2.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class IceTests
    {
        [Test]
        public void IceFishingIsTheSixthMenuGame()
        {
            Assert.AreEqual("ice", MiniGamesMenu.Games[5].Id);
            Assert.AreEqual("IceFishing", MiniGamesMenu.Games[5].Scene);
        }

        [Test]
        public void TheSchoolUsesThreeHolesAndBigFishTakeTwoStrikes()
        {
            List<BeatCue> cues = IceChart.Generate(1);
            CollectionAssert.AreEqual(new[] { IceChart.Left, IceChart.Center, IceChart.Right }, IceChart.Pads(1));
            Assert.AreEqual(3, IceChart.Left, "Pad 4 is the left ice.");
            Assert.AreEqual(4, IceChart.Center, "Pad 5 is the middle ice.");
            Assert.AreEqual(7, IceChart.Right, "Pad 8 is the right ice.");
            Assert.IsTrue(cues.TrueForAll(c => c.Pad == IceChart.Left || c.Pad == IceChart.Center || c.Pad == IceChart.Right));
            int feints = cues.FindAll(c => c.Kind == CueKind.Forbidden).Count;
            Assert.AreEqual(2, feints, "Two fish pretend to stop.");
            var big = new HashSet<int>();
            foreach (BeatCue cue in cues)
            {
                if (!IceChart.IsBig(IceChart.SpeciesOf(cue.Variant))) continue;
                Assert.AreEqual(CueKind.Normal, cue.Kind);
                big.Add(cue.Variant);
            }
            Assert.Greater(big.Count, 0);
            foreach (int variant in big)
                Assert.AreEqual(2, cues.FindAll(c => c.Variant == variant).Count, "A big fish is two strikes.");
            for (int i = 1; i < cues.Count; i++) Assert.Greater(cues[i].Time, cues[i - 1].Time);
            Assert.IsTrue(cues.TrueForAll(c => c.GoodMs == 300), "The hit window is the 0.6s the fish stays.");
        }

        [Test]
        public void TheHoleOnTheBeatCatchesAndAnotherHoleDoesNot()
        {
            List<BeatCue> cues = IceChart.Generate(2);
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            BeatCue first = cues[0];
            int other = first.Pad == IceChart.Left ? IceChart.Right : IceChart.Left;
            judge.Press(other, first.Time);
            Assert.AreEqual(CueOutcome.EmptyHit, results[0].Outcome, "The wrong hole is empty.");
            results.Clear();
            judge.Press(first.Pad, first.Time);
            Assert.AreEqual(CueOutcome.Hit, results[0].Outcome);
        }

        [Test]
        public void ACaughtSpeciesIsOneDexEntry()
        {
            var player = PlayerData.Create(RhythmGameData.LoadDefaultConfig());
            Assert.IsTrue(player.DiscoverFish("pike"));
            Assert.IsFalse(player.DiscoverFish("pike"));
            Assert.AreEqual(1, player.fishDex.Count);
            player.Validate(RhythmGameData.LoadDefaultConfig());
        }
    }
}

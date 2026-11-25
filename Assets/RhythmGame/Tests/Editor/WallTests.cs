using System.Collections.Generic;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class WallTests
    {
        [Test]
        public void BreakingBricksOpensAfterEveryOtherGameHasATime()
        {
            Assert.AreEqual("wall", MiniGamesMenu.Games[9].Id);
            Assert.AreEqual("BreakingBricks", MiniGamesMenu.Games[9].Scene);
            Assert.AreEqual(9, WallChart.TimesToOpen);
            var player = new PlayerData();
            Assert.AreEqual(0, WallChart.RecordedTimes(player));
            Assert.IsFalse(WallChart.Unlocked(player));
            Assert.IsTrue(MiniGamesMenu.IsPlayable(MiniGamesMenu.Games[9], player), "Wall Breaker is built, so its card opens with the others.");
            for (int i = 0; i < 8; i++)
                player.miniGames.Add(new MiniGameRecord { gameId = MiniGamesMenu.Games[i].Id, plays = 1, bestStage = 6, bestSeconds = 30 });
            Assert.AreEqual(8, WallChart.RecordedTimes(player));
            Assert.IsFalse(WallChart.Unlocked(player));
            player.miniGames.Add(new MiniGameRecord { gameId = "race", plays = 1, bestStage = 9 });
            Assert.AreEqual(8, WallChart.RecordedTimes(player), "A stage with no time does not unlock the last game.");
            player.FindMiniGame("race").bestSeconds = 262;
            player.miniGames.Add(new MiniGameRecord { gameId = "wall", plays = 4, bestStage = 1, bestSeconds = 400 });
            Assert.AreEqual(9, WallChart.RecordedTimes(player));
            Assert.IsTrue(WallChart.Unlocked(player));
            Assert.IsTrue(MiniGamesMenu.IsPlayable(MiniGamesMenu.Games[9], player));
            Assert.IsTrue(MiniGamesMenu.IsPlayable(MiniGamesMenu.Games[8], player));
            Assert.AreEqual("4:22", MiniGameController.Clock(player.FindMiniGame("race").bestSeconds));
        }

        [Test]
        public void TheSameSeedBuildsTheSameWalls()
        {
            List<BeatCue> first = WallChart.Generate(4);
            List<BeatCue> second = WallChart.Generate(4);
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Time, second[i].Time, 1e-9);
                Assert.AreEqual(first[i].Pad, second[i].Pad);
                Assert.AreEqual(first[i].Variant, second[i].Variant);
                Assert.AreEqual(first[i].Kind, second[i].Kind);
            }
        }

        [Test]
        public void ThicknessPicksThePadAndThickWallsAreAShortRoll()
        {
            bool thin = false, mid = false, thick = false, chest = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                List<BeatCue> cues = WallChart.Generate(seed);
                Assert.Greater(cues.Count, 40);
                var order = new List<int>();
                var groups = new Dictionary<int, List<BeatCue>>();
                for (int i = 0; i < cues.Count; i++)
                {
                    BeatCue cue = cues[i];
                    Assert.GreaterOrEqual(cue.Time, 2);
                    Assert.Less(cue.Time, StageTable.ClearSeconds);
                    Stage stage = StageTable.At(cue.Time);
                    Assert.AreEqual(stage.GoodMs, cue.GoodMs);
                    Assert.AreEqual(stage.PerfectMs, cue.PerfectMs);
                    if (i > 0) Assert.GreaterOrEqual(cue.Time - cues[i - 1].Time, WallChart.BeatSeconds * 0.5 - 1e-6);
                    int wall = cue.Variant / 10;
                    if (!groups.ContainsKey(wall))
                    {
                        groups[wall] = new List<BeatCue>();
                        order.Add(wall);
                    }
                    groups[wall].Add(cue);
                }
                double previous = -1;
                foreach (int wall in order)
                {
                    List<BeatCue> group = groups[wall];
                    int need = group[0].Variant % 10;
                    int pad = group[0].Pad;
                    Assert.AreEqual(need, group.Count);
                    if (previous >= 0) Assert.AreEqual(WallChart.CruiseGap, group[0].Time - previous, 1e-6);
                    previous = group[group.Count - 1].Time;
                    for (int h = 0; h < group.Count; h++)
                    {
                        Assert.AreEqual(pad, group[h].Pad);
                        Assert.AreEqual(group[0].Kind, group[h].Kind);
                        if (h > 0) Assert.AreEqual(WallChart.BeatSeconds, group[h].Time - group[h - 1].Time, 1e-6);
                    }
                    if (need >= 2)
                    {
                        thick = true;
                        Assert.IsTrue(need == 2 || need == 3);
                        Assert.AreEqual(WallChart.ThickPad, pad);
                        Assert.AreEqual(CueKind.Normal, group[0].Kind);
                    }
                    else if (pad == WallChart.ChestPad) { chest = true; Assert.AreEqual(CueKind.Bonus, group[0].Kind); }
                    else if (pad == WallChart.ThinPad) { thin = true; Assert.AreEqual(CueKind.Normal, group[0].Kind); }
                    else if (pad == WallChart.MidPad) { mid = true; Assert.AreEqual(CueKind.Normal, group[0].Kind); }
                    else Assert.Fail("Unexpected pad " + pad);
                }
            }
            Assert.IsTrue(thin && mid && thick && chest);
        }

        [Test]
        public void AMissLetsTheWaterCatchUpAndAChestHoldsItBack()
        {
            var flood = new WallChart.Flood();
            Assert.AreEqual(WallChart.StartGap, flood.Gap);
            Assert.AreEqual(0, flood.Meters);
            flood.Creep();
            Assert.AreEqual(WallChart.StartGap - 1, flood.Gap);
            flood.Resolve(true, false);
            Assert.AreEqual(1, flood.Meters);
            Assert.AreEqual(WallChart.StartGap, flood.Gap);
            flood.Resolve(true, false);
            Assert.AreEqual(WallChart.StartGap, flood.Gap, "A plain wall never pushes past the starting room.");
            flood.Resolve(true, true);
            Assert.AreEqual(3, flood.Meters);
            Assert.AreEqual(WallChart.StartGap + 2, flood.Gap);
            for (int i = 0; i < WallChart.TreasureAir + 4; i++) flood.Resolve(true, true);
            Assert.AreEqual(WallChart.StartGap + WallChart.TreasureAir, flood.Gap);
            int before = flood.Meters;
            int gap = flood.Gap;
            for (int i = 0; i < gap; i++) flood.Resolve(false, false);
            Assert.IsTrue(flood.Submerged);
            Assert.AreEqual(0, flood.Gap);
            Assert.AreEqual(before, flood.Meters);
            flood.Resolve(true, false);
            Assert.AreEqual(before, flood.Meters);
        }

        [TestCase(9)] [TestCase(21)] [TestCase(77)]
        public void PerfectBreaksOutrunTheRisingWater(int seed)
        {
            List<BeatCue> cues = WallChart.Generate(seed);
            var flood = new WallChart.Flood();
            int walls = 0, index = 0, creeps = 0;
            while (index < cues.Count)
            {
                int wall = cues[index].Variant / 10;
                bool chest = cues[index].Pad == WallChart.ChestPad;
                while (index < cues.Count && cues[index].Variant / 10 == wall) index++;
                double brokeAt = cues[index - 1].Time;
                for (int due = WallChart.CreepsBy(brokeAt); creeps < due; creeps++) flood.Creep();
                Assert.IsFalse(flood.Submerged, "Perfect play is never caught, seed " + seed + " wall " + walls);
                flood.Resolve(true, chest);
                walls++;
            }
            Assert.Greater(creeps, 100, "The water keeps rising through the whole run.");
            Assert.AreEqual(walls, flood.Meters);
        }

        [Test]
        public void StandingStillDrownsWithinSixCreeps()
        {
            var flood = new WallChart.Flood();
            Assert.AreEqual(0, WallChart.CreepsBy(-1));
            Assert.AreEqual(0, WallChart.CreepsBy(WallChart.FirstCue + WallChart.CreepSeconds - 0.01));
            Assert.AreEqual(1, WallChart.CreepsBy(WallChart.FirstCue + WallChart.CreepSeconds));
            int due = WallChart.CreepsBy(WallChart.FirstCue + WallChart.CreepSeconds * WallChart.StartGap);
            for (int i = 0; i < due; i++) flood.Creep();
            Assert.IsTrue(flood.Submerged);
        }
    }
}

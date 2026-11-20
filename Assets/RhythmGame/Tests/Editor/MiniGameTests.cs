using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class MiniGameTests
    {
        private static BeatCue Cue(double time, int pad, CueKind kind = CueKind.Normal) { return new BeatCue { Time = time, Pad = pad, Kind = kind }; }

        private static List<CueResult> Record(BeatJudge judge)
        {
            var results = new List<CueResult>();
            judge.Judged += results.Add;
            return results;
        }

        [TestCase(0, HitGrade.Perfect)] [TestCase(.069, HitGrade.Perfect)] [TestCase(-.065, HitGrade.Perfect)]
        [TestCase(.1, HitGrade.Good)] [TestCase(-.149, HitGrade.Good)]
        public void StrikesAreGradedAgainstTheBeatNotTheCue(double error, HitGrade grade)
        {
            var judge = new BeatJudge(new[] { Cue(2, 5) }, 70, 150, 0, false);
            var results = Record(judge);
            judge.Press(5, 2 + error);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(CueOutcome.Hit, results[0].Outcome);
            Assert.AreEqual(grade, results[0].Grade);
            Assert.AreEqual(error * 1000, results[0].OffsetMs, .001, "Positive offset means late.");
        }

        [Test]
        public void OutsideTheWindowIsEmptyThenTheCueIsMissedOnce()
        {
            var judge = new BeatJudge(new[] { Cue(2, 5) }, 70, 150, 0, true);
            var results = Record(judge);
            judge.Press(5, 1.8);
            Assert.AreEqual(CueOutcome.EmptyHit, results.Single().Outcome, "Too early to belong to the cue.");
            judge.AdvanceTo(2.15);
            Assert.AreEqual(1, results.Count, "Still inside the late window.");
            judge.AdvanceTo(2.16);
            Assert.AreEqual(CueOutcome.Missed, results[1].Outcome);
            judge.Press(5, 2.1);
            judge.AdvanceTo(10);
            Assert.AreEqual(CueOutcome.EmptyHit, results[2].Outcome, "A settled cue cannot be hit again.");
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(judge.AllResolved);
        }

        [Test]
        public void WrongPadsAreIgnoredUnlessEveryPadIsJudged()
        {
            var lenient = new BeatJudge(new[] { Cue(2, 5) }, 70, 150, 0, false);
            var results = Record(lenient);
            lenient.Press(3, 2);
            Assert.IsEmpty(results, "Plan §3: hitting another pad is neither judged nor penalised.");
            var strict = new BeatJudge(new[] { Cue(2, 5) }, 70, 150, 0, true);
            results = Record(strict);
            strict.Press(3, 2);
            Assert.AreEqual(CueOutcome.EmptyHit, results.Single().Outcome);
        }

        [Test]
        public void ForbiddenCuesPenaliseAStrikeAndPassQuietlyOtherwise()
        {
            var judge = new BeatJudge(new[] { Cue(2, 1, CueKind.Forbidden), Cue(3, 2, CueKind.Forbidden) }, 70, 150, 0, true);
            var results = Record(judge);
            judge.Press(1, 2.01);
            judge.AdvanceTo(4);
            Assert.AreEqual(CueOutcome.ForbiddenHit, results[0].Outcome);
            Assert.AreEqual(CueOutcome.ForbiddenAvoided, results[1].Outcome);
        }

        [Test]
        public void NearestCueOnThePadWinsAndCalibrationIsApplied()
        {
            var judge = new BeatJudge(new[] { Cue(2, 4), Cue(2.2, 4) }, 70, 150, 200, false);
            var results = Record(judge);
            judge.Press(4, 2.13 + .2);   // 200 ms output latency removed: 130 ms after the first, 70 ms before the second
            Assert.AreSame(judge.Cues[1], results.Single().Cue);
            Assert.AreEqual(-70, results[0].OffsetMs, .001);
            Assert.IsFalse(judge.IsResolved(judge.Cues[0]), "The farther cue stays open.");
        }

        [Test]
        public void ACueCanCarryItsOwnTighterWindows()
        {
            var tight = new BeatCue { Time = 2, Pad = 0, PerfectMs = 20, GoodMs = 50 };
            var judge = new BeatJudge(new[] { tight }, 70, 150, 0, true);
            var results = Record(judge);
            judge.Press(0, 1.94);
            Assert.AreEqual(CueOutcome.EmptyHit, results[0].Outcome, "60 ms early is outside this cue's 50 ms window.");
            judge.Press(0, 2.03);
            Assert.AreEqual(HitGrade.Good, results[1].Grade, "30 ms is Good, not Perfect, at 20 ms.");
        }

        [Test]
        public void StagesFollowTheClientsCurve()
        {
            Assert.AreEqual(1, StageTable.At(0).Number);
            Assert.AreEqual(1, StageTable.At(44.9).Number);
            Assert.AreEqual(2, StageTable.At(45).Number);
            Assert.LessOrEqual(StageTable.Get(5).StartSeconds, 180, "Gentle for the first 2-3 minutes.");
            Assert.AreEqual(300, StageTable.Get(7).StartSeconds, "Stage 7 at about five minutes.");
            Assert.AreEqual(10, StageTable.At(479).Number);
            Assert.AreEqual(10, StageTable.At(10000).Number, "No stage beyond 10.");
            for (int n = 2; n <= StageTable.Count; n++)
            {
                Assert.Less(StageTable.Get(n).GoodMs, StageTable.Get(n - 1).GoodMs, "Windows narrow every stage.");
                Assert.Less(StageTable.Get(n).PerfectMs, StageTable.Get(n - 1).PerfectMs);
            }
            Assert.AreEqual(.5, StageTable.ProgressIn(22.5), 1e-9);
            Assert.AreEqual(1, StageTable.ProgressIn(StageTable.ClearSeconds));
        }

        [Test]
        public void DrumsticksGrowWithTimeAndStageAndClearingPaysTheBonus()
        {
            Assert.AreEqual(0, DrumstickReward.For(0, 1, false));
            Assert.AreEqual(15, DrumstickReward.For(60, 1, false), "15 a minute.");
            Assert.AreEqual(42 + 2 + 3 + 4 + 6, DrumstickReward.For(170, 5, false), "Plus a bonus for each stage reached.");
            Assert.AreEqual(316, DrumstickReward.For(StageTable.ClearSeconds, 10, true), "A full clear includes the 100 bonus.");
            Assert.AreEqual(DrumstickReward.For(480, 10, false) + DrumstickReward.ClearBonus, DrumstickReward.For(480, 10, true));
        }

        [Test]
        public void HalfHeartsFallOnMissesAndATwentyComboRestoresOneHeart()
        {
            var cue = Cue(1, 0);
            var score = new MiniGameScore();
            Assert.AreEqual(MiniGameScore.MaxHalves, score.Halves);
            Assert.AreEqual(MiniGameScore.MaxLives, score.Lives);
            score.Add(new CueResult { Cue = cue, Outcome = CueOutcome.EmptyHit });
            Assert.AreEqual(MiniGameScore.MaxHalves, score.Halves, "A stray strike breaks the combo but costs no heart.");
            score.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Missed });
            Assert.AreEqual(MiniGameScore.MaxHalves - 1, score.Halves, "A miss costs half a heart.");
            score.Add(new CueResult { Cue = Cue(1, 0, CueKind.Forbidden), Outcome = CueOutcome.ForbiddenHit });
            score.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Missed });
            Assert.AreEqual(MiniGameScore.MaxHalves - 3, score.Halves, "A wrong target costs half a heart.");
            for (int i = 0; i < MiniGameScore.ComboPerLife; i++) score.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Hit, Grade = HitGrade.Perfect });
            Assert.AreEqual(MiniGameScore.MaxHalves - 1, score.Halves, "Twenty in a row gives one heart back.");
            Assert.AreEqual(2000, score.Score);
            score.Add(new CueResult { Cue = Cue(1, 0, CueKind.Bonus), Outcome = CueOutcome.Hit, Grade = HitGrade.Good });
            Assert.AreEqual(2150, score.Score, "Bonus cues are worth triple.");
            Assert.AreEqual(MiniGameScore.MaxHalves - 1, score.Halves, "The heal does not stack past the next twenty.");
            for (int i = 0; i < MiniGameScore.ComboPerLife - 1; i++) score.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Hit, Grade = HitGrade.Perfect });
            Assert.AreEqual(MiniGameScore.MaxHalves, score.Halves, "One heart back cannot rise past five hearts.");
            var fresh = new MiniGameScore();
            for (int i = 0; i < MiniGameScore.MaxHalves; i++) fresh.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Missed });
            Assert.AreEqual(0, fresh.Halves);
            Assert.AreEqual(0, fresh.Lives);
            Assert.IsTrue(fresh.IsOver);
            fresh.Add(new CueResult { Cue = cue, Outcome = CueOutcome.Missed });
            Assert.AreEqual(0, fresh.Halves, "Ten misses end the run, and further misses stay at zero.");
        }

        [Test]
        public void RecordsKeepTheBestStageAndLongestTimeAndPayDrumsticks()
        {
            GameConfig config = RhythmGameData.LoadDefaultConfig();
            PlayerData player = PlayerData.Create(config);
            var service = new ProgressionService(config, player);
            MiniGameResult first = service.RecordMiniGame("mochi", 200, 5, false);
            Assert.IsTrue(first.NewBestStage && first.NewBestTime);
            Assert.AreEqual(DrumstickReward.For(200, 5, false), player.coins);
            MiniGameResult second = service.RecordMiniGame("mochi", 90, 3, false);
            Assert.IsFalse(second.NewBestStage || second.NewBestTime);
            MiniGameRecord record = player.FindMiniGame("mochi");
            Assert.AreEqual(5, record.bestStage);
            Assert.AreEqual(200, record.bestSeconds);
            Assert.AreEqual(2, record.plays);
            Assert.AreEqual(first.Drumsticks + second.Drumsticks, record.totalDrumsticks);
            PlayerData loaded = UnityEngine.JsonUtility.FromJson<PlayerData>(UnityEngine.JsonUtility.ToJson(player));
            loaded.Validate(config);
            Assert.AreEqual(5, loaded.FindMiniGame("mochi").bestStage);
        }

        private static void AssertPlayable(List<BeatCue> cues, string game)
        {
            for (int i = 1; i < cues.Count; i++) Assert.Greater(cues[i].Time, cues[i - 1].Time, game + ": in order");
            Assert.IsTrue(cues.All(c => c.Time >= 2 && c.Time < StageTable.ClearSeconds), game + ": inside the run");
            foreach (BeatCue cue in cues)
            {
                Stage stage = StageTable.At(cue.Time);
                double good = stage.GoodMs, perfect = stage.PerfectMs;
                if (game == "whack") good = WhackAMoleChart.ShowSeconds * 500;
                if (game == "mochi")
                {
                    Stage mochi = MochiChart.StageAt(cue.Time);
                    good = mochi.GoodMs;
                    perfect = mochi.PerfectMs;
                }
                Assert.AreEqual(good, cue.GoodMs, game + ": stage windows");
                Assert.AreEqual(perfect, cue.PerfectMs);
            }
            var judge = new BeatJudge(cues, 80, 170, 0, true);
            var results = Record(judge);
            foreach (BeatCue cue in cues) if (cue.Kind != CueKind.Forbidden) judge.Press(cue.Pad, cue.Time);
            judge.AdvanceTo(1000);
            var score = new MiniGameScore();
            results.ForEach(score.Add);
            Assert.AreEqual(0, score.Miss, game + ": a perfect player clears every stage.");
            Assert.AreEqual(MiniGameScore.MaxLives, score.Lives);
        }

        [TestCase(1)] [TestCase(42)]
        public void WhackChartsAreSeededAndPlayableToTheEnd(int seed)
        {
            List<BeatCue> cues = WhackAMoleChart.Generate(seed);
            Assert.AreEqual(cues.Select(c => c.Time + "/" + c.Pad + "/" + c.Kind), WhackAMoleChart.Generate(seed).Select(c => c.Time + "/" + c.Pad + "/" + c.Kind), "Same seed, same chart.");
            AssertPlayable(cues, "whack");
            for (int i = 1; i < cues.Count; i++) Assert.AreNotEqual(cues[i].Pad, cues[i - 1].Pad, "Consecutive moles come from different holes.");
            foreach (BeatCue cue in cues) Assert.Contains(cue.Pad, WhackAMoleChart.Pads(StageTable.At(cue.Time).Number));
        }

        [Test]
        public void WhackWidensThePadsAndAddsTwistsAsStagesRise()
        {
            List<BeatCue> cues = WhackAMoleChart.Generate(7);
            List<BeatCue> early = cues.Where(c => c.Time < StageTable.Get(3).StartSeconds).ToList();
            List<BeatCue> late = cues.Where(c => c.Time >= StageTable.Get(6).StartSeconds).ToList();
            CollectionAssert.IsSubsetOf(early.Select(c => c.Pad).Distinct(), new[] { 5, 6, 7 });
            Assert.IsFalse(cues.Any(c => c.Kind == CueKind.Forbidden && c.Time < StageTable.Get(4).StartSeconds), "No flower moles before stage 4.");
            Assert.AreEqual(8, late.Select(c => c.Pad).Distinct().Count());
            Assert.IsTrue(late.Any(c => c.Kind == CueKind.Forbidden));
            Assert.IsTrue(late.Any(c => c.Variant > 0), "Disguises appear from stage 6.");
            Assert.Greater(cues.Count(c => c.Time >= 420), cues.Count(c => c.Time < 60) * 1.5, "Much denser by stage 10.");
        }

        [TestCase(3)] [TestCase(11)]
        public void MochiChartsUseOnlyPadEightAndStayPlayable(int seed)
        {
            List<BeatCue> cues = MochiChart.Generate(seed);
            Assert.IsTrue(cues.All(c => c.Pad == MochiChart.Pad), "Tom 3 only (plan §4-01).");
            CollectionAssert.AreEqual(new[] { MochiChart.Pad }, MochiChart.Pads(1));
            for (int i = 1; i < cues.Count; i++)
                Assert.GreaterOrEqual(cues[i].Time - cues[i - 1].Time, MochiChart.BeatSeconds / 2 - 1e-9, "At least half a beat apart.");
            Assert.IsTrue(cues.All(c => c.Time < MochiChart.RunSeconds), "The run ends at about one minute.");
            Assert.IsFalse(cues.Any(c => c.Kind == CueKind.Forbidden), "The one-minute game does not feint.");
            AssertPlayable(cues, "mochi");
        }

        [Test]
        public void MochiLevelOneIsSlowLenientAndStartsOnTheThirdBeatAfterGo()
        {
            Assert.AreEqual((4 + MochiChart.PrepBeats) * MochiChart.BeatSeconds, MochiChart.FirstCue, 1e-9, "GO, then two listen beats, then the first flip.");
            double byWindow = MochiChart.FirstCue - MochiChart.StageOf(1).GoodMs / 1000.0;
            double afterListen = 5 * MochiChart.BeatSeconds + .02;
            Assert.AreEqual(byWindow > afterListen ? byWindow : afterListen, MochiChart.JudgementOpens, 1e-9);
            Assert.Greater(MochiChart.JudgementOpens, 5 * MochiChart.BeatSeconds, "Both listen beats begin before the window opens.");
            Assert.GreaterOrEqual(MochiChart.StageOf(1).GoodMs, 900, "Level 1 counts a strike through the whole slow lift.");
            Assert.AreEqual(1, MochiChart.StageAt(0).Number);
            Assert.AreEqual(1, MochiChart.StageAt(MochiChart.RunSeconds - .01).Number);
            Assert.AreEqual(60, MochiChart.RunSeconds, "About one minute, then the clear dialog.");
            Assert.Greater(MochiChart.StageOf(1).GoodMs, StageTable.Get(1).GoodMs);
            Assert.Greater(MochiChart.StageOf(1).PerfectMs, StageTable.Get(1).PerfectMs);
            for (int n = 2; n <= 10; n++)
            {
                Assert.Less(MochiChart.StageOf(n).GoodMs, MochiChart.StageOf(n - 1).GoodMs);
                Assert.Less(MochiChart.StageOf(n).PerfectMs, MochiChart.StageOf(n - 1).PerfectMs);
                Assert.Greater(MochiChart.StageOf(n).StartSeconds, MochiChart.StageOf(n - 1).StartSeconds);
            }
            List<BeatCue> cues = MochiChart.Generate(3);
            Assert.AreEqual(MochiChart.FirstCue, cues[0].Time, 1e-9);
            Assert.AreEqual(MochiChart.StageOf(1).GoodMs, cues[0].GoodMs);
            double levelOneEnd = MochiChart.StageOf(2).StartSeconds;
            foreach (BeatCue cue in cues)
            {
                if (cue.Time >= levelOneEnd) break;
                Assert.GreaterOrEqual(cue.Time, MochiChart.FirstCue);
            }
            for (int i = 1; i < cues.Count; i++)
            {
                if (cues[i].Time >= levelOneEnd) break;
                Assert.GreaterOrEqual(cues[i].Time - cues[i - 1].Time, MochiChart.BeatSeconds * 4 - 1e-9, "Level 1 stays at least two seconds apart.");
            }
        }

        [Test]
        public void MochiLastsAboutOneMinute()
        {
            for (int seed = 0; seed < 5; seed++)
            {
                List<BeatCue> cues = MochiChart.Generate(seed);
                Assert.Greater(cues.Count, 10, "A minute still has a run of cakes.");
                Assert.Less(cues[cues.Count - 1].Time, MochiChart.RunSeconds);
                Assert.Greater(cues[cues.Count - 1].Time, MochiChart.RunSeconds * .5);
            }
        }
    }
}

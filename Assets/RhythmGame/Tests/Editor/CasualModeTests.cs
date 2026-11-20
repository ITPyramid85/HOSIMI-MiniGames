using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class CasualModeTests
    {
        private GameConfig config;
        private JudgementPoint[] points;
        private ProgressionService service;
        private PlaySession session;
        private PadInputJudge judge;
        [SetUp]
        public void SetUp()
        {
            config = RhythmGameData.LoadDefaultConfig();
            var chart = config.Chart(CasualMode.ChartId);
            points = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text).Compile(chart);
            service = new ProgressionService(config, PlayerData.Create(config));
            session = service.BeginPlay(chart.id);
            judge = new PadInputJudge(session, points, 0);
        }
        [Test]
        public void BeginnerChartStartsOnThreeBottomPadsAndGraduallyExpandsWithoutChords()
        {
            Assert.AreEqual(41, points.Length);
            Assert.IsTrue(points.All(p => p.Kind == RhythmNoteKind.Tap && !p.IsHoldTick));
            Assert.IsTrue(points.Where(p => p.Time < 20).All(p => p.Pad >= 5));
            Assert.IsTrue(points.Where(p => p.Time < 40).All(p => p.Pad >= 2));
            Assert.AreEqual(8, points.Select(p => p.Pad).Distinct().Count());
            for (int i = 1; i < points.Length; i++)
                Assert.Greater(points[i].Time - points[i - 1].Time, CasualMode.GoodWindowMs(points[i - 1].Time) / 1000);
        }
        [TestCase(0, 450, Judgement.Perfect)]
        [TestCase(0, 451, Judgement.Good)]
        [TestCase(0, 900, Judgement.Good)]
        [TestCase(0, 901, Judgement.Miss)]
        [TestCase(9, 350, Judgement.Perfect)]
        [TestCase(9, 700, Judgement.Good)]
        [TestCase(9, 701, Judgement.Miss)]
        [TestCase(22, 250, Judgement.Perfect)]
        [TestCase(22, 500, Judgement.Good)]
        [TestCase(22, 501, Judgement.Miss)]
        public void ThreeJudgementsUseStageOfNoteAndStartAtVisibleLight(int index, double ms, Judgement expected)
        {
            for (int i = 0; i < index; i++)
            {
                judge.Present(points[i].Time);
                judge.Press(points[i].Pad, points[i].Time); judge.Release(points[i].Pad, points[i].Time);
            }
            double shown = points[index].Time + .016;
            judge.Present(shown);
            HitResult hit = null; judge.Judged += result => hit = result;
            judge.Press(points[index].Pad, shown + ms / 1000);
            Assert.AreEqual(expected, hit.Judgement);
        }
        [Test]
        public void GoodKeepsComboAndOnlyMissReducesTheBar()
        {
            judge.Present(2); judge.Press(5, 2.7); judge.Release(5, 2.7);
            Assert.AreEqual(1, session.Combo); Assert.AreEqual(100, session.Health);
            judge.AdvanceTo(4.901);
            Assert.AreEqual(0, session.Combo); Assert.AreEqual(90, session.Health);
            judge.Present(6); judge.Press(7, 6.1);
            Assert.AreEqual(90, session.Health);
        }
        [Test]
        public void EveryCharacterAndLegacyLevelHasIdenticalScoreWithoutMutatingOwnedData()
        {
            service.Abort(session);
            long expected = -1;
            foreach (var character in config.characters)
            foreach (int level in new[] { 1, 100 })
            {
                var player = PlayerData.Create(config);
                if (player.FindCharacter(character.id) == null) player.characters.Add(new OwnedCharacter { characterId = character.id, level = level });
                player.FindCharacter(character.id).level = level; player.equippedCharacterId = character.id;
                string before = JsonUtility.ToJson(player);
                var progression = new ProgressionService(config, player);
                var run = progression.BeginPlay(CasualMode.ChartId);
                var input = new PadInputJudge(run, points, 0);
                foreach (var point in points)
                {
                    input.Present(point.Time); input.Press(point.Pad, point.Time + .1); input.Release(point.Pad, point.Time + .1);
                }
                Assert.AreEqual(before, JsonUtility.ToJson(player), "Playing must not modify legacy levels or ownership.");
                var result = progression.Complete(run);
                if (expected < 0) expected = result.Score;
                Assert.AreEqual(expected, result.Score);
                Assert.IsTrue(result.AllPerfect); Assert.AreEqual(0, result.Great);
                JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(player)).Validate(config);
            }
        }
        [Test]
        public void TenMissesEndRunAndAllGoodCanClearWithoutGreatResults()
        {
            judge.AdvanceTo(60);
            var failed = service.Complete(session);
            Assert.AreEqual(10, failed.Miss); Assert.AreEqual(0, failed.TotalCoins);
            session = service.BeginPlay(CasualMode.ChartId); judge = new PadInputJudge(session, points, 0);
            foreach (var point in points)
            {
                double hit = point.Time + CasualMode.GoodWindowMs(point.Time) * .8 / 1000;
                judge.Present(point.Time); judge.Press(point.Pad, hit); judge.Release(point.Pad, hit);
            }
            var result = service.Complete(session);
            Assert.AreEqual(41, result.Good); Assert.AreEqual(0, result.Great);
            Assert.IsTrue(result.FullCombo); Assert.IsFalse(result.AllPerfect); Assert.IsTrue(result.Cleared);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class RaceTests
    {
        [Test]
        public void RabbitTurtleIsTheNinthMenuGame()
        {
            Assert.AreEqual("race", MiniGamesMenu.Games[8].Id);
            Assert.AreEqual("RabbitTurtle", MiniGamesMenu.Games[8].Scene);
        }

        [Test]
        public void TheWalkIsOnlySandDAlternatingOnTwoBeats()
        {
            List<BeatCue> cues = RaceChart.Generate();
            Assert.AreEqual(cues.Count, RaceChart.Generate().Count);
            for (int i = 0; i < cues.Count; i++)
            {
                BeatCue cue = cues[i];
                Assert.GreaterOrEqual(cue.Time, RaceChart.FirstCue);
                Assert.Less(cue.Time, RaceChart.WakeAt);
                Assert.AreEqual(CueKind.Normal, cue.Kind);
                Assert.AreEqual(i % 2 == 0 ? RaceChart.LeftPad : RaceChart.RightPad, cue.Pad);
                AssertWindows(cue);
                if (i > 0) Assert.AreEqual(RaceChart.CruiseGap, cue.Time - cues[i - 1].Time, 1e-6);
            }
        }

        [Test]
        public void TheSprintIsThreeSecondsOfZAtDoubleSpeed()
        {
            const double now = 20;
            List<BeatCue> cues = RaceChart.Sprint(now);
            Assert.AreEqual(RaceChart.CruiseGap, RaceChart.SprintGap * 2, 1e-9);
            Assert.AreEqual(RaceChart.SprintSeconds, RaceChart.SprintCues * RaceChart.SprintGap, 1e-6);
            Assert.AreEqual(RaceChart.SprintCues, cues.Count);
            for (int i = 0; i < cues.Count; i++)
            {
                Assert.AreEqual(RaceChart.SprintPad, cues[i].Pad);
                Assert.AreEqual(now + RaceChart.SprintGap * (i + 1), cues[i].Time, 1e-6);
                Assert.GreaterOrEqual(RaceChart.SprintGap, RaceChart.BeatSeconds * 0.5 - 1e-9);
                AssertWindows(cues[i]);
            }
            Assert.AreEqual(now + RaceChart.SprintSeconds, cues[cues.Count - 1].Time, 1e-6);
        }

        [Test]
        public void SandDStopAtTheSprintLineAndOnlyZReachesTheFinish()
        {
            Assert.Less(RaceChart.SprintLine, RaceChart.FinishSteps);
            Assert.Greater(RaceChart.SprintLine, RaceChart.NapSteps, "The turtle is past the sleeping rabbit when the sprint starts.");

            // Every walking step is perfect, so the walk alone would overshoot. It stops at the line.
            int walked = 0;
            double reached = -1;
            foreach (BeatCue cue in RaceChart.Generate())
            {
                walked = System.Math.Min(RaceChart.SprintLine, walked + RaceChart.Steps(HitGrade.Perfect, false));
                if (walked >= RaceChart.SprintLine) { reached = cue.Time; break; }
            }
            Assert.Greater(reached, RaceChart.NapAt);
            Assert.Less(reached, RaceChart.WakeAt);

            int left = RaceChart.FinishSteps - RaceChart.SprintLine;
            Assert.AreEqual(left, RaceChart.SprintCues * RaceChart.Steps(HitGrade.Good, true),
                "An all-good Z roll uses the whole 3 seconds.");
            Assert.Less(RaceChart.SprintSeconds + 0.17, RaceChart.ChaseSeconds, "Even a late good last Z beats the rabbit.");
        }

        [Test]
        public void StageTwoRepeatsTheWalkAfterTheTurtleReachesTheLine()
        {
            const double now = 15;
            List<BeatCue> stage = RaceChart.FromNow(now);
            List<BeatCue> first = RaceChart.Generate();
            Assert.AreEqual(first.Count, stage.Count);
            double origin = now - RaceChart.GoAt + RaceChart.BeatSeconds;
            for (int i = 0; i < stage.Count; i++)
            {
                Assert.Greater(stage[i].Time, now);
                Assert.AreEqual(first[i].Pad, stage[i].Pad);
                Assert.AreEqual(first[i].Time + origin, stage[i].Time, 1e-6);
                AssertWindows(stage[i]);
            }
        }

        [Test]
        public void TheRabbitLeadsSleepsAndChasesOnlyOnceTheSprintStarts()
        {
            double never = double.PositiveInfinity;
            Assert.AreEqual(0, RaceChart.RabbitSteps(0, never), 1e-9);
            Assert.AreEqual(0, RaceChart.RabbitSteps(RaceChart.GoAt, never), 1e-9);
            double halfway = (RaceChart.GoAt + RaceChart.NapAt) / 2;
            Assert.AreEqual(RaceChart.NapSteps / 2.0, RaceChart.RabbitSteps(halfway, never), 1e-6);
            Assert.AreEqual(RaceChart.NapSteps, RaceChart.RabbitSteps(RaceChart.NapAt, never), 1e-6);
            Assert.AreEqual(RaceChart.NapSteps, RaceChart.RabbitSteps(60, never), 1e-6);

            const double sprint = 14;
            Assert.AreEqual(RaceChart.NapSteps, RaceChart.RabbitSteps(sprint, sprint), 1e-6);
            Assert.Greater(RaceChart.RabbitSteps(sprint + 1, sprint), RaceChart.NapSteps);
            Assert.Less(RaceChart.RabbitSteps(sprint + RaceChart.SprintSeconds, sprint), RaceChart.FinishSteps);
            Assert.AreEqual(RaceChart.FinishSteps, RaceChart.RabbitSteps(sprint + RaceChart.ChaseSeconds, sprint), 1e-6);
        }

        [Test]
        public void StepsDoubleInTheSprintAndWinsUnlockTheShellAndTheCourse()
        {
            Assert.AreEqual(2, RaceChart.Steps(HitGrade.Perfect, false));
            Assert.AreEqual(1, RaceChart.Steps(HitGrade.Good, false));
            Assert.AreEqual(4, RaceChart.Steps(HitGrade.Perfect, true));
            Assert.AreEqual(2, RaceChart.Steps(HitGrade.Good, true));
            Assert.AreEqual(0, RaceChart.Steps(HitGrade.Miss, true));
            Assert.AreEqual(0, RaceChart.Course(0));
            Assert.AreEqual(1, RaceChart.Course(1));
            Assert.AreEqual(2, RaceChart.Course(2));
            Assert.AreEqual(3, RaceChart.Course(3));
            Assert.AreEqual(3, RaceChart.Course(8));
            Assert.AreEqual(RaceChart.Course(2), RaceChart.Shell(2));
            Assert.IsFalse(RaceChart.CloseLoss(RaceChart.CloseSteps - 1));
            Assert.IsTrue(RaceChart.CloseLoss(RaceChart.CloseSteps));
            Assert.IsTrue(RaceChart.CloseLoss(RaceChart.FinishSteps - 1));
            Assert.IsFalse(RaceChart.CloseLoss(RaceChart.FinishSteps));
        }

        private static void AssertWindows(BeatCue cue)
        {
            Stage stage = StageTable.At(cue.Time);
            Assert.AreEqual(stage.GoodMs, cue.GoodMs);
            Assert.AreEqual(stage.PerfectMs, cue.PerfectMs);
        }
    }
}

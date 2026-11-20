using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class EchoTests
    {
        [Test]
        public void TappingBeatIsTheEighthMenuGame()
        {
            Assert.AreEqual("echo", MiniGamesMenu.Games[7].Id);
            Assert.AreEqual("TappingBeat", MiniGamesMenu.Games[7].Scene);
        }

        // Stage 1: seven rounds from two beats. Stage 2: five from four. Stage 3: five from five.
        [Test]
        public void ThreeStagesEachOpenMorePadsAndTheirOwnRounds()
        {
            Assert.AreEqual(3, EchoChart.Count);
            Assert.AreEqual(7, EchoChart.MaxFails);

            EchoChart.EchoStage one = EchoChart.Get(1);
            Assert.AreEqual(2, one.StartLength);
            Assert.AreEqual(7, one.Rounds);
            Assert.AreEqual(8, one.EndLength);
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 7 }, one.Pads);

            EchoChart.EchoStage two = EchoChart.Get(2);
            Assert.AreEqual(4, two.StartLength);
            Assert.AreEqual(5, two.Rounds);
            Assert.AreEqual(8, two.EndLength);
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 7 }, two.Pads);

            EchoChart.EchoStage three = EchoChart.Get(3);
            Assert.AreEqual(5, three.StartLength);
            Assert.AreEqual(5, three.Rounds);
            Assert.AreEqual(9, three.EndLength);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 7 }, three.Pads);
        }

        [Test]
        public void EachRoundDealsANewPhraseInsideThatStagesPads()
        {
            for (int stage = 1; stage <= EchoChart.Count; stage++)
            {
                EchoChart.EchoStage step = EchoChart.Get(stage);
                int[] opening = EchoChart.Phrase(4, stage, step.StartLength, 0);
                CollectionAssert.AreEqual(opening, EchoChart.Phrase(4, stage, step.StartLength, 0));
                int[] next = EchoChart.Phrase(4, stage, step.StartLength + 1, 1);
                Assert.AreEqual(step.StartLength + 1, next.Length);
                bool samePrefix = true;
                for (int i = 0; i < opening.Length; i++) samePrefix &= opening[i] == next[i];
                Assert.IsFalse(samePrefix, "stage " + stage + " repeated the last phrase");
                foreach (int pad in opening) Assert.Contains(pad, step.Pads);
                foreach (int pad in next) Assert.Contains(pad, step.Pads);
            }
            CollectionAssert.DoesNotContain(EchoChart.AllPads, 6);
        }

        // The rival plays the whole phrase first, so no answer counts while it is still playing.
        [Test]
        public void TheAnswerIsRefusedUntilTheWholePhraseHasBeenHeard()
        {
            int[] notes = EchoChart.Phrase(9, 1, 2, 0);
            var memory = new EchoMemory(notes, 2);
            bool grown;
            Assert.IsTrue(memory.Listening);
            Assert.AreEqual(notes[0], memory.Hear());
            Assert.IsTrue(memory.Listening);
            Assert.IsFalse(memory.Answer(notes[0], out grown));
            Assert.AreEqual(notes[1], memory.Hear());
            Assert.IsFalse(memory.Listening);
            Assert.IsTrue(memory.Answer(notes[0], out grown));
        }

        [Test]
        public void ACorrectEchoGrowsByOneBeatAndAMissKeepsTheLength()
        {
            int[] notes = EchoChart.Phrase(9, 1, 2, 0);
            var memory = new EchoMemory(notes, 2);
            bool grown;
            while (memory.Listening) memory.Hear();
            Assert.IsTrue(memory.Answer(notes[0], out grown));
            Assert.IsFalse(grown);
            Assert.IsTrue(memory.Answer(notes[1], out grown));
            Assert.IsTrue(grown);
            Assert.AreEqual(2, memory.Length);
            Assert.AreEqual(2, memory.Best);

            int[] next = EchoChart.Phrase(9, 1, 3, 1);
            memory.Use(next);
            Assert.AreEqual(3, memory.Length);
            while (memory.Listening) memory.Hear();
            Assert.IsFalse(memory.Answer(Other(next[0]), out grown));
            memory.Fail();
            Assert.AreEqual(3, memory.Length);
            Assert.AreEqual(2, memory.Best);
            Assert.IsTrue(memory.Listening);
            Assert.AreEqual(next[0], memory.Hear());
        }

        // Every round of a stage answered takes the phrase to that stage's longest.
        [Test]
        public void EveryStageRunsItsRoundsUpToItsLongestPhrase()
        {
            for (int stage = 1; stage <= EchoChart.Count; stage++)
            {
                EchoChart.EchoStage step = EchoChart.Get(stage);
                var memory = new EchoMemory(EchoChart.Phrase(5, stage, step.StartLength, 0), step.StartLength);
                bool grown = false;
                for (int round = 1; round <= step.Rounds; round++)
                {
                    int[] notes = EchoChart.Phrase(5, stage, step.StartLength + round - 1, round - 1);
                    if (round > 1) memory.Use(notes);
                    while (memory.Listening) memory.Hear();
                    for (int i = 0; i < notes.Length; i++)
                        Assert.IsTrue(memory.Answer(notes[i], out grown), "stage " + stage + " round " + round);
                    Assert.IsTrue(grown);
                    Assert.AreEqual(notes.Length, memory.Best);
                }
                Assert.AreEqual(step.EndLength, memory.Best);
            }
        }

        private static int Other(int pad) { return pad == 3 ? 4 : 3; }
    }
}

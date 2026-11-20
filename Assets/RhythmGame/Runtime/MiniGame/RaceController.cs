using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 9. Alternate S and D and the turtle walks to the sprint line. Perfect is two steps. The rabbit naps.
    // At the line the last 3 seconds become a Z roll at double speed, and the rabbit wakes up and chases.
    public sealed class RaceController : MiniGameController
    {
        [SerializeField] private RaceView view;
        [SerializeField] private AudioSource snore;
        [SerializeField] private AudioClip foot, tom, snare, wake, whistle, trumpet, nyah;
        private int steps, course, raceStage = 1;
        private double clockOrigin, sprintAt = double.PositiveInfinity;
        private bool won, closeLoss, ended, asleep, sprinted;

        public override string GameId { get { return "race"; } }
        protected override string TitleKey { get { return "race.title"; } }
        protected override string[] PromptKeys { get { return new[] { "race.prompt_what", "race.prompt_when", "race.prompt_twist" }; } }
        protected override string GameOverKey { get { return won ? "race.win" : closeLoss ? "race.close" : "race.lose"; } }
        protected override double Bpm { get { return RaceChart.Bpm; } }
        protected override bool PlaysJudgementAudio { get { return false; } }
        protected override bool DeferGameOver { get { return true; } }
        protected override double LeadSeconds { get { return RaceChart.CruiseGap * 0.5; } }
        protected override int[] PadsFor(int stage) { return RaceChart.Pads; }
        protected override int RecordedStage { get { return raceStage; } }
        protected override int DisplayStage { get { return raceStage; } }
        // S and D walk; in the sprint only Z counts, and before it Z does nothing.
        protected override bool AcceptsPad(int pad)
        {
            if (sprinted) return pad == RaceChart.SprintPad;
            return pad == RaceChart.LeftPad || pad == RaceChart.RightPad;
        }

        protected override List<BeatCue> Generate(int seed)
        {
            steps = 0;
            raceStage = 1;
            clockOrigin = 0;
            sprintAt = double.PositiveInfinity;
            won = false;
            closeLoss = false;
            ended = false;
            asleep = false;
            sprinted = false;
            course = 0;
            if (view != null)
            {
                view.ShowOpening();
                view.CourseName(Strings.T(CourseKey(course)));
                view.ResetTrack();
            }
            if (snore != null) snore.Stop();
            return RaceChart.Generate();
        }

        protected override void Awake()
        {
            base.Awake();
            if (view != null) view.Bind(this);
        }

        protected override void Update()
        {
            base.Update();
            if (IsPaused || (State != MiniGameState.Playing && !ended)) return;
            Tick();
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            if (view != null && cue != null) view.Preview(cue.Pad, secondsToBeat);
        }

        protected override void OnAcceptedPad(int pad)
        {
            Play(pad == RaceChart.SprintPad ? snare : tom);
            if (view != null) view.Strike(pad);
        }

        protected override void ShowOutcome(CueResult result)
        {
            if (ended || result == null) return;
            if (result.Outcome == CueOutcome.Hit)
            {
                bool sprint = sprinted && result.Cue != null && result.Cue.Pad == RaceChart.SprintPad;
                steps += RaceChart.Steps(result.Grade, sprint);
                int cap = sprinted ? RaceChart.FinishSteps : RaceChart.SprintLine;
                if (steps > cap) steps = cap;
                Play(foot);
                if (view != null)
                {
                    view.Step();
                    view.ShowSteps(steps);
                    if (result.Cue != null) view.Hit(result.Cue.Pad);
                }
            }
            else if (result.Outcome == CueOutcome.Missed && view != null)
                view.Stumble();
            Tick();
        }

        protected override void ClearField()
        {
            if (snore != null) snore.Stop();
            if (view != null) view.ResetTrack();
        }

        protected override void OnRoundFinished(bool cleared)
        {
            string line = Strings.T("race.steps") + steps + " / " + RaceChart.FinishSteps
                + "   " + Strings.T(CourseKey(course));
            if (won)
            {
                int wins = GameManager.EnsureInstance().Data.RememberRaceWin();
                if (wins >= 1 && wins <= 3)
                    line += "   " + Strings.T("race.unlocked") + Strings.T(CourseKey(wins));
            }
            AppendResult(line);
        }

        private void Tick()
        {
            double time = RaceTime();
            float rabbit = (float)RaceChart.RabbitSteps(time, sprintAt);
            if (view != null) view.Place(steps, rabbit);
            if (ended || State != MiniGameState.Playing) return;
            if (!asleep && !sprinted && time >= RaceChart.NapAt)
            {
                asleep = true;
                if (view != null) view.Sleep(true);
                if (snore != null && !snore.isPlaying) snore.Play();
            }
            if (!sprinted && (steps >= RaceChart.SprintLine || time >= RaceChart.WakeAt))
            {
                BeginSprint(time);
                return;
            }
            if (steps >= RaceChart.FinishSteps)
            {
                if (raceStage < 2) { EnterStageTwo(); return; }
                Win();
                return;
            }
            if (rabbit >= RaceChart.FinishSteps - 0.001f || (Score != null && Score.IsOver)) Lose();
        }

        // The turtle is at the sprint line, or the rabbit woke by itself. From here only Z moves the turtle.
        private void BeginSprint(double time)
        {
            sprinted = true;
            asleep = false;
            sprintAt = time;
            if (snore != null) snore.Stop();
            if (view != null)
            {
                view.Wake(Strings.T("race.wake"));
                view.ShowSprint();
            }
            Play(wake);
            Play(whistle);
            ReplaceChart(RaceChart.Sprint(SongTime));
        }

        // The turtle has crossed the line. Stage 1 is over; both runners go back for the purple course.
        private void EnterStageTwo()
        {
            raceStage = 2;
            steps = 0;
            asleep = false;
            sprinted = false;
            sprintAt = double.PositiveInfinity;
            clockOrigin = SongTime - RaceChart.GoAt + RaceChart.BeatSeconds;
            if (snore != null) snore.Stop();
            if (view != null)
            {
                view.Sleep(false);
                view.BeginStageTwo();
                view.ShowSteps(0);
                view.Place(0f, 0f);
            }
            Play(whistle);
            ReplaceChart(RaceChart.FromNow(SongTime));
        }

        private double RaceTime()
        {
            return SongTime - clockOrigin;
        }

        private void Win()
        {
            if (ended) return;
            ended = true;
            won = true;
            if (snore != null) snore.Stop();
            Play(trumpet);
            if (view != null) view.Cheer();
            StartCoroutine(EndSoon());
        }

        private void Lose()
        {
            if (ended) return;
            ended = true;
            won = false;
            closeLoss = RaceChart.CloseLoss(steps);
            if (snore != null) snore.Stop();
            if (closeLoss)
            {
                if (view != null) view.Tease(Strings.T("race.tease"));
                Play(nyah);
            }
            StartCoroutine(EndSoon());
        }

        // Shorter than the sprint gap, so the next cue does not expire under the sting.
        private IEnumerator EndSoon()
        {
            yield return new WaitForSeconds(0.5f);
            if (State == MiniGameState.Playing) FinishRound();
        }

        private static string CourseKey(int which)
        {
            if (which <= 0) return "race.course0";
            if (which == 1) return "race.course1";
            if (which == 2) return "race.course2";
            return "race.course3";
        }
    }
}

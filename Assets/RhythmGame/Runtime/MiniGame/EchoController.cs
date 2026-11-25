using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmGame
{
    // Game 8. The rival plays a whole phrase, the player memorises it and answers after the "huh".
    // Three stages: 7 rounds from 2 beats on S D Z C, then 5 rounds from 4 beats with A, then
    // 5 rounds from 5 beats with the cymbals Q and E. Each miss costs half a heart, and none left ends the run.
    public sealed class EchoController : MiniGameController
    {
        private enum Phase { Wait, Listen, Answer, React }

        private static readonly Key[] Keys = { Key.Q, Key.E, Key.A, Key.S, Key.D, Key.Z, Key.X, Key.C };
        [SerializeField] private EchoView view;
        [SerializeField] private AudioClip tomA, tomS, tomD, tomZ, tomC, cymbalQ, cymbalE, huhSound, laughSound;
        private EchoMemory memory;
        private Phase phase = Phase.Wait;
        private double nextTime, expect;
        private int seed, step, best, rounds, draw;
        private bool failed, advancing;
        private Coroutine ending;

        private EchoChart.EchoStage Step { get { return EchoChart.Stages[step]; } }
        private bool LastStage { get { return step >= EchoChart.Count - 1; } }
        private bool StageDone { get { return rounds >= Step.Rounds; } }

        public override string GameId { get { return "echo"; } }
        protected override string TitleKey { get { return "echo.title"; } }
        protected override string[] PromptKeys { get { return new[] { "echo.prompt_what", "echo.prompt_when", "echo.prompt_twist" }; } }
        // Every round of every stage echoed is a win, though the run ends long before the clear time.
        protected override string GameOverKey { get { return LastStage && StageDone ? "echo.done" : "echo.lose"; } }
        protected override double Bpm { get { return EchoChart.Bpm; } }
        protected override bool PlaysJudgementAudio { get { return false; } }
        protected override bool DeferGameOver { get { return true; } }
        protected override int[] PadsFor(int stage) { return EchoChart.PadsIn(stage); }
        protected override int RecordedStage { get { return Step.Number; } }

        protected override List<BeatCue> Generate(int seed)
        {
            this.seed = seed;
            step = 0;
            best = 0;
            StartStage();
            phase = Phase.Wait;
            nextTime = 2;
            Stage last = StageTable.Get(StageTable.Count);
            return new List<BeatCue>
            {
                new BeatCue
                {
                    Time = StageTable.ClearSeconds - 1.5,
                    Pad = 0,
                    Kind = CueKind.Forbidden,
                    PerfectMs = last.PerfectMs,
                    GoodMs = last.GoodMs
                }
            };
        }

        // A stage brings its own pads, its own opening phrase and its own round count.
        private void StartStage()
        {
            draw = 0;
            memory = new EchoMemory(EchoChart.Phrase(seed, Step.Number, Step.StartLength, draw), Step.StartLength);
            rounds = 0;
            failed = false;
            advancing = false;
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureSounds();
            if (view != null) view.Bind(this);
        }

        protected override void Update()
        {
            if (IsPaused || State != MiniGameState.Playing || memory == null) return;
            AdvanceTo(SongTime);
            if (State != MiniGameState.Playing) return;
            FadeBanners((int)System.Math.Floor(SongTime / BeatSeconds));
            ReadKeys();
            Drive(SongTime);
        }

        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Drum(pad) : null; }

        protected override void PresentCue(BeatCue cue, float secondsToBeat) { }

        protected override void ShowOutcome(CueResult result)
        {
            if (view == null || result == null || result.Outcome != CueOutcome.Missed) return;
            view.Laugh();
            Play(laughSound);
        }

        protected override void ClearField()
        {
            if (ending != null) StopCoroutine(ending);
            ending = null;
            if (view != null) view.Clear();
        }

        protected override void OnRoundFinished(bool cleared)
        {
            AppendResult(Strings.T("echo.record") + best + " " + Strings.T("echo.beat_unit"));
            RhythmGameData data = GameManager.EnsureInstance().Data;
            data.RememberEchoBeats(best);
            data.RememberEchoStage(Step.Number);
        }

        // A drum on the screen. Keys come through Update.
        public void Echo(int pad)
        {
            if (State != MiniGameState.Playing) return;
            // X has no drum in this kit, and a tap while the rival is playing is not an answer.
            if (phase != Phase.Answer || System.Array.IndexOf(EchoChart.AllPads, pad) < 0)
            {
                FlashPad(pad, PadFlashPool.Kind.Miss);
                return;
            }
            Resolve(pad, SongTime);
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (int pad = 0; pad < Keys.Length; pad++)
                if (keyboard[Keys[pad]].wasPressedThisFrame) { NotePadInput(); Echo(pad); }
        }

        private void Drive(double time)
        {
            if (time < nextTime) return;
            if (phase == Phase.Wait)
            {
                BeginListen();
                return;
            }
            if (phase == Phase.Listen)
            {
                if (memory.Listening)
                {
                    int pad = memory.Hear();
                    if (view != null) view.Strike(pad);
                    Play(Clip(pad));
                    nextTime = time + Gap;
                }
                else
                {
                    if (view != null) view.Huh();
                    Show(0);
                    Play(huhSound);
                    phase = Phase.Answer;
                    expect = time + Gap;
                    nextTime = expect + Patience;
                }
                return;
            }
            if (phase == Phase.Answer)
            {
                Fail();
                return;
            }
            if (advancing)
            {
                step++;
                StartStage();
            }
            else if (failed)
            {
                failed = false;
                memory.Fail();
            }
            else
                memory.Use(EchoChart.Phrase(seed, Step.Number, System.Math.Min(memory.Length + 1, Step.EndLength), ++draw));
            BeginListen();
        }

        // The phrase is the answer, so only a wrong pad or a silence fails. Timing only grades the hit.
        private void Resolve(int pad, double time)
        {
            bool grown;
            if (!memory.Answer(pad, out grown)) { FlashPad(pad, PadFlashPool.Kind.Miss); Fail(); return; }
            if (view != null) view.Strike(pad);
            Play(Clip(pad));
            double offset = time - expect;
            Report(new CueResult
            {
                Cue = new BeatCue { Pad = pad, PerfectMs = 120, GoodMs = 240 },
                Pad = pad,
                Outcome = CueOutcome.Hit,
                Grade = System.Math.Abs(offset) <= .12 ? HitGrade.Perfect : HitGrade.Good,
                OffsetMs = offset * 1000
            });
            if (!grown)
            {
                expect = System.Math.Max(expect + Gap, time + Gap * .5);
                nextTime = expect + Patience;
                Show(memory.Cursor);
                return;
            }
            rounds++;
            best = System.Math.Max(best, memory.Best);
            if (view != null) view.Fluster();
            // The phrase that was just answered, not the longer one the next round will bring.
            Show(memory.Best, memory.Best);
            phase = Phase.React;
            failed = false;
            nextTime = time + .7;
            if (!StageDone) return;
            // The stage is cleared. The next one opens its pads; past the last, its longest phrases keep coming
            // until the 8 minutes are up.
            if (LastStage) return;
            advancing = true;
            nextTime = time + 1.9;
            if (view != null) view.Announce(Step.Number + 1);
        }

        // A wrong pad or a silence costs half a heart. The same phrase is replayed until none remain.
        private void Fail()
        {
            phase = Phase.React;
            failed = true;
            nextTime = SongTime + .9;
            Report(new CueResult { Outcome = CueOutcome.Missed, Grade = HitGrade.Miss });
            Show(0);
            if (Score != null && Score.IsOver) Stop(.9f);
        }

        // No further phrase starts; the laugh or the fluster plays out and the run ends.
        private void Stop(float seconds)
        {
            nextTime = double.MaxValue;
            if (ending == null) ending = StartCoroutine(EndSoon(seconds));
        }

        private void Show(int answered) { Show(answered, memory.Length); }

        private void Show(int answered, int length)
        {
            if (view == null) return;
            view.Phrase(Step.Number, rounds, Step.Rounds, answered, length, Score == null ? MiniGameScore.MaxHalves : Score.Halves);
        }

        private void BeginListen()
        {
            phase = Phase.Listen;
            if (view != null)
            {
                view.Pride(Step.Number, rounds);
                view.Open(Step.Pads);
                view.Listen();
            }
            Show(0);
            nextTime = SongTime;
        }

        private IEnumerator EndSoon(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ending = null;
            if (State == MiniGameState.Playing) FinishRound();
        }

        private double Gap { get { return BeatSeconds * (memory.Length >= 9 ? .8 : memory.Length >= 5 ? .9 : 1); } }
        // How long a beat of the answer may be late before the rival laughs: one beat of thinking room.
        private double Patience { get { return Gap + .45; } }

        private AudioClip Clip(int pad)
        {
            switch (pad)
            {
                case 0: return cymbalQ;
                case 1: return cymbalE;
                case 2: return tomA;
                case 3: return tomS;
                case 4: return tomD;
                case 5: return tomZ;
                default: return tomC;
            }
        }

        private void EnsureSounds()
        {
            if (tomA == null) tomA = Resources.Load<AudioClip>("RhythmGame/Audio/EchoTomA");
            if (tomS == null) tomS = Resources.Load<AudioClip>("RhythmGame/Audio/EchoTomS");
            if (tomD == null) tomD = Resources.Load<AudioClip>("RhythmGame/Audio/EchoTomD");
            if (tomZ == null) tomZ = Resources.Load<AudioClip>("RhythmGame/Audio/EchoTomZ");
            if (tomC == null) tomC = Resources.Load<AudioClip>("RhythmGame/Audio/EchoTomC");
            if (cymbalQ == null) cymbalQ = Resources.Load<AudioClip>("RhythmGame/Audio/EchoCymbalQ");
            if (cymbalE == null) cymbalE = Resources.Load<AudioClip>("RhythmGame/Audio/EchoCymbalE");
            if (huhSound == null) huhSound = Resources.Load<AudioClip>("RhythmGame/Audio/EchoHuh");
            if (laughSound == null) laughSound = Resources.Load<AudioClip>("RhythmGame/Audio/EchoLaugh");
        }
    }
}

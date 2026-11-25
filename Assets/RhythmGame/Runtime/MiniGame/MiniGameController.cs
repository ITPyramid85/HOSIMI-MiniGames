using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    public enum MiniGameState { Ready, Playing, Result }

    // Shared flow for every mini-game: three-line start prompt (plan §3) -> 3-2-1 -> an endless run on
    // the music clock that climbs stages 1..10 (client reply v5 §1) until the lives run out or stage 10
    // is cleared -> result showing time, stage and drumsticks, with "play again" as the biggest button.
    // Judging, scoring, lives, result sounds, combo, records and calibration live here; a game supplies
    // its chart rule (Generate), prompt text and what its field shows (PresentCue / ShowOutcome).
    // The app plays only result sounds; strike sounds come from the drum pads.
    public abstract class MiniGameController : MonoBehaviour
    {
        // Pad i on the keyboard: the physical layout's two top corners, middle row, bottom row.
        private static readonly Key[] PadKeys = { Key.Q, Key.E, Key.A, Key.S, Key.D, Key.Z, Key.X, Key.C };
        [SerializeField] protected AudioSource music, effects;
        [SerializeField] private AudioClip hitSound, bonusSound, forbiddenSound, missSound, comboSound;
        [SerializeField] private Text scoreText, comboText, judgementText, stageText, timerText, countdownText;
        [SerializeField] private Image stageFill;
        [SerializeField] private Image[] lifeIcons;
        [SerializeField] private GameObject modal;
        [SerializeField] private Text modalTitle, modalBody;
        [SerializeField] private Button primaryButton, menuButton;
        private Button completeButton;
        private bool confirming;
        private bool finishAfterResume;
        private double pausedSong, pausedAtDsp;
        private readonly List<AudioSource> heldAudio = new List<AudioSource>();
        [SerializeField] private Image[] padDiagram;
        private PopupTransition modalTransition;
        private AudioLatencyMonitor latency;
        private List<BeatCue> cues;
        protected List<BeatCue> Chart { get { return cues; } }
        private double anchor, offsetSeconds, played;
        private int nextCue, stageShown;
        private float judgementLife, bannerLife;
        private Coroutine comboPunch, musicRetry;
        private PadFlashPool flashes;
        private DrumstickHud sticks;
        private long shownRun;
        private bool screenTouch, padInput, sticksHeld;
        // Drumsticks follow accurate hits only. A miss, an empty strike or a wrong note closes the clock.
        private double credited, creditFrom;
        private bool crediting = true;
        private int stageWhileMissing = 1;
        // Same places as the prompt's pad diagram, in the 1280x720 canvas. A game can put the flash
        // on its own target instead.
        private static readonly Vector2[] PadSpot = {
            new Vector2(-400f, 140f), new Vector2(400f, 140f),
            new Vector2(-216f, -16f), new Vector2(0f, -16f), new Vector2(216f, -16f),
            new Vector2(-216f, -196f), new Vector2(0f, -196f), new Vector2(216f, -196f) };
        public MiniGameState State { get; private set; }
        public BeatJudge Judge { get; private set; }
        public MiniGameScore Score { get; private set; }
        public MiniGameResult Result { get; private set; }
        public Stage Stage { get { return StageTable.At(played); } }
        public double PlayedSeconds { get { return played; } }
        public double SongTime { get { return confirming ? pausedSong : AudioSettings.dspTime - anchor; } }
        // True while the exit question is up. Play clocks and input stay where they were.
        protected bool IsPaused { get { return confirming; } }

        // ---- What each game supplies ----
        public abstract string GameId { get; }            // key for records
        protected abstract string TitleKey { get; }
        protected abstract string[] PromptKeys { get; }   // what to do / when / the twist (plan §3: three lines)
        protected abstract string GameOverKey { get; }    // result title when the lives run out
        protected abstract int[] PadsFor(int stage);      // lit in the prompt's pad diagram (stage 1)
        // Stage named on the result. Tapping Beat names its own stage; the clock stage still pays the reward.
        protected virtual int RecordedStage { get { return Stage.Number; } }
        // Stage printed on the HUD. The tortoise race calls its finish sprint stage 2.
        protected virtual int DisplayStage { get { return Stage.Number; } }
        // Fill of the stage bar. A game with its own ladder overrides this; rewards still use Stage.
        protected virtual double StageProgress { get { return StageTable.ProgressIn(played); } }
        // Surviving to this time opens the clear dialog. Every game is the shared 8-minute run.
        protected virtual double ClearAtSeconds { get { return StageTable.ClearSeconds; } }
        protected virtual int RewardStage { get { return Stage.Number; } }
        protected abstract List<BeatCue> Generate(int seed);
        // A cue's lead-in has started: it reaches its beat in secondsToBeat (on the heard clock).
        protected abstract void PresentCue(BeatCue cue, float secondsToBeat);
        // Field reaction to a judged cue or empty strike; score, judgement text and sounds are handled here.
        protected abstract void ShowOutcome(CueResult result);
        protected virtual void ClearField() { }
        protected virtual void OnRoundFinished(bool cleared) { }
        // Extra result line. Read before ClearField, so a game can still see its own counters.
        protected virtual string ResultExtra { get { return ""; } }
        // Nail driving waits until the head meets the nail, so the hit and the miss share that moment.
        protected virtual bool PlaysJudgementAudio { get { return true; } }
        // A game can finish the fly-off before the result modal replaces the field.
        protected virtual bool DeferGameOver { get { return false; } }
        protected AudioClip StrikeClip { get { return hitSound; } }
        protected AudioClip MissClip { get { return missSound; } }
        protected virtual double Bpm { get { return 120; } }
        protected virtual double LeadSeconds { get { return BeatSeconds / 2; } }
        // How far ahead of a cue its field animation starts. The opening flip can wait out the listen beats.
        protected virtual double LeadFor(BeatCue cue) { return LeadSeconds; }
        // False during a listen-only count-in, so an early tap is neither a miss nor an empty strike.
        protected virtual bool ScoresAt(double songTime) { return true; }
        // Pads outside this set are ignored entirely (plan §3: other pads are neither judged nor penalised).
        protected virtual bool AcceptsPad(int pad) { return true; }
        // Space / gamepad south also strikes this pad, for one-pad games; -1 for none.
        protected virtual int DefaultPad { get { return -1; } }
        public double BeatSeconds { get { return 60 / Bpm; } }

        protected virtual void Awake()
        {
            GameManager.EnsureInstance();
            latency = AudioLatencyMonitor.EnsureInstance();
            AudioChannel.Bind(music, AudioKind.Music);
            AudioChannel.Bind(effects, AudioKind.Effects);
            music.loop = true;   // a run has no time limit; the track's length is a whole number of beats
            music.spatialBlend = 0f;
            if (music.clip != null) music.clip.LoadAudioData();
            modalTransition = PopupTransition.For(modal);
            primaryButton.onClick.AddListener(PrimaryAction);
            menuButton.onClick.AddListener(BackToMenu);
            EnsureCompleteButton();
            if (stageText != null) stageText.gameObject.SetActive(false);
            ShowPrompt();
            GetComponent<MenuTypography>().Apply();
            flashes = PadFlashPool.Create(transform);
            BuildStickHud();
        }

        // Lifetime total at the top. "+n" rises from the score into it while a pad round is paying.
        private void BuildStickHud()
        {
            if (flashes == null) return;
            if (scoreText != null) flashes.SetFont(scoreText.font);
            sticks = DrumstickHud.Create(flashes, scoreText != null ? scoreText.font : null);
            sticks.FollowScore(scoreText != null ? scoreText.rectTransform : null);
            sticks.Awards(false);
            sticks.Reset(Bank());
        }

        private static long Bank()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || !manager.IsReady || manager.Data == null || manager.Data.Player == null) return 0;
            return manager.Data.Player.coins;
        }

        // A connected drum, or the keyboard and gamepad that stand in for it, pays. A touch-only round does not.
        private bool PaysSticks
        {
            get
            {
                bool drum = DrumInput.Instance != null && DrumInput.Instance.IsConnected;
                if (drum || padInput) return true;
                return !screenTouch;
            }
        }

        // Pointer taps on the field. Touch-only play earns nothing and shows no "+n".
        public void NoteScreenTouch()
        {
            if (State == MiniGameState.Playing) screenTouch = true;
        }

        protected void NotePadInput()
        {
            if (State == MiniGameState.Playing) padInput = true;
        }

        private void TickSticks()
        {
            if (sticks == null) return;
            if (flashes != null) flashes.transform.SetAsLastSibling();
            sticks.Awards(PaysSticks);
            if (!PaysSticks)
            {
                if (!sticksHeld)
                {
                    sticksHeld = true;
                    shownRun = 0;
                    sticks.Reset(Bank());
                }
                return;
            }
            sticksHeld = false;
            long running = EarnedSticks(false);
            long gain = running - shownRun;
            shownRun = running;
            if (gain > 0) sticks.Award(gain);
        }

        // Time up to the last accurate hit. The open tail counts only when the run is still clean.
        private double CreditedSeconds(bool includeOpen)
        {
            double now = Math.Min(Math.Max(0, played), StageTable.ClearSeconds);
            double span = credited;
            if (includeOpen && crediting) span += Math.Max(0, now - creditFrom);
            if (span < 0) span = 0;
            if (span > now) span = now;
            return span;
        }

        // Stage bonus follows accurate time, and never a stage this game does not pay.
        private int PayStage(double seconds, int cap)
        {
            int stage = StageTable.At(seconds).Number;
            if (!crediting && stageWhileMissing < cap) cap = stageWhileMissing;
            if (cap < 1) cap = 1;
            if (cap > StageTable.Count) cap = StageTable.Count;
            if (stage > cap) stage = cap;
            if (stage < 1) stage = 1;
            return stage;
        }

        private long EarnedSticks(bool includeOpen)
        {
            double seconds = CreditedSeconds(includeOpen);
            return DrumstickReward.For(seconds, PayStage(seconds, RewardStage), false);
        }

        private void ResetCredit()
        {
            credited = 0;
            creditFrom = 0;
            crediting = true;
            stageWhileMissing = 1;
        }

        // A Good or Perfect hit keeps the drumstick clock. Anything else stops it at the previous hit.
        private void NoteAccuracy(CueResult result)
        {
            double at = result.Cue != null ? result.Cue.Time : played;
            if (at < 0) at = 0;
            if (at > StageTable.ClearSeconds) at = StageTable.ClearSeconds;
            if (result.Outcome == CueOutcome.Hit)
            {
                if (crediting) credited += Math.Max(0, at - creditFrom);
                crediting = true;
                creditFrom = at;
                return;
            }
            if (result.Outcome != CueOutcome.Missed && result.Outcome != CueOutcome.EmptyHit && result.Outcome != CueOutcome.ForbiddenHit)
                return;
            if (!crediting) return;
            crediting = false;
            creditFrom = at;
            stageWhileMissing = RewardStage;
            if (stageWhileMissing < 1) stageWhileMissing = 1;
            if (stageWhileMissing > StageTable.Count) stageWhileMissing = StageTable.Count;
        }

        // Pays the sticks actually earned. Best time still uses the real clock.
        protected MiniGameResult Settle(int stage, bool cleared)
        {
            if (stage < 1) stage = 1;
            if (stage > StageTable.Count) stage = StageTable.Count;
            long amount = 0;
            if (PaysSticks)
            {
                double seconds = CreditedSeconds(true);
                amount = DrumstickReward.For(seconds, PayStage(seconds, stage), cleared);
                if (sticks != null)
                {
                    long gain = amount - shownRun;
                    shownRun = amount;
                    if (gain > 0) sticks.Award(gain);
                    if (flashes != null) flashes.transform.SetAsLastSibling();
                }
            }
            else if (sticks != null && !sticksHeld)
            {
                sticksHeld = true;
                shownRun = 0;
                sticks.Reset(Bank());
            }
            return GameManager.EnsureInstance().Data.RecordMiniGame(GameId, PlayedSeconds, stage, cleared, PaysSticks, amount);
        }

        // Where a strike on this pad should flash. Null uses the shared pad layout.
        protected virtual RectTransform FlashTarget(int pad) { return null; }

        protected void FlashPad(int pad, PadFlashPool.Kind kind)
        {
            if (flashes == null || pad < 0 || pad >= PadSpot.Length) return;
            RectTransform target = FlashTarget(pad);
            if (target != null) flashes.Play(target, kind);
            else flashes.Play(PadSpot[pad], kind);
        }

        private void ShowPrompt()
        {
            State = MiniGameState.Ready;
            played = 0;
            ResetHud();
            modalTitle.text = Strings.T(TitleKey);
            modalBody.text = string.Join("\n", Array.ConvertAll(PromptKeys, Strings.T));
            int[] pads = PadsFor(1);
            for (int i = 0; i < padDiagram.Length; i++) padDiagram[i].color = Array.IndexOf(pads, i) >= 0 ? Palette.Sun : Palette.Well;
            padDiagram[0].transform.parent.gameObject.SetActive(true);
            SetButtons(Strings.T("minigame.start"));
            modalTransition.Show();
            if (completeButton != null) completeButton.gameObject.SetActive(false);
            if (sticks != null)
            {
                sticks.Awards(false);
                sticks.Reset(Bank());
            }
            if (flashes != null) flashes.transform.SetAsLastSibling();
        }

        public void StartRound() { StartRound(Environment.TickCount); }

        public void StartRound(int seed)
        {
            if (State == MiniGameState.Playing) return;
            cues = Generate(seed);
            latency.Refresh();
            offsetSeconds = latency.EffectiveOffsetMs / 1000;
            if (Judge != null) Judge.Judged -= OnJudged;
            // Pads a game ignores never reach the judge, so every pad that does is judged (empty strikes count).
            // Each cue carries its stage's windows; these defaults are stage 1's.
            Stage first = StageTable.Get(1);
            Judge = new BeatJudge(cues, first.PerfectMs, first.GoodMs, latency.EffectiveOffsetMs, judgeAllPads: true);
            Judge.Judged += OnJudged;
            Score = new MiniGameScore();
            Result = null;
            nextCue = 0;
            played = 0;
            stageShown = 1;
            shownRun = 0;
            ResetCredit();
            screenTouch = false;
            padInput = false;
            sticksHeld = false;
            if (sticks != null)
            {
                sticks.Awards(true);
                sticks.Reset(Bank());
            }
            ClearField();
            ResetHud();
            anchor = AudioSettings.dspTime + .2;
            State = MiniGameState.Playing;
            StartMusic();
            modalTransition.Hide();
            if (completeButton != null)
            {
                completeButton.gameObject.SetActive(true);
                completeButton.transform.SetAsLastSibling();
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        protected virtual void Update()
        {
            if (confirming || State != MiniGameState.Playing) return;
            double time = SongTime;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int pad = 0; pad < PadKeys.Length; pad++)
                    if (keyboard[PadKeys[pad]].wasPressedThisFrame) { NotePadInput(); PressAt(pad, time); }
                if (DefaultPad >= 0 && keyboard.spaceKey.wasPressedThisFrame) { NotePadInput(); PressAt(DefaultPad, time); }
            }
            if (DefaultPad >= 0 && Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                NotePadInput();
                PressAt(DefaultPad, time);
            }
            AdvanceTo(time);
            if (State != MiniGameState.Playing) return;
            PresentCues(time);
            int beat = (int)Math.Floor(time / BeatSeconds);
            if (bannerLife <= 0) countdownText.text = time < 0 ? "" : beat < 3 ? (3 - beat).ToString() : beat == 3 ? "GO" : "";
            FadeBanners(beat);
        }

        // Echo answers on its own clock, so it ticks the banners without the shared pad scan.
        protected void FadeBanners(int beat)
        {
            judgementLife -= Time.unscaledDeltaTime;
            bannerLife -= Time.unscaledDeltaTime;
            if (judgementLife <= 0) judgementText.text = "";
            if (bannerLife <= 0 && beat > 3) countdownText.text = "";
        }

        // A game that judges a phrase itself still uses the shared score, lives and result.
        protected void Report(CueResult result)
        {
            OnJudged(result);
        }

        // Advances the run to a music-clock time: settles passed cues, moves the stage on, and ends the run
        // when the lives are gone or stage 10 is cleared. Update calls it every frame; tests call it directly.
        public void AdvanceTo(double songTime)
        {
            if (State != MiniGameState.Playing) 
                return;
            Judge.AdvanceTo(songTime);

            if (State != MiniGameState.Playing) 
                return;   // the last life went while settling

            played = Math.Max(played, Math.Min(songTime, ClearAtSeconds));
            RefreshProgress();

            if (played < ClearAtSeconds) return;

            // Cues a game queued past the clear time are not part of the run and must not cost a life.
            double clearAt = ClearAtSeconds;
            Judge.Abandon(cue => cue.Time >= clearAt);

            if (Judge.AllResolved) 
                FinishRound();
        }

        private void RefreshProgress()
        {
            int shown = DisplayStage;
            if (shown > stageShown)
            {
                stageShown = shown;
                countdownText.text = "STAGE " + shown;
                bannerLife = 1.2f;
                Play(comboSound);
                StartCoroutine(PixelTween.Punch(countdownText.transform, .4f, .3f));
            }

            stageFill.fillAmount = PixelTween.Steps((float)StageProgress, 40);
            timerText.text = Clock(played);
            if (State == MiniGameState.Playing) TickSticks();
        }

        public static string Clock(double seconds)
        {
            int whole = (int)Math.Floor(Math.Max(0, seconds));
            return whole / 60 + ":" + (whole % 60).ToString("00");
        }

        // Visuals follow the heard beat: with output latency the music is late, so cues are too.
        private void PresentCues(double time)
        {
            double visual = time - offsetSeconds;
            for (; nextCue < cues.Count && cues[nextCue].Time - LeadFor(cues[nextCue]) <= visual; nextCue++)
            {
                BeatCue cue = cues[nextCue];
                if (!Judge.IsResolved(cue)) PresentCue(cue, (float)(cue.Time - visual));
            }
        }

        public void Press(int pad) { PressAt(pad, SongTime); }

        // The tortoise race starts stage 2 when the turtle reaches the line, so the rest of stage 1 is dropped.
        protected void ReplaceChart(List<BeatCue> chart)
        {
            if (chart == null || chart.Count == 0) return;
            if (Judge != null) Judge.Judged -= OnJudged;
            cues = chart;
            nextCue = 0;
            Judge = new BeatJudge(chart, chart[0].PerfectMs, chart[0].GoodMs, offsetSeconds * 1000, true);
            Judge.Judged += OnJudged;
        }

        // A bent nail drops its remaining cues; the next nail's beat may have moved earlier than the cue the lead-in was waiting on.
        protected void PresentFrom(BeatCue cue)
        {
            if (cues == null || cue == null) return;
            int index = cues.IndexOf(cue);
            if (index >= 0) nextCue = index;
        }

        // Exact-time entry for tests and future pad input that carries its own timestamps.
        public void PressAt(int pad, double songTime)
        {
            if (confirming || State != MiniGameState.Playing) return;
            if (!AcceptsPad(pad) || Judge == null || !ScoresAt(songTime))
            {
                FlashPad(pad, PadFlashPool.Kind.Miss);
                return;
            }
            OnAcceptedPad(pad);
            Judge.Press(pad, songTime);
        }

        // A game can answer the press itself before the judge scores it.
        protected virtual void OnAcceptedPad(int pad) { }

        // A game may answer the judge with a different verdict: ice fishing turns a strike on the ice
        // the fish is parked on into a hit instead of an empty one.
        protected virtual CueResult Reinterpret(CueResult result) { return result; }

        private void OnJudged(CueResult result)
        {
            if (State != MiniGameState.Playing) return;   // the judge may still be settling after the last life
            CueResult reread = Reinterpret(result);
            if (reread != null) result = reread;
            // Settling can run ahead of the frame clock (tests, long frames); the run reaches the cue it judged.
            if (result.Cue != null) played = Math.Max(played, Math.Min(result.Cue.Time, ClearAtSeconds));
            NoteAccuracy(result);
            Score.Add(result);
            ShowOutcome(result);
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    bool bonus = result.Cue.Kind == CueKind.Bonus;
                    if (PlaysJudgementAudio) Play(bonus ? bonusSound : hitSound);
                    ShowJudgement(HitJudgement(result.Grade), bonus ? Palette.SunText :
                        result.Grade == HitGrade.Perfect ? Palette.SkyText : Palette.Ink);
                    break;
                case CueOutcome.Missed:
                    if (PlaysJudgementAudio) Play(missSound);
                    ShowJudgement("MISS", Palette.DangerText);
                    break;
                case CueOutcome.ForbiddenHit:
                    Play(forbiddenSound);
                    ShowJudgement("-" + MiniGameScore.ForbiddenPenalty, Palette.DangerText);
                    break;
                case CueOutcome.EmptyHit:
                    // Plan §3: a Miss shows how far off the strike was.
                    ShowJudgement("MISS" + NearestOffset(result.Pad), Palette.DangerText);
                    break;
            }
            RefreshScore(result.Outcome == CueOutcome.Hit);
            if (result.Outcome == CueOutcome.Hit || result.Outcome == CueOutcome.EmptyHit || result.Outcome == CueOutcome.ForbiddenHit)
                FlashPad(result.Pad, result.Outcome == CueOutcome.Hit && result.Grade == HitGrade.Perfect ? PadFlashPool.Kind.Perfect :
                    result.Outcome == CueOutcome.Hit ? PadFlashPool.Kind.Good : PadFlashPool.Kind.Miss);
            if (Score.IsOver && !DeferGameOver) FinishRound();
        }

        // "early 120ms" / "late 90ms" against the closest open cue on that pad, if one is near.
        private string NearestOffset(int pad)
        {
            double best = NearestCueOffset(pad);
            if (double.IsNaN(best) || Math.Abs(best) > .4) return "";
            return "\n" + (best < 0 ? Strings.T("minigame.early") : Strings.T("minigame.late")) + Math.Abs(best * 1000).ToString("0") + "ms";
        }

        // Signed seconds from the nearest open, hittable cue on a pad (negative = early); NaN if none is near.
        protected double NearestCueOffset(int pad)
        {
            double visual = SongTime - offsetSeconds, best = double.NaN;
            foreach (BeatCue cue in cues)
            {
                if (cue.Pad != pad || cue.Kind == CueKind.Forbidden || Judge.IsResolved(cue)) continue;
                double offset = visual - cue.Time;
                if (double.IsNaN(best) || Math.Abs(offset) < Math.Abs(best)) best = offset;
            }
            return best;
        }

        private void RefreshScore(bool hit)
        {
            scoreText.text = Score.Score.ToString("D6");
            comboText.text = ComboLabel(Score.Combo);
            PaintHearts(Score.Halves);
            if (!hit || Score.Combo < 2) return;
            // Plan §3: combo effects grow at every 10.
            bool milestone = Score.Combo % 10 == 0;
            comboText.color = milestone ? Palette.SunText : Palette.Ink;
            if (milestone) Play(comboSound);
            if (comboPunch != null) StopCoroutine(comboPunch);
            comboPunch = StartCoroutine(PixelTween.Punch(comboText.transform, milestone ? .5f + .1f * Mathf.Min(3, Score.Combo / 10) : .15f, milestone ? .3f : .18f));
        }

        protected virtual string HitJudgement(HitGrade grade)
        {
            return grade == HitGrade.Perfect ? "PERFECT" : "GOOD";
        }

        protected virtual string ComboLabel(int combo)
        {
            return combo > 1 ? combo + "\nCOMBO" : "";
        }

        private void ShowJudgement(string text, Color color)
        {
            judgementText.text = text;
            judgementText.color = color;
            judgementLife = .6f;
        }

        // A full run clears by surviving to ClearAtSeconds. A game can clear sooner by overriding this.
        protected virtual bool ClearedRun { get { return Score != null && !Score.IsOver && played >= ClearAtSeconds; } }

        // Ends the run: the lives ran out, stage 10 was cleared, or (from code) it is stopped where it is.
        public void FinishRound()
        {
            if (State != MiniGameState.Playing) 
                return;

            if (confirming) 
            { 
                finishAfterResume = true; 
                return; 
            }

            bool cleared = ClearedRun;
            string extra = ResultExtra;
            
            State = MiniGameState.Result;
            music.Stop();
            ClearField();
            RefreshProgress();   // the HUD shows where the run actually ended

            if (Score != null) 
                PaintHearts(Score.Halves);

            countdownText.text = judgementText.text = "";
            Result = Settle(RewardStage, cleared);

            // The result names the clear and shows only the time and the drumsticks earned.
            modalTitle.text = Strings.T(cleared ? "minigame.cleared" : GameOverKey);
            modalBody.text =
                Strings.T("minigame.time") + Clock(played) + (Result.NewBestTime ? "  " + Strings.T("minigame.new_best") : "") + "\n" +
                Strings.T("minigame.drumsticks") + Result.Drumsticks.ToString("N0");
            padDiagram[0].transform.parent.gameObject.SetActive(false);
            SetButtons(Strings.T("minigame.play_again"));
            modalTransition.Show();

            if (flashes != null) 
                flashes.transform.SetAsLastSibling();

            if (completeButton != null) 
                completeButton.gameObject.SetActive(false);

            OnRoundFinished(cleared);
        }

        // The shared result stays time and drumsticks. Games may still call this; it does not add a line.
        protected void AppendResult(string line) { }

        // Opens the shared modal after the nail job. The primary button starts a new round.
        protected void ShowRewardModal(string title, string body)
        {
            if (State != MiniGameState.Playing) return;
            music.Stop();
            State = MiniGameState.Ready;
            modalTitle.text = title;
            modalBody.text = body;
            padDiagram[0].transform.parent.gameObject.SetActive(false);
            SetButtons(Strings.T("minigame.start"));
            modalTransition.Show();
            if (completeButton != null) completeButton.gameObject.SetActive(false);
        }

        // From the prompt and the finished nail job this starts; from the result it restarts.
        // From the exit question it leaves for the mini-game list.
        private void PrimaryAction()
        {
            if (confirming) ConfirmExit();
            else StartRound();
        }

        // Mid-run exit back to the opening prompt. Nothing is recorded.
        public void ReturnToPrompt()
        {
            if (State != MiniGameState.Playing) return;
            if (music != null) music.Stop();
            if (Judge != null) Judge.Judged -= OnJudged;
            ClearField();
            ShowPrompt();
        }

        private void EnsureCompleteButton()
        {
            if (menuButton == null) return;
            Transform existing = transform.Find("CompleteButton");
            completeButton = existing != null ? existing.GetComponent<Button>() : Instantiate(menuButton, transform);
            completeButton.name = "CompleteButton";
            completeButton.onClick.RemoveAllListeners();
            completeButton.onClick.AddListener(AskExit);
            RectTransform rect = completeButton.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(500f, 300f);
            rect.sizeDelta = new Vector2(180f, 56f);
            Text label = completeButton.GetComponentInChildren<Text>();
            if (label != null) label.text = Strings.T("minigame.complete");
            completeButton.gameObject.SetActive(false);
        }

        // Freezes the run and asks whether to leave. Nothing is recorded until a real finish.
        private void AskExit()
        {
            if (State != MiniGameState.Playing || confirming) return;
            pausedSong = AudioSettings.dspTime - anchor;
            pausedAtDsp = AudioSettings.dspTime;
            confirming = true;
            HoldAudio();
            modalTitle.text = Strings.T("minigame.complete");
            modalBody.text = Strings.T("minigame.exit_ask");
            if (padDiagram != null && padDiagram.Length > 0 && padDiagram[0] != null)
                padDiagram[0].transform.parent.gameObject.SetActive(false);
            SetButtons(Strings.T("minigame.complete"), Strings.T("minigame.continue"));
            modal.transform.SetAsLastSibling();
            modalTransition.Show();
            if (completeButton != null) completeButton.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ResumeFromExit()
        {
            if (!confirming) return;
            anchor += AudioSettings.dspTime - pausedAtDsp;
            confirming = false;
            ReleaseAudio();
            modalTransition.Hide();
            if (finishAfterResume)
            {
                finishAfterResume = false;
                FinishRound();
                return;
            }
            if (completeButton != null)
            {
                completeButton.gameObject.SetActive(State == MiniGameState.Playing);
                completeButton.transform.SetAsLastSibling();
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ConfirmExit()
        {
            confirming = false;
            finishAfterResume = false;
            foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
                if (source != null) source.Stop();
            heldAudio.Clear();
            BackToMenu();
        }

        private void HoldAudio()
        {
            heldAudio.Clear();
            foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
            {
                if (source == null || !source.isPlaying) continue;
                source.Pause();
                heldAudio.Add(source);
            }
        }

        private void ReleaseAudio()
        {
            foreach (AudioSource source in heldAudio)
                if (source != null) source.UnPause();
            heldAudio.Clear();
        }

        public void BackToMenu()
        {
            if (confirming) { ResumeFromExit(); return; }
            if (State == MiniGameState.Playing) { music.Stop(); State = MiniGameState.Ready; }
            SceneTransition.Load("MiniGames");
        }

        private void ResetHud()
        {
            scoreText.text = "000000";
            comboText.text = judgementText.text = countdownText.text = "";
            timerText.text = Clock(0);
            stageFill.fillAmount = 0;
            PaintHearts(MiniGameScore.MaxHalves);
        }

        // Five hearts. A full heart covers its slot; a half heart covers the left side and leaves the rest pale.
        private void PaintHearts(int halves)
        {
            if (lifeIcons == null) return;
            for (int i = 0; i < lifeIcons.Length; i++)
            {
                Image slot = lifeIcons[i];
                if (slot == null) continue;
                slot.color = Palette.Well;
                Image fill = HeartFill(slot);
                int have = halves - i * MiniGameScore.HalvesPerHeart;
                fill.color = Palette.Coral;
                fill.fillAmount = have >= MiniGameScore.HalvesPerHeart ? 1f : have == 1 ? .5f : 0f;
            }
        }

        private static Image HeartFill(Image slot)
        {
            Transform child = slot.transform.Find("Fill");
            Image fill = child != null ? child.GetComponent<Image>() : null;
            if (fill == null)
            {
                var overlay = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(slot.transform, false);
                fill = overlay.GetComponent<Image>();
                fill.raycastTarget = false;
                RectTransform rect = fill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            fill.sprite = slot.sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            return fill;
        }

        private void SetButtons(string primary, string menu = null)
        {
            primaryButton.GetComponentInChildren<Text>().text = primary;
            menuButton.GetComponentInChildren<Text>().text = menu ?? Strings.T("minigame.back");
        }

        // The beat clock is the scheduled start. A clip that is still loading would miss PlayScheduled
        // and stay silent, so the start is retried once the samples are in memory.
        private void StartMusic()
        {
            music.spatialBlend = 0f;
            music.loop = true;
            if (music.clip != null && music.clip.loadState != AudioDataLoadState.Loaded) music.clip.LoadAudioData();
            music.Stop();
            music.PlayScheduled(anchor);
            if (musicRetry != null) StopCoroutine(musicRetry);
            musicRetry = music.clip != null && music.clip.loadState != AudioDataLoadState.Loaded
                ? StartCoroutine(RetryMusic()) : null;
        }

        private IEnumerator RetryMusic()
        {
            while (State == MiniGameState.Playing && music.clip != null && music.clip.loadState == AudioDataLoadState.Loading)
                yield return null;
            musicRetry = null;
            if (State != MiniGameState.Playing || music.clip == null || music.clip.loadState != AudioDataLoadState.Loaded || music.isPlaying)
                yield break;
            double now = AudioSettings.dspTime;
            if (now + .05 < anchor) music.PlayScheduled(anchor);
            else
            {
                music.time = Mathf.Clamp((float)(now - anchor), 0f, Mathf.Max(0f, music.clip.length - .05f));
                music.Play();
            }
        }

        protected void Play(AudioClip clip)
        {
            if (clip == null || effects == null) return;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            effects.spatialBlend = 0f;
            effects.PlayOneShot(clip);
        }

        // Leaving the app mid-run abandons it; the prompt lets the player restart cleanly.
        private void OnApplicationPause(bool paused)
        {
            if (!paused || State != MiniGameState.Playing) return;
            music.Stop();
            ClearField();
            ShowPrompt();
        }

        protected virtual void OnDestroy()
        {
            if (Judge != null) Judge.Judged -= OnJudged;
        }
    }
}

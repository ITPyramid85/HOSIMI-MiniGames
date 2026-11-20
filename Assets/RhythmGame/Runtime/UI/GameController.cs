using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmGame
{
    public enum GameRunState { Ready, Playing, Paused, Result, Error }

    public sealed class GameController : MonoBehaviour
    {
        public const string DefaultChartId = CasualMode.ChartId;
        [SerializeField] private string chartId = DefaultChartId;
        [SerializeField] private DrumPadView[] pads;
        private static readonly Key[] PadKeys = { Key.Q, Key.E, Key.A, Key.S, Key.D, Key.Z, Key.X, Key.C };
        [SerializeField] private Image healthFill, progressFill;
        [SerializeField] private RawImage hitEffect;
        [SerializeField] private Text scoreText, comboText, healthText, judgementText, countdownText;
        [SerializeField] private Text characterText, songText, hintText, inputLabel;
        // Shows the casual-mode stage; was named for the removed skill status.
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("skillText")] private Text stageText;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private GameObject modal;
        [SerializeField] private Text modalTitle, modalBody;
        [SerializeField] private Button primaryButton, menuButton, pauseButton;
        [SerializeField] private AudioSource music, effects;
        [SerializeField] private AudioClip tapSound, missSound;
        private RhythmGameData data;
        private BeatmapData beatmap;
        private JudgementPoint[] points;
        private PadInputJudge judge;
        private double anchor, frozenTime;
        private bool settled, leaving;
        private readonly bool[] keyboardHeld = new bool[BeatmapData.PadCount];
        private float flashTime, judgementLife;
        private MiniGameGuide guide;
        private AudioLatencyMonitor latency;
        private double appliedOffsetMs;
        private bool musicNeedsSeek;
        private PopupTransition modalTransition;
        private GameUIAnimator animator;
        public GameRunState State { get; private set; }
        public PlaySession Session { get; private set; }
        public PlayResult Result { get; private set; }
        public double SongTime { get { return State == GameRunState.Playing ? AudioSettings.dspTime - anchor : frozenTime; } }
        public bool AcceptsPadPointer(int pad) { return State == GameRunState.Playing && !keyboardHeld[pad] && !Session.IsFinished; }
        public bool IsPadLit(int pad) { return State == GameRunState.Playing && judge != null && judge.IsLit(pad, SongTime); }
        public bool ButtonIsLit
        {
            get { for (int i = 0; i < BeatmapData.PadCount; i++) if (IsPadLit(i)) return true; return false; }
        }
        // Reserved for later character dialogue/voice presentation. No UI or audio subscribes in this version.
        public event Action<DialogueCue> DialogueRequested;


        private void Awake()
        {
            guide = GetComponentInChildren<MiniGameGuide>(true);
            modalTransition = PopupTransition.For(modal);
            animator = GetComponent<GameUIAnimator>();
            primaryButton.onClick.AddListener(PrimaryAction);
            menuButton.onClick.AddListener(BackToMenu);
            pauseButton.onClick.AddListener(TogglePause);
            SetButton(menuButton, Strings.T("game.main_menu"));
            SetButton(pauseButton, Strings.T("game.pause"));
            inputLabel.text = Strings.T("game.tap_when_lit_start_slow");
            hintText.text = Strings.T("game.start_with_the_bottom_three_pads");
            transform.Find("Layout/HealthCaption").GetComponent<Text>().text = "MISS";
            try
            {
                GameManager manager = GameManager.EnsureInstance();
                if (!manager.IsReady) throw new InvalidOperationException(manager.InitializationError);
                data = manager.Data;
                latency = AudioLatencyMonitor.EnsureInstance();
                latency.Changed += OnAudioOutputChanged;
                ChartConfig chart = data.Config.Chart(chartId);
                TextAsset json = Resources.Load<TextAsset>(chart.chartResource);
                if (json == null) throw new InvalidOperationException("Missing beatmap: " + chart.chartResource);
                beatmap = JsonUtility.FromJson<BeatmapData>(json.text);
                points = beatmap.Compile(chart);
                music.clip = Resources.Load<AudioClip>(chart.audioResource);
                if (music.clip == null || music.clip.length + .02 < beatmap.duration)
                    throw new InvalidOperationException("Missing or short music clip: " + chart.audioResource);
                AudioChannel.Bind(music, AudioKind.Music);
                AudioChannel.Bind(effects, AudioKind.Effects);
                songText.text = chart.songTitle + "\n" + beatmap.bpm.ToString("0.#") + " BPM  /  " + Strings.T("game.practice") +
                    "  /  " + TimeSpan.FromSeconds(beatmap.duration).ToString(@"mm\:ss");
                CharacterConfig character = data.Config.Character(data.Player.equippedCharacterId);
                characterText.text = character.DisplayName + "\n\n" + Strings.T("game.tap_the_light_with_me");
                if (characterPortrait != null)
                {
                    characterPortrait.sprite = CharacterArt.Portrait(character);
                    characterPortrait.enabled = characterPortrait.sprite != null;
                    // With art on screen, keep only the name so the text doesn't cover the portrait.
                    if (characterPortrait.enabled) characterText.text = character.DisplayName;
                }
                stageText.text = StageLabel(0);
                State = GameRunState.Ready;
                ShowModal(Strings.T("game.follow_my_lead"), "", Strings.T("game.start"));

            }
            catch (Exception e) { ShowError(e); }
            healthText.text = "0 / 10";
            healthFill.fillAmount = 1;
            progressFill.fillAmount = 0;
            scoreText.text = "000000"; comboText.text = "0\nCOMBO";
            judgementText.text = countdownText.text = "";
            hitEffect.color = Color.clear;
            RenderButton(-3);
            GetComponent<MenuTypography>().Apply();
        }

        private static string StageLabel(double time)
        {
            switch (CasualMode.Stage(time))
            {
                case 0: return Strings.T("game.start_slow_3_pads");
                case 1: return Strings.T("game.find_the_rhythm_6_pads");
                default: return Strings.T("game.a_little_challenge_8_pads");
            }
        }

        public void StartRun()
        {
            if (leaving || (State != GameRunState.Ready && State != GameRunState.Result)) return;
            try
            {
                Session = data.BeginPlay(chartId);
                settled = false; Result = null;
                if (judge != null) judge.Judged -= OnJudged;
                latency.Refresh();
                appliedOffsetMs = latency.EffectiveOffsetMs;
                musicNeedsSeek = false;
                judge = new PadInputJudge(Session, points, appliedOffsetMs);
                foreach (var pad in pads) pad.ResetFeedback();
                judge.Judged += OnJudged;
                ResetInput();
                anchor = AudioSettings.dspTime + 3;
                frozenTime = -3;
                music.Stop(); music.PlayScheduled(anchor);
                State = GameRunState.Playing;
                modalTransition.Hide(); pauseButton.interactable = true;
                judgementText.text = ""; judgementLife = 0;
                RefreshHud();
                ClearSelection();
                RenderButton(SongTime);
            }
            catch (Exception e) { ShowError(e); }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            if (State != GameRunState.Playing) return;
            double time = SongTime;
            if (keyboard != null && !Session.IsFinished)
            {
                for (int pad = 0; pad < pads.Length && State == GameRunState.Playing && !Session.IsFinished; pad++)
                {
                    var key = keyboard[PadKeys[pad]];
                    if (key.wasPressedThisFrame && !pads[pad].Input.HasPointer)
                    {
                        keyboardHeld[pad] = true;
                        judge.Press(pad, time);
                    }
                    if (key.wasReleasedThisFrame && keyboardHeld[pad])
                    {
                        keyboardHeld[pad] = false;
                        judge.Release(pad, time);
                    }
                }
            }
            if (State != GameRunState.Playing) return;
            judge.AdvanceTo(time);
            if (State != GameRunState.Playing) return;
            RenderButton(time);
            progressFill.fillAmount = Mathf.Clamp01((float)(time / beatmap.duration));
            countdownText.text = time < 0 ? Math.Ceiling(-time).ToString("0") : time < .5 ? "GO" : "";
            judgementLife -= Time.unscaledDeltaTime;
            if (judgementLife <= 0) judgementText.text = "";
            stageText.text = StageLabel(time);
            flashTime = Mathf.Max(0, flashTime - Time.unscaledDeltaTime);
            Color flash = hitEffect.color; flash.a = flashTime / .25f; hitEffect.color = flash;
            hitEffect.rectTransform.localScale = Vector3.one * (1 + (.25f - flashTime) * 3);
            if (time >= beatmap.duration && Session.IsFinished) FinishRun();
        }

        public void Press(int pad = 0)
        {
            if (State != GameRunState.Playing || Session.IsFinished) return;
            judge.Press(pad, SongTime);
            RenderButton(SongTime);
        }
        public void Release(int pad = 0) { if (State == GameRunState.Playing && judge != null) judge.Release(pad, SongTime); }

        private void OnJudged(HitResult hit)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hit.ColorHtml, out color);
            pads[points[hit.NoteIndex].Pad].ShowFeedback(hit, color);
            judgementText.color = color;
            judgementText.text = hit.Judgement.ToString().ToUpperInvariant();
            judgementLife = .65f;
            hitEffect.rectTransform.anchoredPosition = ((RectTransform)pads[points[hit.NoteIndex].Pad].transform).anchoredPosition;
            hitEffect.color = color; flashTime = .25f;
            effects.PlayOneShot(hit.Judgement == Judgement.Miss ? missSound : tapSound, .65f);
            RefreshHud();
            if (hit.Dialogue != null && DialogueRequested != null) DialogueRequested(hit.Dialogue);
            if (hit.HasFailed) FinishRun();
        }

        private void RefreshHud()
        {
            scoreText.text = Session.Score.ToString("D6");
            comboText.text = Session.Combo + "\nCOMBO";
            healthText.text = ((Session.MaxHealth - Session.Health) / data.Config.rules.missDamage) + " / " +
                (Session.MaxHealth / data.Config.rules.missDamage);
            healthFill.fillAmount = (float)Session.Health / Session.MaxHealth;
            healthFill.color = Session.Health <= 30 ? Palette.Coral : Palette.Mint;
        }

        private void RenderButton(double time)
        {
            if (State == GameRunState.Playing && judge != null) judge.Present(time);
            bool playing = State == GameRunState.Playing && judge != null;
            for (int pad = 0; pad < pads.Length; pad++)
            {
                JudgementPoint next = playing ? judge.NextPoint(pad) : null;
                double lead = playing ? judge.NextTime(pad) - time : double.PositiveInfinity;
                pads[pad].Render(playing && judge.IsLit(pad, time),
                    next != null ? Mathf.Clamp01(1 - (float)(lead / (60 / beatmap.bpm))) : 0);
            }
        }

        public void TogglePause()
        {
            if (State == GameRunState.Playing)
            {
                frozenTime = SongTime;
                judge.ReleaseAll(frozenTime);
                if (State != GameRunState.Playing) return;
                if (frozenTime >= 0) music.Pause(); else music.Stop();
                State = GameRunState.Paused;
                RenderButton(frozenTime);
                ResetInput();
                ShowModal(Strings.T("game.paused"),
                    Strings.T("game.resume_and_tap_when_the_button_lights_up"),
                    Strings.T("game.resume"));
            }
            else if (State == GameRunState.Paused)
            {
                anchor = AudioSettings.dspTime - frozenTime;
                if (musicNeedsSeek)
                {
                    music.Stop();
                    if (frozenTime < 0) music.PlayScheduled(anchor);
                    else
                    {
                        music.time = Mathf.Clamp((float)frozenTime, 0, music.clip.length - .01f);
                        music.Play();
                    }
                    musicNeedsSeek = false;
                }
                else if (frozenTime < 0) music.PlayScheduled(anchor); else music.UnPause();
                State = GameRunState.Playing;
                modalTransition.Hide();
                RenderButton(frozenTime);
                ClearSelection();
            }
        }

        private void OnAudioOutputChanged()
        {
            if (State == GameRunState.Playing) TogglePause();
            if (State != GameRunState.Paused || judge == null) return;
            frozenTime += judge.ChangeCalibrationWhilePaused(latency.EffectiveOffsetMs);
            appliedOffsetMs = latency.EffectiveOffsetMs;
            // Audio devices may discard a paused buffer. Seek on resume even when
            // the offset is unchanged, preserving the current cue's reaction clock.
            musicNeedsSeek = true;
            ResetInput();
            ShowModal(Strings.T("game.audio_output_changed"),
                Strings.T("game.paused_and_updated_timing_resume_when_re"), Strings.T("game.resume"));
        }

        private void FinishRun()
        {
            if (settled || Session == null) return;
            frozenTime = SongTime;
            Result = data.Complete(Session);
            settled = true;
            State = GameRunState.Result;
            RenderButton(frozenTime);
            music.Stop(); ResetInput(); pauseButton.interactable = false;
            countdownText.text = "";
            string title = Result.AllPerfect ? "ALL PERFECT" : Result.FullCombo ? "FULL COMBO" : Result.Cleared ? "CLEAR" : Strings.T("game.try_again");
            string body = "SCORE  " + Result.Score + "     /     " + Result.Grade + "\n" +
                Strings.T("game.accuracy") + (Result.Accuracy * 100).ToString("F2") + "%\n\n" +
                "PERFECT  " + Result.Perfect + "     GOOD  " + Result.Good + "     MISS  " + Result.Miss +
                "\n\nMAX COMBO  " + Result.MaxCombo + "\n" +
                Strings.T("game.drumsticks") + Result.TotalCoins + "\n\n" +
                (Result.FailedByHealth ? Strings.T("game.miss_limit_reached_remaining") + Result.UnplayedNotes :
                Result.Cleared ? Strings.T("game.result_saved") : Strings.T("game.reach_60_accuracy_to_clear"));
            if (data.HasUnsavedChanges) body += "\n" + Strings.T("game.save_failed_return_to_the_menu_to_retry");
            ShowModal(title, body, Strings.T("game.retry"));
        }

        private void PrimaryAction()
        {
            if (State == GameRunState.Paused) TogglePause(); else StartRun();
        }
        public void BackToMenu()
        {
            if (leaving) return;
            leaving = true;
            AbortRun();
            State = GameRunState.Ready;
            ResetInput();
            if (data != null && data.HasUnsavedChanges) GameManager.Instance.Save();
            if (animator != null) animator.PlayExit(() => SceneManager.LoadSceneAsync("MainMenu"));
            else SceneManager.LoadSceneAsync("MainMenu");
        }
        private void AbortRun()
        {
            if (music != null) music.Stop();
            if (Session != null && !settled) { data.Abort(Session); settled = true; }
        }
        private void ShowError(Exception e)
        {
            Debug.LogException(e);
            AbortRun(); State = GameRunState.Error;
            ShowModal(Strings.T("game.unable_to_play"), e.Message, "");
            primaryButton.gameObject.SetActive(false); pauseButton.interactable = false;
        }
        private void ShowModal(string title, string body, string action)
        {
            if (modalTransition.IsOpen) modalTransition.Pulse(); else modalTransition.Show();
            modalTitle.text = title; modalBody.text = body;
            if (guide != null) guide.gameObject.SetActive(State == GameRunState.Ready);
            modalBody.gameObject.SetActive(State != GameRunState.Ready);
            SetButton(primaryButton, action);
            ClearSelection();
        }
        private static void SetButton(Button button, string value) { button.GetComponentInChildren<Text>().text = value; }
        private static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        private void ResetInput()
        {
            Array.Clear(keyboardHeld, 0, keyboardHeld.Length);
            foreach (var pad in pads) pad.Input.ResetGesture();
        }
        private void OnApplicationPause(bool paused) { if (paused && State == GameRunState.Playing) TogglePause(); }
        private void OnApplicationFocus(bool focused) { if (!focused && !Application.isBatchMode && State == GameRunState.Playing) TogglePause(); }
        private void OnDestroy()
        {
            if (latency != null) latency.Changed -= OnAudioOutputChanged;
            if (judge != null) judge.Judged -= OnJudged;
            AbortRun();
        }
    }
}

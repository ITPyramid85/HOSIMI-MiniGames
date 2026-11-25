using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 10. Break the brick on the beat and stay ahead of the water. The record is the distance.
    public sealed class WallController : MiniGameController
    {
        [SerializeField] private WallView view;
        [SerializeField] private AudioSource floodAudio;
        [SerializeField] private AudioClip tomThin, tomMid, tomThick, crash, rubble, chime, splash, gurgle;
        private WallChart.Flood flood;
        private int wallId = -1, previewed = -1, got, needHits, creeps;
        private bool failed, ended;
        private float musicLoud = 1f;
        private double heardOffset;
        private Coroutine pending;

        public override string GameId { get { return "wall"; } }
        protected override string TitleKey { get { return "wall.title"; } }
        protected override string[] PromptKeys { get { return new[] { "wall.prompt_what", "wall.prompt_when", "wall.prompt_twist" }; } }
        protected override string GameOverKey { get { return "wall.lose"; } }
        protected override double Bpm { get { return WallChart.Bpm; } }
        protected override bool PlaysJudgementAudio { get { return false; } }
        protected override bool DeferGameOver { get { return true; } }
        protected override double LeadSeconds { get { return 0.5; } }
        protected override int[] PadsFor(int stage) { return WallChart.Pads; }

        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Drum(pad) : null; }

        protected override bool AcceptsPad(int pad)
        {
            if (ended) return false;
            return pad == WallChart.ThinPad || pad == WallChart.MidPad || pad == WallChart.ThickPad || pad == WallChart.ChestPad;
        }

        protected override void Awake()
        {
            base.Awake();
            if (view != null) view.Bind(this);
        }

        protected override List<BeatCue> Generate(int seed)
        {
            ended = false;
            wallId = -1;
            previewed = -1;
            got = 0;
            creeps = 0;
            failed = false;
            flood = new WallChart.Flood();
            if (pending != null) StopCoroutine(pending);
            List<BeatCue> chart = WallChart.Generate(seed);
            if (view != null)
            {
                view.Reset();
                view.ShowMeters(Label(0));
                view.SetWater(flood.Gap, flood.Meters);
                if (chart.Count > 0) ShowUpcoming(chart[0]);
            }
            if (music != null) musicLoud = music.volume;
            if (floodAudio != null)
            {
                floodAudio.volume = 0.02f;
                if (!floodAudio.isPlaying) floodAudio.Play();
                SyncDangerAudio();
            }
            return chart;
        }

        protected override void Update()
        {
            base.Update();
            if (IsPaused || flood == null || State != MiniGameState.Playing) return;
            Rise();
            SyncDangerAudio();
        }

        // Rising water is the warning: loud flood loop, softer beat track.
        private void SyncDangerAudio()
        {
            if (flood == null || floodAudio == null) return;
            if (ended)
            {
                floodAudio.volume = Mathf.MoveTowards(floodAudio.volume, 1f, Time.deltaTime * 2.5f);
                if (music != null) music.volume = Mathf.MoveTowards(music.volume, musicLoud * 0.08f, Time.deltaTime * 2f);
                return;
            }
            float danger = FloodDanger(flood);
            float creep = Mathf.Clamp01(flood.Meters / 48f) * 0.08f;
            float target = Mathf.Clamp01(0.02f + danger * (0.96f - creep) + creep);
            floodAudio.volume = Mathf.MoveTowards(floodAudio.volume, target, Time.deltaTime * 2.2f);
            if (music != null)
                music.volume = musicLoud * Mathf.Lerp(0.76f, 0.18f, danger);
        }

        private static float FloodDanger(WallChart.Flood flood)
        {
            if (flood.Gap >= WallChart.StartGap) return 0f;
            return Mathf.Clamp01((WallChart.StartGap - flood.Gap) / (float)WallChart.StartGap);
        }

        // The water moves on its own clock, so standing still is never safe.
        private void Rise()
        {
            if (ended) return;
            int due = WallChart.CreepsBy(SongTime);
            if (due <= creeps) return;
            for (; creeps < due; creeps++) flood.Creep();
            if (view != null) view.SetWater(flood.Gap, flood.Meters);
            if (flood.Submerged) Drown();
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            if (ended || cue == null) return;
            int wall = cue.Variant / 10;
            if (wall < previewed) return;
            if (failed && wall == wallId) return;
            heardOffset = SongTime - (cue.Time - secondsToBeat);
            ShowUpcoming(cue);
            if (view != null)
            {
                view.Crack(secondsToBeat);
                view.Pulse(cue.Pad, secondsToBeat);
            }
        }

        protected override void OnAcceptedPad(int pad)
        {
            Play(pad == WallChart.ChestPad ? crash : pad == WallChart.ThickPad ? tomThick : pad == WallChart.MidPad ? tomMid : tomThin);
            if (view != null) view.Strike(pad);
        }

        // Leftover beats of a wall that already failed must not cost another life.
        // A strike on the lit drum is that beat's hit: the drum lights LeadSeconds before the beat,
        // which is earlier than the stage window opens, and stays lit until the window closes.
        protected override CueResult Reinterpret(CueResult result)
        {
            if (result == null) return result;
            bool spent = failed && result.Cue != null && result.Cue.Variant / 10 == wallId;
            if (ended || spent) { result.Outcome = CueOutcome.ForbiddenAvoided; return result; }
            if (result.Outcome != CueOutcome.EmptyHit || Judge == null) return result;
            BeatCue lit = LitCue(result.Pad);
            if (lit == null) return result;
            Judge.Abandon(c => c == lit);
            double offsetMs = (SongTime - heardOffset - lit.Time) * 1000;
            double perfect = lit.PerfectMs > 0 ? lit.PerfectMs : 80;
            return new CueResult
            {
                Cue = lit, Pad = result.Pad, Outcome = CueOutcome.Hit,
                Grade = System.Math.Abs(offsetMs) <= perfect ? HitGrade.Perfect : HitGrade.Good,
                OffsetMs = offsetMs
            };
        }

        private BeatCue LitCue(int pad)
        {
            double heard = SongTime - heardOffset;
            foreach (BeatCue cue in Judge.Cues)
            {
                if (Judge.IsResolved(cue) || cue.Kind == CueKind.Forbidden) continue;
                if (cue.Time - LeadSeconds - 1e-6 > heard) return null;
                if (cue.Pad != pad || (failed && cue.Variant / 10 == wallId)) continue;
                double good = cue.GoodMs > 0 ? cue.GoodMs / 1000 : Judge.GoodWindowSeconds;
                if (heard <= cue.Time + good) return cue;
            }
            return null;
        }

        protected override void ShowOutcome(CueResult result)
        {
            if (ended || result == null || result.Cue == null) return;
            int wall = result.Cue.Variant / 10;
            int need = result.Cue.Variant % 10;
            if (wall != wallId)
            {
                wallId = wall;
                got = 0;
                failed = false;
                needHits = need;
            }
            if (failed) return;
            if (result.Outcome == CueOutcome.Hit)
            {
                got++;
                if (view != null) view.Chip(needHits - got);
                if (!LastOf(result.Cue)) return;
                bool treasure = result.Cue.Pad == WallChart.ChestPad;
                flood.Resolve(got >= needHits, treasure);
                if (view != null)
                {
                    if (got >= needHits) view.Break(); else view.Hold();
                    view.ShowMeters(Label(flood.Meters));
                    view.SetWater(flood.Gap, flood.Meters);
                }
                if (got >= needHits) Play(treasure ? chime : rubble);
                if (flood.Submerged || (Score != null && Score.IsOver)) Drown();
                else QueueNext(result.Cue);
                return;
            }
            if (result.Outcome != CueOutcome.Missed) return;
            failed = true;
            flood.Resolve(false, false);
            if (view != null)
            {
                view.Hold();
                view.ShowMeters(Label(flood.Meters));
                view.SetWater(flood.Gap, flood.Meters);
            }
            if (flood.Submerged || (Score != null && Score.IsOver)) Drown();
            else QueueNext(result.Cue);
        }

        protected override void ClearField()
        {
            if (floodAudio != null) floodAudio.Stop();
            if (music != null) music.volume = musicLoud;
            if (pending != null) StopCoroutine(pending);
            // StartRound also calls ClearField while State is still Ready; do not freeze there or the wave stalls.
            if (view != null && State != MiniGameState.Ready) view.Freeze();
        }

        protected override void OnRoundFinished(bool cleared)
        {
            int meters = flood == null ? 0 : flood.Meters;
            GameManager.EnsureInstance().Data.RememberWallDistance(meters);
            AppendResult(Label(meters));
        }

        private void ShowUpcoming(BeatCue cue)
        {
            if (cue == null || view == null) return;
            int wall = cue.Variant / 10;
            if (wall == previewed) return;
            previewed = wall;
            view.ShowWall(cue.Pad, cue.Variant % 10, cue.Kind == CueKind.Bonus);
        }

        private void QueueNext(BeatCue cue)
        {
            List<BeatCue> chart = Chart;
            if (chart == null) return;
            int index = chart.IndexOf(cue);
            for (int i = index + 1; i < chart.Count; i++)
            {
                if (chart[i].Variant / 10 != cue.Variant / 10)
                {
                    BeatCue next = chart[i];
                    if (pending != null) StopCoroutine(pending);
                    pending = StartCoroutine(Reveal(next));
                    return;
                }
            }
        }

        private IEnumerator Reveal(BeatCue cue)
        {
            yield return new WaitForSeconds(0.45f);
            if (!ended) ShowUpcoming(cue);
        }

        private bool LastOf(BeatCue cue)
        {
            List<BeatCue> chart = Chart;
            if (chart == null) return true;
            int index = chart.IndexOf(cue);
            if (index < 0 || index + 1 >= chart.Count) return true;
            return chart[index + 1].Variant / 10 != cue.Variant / 10;
        }

        private void Drown()
        {
            if (ended) return;
            ended = true;
            Play(splash);
            Play(gurgle);
            if (view != null) view.Drown();
            StartCoroutine(EndSoon());
        }

        private IEnumerator EndSoon()
        {
            yield return new WaitForSeconds(0.85f);
            if (State == MiniGameState.Playing) FinishRound();
        }

        private static string Label(int meters) { return meters.ToString() + Strings.T("wall.meter"); }
    }
}

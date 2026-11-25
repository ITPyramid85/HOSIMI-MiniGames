using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 6. The fish stops under the left, middle, or right hole. Strike that ice on the beat.
    // A big fish takes two strikes. Breaking empty ice drops the fisher in the water for half a heart;
    // he shivers, climbs back out, and the run goes on.
    public sealed class IceFishingController : MiniGameController
    {
        // Fall, shiver in the water, climb out. No strike lands and no fish is owed while he is in.
        private const float RecoverSeconds = 1.5f;
        [SerializeField] private IceFishingView view;
        // Breaking the ice is a tom per hole: low left, middle centre, high right.
        [SerializeField] private AudioClip[] padSounds;
        [SerializeField] private AudioClip crackSound, splashSound, bucketSound, fleeSound;
        private readonly Dictionary<int, int> landed = new Dictionary<int, int>();
        private int caught;
        private bool closing;
        private float finishAt = -1f, dryAt = -1f;

        public override string GameId { get { return "ice"; } }
        protected override string TitleKey { get { return "ice.title"; } }
        protected override string[] PromptKeys { get { return new[] { "ice.prompt_what", "ice.prompt_when", "ice.prompt_twist" }; } }
        protected override string GameOverKey { get { return "ice.fall"; } }
        protected override double Bpm { get { return IceChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return IceChart.Center; } }
        protected override RectTransform FlashTarget(int pad) { return view != null ? view.HoleRect(pad) : null; }
        // An unstruck fish just swims off. A bad strike costs half a heart and the last heart ends the run;
        // surviving the two minutes clears it.
        protected override bool DeferGameOver { get { return true; } }
        // Two minutes, then the result. The shared clock would print STAGE 2 at 45s; this game never does.
        protected override double ClearAtSeconds { get { return IceChart.RunSeconds; } }
        protected override int DisplayStage { get { return 1; } }
        protected override int RecordedStage { get { return 1; } }
        protected override int RewardStage { get { return 1; } }
        protected override double StageProgress { get { return Math.Min(1, PlayedSeconds / ClearAtSeconds); } }
        private bool InWater { get { return Time.unscaledTime < dryAt; } }
        protected override bool AcceptsPad(int pad)
        {
            if (closing || InWater) return false;
            return pad == IceChart.Left || pad == IceChart.Center || pad == IceChart.Right;
        }
        protected override int[] PadsFor(int stage) { return IceChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return IceChart.Generate(seed); }
        protected override bool PlaysJudgementAudio { get { return false; } }

        protected override void Awake()
        {
            if (view != null) view.Pressed += OnHole;
            base.Awake();
            EnsureSounds();
        }

        // The scene may have been built before these clips existed. Load them from Resources,
        // and keep the shared hit and miss clips warm so a strike is never silent.
        private void EnsureSounds()
        {
            if (padSounds == null || padSounds.Length < 3 || padSounds[0] == null)
                padSounds = new[] { LoadClip("IceTom1"), LoadClip("IceTom2"), LoadClip("IceTom3") };
            if (crackSound == null) crackSound = LoadClip("IceCrack");
            if (splashSound == null) splashSound = LoadClip("IceSplash");
            if (bucketSound == null) bucketSound = LoadClip("IceBucket");
            if (fleeSound == null) fleeSound = LoadClip("IceGurgle");
            Warm(StrikeClip);
            Warm(MissClip);
            Warm(crackSound);
            Warm(splashSound);
            Warm(bucketSound);
            Warm(fleeSound);
            if (padSounds != null)
                foreach (AudioClip clip in padSounds) Warm(clip);
        }

        private static AudioClip LoadClip(string name)
        {
            return Resources.Load<AudioClip>("RhythmGame/Audio/" + name);
        }

        private static void Warm(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        private void OnHole(int pad) { NoteScreenTouch(); if (!closing) Press(pad); }

        protected override void OnAcceptedPad(int pad)
        {
            if (view == null || closing) return;
            view.Chop(pad);
            PlayStrike(pad);
        }

        // Every hammer blow breaks the ice: the tom for that hole, then the crack.
        private void PlayStrike(int pad)
        {
            int hole = IceChart.HoleIndex(pad);
            AudioClip tom = padSounds != null && hole >= 0 && hole < padSounds.Length ? padSounds[hole] : null;
            Play(tom != null ? tom : StrikeClip);
            Play(crackSound);
        }

        // The fish is up on an ice only while it can be struck, so a strike there always answers that fish:
        // a catch for a real stop, the feint's penalty for a fake one. Never an empty strike.
        protected override CueResult Reinterpret(CueResult result)
        {
            // Only a hammer blow is judged. A fish that stops and goes unstruck just swims off: no MISS, no heart.
            if (result.Outcome == CueOutcome.Missed)
                return new CueResult { Cue = result.Cue, Pad = result.Pad, Outcome = CueOutcome.ForbiddenAvoided, Grade = HitGrade.Perfect, OffsetMs = double.NaN };
            if (result.Outcome != CueOutcome.EmptyHit || view == null || !FishIsOn(result.Pad)) return result;
            BeatCue cue = OpenCue(result.Pad);
            if (cue == null) return result;
            double offsetMs = (SongTime - cue.Time) * 1000;
            Judge.Abandon(c => c == cue);
            if (cue.Kind == CueKind.Forbidden)
                return new CueResult { Cue = cue, Pad = result.Pad, Outcome = CueOutcome.ForbiddenHit, Grade = HitGrade.Miss, OffsetMs = offsetMs };
            return new CueResult
            {
                Cue = cue, Pad = result.Pad, Outcome = CueOutcome.Hit,
                Grade = Math.Abs(offsetMs) <= cue.PerfectMs ? HitGrade.Perfect : HitGrade.Good,
                OffsetMs = offsetMs
            };
        }

        // The cue the fish on that ice belongs to: its own still-open cue, or failing that the nearest one there.
        private BeatCue OpenCue(int pad)
        {
            int variant = view.ShownVariant;
            BeatCue own = null, near = null;
            foreach (BeatCue cue in Judge.Cues)
            {
                if (cue.Pad != pad || Judge.IsResolved(cue)) continue;
                if (cue.Variant == variant && cue.Kind != CueKind.Forbidden && (own == null || cue.Time < own.Time)) own = cue;
                if (near == null || Math.Abs(cue.Time - SongTime) < Math.Abs(near.Time - SongTime)) near = cue;
            }
            return own != null ? own : near;
        }

        protected override void Update()
        {
            base.Update();
            if (IsPaused || State != MiniGameState.Playing) return;
            if (!closing && Score != null && Score.IsOver) ScheduleEnd(.85f);
            if (closing && Time.unscaledTime >= finishAt)
            {
                closing = false;
                FinishRound();
            }
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            if (closing) return;
            view.Approach(cue, secondsToBeat);
        }

        protected override void ShowOutcome(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    int have = 0;
                    landed.TryGetValue(result.Cue.Variant, out have);
                    have++;
                    landed[result.Cue.Variant] = have;
                    if (have >= StrikesNeeded(result.Cue.Variant))
                        Catch(result.Cue);
                    else
                        view.Flinch(result.Cue.Pad);
                    break;
                case CueOutcome.Missed:
                    view.Escape(result.Cue.Pad);
                    Play(fleeSound);
                    break;
                case CueOutcome.ForbiddenHit:
                    // A feint: the fish was never caught. Only an empty ice drops the fisher in.
                    // The judge has already taken the half heart for this strike.
                    if (!FishIsOn(result.Pad))
                    {
                        Drown(result.Pad, false);
                        return;
                    }
                    view.Escape(result.Pad);
                    Play(fleeSound);
                    break;
                case CueOutcome.ForbiddenAvoided:
                    view.Escape(result.Cue.Pad);
                    Play(fleeSound);
                    break;
                case CueOutcome.EmptyHit:
                    // Nothing under that ice: the strike breaks it and the fisher goes in.
                    if (AcceptsPad(result.Pad) && !FishIsOn(result.Pad))
                    {
                        Drown(result.Pad, true);
                        return;
                    }
                    break;
            }
        }

        private int StrikesNeeded(int variant)
        {
            int count = 0;
            foreach (BeatCue cue in Judge.Cues)
                if (cue.Variant == variant && cue.Kind != CueKind.Forbidden) count++;
            return Math.Max(1, count);
        }

        private void Catch(BeatCue cue)
        {
            int species = IceChart.SpeciesOf(cue.Variant);
            caught++;
            view.Catch(cue.Pad, species, caught);
            Play(splashSound);
            Play(bucketSound);
            GameManager.EnsureInstance().Data.DiscoverFish(IceChart.SpeciesId(cue.Variant));
        }

        private bool FishIsOn(int pad)
        {
            return view != null && view.FishAt(pad);
        }

        // An empty ice breaks under the hammer: the fisher goes in for half a heart, shivers, and climbs
        // back out. Only the last half heart keeps him in the water and ends the run.
        private void Drown(int pad, bool charge)
        {
            if (closing || InWater) return;
            if (charge) Score.LoseHalf();
            bool last = Score.IsOver;
            view.Fall(pad, !last);
            Play(splashSound != null ? splashSound : MissClip);
            if (last)
            {
                ScheduleEnd(.9f);
                return;
            }
            dryAt = Time.unscaledTime + RecoverSeconds;
            // Fish that stop while he is in the water swim on without costing a heart.
            double until = SongTime + RecoverSeconds;
            Judge.Abandon(c => c.Time <= until);
        }

        private void ScheduleEnd(float delay)
        {
            closing = true;
            finishAt = Time.unscaledTime + delay;
        }

        protected override void ClearField()
        {
            caught = 0;
            closing = false;
            finishAt = dryAt = -1f;
            landed.Clear();
            if (view != null) view.Clear(KnownDex());
        }

        private bool[] KnownDex()
        {
            var known = new bool[IceChart.SpeciesCount];
            List<string> dex = GameManager.EnsureInstance().Data.Player.fishDex;
            if (dex == null) return known;
            for (int i = 0; i < IceChart.Species.Length; i++) known[i] = dex.Contains(IceChart.Species[i]);
            return known;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnHole;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 6. The fish stops under the left, middle, or right hole. Strike that ice on the beat.
    // A big fish takes two strikes. A fake stop, or the wrong hole, drops the fisher in the water.
    public sealed class IceFishingController : MiniGameController
    {
        [SerializeField] private IceFishingView view;
        // Breaking the ice is a tom per hole: low left, middle centre, high right.
        [SerializeField] private AudioClip[] padSounds;
        [SerializeField] private AudioClip crackSound, splashSound, bucketSound, fleeSound;
        private const int StrikeCount = 7;
        private readonly Dictionary<int, int> landed = new Dictionary<int, int>();
        private int caught, blows;
        private bool closing, drowned;
        private float finishAt = -1f;

        public override string GameId { get { return "ice"; } }
        protected override string TitleKey { get { return "ice.title"; } }
        protected override string[] PromptKeys { get { return new[] { "ice.prompt_what", "ice.prompt_when", "ice.prompt_twist" }; } }
        protected override string GameOverKey { get { return "ice.fall"; } }
        protected override double Bpm { get { return IceChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return IceChart.Center; } }
        // Missing a fish lets it go. The run ends on the 7th strike, or when a bad strike drops the fisher in.
        protected override bool DeferGameOver { get { return true; } }
        protected override bool AcceptsPad(int pad)
        {
            if (closing) return false;
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

        private void OnHole(int pad) { if (!closing) Press(pad); }

        protected override void OnAcceptedPad(int pad)
        {
            if (view == null || closing) return;
            view.Chop(pad);
            PlayStrike(pad);
            blows++;
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
            if (closing && Time.unscaledTime >= finishAt)
            {
                closing = false;
                ShowFinished();
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
                    if (!FishIsOn(result.Pad))
                    {
                        Drown(result.Pad);
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
                        Drown(result.Pad);
                        return;
                    }
                    break;
            }
            if (!closing && blows >= StrikeCount) ScheduleEnd(.85f);
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

        // An empty ice breaks under the hammer: the fisher goes in and that is the end of the run.
        private void Drown(int pad)
        {
            if (closing) return;
            drowned = true;
            Score.Defeat();
            view.Shiver(pad);
            view.Fall(pad);
            Play(splashSound != null ? splashSound : MissClip);
            ScheduleEnd(.9f);
        }

        private void ScheduleEnd(float delay)
        {
            closing = true;
            finishAt = Time.unscaledTime + delay;
        }

        protected override void ClearField()
        {
            caught = blows = 0;
            drowned = closing = false;
            finishAt = -1f;
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

        private void ShowFinished()
        {
            int stage = Math.Max(1, Math.Min(StageTable.Count, Math.Max(1, caught)));
            RhythmGameData data = GameManager.EnsureInstance().Data;
            data.RecordMiniGame(GameId, PlayedSeconds, stage, !drowned && caught > 0);
            int found = data.Player.fishDex == null ? 0 : data.Player.fishDex.Count;
            string title = drowned ? Strings.T("ice.fall") : Strings.T("ice.caught") + caught;
            string body = Strings.T("ice.caught") + caught + "\n" + Strings.T("ice.dex") + found + "/" + IceChart.SpeciesCount;
            ShowRewardModal(title, body);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnHole;
        }
    }
}

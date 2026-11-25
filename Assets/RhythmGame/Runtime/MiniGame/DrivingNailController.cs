using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Nails for two minutes, one strike each, with no stage change. A bent nail is followed by the next one.
    // Two seated light the chair, three the desk, and five the house.
    // Lasting the two minutes clears; losing the hearts fails. Either way the result dialog opens.
    public sealed class DrivingNailController : MiniGameController
    {
        [SerializeField] private DrivingNailView view;
        private int nailId = -1, hitsLeft, hitsTotal, seated, jobSeated, swings;
        private AudioClip impactClip;
        protected override bool PlaysJudgementAudio { get { return false; } }

        public override string GameId { get { return "nails"; } }
        protected override string TitleKey { get { return "nails.title"; } }
        protected override string[] PromptKeys { get { return new[] { "nails.prompt_what", "nails.prompt_when", "nails.prompt_twist" }; } }
        protected override string GameOverKey { get { return "nails.lose"; } }
        protected override double Bpm { get { return NailChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return NailChart.Pad; } }
        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Hammer : null; }
        protected override bool AcceptsPad(int pad) { return pad == NailChart.Pad; }
        protected override int[] PadsFor(int stage) { return NailChart.Pads(stage); }
        protected override double ClearAtSeconds { get { return NailChart.RunSeconds; } }
        // The shared clock would print STAGE 2 at 45s and keep going. This game stays on one stage and ends at two minutes.
        protected override int DisplayStage { get { return 1; } }
        protected override int RecordedStage { get { return 1; } }
        protected override int RewardStage { get { return 1; } }
        protected override double StageProgress { get { return Math.Min(1, PlayedSeconds / NailChart.RunSeconds); } }
        protected override List<BeatCue> Generate(int seed) { return NailChart.Generate(seed); }
        // Seated nails over every nail in the two minutes. Read before ClearField zeroes the counter.
        protected override string ResultExtra
        {
            get
            {
                int total = Judge != null ? Judge.Cues.Count : 0;
                return "\n" + Strings.T("minigame.drumsticks") + seated + "/" + total;
            }
        }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            view.Struck += OnHammerStruck;
            base.Awake();
        }

        private void OnFieldTapped() { NoteScreenTouch(); Press(NailChart.Pad); }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            BeginNail(cue);
            view.Raise(secondsToBeat);
        }

        protected override void ShowOutcome(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    swings++;
                    hitsLeft = Math.Max(0, hitsLeft - 1);
                    impactClip = StrikeClip;
                    view.Strike(hitsLeft, hitsTotal);
                    if (hitsLeft == 0)
                    {
                        view.Seat();
                        seated++;
                        jobSeated++;
                        Grant(jobSeated);
                        int done = result.Cue.Variant;
                        Judge.Abandon(cue => cue.Variant == done);
                    }
                    break;
                case CueOutcome.Missed:
                    impactClip = MissClip;
                    hitsLeft = 0;
                    int ruined = result.Cue.Variant;
                    // Bend this nail before the next one is armed. A deferred bend landed on the new nail,
                    // and every miss after that copied the bent sprite onto the nail that followed.
                    view.Bend(ruined, Strings.T("nails.bend"), hitsTotal);
                    BeatCue upcoming = NailChart.NextNail(Chart, ruined);
                    NailChart.PullForward(Chart, result.Cue, BeatSeconds);
                    Judge.Abandon(cue => cue.Variant == ruined);
                    if (upcoming == null) return;
                    PresentFrom(upcoming);
                    BeginNail(upcoming);
                    break;
                case CueOutcome.EmptyHit:
                    swings++;
                    double offset = NearestCueOffset(NailChart.Pad);
                    if (double.IsNaN(offset) || offset < 0) view.Thumb(Strings.T("nails.thumb"));
                    else view.Scratch(Strings.T("nails.bend"));
                    break;
            }
        }

        protected override void ClearField()
        {
            nailId = -1;
            hitsLeft = hitsTotal = seated = jobSeated = swings = 0;
            if (view == null) return;
            view.Clear();
            // The counter is on screen from the first moment, before the hammer rises.
            if (PlayedSeconds == 0 && Judge != null && Judge.Cues.Count > 0) BeginNail(Judge.Cues[0]);
        }

        private void Grant(int done)
        {
            if (done == NailChart.ChairAt) view.Grant(NailChart.Piece.Chair);
            else if (done == NailChart.DeskAt) view.Grant(NailChart.Piece.Desk);
            else if (done == NailChart.HouseAt) view.Grant(NailChart.Piece.House);
        }

        private void BeginNail(BeatCue cue)
        {
            if (cue.Variant == nailId) return;
            nailId = cue.Variant;
            hitsTotal = 0;
            foreach (BeatCue other in Judge.Cues) if (other.Variant == nailId) hitsTotal++;
            hitsLeft = hitsTotal;
            view.Arm(cue.Variant, hitsLeft, hitsTotal);
        }

        private void OnHammerStruck()
        {
            AudioClip clip = impactClip;
            impactClip = null;
            if (clip != null) Play(clip);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null)
            {
                view.Pressed -= OnFieldTapped;
                view.Struck -= OnHammerStruck;
            }
        }
    }
}

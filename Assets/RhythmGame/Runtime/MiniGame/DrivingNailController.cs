using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Five nails, one strike each. A bent nail is followed by the next one. Two seated nails light
    // the chair, three the desk, and five the house.
    public sealed class DrivingNailController : MiniGameController
    {
        [SerializeField] private DrivingNailView view;
        private int nailId = -1, hitsLeft, hitsTotal, seated, swings;
        private AudioClip impactClip;
        protected override bool PlaysJudgementAudio { get { return false; } }

        public override string GameId { get { return "nails"; } }
        protected override string TitleKey { get { return "nails.title"; } }
        protected override string[] PromptKeys { get { return new[] { "nails.prompt_what", "nails.prompt_when", "nails.prompt_twist" }; } }
        protected override string GameOverKey { get { return "nails.lose"; } }
        protected override double Bpm { get { return NailChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return NailChart.Pad; } }
        protected override bool AcceptsPad(int pad) { return pad == NailChart.Pad; }
        protected override int[] PadsFor(int stage) { return NailChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return NailChart.Generate(seed); }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            view.Struck += OnHammerStruck;
            base.Awake();
        }

        private void OnFieldTapped() { Press(NailChart.Pad); }

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
                        Grant(seated);
                        int done = result.Cue.Variant;
                        Judge.Abandon(cue => cue.Variant == done);
                        if (done >= NailChart.NailCount - 1) ShowFinished();
                    }
                    break;
                case CueOutcome.Missed:
                    impactClip = MissClip;
                    view.Bend(Strings.T("nails.bend"), hitsTotal);
                    hitsLeft = 0;
                    int ruined = result.Cue.Variant;
                    BeatCue upcoming = NailChart.NextNail(Chart, ruined);
                    NailChart.PullForward(Chart, result.Cue, BeatSeconds);
                    Judge.Abandon(cue => cue.Variant == ruined);
                    if (upcoming == null)
                    {
                        ShowFinished();
                        return;
                    }
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
            hitsLeft = hitsTotal = seated = swings = 0;
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

        // The dialog opens once every nail has been struck, not while the row is still growing.
        private void ShowFinished()
        {
            // Seated nails are this job's stages: two is the chair, three the desk, five the house.
            int stage = Math.Max(1, Math.Min(StageTable.Count, seated));
            GameManager.EnsureInstance().Data.RecordMiniGame(GameId, PlayedSeconds, stage, seated >= NailChart.HouseAt);
            string title = seated >= NailChart.HouseAt
                ? Strings.T(NailChart.PieceKey(NailChart.Piece.House))
                : Strings.T("nails.lose");
            string body =
                Strings.T("nails.played") + MiniGameController.Clock(PlayedSeconds) + "\n" +
                Strings.T("nails.swung") + swings + "\n" +
                Strings.T("nails.landed") + seated;
            ShowRewardModal(title, body);
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

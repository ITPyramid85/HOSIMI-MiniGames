using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
    {
    // Rice-cake pounding. Grandma flips the cake on the beat. Level 1 is slow and the window is wide.
    // After GO, two beats are only for listening; the first flip is the third beat. Gaps then shorten
    // down to a half beat, so the flip you see is the flip you strike.
    public sealed class MochiController : MiniGameController
    {
        [SerializeField] private MochiView view;
        private bool missedThisPress;

        public override string GameId { get { return "mochi"; } }
        protected override string TitleKey { get { return "mochi.title"; } }
        protected override string[] PromptKeys { get { return new[] { "mochi.prompt_what", "mochi.prompt_when", "mochi.prompt_twist" }; } }
        protected override string GameOverKey { get { return "mochi.lose"; } }
        protected override double Bpm { get { return MochiChart.Bpm; } }
        // The hand travels for as long as level 1 will still count the strike, so the lift itself is the window.
        protected override double LeadSeconds { get { return MochiChart.StageOf(1).GoodMs / 1000.0; } }
        protected override int DisplayStage { get { return MochiChart.StageAt(PlayedSeconds).Number; } }
        protected override double StageProgress { get { return MochiChart.ProgressIn(PlayedSeconds); } }
        protected override int RewardStage { get { return MochiChart.StageAt(PlayedSeconds).Number; } }        protected override bool ScoresAt(double songTime) { return songTime + 1e-9 >= MochiChart.JudgementOpens; }
        protected override double LeadFor(BeatCue cue)
        {
            if (cue == null) return LeadSeconds;
            double window = cue.GoodMs > 0 ? cue.GoodMs / 1000.0 : LeadSeconds;
            double lead = window < LeadSeconds ? window : LeadSeconds;
            // The first flip waits until the listen beats are over, even when its window is wide.
            if (cue.Time <= MochiChart.FirstCue + 1e-6)
            {
                double room = cue.Time - MochiChart.JudgementOpens;
                if (room < lead) lead = room;
            }
            return lead < .2 ? .2 : lead;
        }
        protected override int DefaultPad { get { return MochiChart.Pad; } }
        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Mallet : null; }
        protected override bool AcceptsPad(int pad) { return pad == MochiChart.Pad; }
        protected override int[] PadsFor(int stage) { return MochiChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return MochiChart.Generate(seed); }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            base.Awake();
        }

        private void OnFieldTapped() { NoteScreenTouch(); Press(MochiChart.Pad); }

        // The mallet comes down on every strike, so a swing into an empty mortar still reads as a swing.
        protected override void OnAcceptedPad(int pad)
        {
            missedThisPress = false;
            view.SwingMallet();
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat) { view.Reach(cue, secondsToBeat); }

        protected override string HitJudgement(HitGrade grade)
        {
            return grade == HitGrade.Perfect ? "Perfect!" : "Good!";
        }

        protected override string ComboLabel(int combo)
        {
            return combo > 1 ? "Combo " + combo : "";
        }

        protected override void ShowOutcome(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    // Bean-powder cake until a perfect run earns the rainbow one. It stays collected.
                    bool rainbow = result.Cue.Kind == CueKind.Bonus || (result.Grade == HitGrade.Perfect && Score.Combo >= 12);
                    if (rainbow) GameManager.EnsureInstance().Data.DiscoverMochi("rainbow");
                    view.Strike(result.Grade == HitGrade.Perfect, rainbow);
                    break;
                case CueOutcome.Missed:
                    missedThisPress = true;
                    view.TooLate(Strings.T("mochi.too_slow"));
                    break;
                case CueOutcome.ForbiddenHit: view.HandHit(Strings.T("mochi.ouch")); break;
                case CueOutcome.ForbiddenAvoided: view.FeintAvoided(); break;
                case CueOutcome.EmptyHit:
                    // A late press closes the flip first, then lands as an empty hit. That is still late.
                    // The cake hits the ceiling only when this swing is early for a flip already reaching in.
                    // A swing with no cake in play leaves it in the mortar.
                    double offset = NearestCueOffset(MochiChart.Pad);
                    bool flipping = !double.IsNaN(offset) && offset < 0 && offset >= -LeadSeconds;
                    if (!missedThisPress && flipping) view.TooEarly(Strings.T("mochi.splat"));
                    else if (missedThisPress || (!double.IsNaN(offset) && offset >= 0)) view.TooLate(Strings.T("mochi.too_slow"));
                    break;
            }
        }

        protected override void ClearField()
        {
            view.Clear();
            GameManager.EnsureInstance().Data.DiscoverMochi("bean");
        }

        // The whole run on the beat: the cakes pile into a mountain and Grandma dances over the result.
        protected override void OnRoundFinished(bool cleared)
        {
            bool perfectRun = cleared && Score.Perfect > 0 && Score.Good == 0 && Score.Miss == 0;
            if (perfectRun) view.Celebrate();
            string kinds = Strings.T("mochi.bean");
            var dex = GameManager.EnsureInstance().Data.Player.mochiDex;
            if (dex != null && dex.Contains("rainbow")) kinds += " → " + Strings.T("mochi.rainbow");
            AppendResult(kinds);
            if (perfectRun) AppendResult(Strings.T("mochi.pile"));
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnFieldTapped;
        }
    }
}

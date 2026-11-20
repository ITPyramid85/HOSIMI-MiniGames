using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 4. Logs ride the belt and are split on the beat they stop. A thick log takes the next
    // beat as well. Watermelons pay a bonus; stones must be left alone. A streak gilds the axe.
    public sealed class WoodController : MiniGameController
    {
        [SerializeField] private WoodView view;
        private int pieceId = -1;
        private bool ending, hideAxe;
        private float endAt = -1f;

        public override string GameId { get { return "wood"; } }
        protected override string TitleKey { get { return "wood.title"; } }
        protected override string[] PromptKeys { get { return new[] { "wood.prompt_what", "wood.prompt_when", "wood.prompt_twist" }; } }
        protected override string GameOverKey { get { return "wood.lose"; } }
        protected override double Bpm { get { return WoodChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return WoodChart.Pad; } }
        protected override bool AcceptsPad(int pad) { return pad == WoodChart.Pad; }
        protected override int[] PadsFor(int stage) { return WoodChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return WoodChart.Generate(seed); }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            view.StoneHit += OnStoneHit;
            base.Awake();
        }

        private void OnFieldTapped() { if (!ending) Press(WoodChart.Pad); }

        protected override bool DeferGameOver { get { return view != null && view.AwaitingStone; } }

        protected override void Update()
        {
            if (IsPaused) return;
            // The axe is flying off a stone. The belt waits, then the result opens once the fly has played.
            if (ending)
            {
                if (view != null) view.SetBeltRunning(false);
                if (State == MiniGameState.Playing && Score != null && Time.unscaledTime >= endAt)
                {
                    hideAxe = true;
                    Score.Defeat();
                    FinishRound();
                }
                return;
            }
            base.Update();
            if (view != null) view.SetBeltRunning(State == MiniGameState.Playing);
            // A stone chop spends a life before the blade lands. Finish once that chop has missed.
            if (Score != null && Score.IsOver && State == MiniGameState.Playing && (view == null || !view.AwaitingStone))
                FinishRound();
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            if (cue.Variant != pieceId)
            {
                pieceId = cue.Variant;
                bool thick = Thick(cue);
                view.Bring(cue, thick, secondsToBeat, thick ? (float)BeatSeconds + .5f : .5f);
            }
            // Wood and melons are chopped on the beat whether or not the player strikes. A stone is only
            // ever struck by the player, so the axe stays up unless a chop is asked for.
            if (cue.Kind == CueKind.Forbidden) view.SetChopWindow((float)(cue.GoodMs / 1000.0));
            else view.Raise(secondsToBeat, (float)(cue.GoodMs / 1000.0));
        }

        protected override void ShowOutcome(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    bool last = LastOf(result.Cue);
                    view.Chop(last, result.Cue.Kind == CueKind.Bonus);
                    view.SetGold(Score.Combo >= WoodChart.GoldAt);
                    break;
                case CueOutcome.Missed:
                    view.Fail();
                    int ruined = result.Cue.Variant;
                    Judge.Abandon(cue => cue.Variant == ruined);
                    break;
                case CueOutcome.ForbiddenHit:
                    view.CommitStoneChop();
                    break;
                case CueOutcome.ForbiddenAvoided:
                    view.Pass();
                    break;
                case CueOutcome.EmptyHit:
                    if (view.StoneAtBlade) view.CommitStoneChop();
                    else view.Whiff();
                    break;
            }
        }

        protected override void ClearField()
        {
            pieceId = -1;
            ending = false;
            endAt = -1f;
            bool showAxe = !hideAxe;
            hideAxe = false;
            if (view != null) view.Clear(showAxe);
        }

        private void OnStoneHit()
        {
            if (ending || State != MiniGameState.Playing) return;
            ending = true;
            endAt = Time.unscaledTime + .55f;
        }

        private bool Thick(BeatCue cue)
        {
            if (cue.Kind != CueKind.Normal) return false;
            int count = 0;
            foreach (BeatCue other in Judge.Cues) if (other.Variant == cue.Variant) count++;
            return count >= 2;
        }

        // The cue that just resolved is already closed, so none left means this was the last stroke.
        private bool LastOf(BeatCue cue)
        {
            foreach (BeatCue other in Judge.Cues)
                if (other.Variant == cue.Variant && !Judge.IsResolved(other)) return false;
            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnFieldTapped;
            if (view != null) view.StoneHit -= OnStoneHit;
        }
    }
}

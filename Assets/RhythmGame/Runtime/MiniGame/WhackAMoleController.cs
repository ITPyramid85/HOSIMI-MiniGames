using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Whack-a-mole (plan §4-02): one mole per beat from the hole matching a pad; every pad is judged,
    // so striking an empty hole is a miss. Round flow, scoring and sounds come from MiniGameController.
    public sealed class WhackAMoleController : MiniGameController
    {
        [SerializeField] private MoleHoleView[] holes;

        public override string GameId { get { return "whack"; } }
        protected override string TitleKey { get { return "whack.whack_a_mole"; } }
        protected override string[] PromptKeys
        {
            get { return new[] { "whack.strike_the_pad_where_a_mole_pops_up", "whack.moles_rise_half_a_beat_early_strike_at_t", "whack.gold_bonus_don_t_hit_the_flowers" }; }
        }
        protected override string GameOverKey { get { return "whack.the_moles_win_again"; } }
        protected override double Bpm { get { return WhackAMoleChart.Bpm; } }
        // The mole is on screen for the whole second it can be hit, starting as that window opens.
        protected override double LeadSeconds { get { return WhackAMoleChart.ShowSeconds * .5; } }
        protected override int[] PadsFor(int stage) { return WhackAMoleChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return WhackAMoleChart.Generate(seed); }

        protected override void Awake()
        {
            foreach (MoleHoleView hole in holes) hole.Pressed += Press;
            base.Awake();
        }

        // Moles peak on their beat.
        protected override void PresentCue(BeatCue cue, float secondsToBeat) { holes[cue.Pad].Rise(cue, secondsToBeat); }

        protected override void ShowOutcome(CueResult result)
        {
            MoleHoleView hole = holes[result.Pad];
            switch (result.Outcome)
            {
                case CueOutcome.Hit: hole.Hit(result.Grade == HitGrade.Perfect); break;
                case CueOutcome.Missed: hole.Escape(); break;
                case CueOutcome.ForbiddenHit: hole.ForbiddenHit(); break;
                case CueOutcome.ForbiddenAvoided: hole.Leave(); break;
                case CueOutcome.EmptyHit: hole.Thump(); break;
            }
        }

        protected override void ClearField() { foreach (MoleHoleView hole in holes) hole.Clear(); }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (MoleHoleView hole in holes) if (hole != null) hole.Pressed -= Press;
        }
    }
}

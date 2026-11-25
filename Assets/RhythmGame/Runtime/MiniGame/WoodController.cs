using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Game 4. Logs ride the belt and are split on the beat they stop. A thick log takes the next
    // beat as well. Watermelons pay a bonus. A stone costs half a heart and the axe stays put.
    // The run lasts two minutes with no stage change, or ends sooner when the hearts are gone.
    public sealed class WoodController : MiniGameController
    {
        // A piece sits on the board under the blade for a while after its beat. A chop that lands on it
        // there is still that blow, graded by how far it was from the beat; a log that leaves untouched is missed.
        private const double StayAfterBeat = .45;
        [SerializeField] private WoodView view;
        private int pieceId = -1;
        private bool hideAxe;
        private sealed class Open { public BeatCue Cue; public double Until; }
        private readonly List<Open> open = new List<Open>();
        // Cues already scored as a chop. The open tail must not judge them again as a miss.
        private readonly HashSet<int> landed = new HashSet<int>();
        private double heardOffset;
        private bool choppedThisFrame;

        public override string GameId { get { return "wood"; } }
        protected override string TitleKey { get { return "wood.title"; } }
        protected override string[] PromptKeys { get { return new[] { "wood.prompt_what", "wood.prompt_when", "wood.prompt_twist" }; } }
        protected override string GameOverKey { get { return "wood.lose"; } }
        protected override double Bpm { get { return WoodChart.Bpm; } }
        // Each piece rides the whole belt in three beats. That ride must not outlast WoodChart.GapBeats.
        protected override double LeadSeconds { get { return BeatSeconds * 3; } }
        protected override int DefaultPad { get { return WoodChart.Pad; } }
        protected override RectTransform FlashTarget(int pad) { return view != null ? view.StrikePoint : null; }
        protected override bool AcceptsPad(int pad) { return pad == WoodChart.Pad; }
        protected override int[] PadsFor(int stage) { return WoodChart.Pads(stage); }
        // Two minutes, then the result. The shared clock would print STAGE 2 at 45s; this game never does.
        protected override double ClearAtSeconds { get { return WoodChart.RunSeconds; } }
        protected override int DisplayStage { get { return 1; } }
        protected override int RecordedStage { get { return 1; } }
        protected override int RewardStage { get { return 1; } }
        protected override double StageProgress { get { return System.Math.Min(1, PlayedSeconds / ClearAtSeconds); } }
        // A piece is kept whole or dropped whole, and only if it has left the board before the run ends.
        protected override List<BeatCue> Generate(int seed)
        {
            List<BeatCue> cues = WoodChart.Generate(seed);
            var late = new HashSet<int>();
            foreach (BeatCue cue in cues) if (cue.Time + 1.0 > ClearAtSeconds) late.Add(cue.Variant);
            cues.RemoveAll(cue => late.Contains(cue.Variant));
            return cues;
        }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            base.Awake();
        }

        private void OnFieldTapped() { NoteScreenTouch(); Press(WoodChart.Pad); }

        protected override bool DeferGameOver { get { return view != null && view.AwaitingStone; } }

        protected override void Update()
        {
            if (IsPaused) return;
            choppedThisFrame = false;
            base.Update();
            if (State == MiniGameState.Playing) ExpireOpen();
            if (view != null) view.SetBeltRunning(State == MiniGameState.Playing);
            // The last half heart can be lost while the blade is still coming down. Finish once that chop has landed.
            if (Score != null && Score.IsOver && State == MiniGameState.Playing && (view == null || !view.AwaitingStone))
                FinishRound();
        }

        // The shared judge never scores wood. A chop during the downswing or while the piece is still
        // on the board is the hit; the empty strike that follows must not overwrite it with MISS.
        protected override void OnAcceptedPad(int pad)
        {
            if (pad == WoodChart.Pad) Claim(pad);
        }

        // The earliest stroke still on the board takes the chop. A thick log's first beat used to
        // close early and hand that chop to the second beat, then the first beat came back as a miss.
        private bool Claim(int pad)
        {
            if (Judge == null) return false;
            double heard = Heard();
            int best = -1;
            double bestTime = double.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                Open o = open[i];
                if (landed.Contains(o.Cue.Index)) continue;
                double good = Window(o.Cue.GoodMs, .17);
                bool follows = EarlierStrokesLanded(o.Cue);
                if ((!follows && heard < o.Cue.Time - good) || heard >= o.Until) continue;
                if (o.Cue.Time < bestTime) { best = i; bestTime = o.Cue.Time; }
            }
            if (best < 0) return false;
            Open hit = open[best];
            open.RemoveAt(best);
            Seal(hit.Cue);
            choppedThisFrame = true;
            Judge.Abandon(c => c == hit.Cue);
            double perfect = Window(hit.Cue.PerfectMs, .08);
            double offsetMs = (heard - hit.Cue.Time) * 1000;
            restrike = heard > hit.Cue.Time + .02;
            Report(new CueResult
            {
                Cue = hit.Cue, Pad = pad, Outcome = CueOutcome.Hit,
                Grade = System.Math.Abs(offsetMs) <= perfect * 1000 ? HitGrade.Perfect : HitGrade.Good,
                OffsetMs = offsetMs
            });
            return true;
        }

        // A piece that leaves the board unchopped is the only miss.
        private void ExpireOpen()
        {
            if (Judge == null) return;
            double heard = Heard();
            for (int i = 0; i < open.Count; i++)
            {
                Open o = open[i];
                if (heard < o.Until) continue;
                open.RemoveAt(i--);
                if (landed.Contains(o.Cue.Index) || PieceComplete(o.Cue.Variant)) continue;
                Report(new CueResult { Cue = o.Cue, Pad = o.Cue.Pad, Outcome = CueOutcome.Missed, Grade = HitGrade.Miss, OffsetMs = double.NaN });
                if (State != MiniGameState.Playing) return;
            }
        }

        private double Heard() { return SongTime - heardOffset; }
        private static double Window(double milliseconds, double fallback) { return (milliseconds > 0 ? milliseconds : fallback * 1000) / 1000; }

        // A chop this frame already has its grade. The empty strike behind it, and a miss of a log
        // whose strokes have all landed, must not replace that grade or take a heart.
        protected override CueResult Reinterpret(CueResult result)
        {
            bool miss = result.Outcome == CueOutcome.EmptyHit || result.Outcome == CueOutcome.Missed;
            bool settled = result.Cue != null && (landed.Contains(result.Cue.Index) || PieceComplete(result.Cue.Variant));
            if (miss && (result.Outcome == CueOutcome.EmptyHit ? choppedThisFrame : settled))
            {
                return new CueResult { Cue = result.Cue, Pad = result.Pad, Outcome = CueOutcome.ForbiddenAvoided, Grade = HitGrade.Perfect, OffsetMs = double.NaN };
            }
            return result;
        }

        private bool restrike;

        // Each stroke keeps its own beat. Cutting the first beat off early made the second chop
        // steal it, and the first beat then scored as a miss.
        private double OpenUntil(BeatCue cue)
        {
            return cue.Time + StayAfterBeat;
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            heardOffset = SongTime - (cue.Time - secondsToBeat);
            if (cue.Kind != CueKind.Forbidden)
            {
                open.Add(new Open { Cue = cue, Until = OpenUntil(cue) });
                // The shared judge's short window was calling a good downswing a miss. Wood scores the chop itself.
                if (Judge != null) Judge.Abandon(c => c == cue);
            }
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
                    Seal(result.Cue);
                    bool last = LastOf(result.Cue);
                    // A late chop: the scheduled stroke already rests on the piece, so the axe strikes again.
                    view.Chop(last, result.Cue.Kind == CueKind.Bonus, restrike);
                    restrike = false;
                    view.SetGold(Score.Combo >= WoodChart.GoldAt);
                    break;
                case CueOutcome.Missed:
                    view.Fail();
                    if (result.Cue != null) DropPiece(result.Cue.Variant);
                    break;
                case CueOutcome.ForbiddenHit:
                    view.CommitStoneChop();
                    break;
                case CueOutcome.ForbiddenAvoided:
                    if (result.Cue != null && result.Cue.Kind == CueKind.Forbidden) view.Pass();
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
            open.Clear();
            landed.Clear();
            restrike = false;
            bool showAxe = !hideAxe;
            hideAxe = false;
            if (view != null) view.Clear(showAxe);
        }

        // One chop settles that cue. The log splits only once every stroke of it has landed.
        private void Seal(BeatCue cue)
        {
            if (cue == null) return;
            landed.Add(cue.Index);
            DropSealed();
        }

        private void DropSealed()
        {
            for (int i = open.Count - 1; i >= 0; i--)
                if (landed.Contains(open[i].Cue.Index)) open.RemoveAt(i);
        }

        // One missed stroke spends the rest of that log, so a thick log does not cost a heart per beat.
        private void DropPiece(int variant)
        {
            if (Judge != null) Judge.Abandon(cue => cue.Variant == variant);
            for (int i = open.Count - 1; i >= 0; i--)
                if (open[i].Cue.Variant == variant) open.RemoveAt(i);
        }

        private bool Thick(BeatCue cue)
        {
            if (cue.Kind != CueKind.Normal) return false;
            int count = 0;
            foreach (BeatCue other in Judge.Cues) if (other.Variant == cue.Variant) count++;
            return count >= 2;
        }

        // Abandoned cues are not chops. The log splits only after every stroke of it has landed.
        private bool LastOf(BeatCue cue)
        {
            return cue != null && PieceComplete(cue.Variant);
        }

        // A thick log's second blow follows the first straight away: players chop twice in quick
        // succession, well before that stroke's own window. Once the first blow has landed, the next press is it.
        private bool EarlierStrokesLanded(BeatCue cue)
        {
            if (Judge == null) return false;
            bool any = false;
            foreach (BeatCue other in Judge.Cues)
            {
                if (other.Variant != cue.Variant || other.Time >= cue.Time) continue;
                any = true;
                if (!landed.Contains(other.Index)) return false;
            }
            return any;
        }

        private bool PieceComplete(int variant)
        {
            if (Judge == null) return false;
            bool any = false;
            foreach (BeatCue other in Judge.Cues)
            {
                if (other.Variant != variant) continue;
                any = true;
                if (!landed.Contains(other.Index)) return false;
            }
            return any;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnFieldTapped;
        }
    }
}

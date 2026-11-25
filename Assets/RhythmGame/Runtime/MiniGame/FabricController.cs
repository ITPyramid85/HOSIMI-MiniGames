using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Game 7. Pad 4 (S, tom 1) is the left club. Pad 5 (D, tom 2) is the right club.
    // They alternate, and the gaps get shorter. The same hand twice throws the club and Mother stares.
    public sealed class FabricController : MiniGameController
    {
        [SerializeField] private FabricView view;
        [SerializeField] private AudioClip tomLeft, tomRight, whooshSound, coughSound, spreadSound;
        // A club holds up past its beat, and the blow stays open for exactly as long as it is up, so the
        // raised club is always the key that scores. The hold is cut short when this hand's next blow is
        // close, so the head is always back on the cloth before it rises again.
        private const float HoldSeconds = .22f, LeastHoldSeconds = .06f, HoldMargin = .02f;
        private int lastPad = -1;
        // The blow each raised club is waiting for, how long it stays up, and whether this game has
        // taken the cue off the shared judge so the narrower stage window cannot miss it early.
        private BeatCue leftCue, rightCue;
        private float leftWait, rightWait, leftHold, rightHold;
        private float leftSwing = FabricView.SwingSeconds, rightSwing = FabricView.SwingSeconds;
        private bool leftTaken, rightTaken;

        public override string GameId { get { return "laundry"; } }
        protected override string TitleKey { get { return "laundry.title"; } }
        protected override string[] PromptKeys { get { return new[] { "laundry.prompt_what", "laundry.prompt_when", "laundry.prompt_twist" }; } }
        protected override string GameOverKey { get { return "laundry.lose"; } }
        protected override double Bpm { get { return FabricChart.Bpm; } }
        // Half a beat, the shortest gap. The next club rises then, and the other stays on the cloth.
        protected override double LeadSeconds { get { return BeatSeconds * .5; } }
        protected override int[] PadsFor(int stage) { return FabricChart.Pads(stage); }
        // The first two blows come right as GO clears, before a hand can answer. Those beats pass unjudged;
        // the run is scored from the third blow, which is a left one, so the hands still alternate.
        private const int SkippedBlows = 2;
        protected override List<BeatCue> Generate(int seed)
        {
            List<BeatCue> cues = FabricChart.Generate(seed);
            cues.RemoveRange(0, System.Math.Min(SkippedBlows, cues.Count));
            for (int i = 0; i < cues.Count; i++) cues[i].Index = i;
            return cues;
        }
        protected override bool AcceptsPad(int pad) { return pad == FabricChart.LeftPad || pad == FabricChart.RightPad; }
        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Club(pad == FabricChart.LeftPad) : null; }
        protected override bool PlaysJudgementAudio { get { return false; } }
        // Two minutes, then the result. The shared clock would print STAGE 2 at 45s; this game never does.
        protected override double ClearAtSeconds { get { return FabricChart.RunSeconds; } }
        protected override int DisplayStage { get { return 1; } }
        protected override int RecordedStage { get { return 1; } }
        protected override int RewardStage { get { return 1; } }
        protected override double StageProgress
        {
            get { return System.Math.Min(1, PlayedSeconds / ClearAtSeconds); }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureSounds();
            BindPads();
        }

        // The holds run down before the base reads the keys and settles the chart, so a club is never
        // missed by the judge while it is still standing up.
        protected override void Update()
        {
            if (!IsPaused && State == MiniGameState.Playing)
            {
                Hold(ref leftCue, ref leftWait, leftHold, ref leftTaken);
                Hold(ref rightCue, ref rightWait, rightHold, ref rightTaken);
            }
            base.Update();
            if (IsPaused || music == null) return;
            // The loop itself does not speed up, so the playback does, a little more every moment of the run.
            double along = State == MiniGameState.Playing
                ? System.Math.Max(0, System.Math.Min(1, PlayedSeconds / ClearAtSeconds))
                : 0;
            music.pitch = (float)(1 + .6 * along);
        }

        protected override void OnAcceptedPad(int pad)
        {
            if (view == null) return;
            bool left = pad == FabricChart.LeftPad;
            // The club for this key is up: this press is its blow, so it swings down instead of flying off.
            bool waiting = (left ? leftCue : rightCue) != null;
            bool repeat = !waiting && pad == lastPad;
            // A throw spends the pair. The next press of this key strikes, and the one after throws again.
            // A blow is never half of a pair, so a second tap right after one cannot throw the club.
            lastPad = repeat || waiting ? -1 : pad;
            Play(left ? tomLeft : tomRight);
            if (repeat)
            {
                view.Fling(left);
                view.Stare();
                Play(whooshSound);
                Play(coughSound);
            }
            else view.Strike(left, left ? leftSwing : rightSwing);
        }

        // The hand for this cue leaves the cloth and holds up there, waiting for its key.
        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            bool left = cue.Pad == FabricChart.LeftPad;
            float hold, swing;
            Plan(cue, out hold, out swing);
            if (left) { leftCue = cue; leftWait = secondsToBeat + hold; leftHold = hold; leftSwing = swing; leftTaken = false; }
            else { rightCue = cue; rightWait = secondsToBeat + hold; rightHold = hold; rightSwing = swing; rightTaken = false; }
            if (view == null) return;
            view.Raise(left, secondsToBeat);
        }

        // Shares out the room between this blow and the next one for the same hand: the club stands up
        // for the hold, with the blow open the whole time, and the swing has to land before it rises again.
        private void Plan(BeatCue cue, out float hold, out float swing)
        {
            double room = 10;
            List<BeatCue> chart = Chart;
            int from = chart == null ? -1 : chart.IndexOf(cue);
            for (int i = from + 1; from >= 0 && i < chart.Count; i++)
            {
                if (chart[i].Pad != cue.Pad) continue;
                room = chart[i].Time - LeadSeconds - cue.Time;
                break;
            }
            hold = (float)System.Math.Max(LeastHoldSeconds, System.Math.Min(HoldSeconds, room * .45));
            swing = (float)System.Math.Max(FabricView.LeastSwingSeconds,
                System.Math.Min(FabricView.SwingSeconds, room - hold - HoldMargin));
        }

        // One club's hold. On its beat the cue is taken off the judge, so the blow stays open for the
        // rest of the hold; when the hold runs out the blow is missed and the club drops to the cloth.
        private void Hold(ref BeatCue cue, ref float wait, float hold, ref bool taken)
        {
            if (cue == null) return;
            if (Judge == null || (!taken && Judge.IsResolved(cue))) { cue = null; taken = false; return; }
            wait -= Time.unscaledDeltaTime;
            if (!taken && wait <= hold)
            {
                taken = true;
                BeatCue held = cue;
                Judge.Abandon(c => c == held);
            }
            if (wait > 0) return;
            BeatCue missed = cue;
            cue = null;
            taken = false;
            Report(new CueResult { Cue = missed, Pad = missed.Pad, Outcome = CueOutcome.Missed, Grade = HitGrade.Miss, OffsetMs = double.NaN });
        }

        // The club is up well before the judge's window opens and stays up past where it closes, so
        // the judge calls those presses empty. While a club is up, its key is that blow.
        protected override CueResult Reinterpret(CueResult result)
        {
            if (result.Outcome != CueOutcome.EmptyHit) return result;
            bool left = result.Pad == FabricChart.LeftPad;
            BeatCue cue = left ? leftCue : rightCue;
            if (cue == null) return result;
            double offsetMs = ((left ? leftHold - leftWait : rightHold - rightWait)) * 1000;
            if (left) { leftCue = null; leftTaken = false; } else { rightCue = null; rightTaken = false; }
            Judge.Abandon(c => c == cue);
            double perfect = cue.PerfectMs > 0 ? cue.PerfectMs : 80;
            return new CueResult
            {
                Cue = cue, Pad = result.Pad, Outcome = CueOutcome.Hit,
                Grade = System.Math.Abs(offsetMs) <= perfect ? HitGrade.Perfect : HitGrade.Good,
                OffsetMs = offsetMs
            };
        }

        protected override void ShowOutcome(CueResult result)
        {
            if (result.Cue == null) return;
            bool left = result.Cue.Pad == FabricChart.LeftPad;
            // The club has answered this blow: it is back on the cloth and no longer the raised one.
            if (left) { if (result.Cue == leftCue) { leftCue = null; leftTaken = false; } }
            else { if (result.Cue == rightCue) { rightCue = null; rightTaken = false; } }
            if (view == null) return;
            if (result.Outcome == CueOutcome.Hit) view.Flatten();
            else if (result.Outcome == CueOutcome.Missed) view.Lower(left, left ? leftSwing : rightSwing);
        }

        protected override void ClearField()
        {
            lastPad = -1;
            leftCue = rightCue = null;
            leftWait = rightWait = leftHold = rightHold = 0;
            leftSwing = rightSwing = FabricView.SwingSeconds;
            leftTaken = rightTaken = false;
            if (view != null) view.Clear();
        }

        protected override void OnRoundFinished(bool cleared)
        {
            bool perfectRun = cleared && Score.Perfect > 0 && Score.Good == 0 && Score.Miss == 0;
            if (!perfectRun || view == null) return;
            view.Celebrate();
            Play(spreadSound);
            AppendResult(Strings.T("laundry.spread"));
        }

        private void EnsureSounds()
        {
            if (tomLeft == null) tomLeft = Resources.Load<AudioClip>("RhythmGame/Audio/FabricTom1");
            if (tomRight == null) tomRight = Resources.Load<AudioClip>("RhythmGame/Audio/FabricTom2");
            if (whooshSound == null) whooshSound = Resources.Load<AudioClip>("RhythmGame/Audio/FabricWhoosh");
            if (coughSound == null) coughSound = Resources.Load<AudioClip>("RhythmGame/Audio/FabricCough");
            if (spreadSound == null) spreadSound = Resources.Load<AudioClip>("RhythmGame/Audio/FabricSpread");
        }

        // On-screen pads sit on the cloth. S and D are the same two pads.
        private void BindPads()
        {
            if (view == null) return;
            Transform field = view.transform.parent;
            if (field == null) return;
            Text sample = null;
            Transform key = field.Find("Key");
            if (key != null) sample = key.GetComponent<Text>();
            float y = -300f;
            PlacePad(field, "LeftPad", new Vector2(-150f, y), Strings.T("laundry.left") + "\n4 / S", Palette.Sun, FabricChart.LeftPad, sample);
            PlacePad(field, "RightPad", new Vector2(150f, y), Strings.T("laundry.right") + "\n5 / D", Palette.Sky, FabricChart.RightPad, sample);
        }

        private void PlacePad(Transform field, string name, Vector2 at, string caption, Color color, int pad, Text sample)
        {
            Transform hit = field.Find(name);
            RectTransform padRect;
            if (hit == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(field, false);
                padRect = go.GetComponent<RectTransform>();
            }
            else padRect = hit as RectTransform;
            padRect.anchorMin = padRect.anchorMax = new Vector2(.5f, .5f);
            padRect.pivot = new Vector2(.5f, .5f);
            padRect.anchoredPosition = at;
            padRect.sizeDelta = new Vector2(240f, 72f);
            Image image = padRect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            ClothPad button = padRect.GetComponent<ClothPad>();
            if (button == null) button = padRect.gameObject.AddComponent<ClothPad>();
            button.Bind(this, pad);

            Transform labelHit = field.Find(name + "Label");
            Text label;
            if (labelHit == null)
            {
                var textGo = new GameObject(name + "Label", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(field, false);
                label = textGo.GetComponent<Text>();
                if (sample != null)
                {
                    label.font = sample.font;
                    label.fontSize = sample.fontSize;
                }
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.color = Palette.Ink;
            }
            else label = labelHit.GetComponent<Text>();
            label.text = caption;
            label.raycastTarget = false;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = at;
            textRect.sizeDelta = new Vector2(240f, 72f);
            label.transform.SetAsLastSibling();
        }

        private sealed class ClothPad : MonoBehaviour, IPointerDownHandler
        {
            private FabricController owner;
            private int pad;

            public void Bind(FabricController owner, int pad)
            {
                this.owner = owner;
                this.pad = pad;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
                owner.NoteScreenTouch();
                owner.Press(pad);
            }
        }
    }
}

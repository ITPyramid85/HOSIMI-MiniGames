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
        private int lastPad = -1;

        public override string GameId { get { return "laundry"; } }
        protected override string TitleKey { get { return "laundry.title"; } }
        protected override string[] PromptKeys { get { return new[] { "laundry.prompt_what", "laundry.prompt_when", "laundry.prompt_twist" }; } }
        protected override string GameOverKey { get { return "laundry.lose"; } }
        protected override double Bpm { get { return FabricChart.Bpm; } }
        // Half a beat: the shortest gap. The club is up as the blow arrives.
        protected override double LeadSeconds { get { return BeatSeconds * .5; } }
        protected override int[] PadsFor(int stage) { return FabricChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return FabricChart.Generate(seed); }
        protected override bool AcceptsPad(int pad) { return pad == FabricChart.LeftPad || pad == FabricChart.RightPad; }
        protected override bool PlaysJudgementAudio { get { return false; } }

        protected override void Awake()
        {
            base.Awake();
            EnsureSounds();
            BindPads();
        }

        protected override void Update()
        {
            base.Update();
            if (IsPaused || music == null) return;
            // The loop itself does not speed up, so the playback does, a little more every moment of the run.
            double along = State == MiniGameState.Playing
                ? System.Math.Max(0, System.Math.Min(1, PlayedSeconds / StageTable.ClearSeconds))
                : 0;
            music.pitch = (float)(1 + .6 * along);
        }

        protected override void OnAcceptedPad(int pad)
        {
            if (view == null) return;
            bool left = pad == FabricChart.LeftPad;
            bool repeat = pad == lastPad;
            // A throw spends the pair. The next press of this key strikes, and the one after throws again.
            lastPad = repeat ? -1 : pad;
            // The other hand stays. A press never starts the opposite club.
            view.Plant(!left);
            Play(left ? tomLeft : tomRight);
            if (repeat)
            {
                view.Fling(left);
                view.Stare();
                Play(whooshSound);
                Play(coughSound);
            }
            else view.Strike(left);
        }

        // The beat no longer lifts the other club. Only the key for that hand moves it.
        protected override void PresentCue(BeatCue cue, float secondsToBeat) { }

        protected override void ShowOutcome(CueResult result)
        {
            if (view == null || result.Outcome != CueOutcome.Hit) return;
            view.Flatten();
        }

        protected override void ClearField()
        {
            lastPad = -1;
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
                owner.Press(pad);
            }
        }
    }
}

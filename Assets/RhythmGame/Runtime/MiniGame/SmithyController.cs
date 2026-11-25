using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Game 5. Hammering blows are pad 2. The last blow is crash 1.
    // The bellows in the music keep time. The iron turning red is the only cue.
    public sealed class SmithyController : MiniGameController
    {
        [SerializeField] private SmithyView view;
        [SerializeField] private AudioClip crashSound;
        private int heatId = -1, forged, armedPad = SmithChart.StackPad;
        private bool closing;
        private float finishAt = -1f;

        public override string GameId { get { return "smith"; } }
        protected override string TitleKey { get { return "smith.title"; } }
        protected override string[] PromptKeys { get { return new[] { "smith.prompt_what", "smith.prompt_when", "smith.prompt_twist" }; } }
        protected override string GameOverKey { get { return "smith.lose"; } }
        protected override double Bpm { get { return SmithChart.Bpm; } }
        protected override double LeadSeconds { get { return BeatSeconds * 2; } }
        protected override int DefaultPad { get { return armedPad; } }
        // The last heart, or a shattered bar, ends the run once the blow has played out. Surviving 8 minutes clears it.
        protected override bool DeferGameOver { get { return true; } }
        protected override bool AcceptsPad(int pad)
        {
            if (closing) return false;
            // Pad 2 is the hammer on every heat. Pad 1 is only the finishing blow.
            return pad == SmithChart.StackPad || pad == SmithChart.CrashPad;
        }

        protected override RectTransform FlashTarget(int pad) { return view != null ? view.Hammer : null; }
        protected override int[] PadsFor(int stage) { return SmithChart.Pads(stage); }
        protected override List<BeatCue> Generate(int seed) { return SmithChart.Generate(seed); }
        protected override bool PlaysJudgementAudio { get { return false; } }

        protected override void Awake()
        {
            view.Pressed += OnFieldTapped;
            base.Awake();
            BindHammerPad();
        }

        // Pad 2 taps. Pad 1 is the one heavy finishing blow.
        protected override void OnAcceptedPad(int pad)
        {
            if (view == null || closing) return;
            if (pad == SmithChart.CrashPad) view.FinishBlow();
            else if (pad == SmithChart.StackPad) view.Tap();
        }

        private void OnFieldTapped() { NoteScreenTouch(); if (!closing) Press(armedPad); }

        protected override void Update()
        {
            if (IsPaused) return;
            if (State == MiniGameState.Playing && !closing)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    double time = SongTime;
                    // The diagram's "2" is also the number key. E is the same pad.
                    if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                    {
                        NotePadInput();
                        PressAt(SmithChart.StackPad, time);
                    }
                    if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                    {
                        NotePadInput();
                        PressAt(SmithChart.CrashPad, time);
                    }
                }
            }
            base.Update();
            if (State != MiniGameState.Playing) return;
            if (!closing && Score != null && Score.IsOver)
            {
                closing = true;
                finishAt = Time.unscaledTime + .85f;
            }
            if (closing && Time.unscaledTime >= finishAt)
            {
                closing = false;
                FinishRound();
            }
        }

        protected override void PresentCue(BeatCue cue, float secondsToBeat)
        {
            if (closing || cue.Variant == heatId) return;
            heatId = cue.Variant;
            if (cue.Variant > 0 && cue.Variant % SmithChart.HeatCount == 0)
            {
                forged = 0;
                view.NewSword();
            }
            armedPad = cue.Pad;
            view.Arm();
            view.ShowCall(cue.Pad == SmithChart.CrashPad);
            view.Heat(secondsToBeat, (float)(cue.GoodMs / 1000.0));
        }

        protected override void ShowOutcome(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    forged++;
                    view.Forge(forged, result.Pad == SmithChart.CrashPad);
                    Play(result.Pad == SmithChart.CrashPad ? crashSound : StrikeClip);
                    break;
                case CueOutcome.EmptyHit:
                    if (result.Pad != SmithChart.StackPad && result.Pad != SmithChart.CrashPad) return;
                    // The other pad still rings and swings. Only this heat's pad can crack the iron.
                    Play(result.Pad == SmithChart.CrashPad ? crashSound : StrikeClip);
                    if (result.Pad != armedPad) return;
                    // A finished sword stays whole. Only the raw bar shatters once the glow is gone.
                    if (view.IsSword) return;
                    if (view.Cooling)
                    {
                        view.Shatter();
                        EndBecauseTheIronIsGone();
                        return;
                    }
                    view.Crack(Strings.T("smith.sigh"));
                    BeatCue open = ArmedHeat();
                    if (open == null) return;
                    Judge.Abandon(cue => cue.Variant == open.Variant);
                    break;
            }
        }

        protected override void ClearField()
        {
            heatId = -1;
            forged = 0;
            armedPad = SmithChart.StackPad;
            closing = false;
            finishAt = -1f;
            if (view != null) view.Clear();
        }

        // Only the heat on the anvil can be cracked. A late tap must not skip the next glow.
        private BeatCue ArmedHeat()
        {
            foreach (BeatCue cue in Judge.Cues)
                if (cue.Variant == heatId && !Judge.IsResolved(cue)) return cue;
            return null;
        }

        // The cooling blow threw the iron apart. Nothing is left on the anvil, so the run is lost.
        private void EndBecauseTheIronIsGone()
        {
            Score.Defeat();
            closing = true;
            if (finishAt < 0f) finishAt = Time.unscaledTime + .75f;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view != null) view.Pressed -= OnFieldTapped;
        }

        // On-screen pads: blue "1 / Q" is the finishing crash, orange "2 / E" is the hammering stack.
        private void BindHammerPad()
        {
            if (view == null) return;
            Transform field = view.transform.parent;
            Transform key = field.Find("Key");
            if (key == null) return;
            RectTransform labelRect = key as RectTransform;
            Text hammerLabel = key.GetComponent<Text>();
            float y = labelRect != null ? labelRect.anchoredPosition.y : -312f;
            PlacePad(field, "FinishPad", null, new Vector2(-140f, y), Strings.T("smith.finish") + "\n1 / Q", Palette.Sky, SmithChart.CrashPad, hammerLabel);
            PlacePad(field, "HammerPad", hammerLabel, new Vector2(140f, y), Strings.T("smith.tap") + "\n2 / E", Palette.Sun, SmithChart.StackPad, hammerLabel);
        }

        private void PlacePad(Transform field, string name, Text label, Vector2 at, string caption, Color color, int pad, Text sample)
        {
            Transform hit = field.Find(name);
            RectTransform padRect;
            if (hit == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(field, false);
                padRect = go.GetComponent<RectTransform>();
                padRect.anchorMin = padRect.anchorMax = new Vector2(.5f, .5f);
                padRect.pivot = new Vector2(.5f, .5f);
            }
            else padRect = hit as RectTransform;
            padRect.anchoredPosition = at;
            padRect.sizeDelta = new Vector2(240f, 88f);
            Image image = padRect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            HammerPad button = padRect.GetComponent<HammerPad>();
            if (button == null) button = padRect.gameObject.AddComponent<HammerPad>();
            button.Bind(this, pad);

            if (label == null)
            {
                Transform existing = field.Find(name + "Label");
                if (existing == null)
                {
                    var textGo = new GameObject(name + "Label", typeof(RectTransform), typeof(Text));
                    textGo.transform.SetParent(field, false);
                    label = textGo.GetComponent<Text>();
                    if (sample != null)
                    {
                        label.font = sample.font;
                        label.fontSize = sample.fontSize;
                        label.color = sample.color;
                    }
                    label.alignment = TextAnchor.MiddleCenter;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                }
                else label = existing.GetComponent<Text>();
            }
            label.text = caption;
            label.raycastTarget = false;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = at;
            textRect.sizeDelta = new Vector2(240f, 88f);
            label.transform.SetAsLastSibling();
        }

        private sealed class HammerPad : MonoBehaviour, IPointerDownHandler
        {
            private SmithyController owner;
            private int pad;

            public void Bind(SmithyController owner, int pad)
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

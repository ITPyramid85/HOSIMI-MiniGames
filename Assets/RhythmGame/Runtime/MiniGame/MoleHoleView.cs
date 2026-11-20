using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // One hole = one pad position. Presentation only: the controller decides outcomes and tells the
    // hole what to show. Touching the hole counts as striking its pad.
    public sealed class MoleHoleView : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private int pad;
        [SerializeField] private RectTransform mole;
        [SerializeField] private Image moleImage, holeImage;
        [SerializeField] private Image[] sparks;
        [SerializeField] private Text bubble;
        [SerializeField] private Sprite normal, gold, flower, tongue;
        [SerializeField] private Sprite[] disguises;
        private static readonly Color Flash = Palette.Coral;
        private Color holeColor;
        private float hiddenY, shownY;
        private Coroutine motion, punch;
        public int Pad { get { return pad; } }
        public BeatCue Current { get; private set; }
        public event Action<int> Pressed;

        private void Awake()
        {
            holeColor = holeImage.color;
            shownY = 0;
            hiddenY = -mole.rect.height;
            Clear();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Pressed != null) Pressed(pad);
        }

        public void Clear()
        {
            StopAllCoroutines();
            motion = punch = null;
            Current = null;
            SetMoleY(hiddenY);
            mole.localScale = Vector3.one;
            transform.localScale = Vector3.one;
            holeImage.color = holeColor;
            bubble.text = "";
            foreach (Image spark in sparks) spark.gameObject.SetActive(false);
        }

        // Starts emerging so the mole peaks exactly on the beat, `seconds` from now.
        public void Rise(BeatCue cue, float seconds)
        {
            Current = cue;
            moleImage.sprite = cue.Kind == CueKind.Bonus ? gold : cue.Kind == CueKind.Forbidden ? flower :
                cue.Variant > 0 && cue.Variant <= disguises.Length ? disguises[cue.Variant - 1] : normal;
            mole.localScale = Vector3.one;
            bubble.text = "";
            float from = mole.anchoredPosition.y;
            // Pop up quickly, then stay until the hit window closes and the controller sends it down.
            float pop = Mathf.Min(.15f, Mathf.Max(.04f, seconds));
            Play(PixelTween.Run(pop, t => SetMoleY(Mathf.LerpUnclamped(from, shownY, PixelTween.OutBack(t)))));
        }

        public void Hit(bool perfect)
        {
            Current = null;
            Sparks(perfect ? 6 : 4);
            Punch(perfect ? .1f : .06f);
            Play(Squash());
        }

        public void Escape()
        {
            Current = null;
            moleImage.sprite = tongue;
            Play(Taunt(Strings.T("whack.nyah")));
        }

        public void ForbiddenHit()
        {
            Current = null;
            Punch(-.08f);
            Play(Warn());
        }

        public void Leave()
        {
            Current = null;
            Play(Sink(.2f));
        }

        // A strike on an empty hole: a small knock so the touch still feels answered.
        public void Thump() { Punch(-.05f); }

        private IEnumerator Squash()
        {
            yield return PixelTween.Run(.1f, t => mole.localScale = new Vector3(
                PixelTween.Steps(Mathf.Lerp(1.35f, 1.2f, t), 20), PixelTween.Steps(Mathf.Lerp(.45f, .55f, t), 20), 1));
            yield return Sink(.1f);
            mole.localScale = Vector3.one;
        }

        private IEnumerator Taunt(string text)
        {
            bubble.text = text;
            yield return new WaitForSecondsRealtime(.35f);
            bubble.text = "";
            yield return Sink(.15f);
        }

        private IEnumerator Warn()
        {
            bubble.text = Strings.T("whack.beep");
            for (int i = 0; i < 3; i++)
            {
                holeImage.color = Flash; yield return new WaitForSecondsRealtime(.06f);
                holeImage.color = holeColor; yield return new WaitForSecondsRealtime(.06f);
            }
            bubble.text = "";
            yield return Sink(.12f);
        }

        private IEnumerator Sink(float seconds)
        {
            float from = mole.anchoredPosition.y;
            yield return PixelTween.Run(seconds, t => SetMoleY(Mathf.Lerp(from, hiddenY, PixelTween.InCubic(t))));
        }

        // Pixel stars fly outward in a ring and fade in steps.
        private void Sparks(int count)
        {
            for (int i = 0; i < sparks.Length; i++)
            {
                Image spark = sparks[i];
                bool on = i < count;
                spark.gameObject.SetActive(on);
                if (!on) continue;
                float angle = (90 + 360f * i / count) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StartCoroutine(PixelTween.Run(.28f, t =>
                {
                    spark.rectTransform.anchoredPosition = PixelTween.Snap(direction * Mathf.Lerp(16, 76, PixelTween.OutCubic(t)));
                    Color c = spark.color; c.a = 1 - PixelTween.Steps(t, 4); spark.color = c;
                    if (t >= 1) spark.gameObject.SetActive(false);
                }));
            }
        }

        private void Punch(float amount)
        {
            if (punch != null) StopCoroutine(punch);
            punch = StartCoroutine(PixelTween.Punch(transform, amount));
        }

        private void Play(IEnumerator routine)
        {
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(routine);
        }

        private void SetMoleY(float y) { mole.anchoredPosition = new Vector2(0, PixelTween.Snap(y)); }
    }
}

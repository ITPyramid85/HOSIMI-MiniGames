using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Game scene presentation: shutter reveal, HUD columns sliding in, the track unfolding, and
    // punches when HUD texts change. It only reads what GameController writes, never game state.
    public sealed class GameUIAnimator : MonoBehaviour
    {
        private const float GridSpeed = 8f, GridTile = 64f;
        [SerializeField] private RectTransform[] wipeBars;
        [SerializeField] private RawImage grid;
        [SerializeField] private RectTransform[] leftColumn, rightColumn;
        [SerializeField] private RectTransform track;
        [SerializeField] private Text judgement, countdown, combo, score;
        private readonly Dictionary<Text, string> lastText = new Dictionary<Text, string>();
        private readonly Dictionary<Transform, Coroutine> punches = new Dictionary<Transform, Coroutine>();
        private float startedAt;
        private bool exiting;

        private void Start()
        {
            startedAt = Time.unscaledTime;
            StartCoroutine(PixelTween.Blinds(this, wipeBars, false));
            SlideIn(leftColumn, -640, .1f);
            SlideIn(rightColumn, 640, .16f);
            StartCoroutine(PixelTween.Run(.36f, t => track.localScale =
                new Vector3(1, PixelTween.Steps(Mathf.LerpUnclamped(.04f, 1, PixelTween.OutBack(t)), 24), 1), .2f));
            foreach (Text text in new[] { judgement, countdown, combo, score }) if (text != null) lastText[text] = text.text;
        }

        private void SlideIn(RectTransform[] items, float offsetX, float delay)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Vector2 home = items[i].anchoredPosition;
                StartCoroutine(PixelTween.Move(items[i], home + new Vector2(offsetX, 0), home, .4f, PixelTween.OutCubic, delay + i * .03f));
            }
        }

        public void PlayExit(Action done)
        {
            if (exiting) return;
            exiting = true;
            StartCoroutine(Exit(done));
        }

        private IEnumerator Exit(Action done)
        {
            yield return PixelTween.Blinds(this, wipeBars, true);
            done();
        }

        private void Update()
        {
            PixelTween.ScrollGrid(grid, Time.unscaledTime - startedAt, GridSpeed, GridTile);
            PunchOnChange(judgement, .45f);
            PunchOnChange(countdown, .6f);
            PunchOnChange(combo, .2f);
            PunchOnChange(score, .08f);
        }

        // Pops a label whenever its controller-written text changes to something non-empty.
        // A null label is left to its own controller (e.g. combo milestones in mini-games).
        private void PunchOnChange(Text text, float amount)
        {
            if (text == null) return;
            string value = text.text, previous;
            if (lastText.TryGetValue(text, out previous) && previous == value) return;
            lastText[text] = value;
            if (string.IsNullOrEmpty(value)) return;
            Coroutine running;
            if (punches.TryGetValue(text.transform, out running) && running != null) StopCoroutine(running);
            punches[text.transform] = StartCoroutine(PixelTween.Punch(text.transform, amount));
        }
    }
}

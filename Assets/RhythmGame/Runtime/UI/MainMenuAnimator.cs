using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Main menu motion: staggered entrances, then idle loops locked to the menu BGM's beat
    // (client reply v5 §4-3): the beat bars jump and the guide character bobs on it.
    // Purely presentational: buttons stay clickable during every animation.
    public sealed class MainMenuAnimator : MonoBehaviour
    {
        private const float Bpm = 120;
        private const float GridSpeed = 12f;          // reference units per second
        private const float GridTile = 64f;           // one grid cell, see the grid texture
        [SerializeField] private RectTransform[] wipeBars;
        [SerializeField] private RawImage grid;
        [SerializeField] private RectTransform logo, guide, bubble;
        [SerializeField] private RectTransform[] beatBars;
        [SerializeField] private RectTransform[] buttons;
        [SerializeField] private AudioSource music;
        private Vector2 logoBase, guideBase;
        private Vector2[] buttonBase;
        private float[] barHeights;
        private float introStarted;
        private bool introDone, exiting;

        private void Awake()
        {
            logoBase = logo.anchoredPosition;
            guideBase = guide.anchoredPosition;
            buttonBase = Array.ConvertAll(buttons, b => b.anchoredPosition);
            barHeights = Array.ConvertAll(beatBars, b => b.sizeDelta.y);
            HideWipe();
        }

        private void Start() { StartCoroutine(Intro()); }

        private IEnumerator Intro()
        {
            introStarted = Time.unscaledTime;
            StartCoroutine(PixelTween.Move(logo, logoBase + new Vector2(0, 160), logoBase, .6f, PixelTween.OutBounce, .1f));
            StartCoroutine(PixelTween.Move(guide, guideBase - new Vector2(0, 260), guideBase, .6f, PixelTween.OutBack, .25f));
            StartCoroutine(PixelTween.Punch(bubble, .2f, .3f));
            for (int i = 0; i < buttons.Length; i++)
                StartCoroutine(PixelTween.Move(buttons[i], buttonBase[i] + new Vector2(560, 0), buttonBase[i], .45f, PixelTween.OutBack, .3f + i * .06f));
            yield return new WaitForSecondsRealtime(1f);
            introDone = true;
        }

        public void PlayExit(Action done)
        {
            if (exiting) return;
            exiting = true;
            HideWipe();
            if (done != null) done();
        }

        private void HideWipe()
        {
            if (wipeBars == null) return;
            foreach (RectTransform bar in wipeBars)
                if (bar != null) bar.gameObject.SetActive(false);
        }

        // Beat phase from the music itself when it plays, so the bars stay on the BGM's beat.
        private float BeatPhase()
        {
            float beat = 60 / Bpm;
            float time = music != null && music.isPlaying ? music.time : Time.unscaledTime - introStarted;
            return Mathf.Repeat(time, beat) / beat;
        }

        private void Update()
        {
            float time = Time.unscaledTime - introStarted;
            float kick = 1 - PixelTween.OutCubic(Mathf.Clamp01(BeatPhase() / .4f));   // 1 on the beat, decays fast
            PixelTween.ScrollGrid(grid, time, GridSpeed, GridTile);

            // Equalizer: noise per bar, quantized to two-pixel blocks, punched up on each beat.
            float grow = Mathf.Clamp01((time - .4f) / .4f);
            for (int i = 0; i < beatBars.Length; i++)
            {
                float noise = Mathf.PerlinNoise(i * .31f, time * 1.3f);
                float height = barHeights[i] * grow * (.25f + .45f * noise + .5f * kick * (i % 2 == 0 ? 1 : .7f));
                Vector2 size = beatBars[i].sizeDelta;
                beatBars[i].sizeDelta = new Vector2(size.x, Mathf.Max(PixelTween.Pixel, PixelTween.Snap(height, PixelTween.Pixel * 2)));
            }

            if (!introDone) return;
            guide.anchoredPosition = guideBase + new Vector2(0, PixelTween.Snap(12 * kick));
            logo.anchoredPosition = logoBase + new Vector2(0, PixelTween.Snap(4 * kick));
        }
    }
}

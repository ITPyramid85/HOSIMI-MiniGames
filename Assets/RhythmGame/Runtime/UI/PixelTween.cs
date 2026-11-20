using System;
using System.Collections;
using UnityEngine;

namespace RhythmGame
{
    // Interpolated tweens with pixel-art output: values are eased smoothly, then snapped to the
    // art grid (positions), or to a few discrete steps (scale, alpha) so motion reads as chunky pixels.
    // All tweens run on unscaled time so they keep playing while the game is paused.
    public static class PixelTween
    {
        // One art pixel in 1280x720 reference units; matches the 4x upscaled frame sprites.
        public const float Pixel = 4f;

        public static float Snap(float value, float step = Pixel) { return Mathf.Round(value / step) * step; }
        public static Vector2 Snap(Vector2 value, float step = Pixel) { return new Vector2(Snap(value.x, step), Snap(value.y, step)); }
        public static float Steps(float t, int steps) { return Mathf.Round(t * steps) / steps; }

        public static float OutCubic(float t) { t = 1 - t; return 1 - t * t * t; }
        public static float InCubic(float t) { return t * t * t; }
        public static float InOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1) / 2; }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            t -= 1;
            return 1 + c3 * t * t * t + c1 * t * t;
        }

        public static float OutBounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1 / d) return n * t * t;
            if (t < 2 / d) { t -= 1.5f / d; return n * t * t + .75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + .9375f; }
            t -= 2.625f / d; return n * t * t + .984375f;
        }

        // Calls step with linear progress 0..1 every frame, always ending on exactly 1.
        public static IEnumerator Run(float duration, Action<float> step, float delay = 0)
        {
            if (delay > 0)
            {
                step(0);
                for (float waited = 0; waited < delay; waited += Time.unscaledDeltaTime) yield return null;
            }
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            step(1);
        }

        public static IEnumerator Move(RectTransform target, Vector2 from, Vector2 to, float duration, Func<float, float> ease, float delay = 0)
        {
            return Run(duration, t => target.anchoredPosition = Snap(Vector2.LerpUnclamped(from, to, ease(t))), delay);
        }

        public static IEnumerator Fade(UnityEngine.UI.Graphic target,float from, float to, float duration, float delay = 0, int steps = 4)
        {
            return Run(duration, t =>
            {
                Color color = target.color;
                color.a = Mathf.Lerp(from, to, Steps(t, steps));
                target.color = color;
            }, delay);
        }

        // Scale pop that settles back to 1, in a few discrete steps.
        public static IEnumerator Punch(Transform target, float amount, float duration = .18f)
        {
            return Run(duration, t => target.localScale = Vector3.one * (1 + amount * Steps(1 - OutCubic(t), 4)));
        }

        // Horizontal shutter bars (alternating anchor sides) that cover or reveal the screen.
        public static IEnumerator Blinds(MonoBehaviour host, RectTransform[] bars, bool cover)
        {
            const float duration = .22f, stagger = .025f;
            for (int i = 0; i < bars.Length; i++)
            {
                RectTransform bar = bars[i];
                bar.gameObject.SetActive(true);
                host.StartCoroutine(Run(duration, t =>
                {
                    float width = cover ? OutCubic(t) : 1 - OutCubic(t);
                    bar.localScale = new Vector3(Steps(width, 8), 1, 1);
                }, i * stagger));
            }
            for (float waited = 0; waited < duration + bars.Length * stagger; waited += Time.unscaledDeltaTime) yield return null;
            if (!cover) foreach (RectTransform bar in bars) bar.gameObject.SetActive(false);
        }

        // Diagonal background scroll that advances one art pixel at a time.
        public static void ScrollGrid(UnityEngine.UI.RawImage grid, float time, float speed, float tile)
        {
            float offset = Snap(time * speed) / tile;
            Rect uv = grid.uvRect; uv.x = offset; uv.y = offset; grid.uvRect = uv;
        }

        // Reveals text one character at a time; the full string stays the source of truth.
        public static IEnumerator Typewrite(UnityEngine.UI.Text target, float charactersPerSecond, float delay = 0)
        {
            string full = target.text;
            return Run(full.Length / charactersPerSecond, t => target.text = full.Substring(0, Mathf.FloorToInt(full.Length * t)), delay);
        }
    }
}

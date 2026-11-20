using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Night tunnel: a staggered brick wall whose color is the thickness, a hammer that swings on every press,
    // a crack that flashes just before the beat, and a wave that creeps in from the left.
    public sealed class WallView : MonoBehaviour
    {
        private static readonly Color Thin = new Color32(245, 212, 100, 255);
        private static readonly Color Mid = new Color32(240, 140, 64, 255);
        private static readonly Color Thick = new Color32(214, 84, 84, 255);
        private static readonly Color Chest = new Color32(232, 200, 106, 255);
        private const float RestAngle = 50f, HitAngle = -78f;
        private const float WaterSafe = 160f, WaterReach = 360f;
        // Heights are measured from the floor top, so the wave always sits on the slate.
        private const float WaterLow = 220f, WaterHigh = 380f;
        private const float DrownWidth = 1040f, DrownHeight = 480f;

        [SerializeField] private RectTransform wall, water, hero, fish, hammer;
        [SerializeField] private Image[] bricks;
        [SerializeField] private Image crack, chest;
        [SerializeField] private Text hits, meters;
        [SerializeField] private Image[] drums;
        private Vector3[] drumScale;
        private Vector2[] brickHome;
        private Vector2 wallHome, heroHome, fishHome;
        private Coroutine crackRoutine, motion, lunge, swing;
        private float waterWidth = WaterSafe, waterTarget = WaterSafe, waterHeight = WaterLow, waterHeightTarget = WaterLow;
        private bool swimming, homed, frozen;
        private float clock;

        public void Bind(WallController owner)
        {
            Home();
            if (drums == null) return;
            drumScale = new Vector3[drums.Length];
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                drumScale[i] = drums[i].rectTransform.localScale;
                WallPad pad = drums[i].GetComponent<WallPad>();
                if (pad == null) pad = drums[i].gameObject.AddComponent<WallPad>();
                pad.Bind(owner, i);
            }
        }

        private void Home()
        {
            if (homed) return;
            homed = true;
            if (wall != null) wallHome = wall.anchoredPosition;
            if (hero != null) heroHome = hero.anchoredPosition;
            if (fish != null) fishHome = fish.anchoredPosition;
            if (bricks == null) return;
            brickHome = new Vector2[bricks.Length];
            for (int i = 0; i < bricks.Length; i++) if (bricks[i] != null) brickHome[i] = bricks[i].rectTransform.anchoredPosition;
        }

        // The result screen should not keep bobbing the wave.
        public void Freeze()
        {
            frozen = true;
            StopAll();
            waterWidth = waterTarget;
            waterHeight = waterHeightTarget;
            ApplyWater(0f);
        }

        public void Reset()
        {
            Home();
            frozen = false;
            swimming = false;
            StopAll();
            if (hero != null)
            {
                hero.gameObject.SetActive(true);
                hero.anchoredPosition = heroHome;
            }
            if (fish != null)
            {
                fish.gameObject.SetActive(false);
                fish.anchoredPosition = fishHome;
            }
            if (hammer != null) hammer.localEulerAngles = new Vector3(0f, 0f, RestAngle);
            if (wall != null) wall.anchoredPosition = wallHome;
            RestoreBricks(Thin);
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0f);
            if (chest != null) chest.gameObject.SetActive(false);
            if (hits != null) hits.text = "";
            waterTarget = waterWidth = WaterSafe;
            waterHeightTarget = waterHeight = WaterLow;
            ApplyWater(0f);
        }

        public void ShowWall(int pad, int need, bool treasure)
        {
            Home();
            if (motion != null) StopCoroutine(motion);
            RestoreBricks(treasure ? Chest : pad == WallChart.ThickPad ? Thick : pad == WallChart.MidPad ? Mid : Thin);
            if (chest != null) chest.gameObject.SetActive(treasure);
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0f);
            if (hits != null) hits.text = need > 1 ? need.ToString() : "";
            motion = StartCoroutine(SlideIn());
        }

        // The crack stays dark until just before the beat, then blinks to mark the strike.
        public void Crack(float seconds)
        {
            if (crackRoutine != null) StopCoroutine(crackRoutine);
            crackRoutine = StartCoroutine(Flash(seconds));
        }

        public void Chip(int left)
        {
            if (crackRoutine != null) StopCoroutine(crackRoutine);
            if (hits != null) hits.text = left > 1 ? left.ToString() : "";
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 1f);
            if (left > 0)
            {
                if (motion != null) StopCoroutine(motion);
                motion = StartCoroutine(Shake(6f));
            }
        }

        public void Break()
        {
            if (crackRoutine != null) StopCoroutine(crackRoutine);
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0f);
            if (chest != null) chest.gameObject.SetActive(false);
            if (hits != null) hits.text = "";
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Tumble());
            if (!swimming && hero != null)
            {
                if (lunge != null) StopCoroutine(lunge);
                lunge = StartCoroutine(Lunge());
            }
        }

        public void Hold()
        {
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0.15f);
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Shake(12f));
        }

        public void ShowMeters(string text) { if (meters != null) meters.text = text; }

        // Width closes when the gap shrinks. Height starts low and climbs with distance, faster when the water is near.
        public void SetWater(int gap, int meters)
        {
            float danger = (WallChart.StartGap - gap) / (float)WallChart.StartGap;
            float climb = Mathf.Clamp01(meters / 40f);
            waterTarget = Mathf.Max(60f, WaterSafe + WaterReach * danger);
            waterHeightTarget = Mathf.Lerp(WaterLow, WaterHigh, Mathf.Clamp01(climb * 0.75f + danger * 0.35f));
        }

        public void Drown()
        {
            swimming = true;
            if (lunge != null) StopCoroutine(lunge);
            if (hero != null) hero.gameObject.SetActive(false);
            if (fish != null)
            {
                fish.gameObject.SetActive(true);
                fish.anchoredPosition = fishHome;
            }
            waterTarget = DrownWidth;
            waterHeightTarget = DrownHeight;
        }

        public void Strike(int pad)
        {
            if (!swimming && hammer != null)
            {
                if (swing != null) StopCoroutine(swing);
                swing = StartCoroutine(Swing());
            }
            if (drums == null || pad < 0 || pad >= drums.Length || drums[pad] == null) return;
            StartCoroutine(Punch(pad));
        }

        private void Update()
        {
            if (frozen) return;
            clock += Time.deltaTime;
            waterWidth = Mathf.Lerp(waterWidth, waterTarget, Time.deltaTime * 4f);
            waterHeight = Mathf.Lerp(waterHeight, waterHeightTarget, Time.deltaTime * 3f);
            ApplyWater(Mathf.Sin(clock * 2.4f) * 6f);
            if (swimming && fish != null)
                fish.anchoredPosition = fishHome + new Vector2(Mathf.Sin(clock * 1.3f) * 40f, Mathf.Sin(clock * 2.1f) * 16f);
        }

        private void ApplyWater(float swell)
        {
            if (water != null) water.sizeDelta = new Vector2(waterWidth + swell * 2f, waterHeight + swell);
        }

        private void StopAll()
        {
            if (crackRoutine != null) StopCoroutine(crackRoutine);
            if (motion != null) StopCoroutine(motion);
            if (lunge != null) StopCoroutine(lunge);
            if (swing != null) StopCoroutine(swing);
        }

        // Each row is a touch lighter toward the top, like the reference wall.
        private void RestoreBricks(Color color)
        {
            if (bricks == null) return;
            for (int i = 0; i < bricks.Length; i++)
            {
                Image brick = bricks[i];
                if (brick == null) continue;
                RectTransform rect = brick.rectTransform;
                if (brickHome != null && i < brickHome.Length) rect.anchoredPosition = brickHome[i];
                rect.localEulerAngles = Vector3.zero;
                float lift = brickHome != null && i < brickHome.Length ? Mathf.Clamp01(brickHome[i].y / 300f) * 0.18f : 0f;
                Color shade = Color.Lerp(color, Color.white, lift);
                shade.a = 1f;
                brick.color = shade;
            }
        }

        private IEnumerator SlideIn()
        {
            if (wall == null) yield break;
            float t = 0f;
            const float duration = 0.2f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                wall.anchoredPosition = wallHome + new Vector2(260f * (1f - u) * (1f - u), 0f);
                yield return null;
            }
            wall.anchoredPosition = wallHome;
        }

        private IEnumerator Flash(float seconds)
        {
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0f);
            float wait = Mathf.Max(0f, seconds - 0.22f);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float blink = Mathf.PingPong(t / duration * 3f, 1f);
                if (crack != null) crack.color = new Color(1f, 1f, 1f, 0.25f + 0.75f * blink);
                yield return null;
            }
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0.95f);
        }

        // The bricks burst outward and fall away.
        private IEnumerator Tumble()
        {
            if (bricks == null || brickHome == null) yield break;
            if (wall != null) wall.anchoredPosition = wallHome;
            int count = bricks.Length;
            var velocity = new Vector2[count];
            var spin = new float[count];
            var start = new Color[count];
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Repeat(i * 0.618f, 1f);
                velocity[i] = new Vector2(180f + 360f * a, 120f + 320f * Mathf.Repeat(i * 0.383f + 0.2f, 1f));
                spin[i] = (i % 2 == 0 ? 1f : -1f) * (240f + 300f * a);
                start[i] = bricks[i] != null ? bricks[i].color : Color.white;
            }
            float t = 0f;
            const float duration = 0.5f;
            while (t < duration)
            {
                t += Time.deltaTime;
                for (int i = 0; i < count; i++)
                {
                    if (bricks[i] == null) continue;
                    RectTransform rect = bricks[i].rectTransform;
                    rect.anchoredPosition = brickHome[i] + velocity[i] * t + new Vector2(0f, -1500f * t * t);
                    rect.localEulerAngles = new Vector3(0f, 0f, spin[i] * t);
                    Color c = start[i];
                    c.a = 1f - Mathf.Clamp01(t / duration);
                    bricks[i].color = c;
                }
                yield return null;
            }
        }

        private IEnumerator Shake(float amount)
        {
            if (wall == null) yield break;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                wall.anchoredPosition = wallHome + new Vector2(Mathf.Sin(t * 90f) * amount, 0f);
                yield return null;
            }
            wall.anchoredPosition = wallHome;
        }

        private IEnumerator Lunge()
        {
            float t = 0f;
            while (t < 0.26f)
            {
                t += Time.deltaTime;
                float hop = Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI);
                hero.anchoredPosition = heroHome + new Vector2(34f * hop, 14f * hop);
                yield return null;
            }
            hero.anchoredPosition = heroHome;
        }

        private IEnumerator Swing()
        {
            float t = 0f;
            while (t < 0.06f)
            {
                t += Time.deltaTime;
                hammer.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(RestAngle, HitAngle, Mathf.Clamp01(t / 0.06f)));
                yield return null;
            }
            t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                hammer.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(HitAngle, RestAngle, Mathf.Clamp01(t / 0.16f)));
                yield return null;
            }
            hammer.localEulerAngles = new Vector3(0f, 0f, RestAngle);
        }

        private IEnumerator Punch(int pad)
        {
            RectTransform drum = drums[pad].rectTransform;
            Vector3 rest = drumScale != null && pad < drumScale.Length ? drumScale[pad] : drum.localScale;
            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                drum.localScale = rest * (1f + 0.12f * Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI));
                yield return null;
            }
            drum.localScale = rest;
        }

        private sealed class WallPad : MonoBehaviour, IPointerDownHandler
        {
            private WallController owner;
            private int pad;

            public void Bind(WallController owner, int pad)
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

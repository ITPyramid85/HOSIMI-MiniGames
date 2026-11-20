using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // An anvil, the iron on it, and the smith. The hammer stays cocked until the player strikes.
    // A blow while the iron is glowing forges the next shape. A blow after it has cooled cracks it.
    public sealed class SmithyView : MonoBehaviour, IPointerDownHandler
    {
        // Cocked straight up. A tap stays near the iron. The finish pulls the head back, then slams it down.
        private const float Raised = 180f, StrikeAngle = 270f, TapLift = 236f, FinishWindup = 120f;
        private static readonly Color ColdIron = new Color(.45f, .48f, .52f, 1f);
        private static readonly Color WarmIron = new Color(1f, .42f, .1f, 1f);
        private static readonly Color HotIron = new Color(1f, .96f, .78f, 1f);
        [SerializeField] private RectTransform anvil, hammer, smith, blade;
        [SerializeField] private Image iron, glow, smithImage, bladeImage;
        [SerializeField] private Image[] sparks, pips;
        [SerializeField] private Sprite ironBar, ironCracked, smithIdle, smithGlad, smithSigh;
        [SerializeField] private Sprite[] swords;
        [SerializeField] private Text countText, caption, bubble;
        private Vector2 anvilHome, smithHome, hammerTarget, barSize;
        private float restY, angle;
        private int form, blows;
        private const float CoolSeconds = .55f;
        private bool broken, cooling;
        private Coroutine swing, heat, reaction, shake;
        public bool Cooling { get { return cooling; } }
        public bool IsSword { get { return form > 0; } }
        public event Action Pressed;

        private void Awake()
        {
            anvilHome = anvil.anchoredPosition;
            smithHome = smith.anchoredPosition;
            barSize = iron.rectTransform.sizeDelta;
            restY = iron.rectTransform.anchoredPosition.y + iron.rectTransform.sizeDelta.y * .5f;
            hammerTarget = SideHold(iron.rectTransform.anchoredPosition.x);
            hammer.anchoredPosition = hammerTarget;
            SetAngle(Raised);
            bladeImage.enabled = false;
            iron.color = ColdIron;
            glow.color = new Color(1f, .72f, .2f, 0f);
            caption.text = Strings.T("smith.remaining");
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Pressed != null) Pressed();
        }

        public void Clear()
        {
            StopAllCoroutines();
            swing = heat = reaction = shake = null;
            anvil.anchoredPosition = anvilHome;
            smith.anchoredPosition = smithHome;
            broken = cooling = false;
            form = blows = 0;
            if (iron != null) iron.enabled = true;
            ShowForm(0);
            glow.color = new Color(1f, .72f, .2f, 0f);
            smithImage.sprite = smithIdle;
            bladeImage.enabled = false;
            blade.localScale = Vector3.one;
            SetAngle(Raised);
            hammer.anchoredPosition = hammerTarget;
            countText.text = "0 / " + SmithChart.HeatCount;
            bubble.text = "";
            caption.text = Strings.T("smith.remaining");
            caption.color = Palette.Ink;
            if (pips != null)
                foreach (Image pip in pips) pip.color = new Color(1f, 1f, 1f, .28f);
            if (sparks != null)
                foreach (Image speck in sparks) speck.gameObject.SetActive(false);
        }

        public void Arm()
        {
            broken = cooling = false;
            if (iron != null) iron.enabled = true;
            ShowForm(form);
            glow.color = new Color(1f, .72f, .2f, 0f);
            caption.text = Strings.T("smith.remaining");
        }

        // The heat on the anvil names the pad that forges it.
        public void ShowCall(bool finishing)
        {
            caption.text = Strings.T(finishing ? "smith.call_finish" : "smith.call_tap");
            caption.color = finishing ? Palette.SkyText : Palette.SunText;
        }

        // Pad 2. Three short blows, the hammer never leaving the iron. Stack cymbal.
        public void Tap()
        {
            Halt();
            CountBlow();
            swing = StartCoroutine(TapSwing());
            React(Strings.T("smith.tap"), Palette.SunText);
        }

        // Pad 1. One heavy windup and one crash down onto the iron.
        public void FinishBlow()
        {
            Halt();
            CountBlow();
            swing = StartCoroutine(FinishSwing());
            React(Strings.T("smith.finish"), Palette.SkyText);
        }

        // The number follows the hammer, whether or not the iron was hot.
        private void CountBlow()
        {
            if (blows >= SmithChart.HeatCount) return;
            blows++;
            countText.text = blows + " / " + SmithChart.HeatCount;
        }

        // halfWindow is the hot strike window. It is followed by a cooling fade that is not a hit.
        public void Heat(float secondsToBeat, float halfWindow)
        {
            if (heat != null) StopCoroutine(heat);
            heat = StartCoroutine(Glow(Mathf.Max(.05f, secondsToBeat), Mathf.Max(.04f, halfWindow)));
        }

        // Sparks and the next shape land on this frame. A finish throws a much larger burst.
        public void Forge(int stage, bool finishing)
        {
            cooling = false;
            if (heat != null) StopCoroutine(heat);
            heat = null;
            form = stage;
            ShowForm(form);
            Advance(stage);
            Face(smithGlad);
            React(Strings.T(finishing ? "smith.finish" : "smith.tap"), finishing ? Palette.SkyText : Palette.SunText);
            Vector2 at = IronPoint();
            Burst(at, finishing ? 150f : 42f, true);
            if (finishing) Burst(at, 86f, false);
            if (iron != null) StartCoroutine(PixelTween.Punch(iron.rectTransform, finishing ? .5f : .14f, finishing ? .26f : .1f));
        }

        public void Advance(int forged)
        {
            if (swords == null || forged <= 0 || forged > swords.Length || swords[forged - 1] == null) return;
            bladeImage.sprite = swords[forged - 1];
            bladeImage.enabled = true;
            blade.localScale = Vector3.one;
            StartCoroutine(PixelTween.Punch(blade, .35f, .24f));
            if (pips != null && forged - 1 < pips.Length) pips[forged - 1].color = Color.white;
        }

        public void Crack(string text)
        {
            Face(smithSigh);
            React(text, Palette.DangerText);
            ApplyCrack();
        }

        // A blow while the glow is fading. The piece flies apart. The smith does not sigh.
        public void Shatter()
        {
            cooling = false;
            if (heat != null) StopCoroutine(heat);
            heat = null;
            broken = true;
            Face(smithIdle);
            React(Strings.T("smith.shatter"), Palette.DangerText);
            glow.color = new Color(1f, .72f, .2f, 0f);
            if (iron != null) iron.enabled = false;
            Vector2 at = IronPoint();
            Burst(at, 120f, false);
            Burst(at, 70f, true);
        }

        public void Cool()
        {
            if (heat != null) StopCoroutine(heat);
            heat = null;
            cooling = false;
            broken = false;
            ShowForm(form);
            glow.color = new Color(1f, .72f, .2f, 0f);
            glow.rectTransform.localScale = Vector3.one;
        }

        private void ShowForm(int stage)
        {
            if (iron != null) iron.enabled = true;
            if (stage <= 0 || swords == null || stage > swords.Length || swords[stage - 1] == null)
            {
                iron.sprite = ironBar;
                iron.color = ColdIron;
                iron.rectTransform.sizeDelta = barSize;
                return;
            }
            iron.sprite = swords[stage - 1];
            iron.color = Color.white;
            iron.rectTransform.sizeDelta = swords[stage - 1].rect.size * 6f;
        }

        private IEnumerator Glow(float seconds, float half)
        {
            cooling = false;
            float beat = Time.unscaledTime + seconds;
            float open = beat - half;
            float close = beat + half;
            while (Time.unscaledTime < open)
            {
                if (broken) yield break;
                SetHeat(0f);
                yield return null;
            }
            while (Time.unscaledTime < close)
            {
                if (broken) yield break;
                float u = Mathf.InverseLerp(open, close, Time.unscaledTime);
                float peak = 1f - Mathf.Abs(u * 2f - 1f);
                SetHeat(Mathf.Lerp(.62f, 1f, peak));
                yield return null;
            }
            float coolEnd = close + CoolSeconds;
            cooling = true;
            while (Time.unscaledTime < coolEnd)
            {
                if (broken) yield break;
                float u = Mathf.InverseLerp(close, coolEnd, Time.unscaledTime);
                SetHeat(Mathf.Lerp(.62f, 0f, u));
                yield return null;
            }
            cooling = false;
            if (!broken) SetHeat(0f);
            heat = null;
        }

        private void SetHeat(float hot)
        {
            if (hot <= 0f) iron.color = form > 0 ? Color.white : ColdIron;
            else iron.color = hot < .5f ? Color.Lerp(ColdIron, WarmIron, hot * 2f) : Color.Lerp(WarmIron, HotIron, (hot - .5f) * 2f);
            Color halo = glow.color;
            halo.a = hot * .9f;
            glow.color = halo;
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, hot);
        }

        // Short bounces on the iron. The head never cocks back up until the flurry is done.
        private IEnumerator TapSwing()
        {
            hammer.anchoredPosition = hammerTarget;
            for (int i = 0; i < 3; i++)
            {
                yield return StartCoroutine(SwingBetween(angle, TapLift, .035f));
                yield return StartCoroutine(SwingBetween(TapLift, StrikeAngle, .03f));
                SetAngle(StrikeAngle);
                Shake(4f, 2);
            }
            yield return StartCoroutine(SwingBetween(StrikeAngle, Raised, .08f));
            SetAngle(Raised);
            swing = null;
        }

        // The head draws back past the cocked pose, then comes down once, hard, and stays.
        private IEnumerator FinishSwing()
        {
            hammer.anchoredPosition = hammerTarget;
            yield return StartCoroutine(SwingBetween(angle, FinishWindup, .14f));
            SetAngle(FinishWindup);
            yield return StartCoroutine(SwingBetween(FinishWindup, StrikeAngle, .07f));
            SetAngle(StrikeAngle);
            Shake(18f, 7);
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .22f) yield return null;
            yield return StartCoroutine(SwingBetween(StrikeAngle, Raised, .18f));
            SetAngle(Raised);
            swing = null;
        }

        private void ApplyCrack()
        {
            broken = true;
            iron.sprite = ironCracked;
            iron.color = Color.white;
            glow.color = new Color(1f, .72f, .2f, 0f);
        }

        private void Burst(Vector2 at, float distance, bool bright)
        {
            if (sparks == null) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                Image speck = sparks[i];
                speck.gameObject.SetActive(true);
                speck.color = bright ? (i % 2 == 0 ? new Color(1f, .86f, .35f, 1f) : new Color(1f, .45f, .12f, 1f))
                    : new Color(.95f, .82f, .62f, 1f);
                float theta = (i / (float)sparks.Length) * Mathf.PI * (bright ? 2f : 1f);
                Vector2 dir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta) + .35f);
                Vector2 from = at;
                StartCoroutine(PixelTween.Run(bright ? .42f : .28f, t =>
                {
                    speck.rectTransform.anchoredPosition = PixelTween.Snap(from + dir * Mathf.Lerp(6f, distance, PixelTween.OutCubic(t)));
                    Color c = speck.color;
                    c.a = 1f - PixelTween.Steps(t, 4);
                    speck.color = c;
                    if (t >= 1f) speck.gameObject.SetActive(false);
                }));
            }
        }

        private void Shake(float amount, int times)
        {
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(Wiggle(amount, times));
        }

        private IEnumerator Wiggle(float amount, int times)
        {
            for (int i = 0; i < times; i++)
            {
                Vector2 kick = new Vector2(i % 2 == 0 ? amount : -amount, i % 2 == 0 ? amount * .4f : 0f);
                anvil.anchoredPosition = anvilHome + kick;
                smith.anchoredPosition = smithHome + kick * .35f;
                yield return new WaitForSecondsRealtime(.03f);
            }
            anvil.anchoredPosition = anvilHome;
            smith.anchoredPosition = smithHome;
            shake = null;
        }

        private IEnumerator SwingBetween(float from, float to, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + seconds)
            {
                float t = PixelTween.InOutSine(Mathf.InverseLerp(start, start + seconds, Time.unscaledTime));
                SetAngle(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetAngle(to);
        }

        private void Halt()
        {
            if (swing != null) StopCoroutine(swing);
            swing = null;
        }

        private void SetAngle(float degrees)
        {
            angle = degrees;
            hammer.localRotation = Quaternion.Euler(0, 0, degrees);
        }

        private Vector2 SideHold(float ironX)
        {
            float along = hammer.sizeDelta.y * .77f;
            float half = hammer.sizeDelta.x * .5f;
            return new Vector2(ironX + along, restY - 8f + half);
        }

        private Vector2 IronPoint()
        {
            return ToSparkSpace(iron.rectTransform);
        }

        private Vector2 ToSparkSpace(RectTransform target)
        {
            RectTransform parent = sparks != null && sparks.Length > 0 ? sparks[0].rectTransform.parent as RectTransform : null;
            if (parent == null) return target.anchoredPosition;
            return parent.InverseTransformPoint(target.position);
        }

        private void Face(Sprite sprite)
        {
            if (smithImage != null && sprite != null) smithImage.sprite = sprite;
        }

        private void React(string text, Color color)
        {
            if (reaction != null) StopCoroutine(reaction);
            reaction = StartCoroutine(ReactRoutine(text, color));
        }

        private IEnumerator ReactRoutine(string text, Color color)
        {
            bubble.text = text;
            bubble.color = color;
            yield return new WaitForSecondsRealtime(.8f);
            if (smithImage.sprite == smithSigh || smithImage.sprite == smithGlad) smithImage.sprite = smithIdle;
            if (bubble.text == text) bubble.text = "";
            reaction = null;
        }
    }
}

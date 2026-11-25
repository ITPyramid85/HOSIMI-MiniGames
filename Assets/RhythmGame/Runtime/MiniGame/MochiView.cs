using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Rice-cake field (plan §4-01). Grandma's hand going into the mortar is the beat cue: it reaches in,
    // flips the cake halfway, and is out again on the beat, when the mallet should land. Reactions:
    // a good strike squashes the cake; too late, Grandma pulls back and glares; too early, the cake
    // sticks to the ceiling; striking during a feint (hand still in) hits her hand. A tap anywhere
    // on the field counts as pad 8.
    public sealed class MochiView : MonoBehaviour, IPointerDownHandler
    {
        private static readonly Color Bean = new Color(0.93f, 0.74f, 0.38f);
        private Coroutine reach, swing, reaction, celebrate;
        private Vector2 grandmaHome, mortarHome, pileHome, danceHome;
        private Vector3 pileScale;
        private Transform fieldParent;
        private BeatCue pendingCue;
        private float pendingSeconds, reachScale = 1f;
        private bool flipped, rainbow, recoiling, armLocked, shownThisFlip, pendingFlip, pendingBonus, lifted, mountain;
        private Text kindLabel;
        private Coroutine stuckRoutine;
        [SerializeField] private RectTransform arm, mallet, mochi, mortar, grandma, ceilingMochi, pile;
        [SerializeField] private Image face, mochiImage, flash;
        [SerializeField] private Image[] flour;
        [SerializeField] private Text bubble;
        [SerializeField] private Sprite faceNormal, faceHappy, faceGlare, faceOuch, mochiNormal, mochiFlipped, mochiRainbow;
        [SerializeField] private Vector2 armRest, armReach;
        [SerializeField] private float malletRest = -20, malletStrike = 58;
        public event Action Pressed;

        private void Awake()
        {
            StandBesideMortar();
            NudgeMallet();
            JoinArm();
            grandmaHome = grandma.anchoredPosition;
            mortarHome = mortar.anchoredPosition;
            pileHome = pile.anchoredPosition;
            pileScale = pile.localScale;
            fieldParent = grandma.parent;
            EnsureKindLabel();
            TakeEveryTap();
        }

        // A tap anywhere is pad 8, so only this stretched area may answer the pointer. The cake, the
        // mortar and Grandma are drawn over it and would otherwise swallow a click on the middle of the field.
        private void TakeEveryTap()
        {
            Transform field = fieldParent != null ? fieldParent : transform;
            foreach (Graphic graphic in field.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic.gameObject == gameObject;
            Graphic self = GetComponent<Graphic>();
            if (self != null) self.raycastTarget = true;
        }

        public void OnPointerDown(PointerEventData e) { if (Pressed != null) Pressed(); }

        public RectTransform Mallet { get { return mallet; } }

        // Every accepted strike swings the mallet, whatever the judge then makes of it.
        public void SwingMallet() { Swing(); }

        // The body stands next to the mortar. The existing scene still has her further left; a rebuild already places her here.
        private void StandBesideMortar()
        {
            const float targetX = -144f;
            float dx = targetX - grandma.anchoredPosition.x;
            if (dx < 8f) return;
            grandma.anchoredPosition += new Vector2(dx, 0);
            if (bubble != null) bubble.rectTransform.anchoredPosition += new Vector2(dx, 0);
        }

        // Sit the head a little right and higher so the downswing meets the cake.
        private void NudgeMallet()
        {
            if (mallet == null) return;
            mallet.anchoredPosition += new Vector2(-32f, 48f);
        }

        // The sleeve pivots on the shoulder and grows toward the cake, so the reach never leaves a gap.
        private void JoinArm()
        {
            float width = arm.sizeDelta.x;
            Vector2 reachCenter = armReach;
            if (arm.pivot.x > .25f)
            {
                arm.anchoredPosition = new Vector2(arm.anchoredPosition.x - width * arm.pivot.x, arm.anchoredPosition.y);
                arm.pivot = new Vector2(0f, .5f);
            }
            float shoulderX = grandma.anchoredPosition.x + grandma.sizeDelta.x * .5f - 52f;
            float tipX = reachCenter.x + width * .5f;
            armRest = new Vector2(shoulderX, arm.anchoredPosition.y);
            armReach = new Vector2(shoulderX, reachCenter.y);
            reachScale = Mathf.Max(1f, (tipX - shoulderX) / Mathf.Max(1f, width));
            RestArm();
        }

        private void RestArm() { PlaceArm(armRest, 1f); }

        private void PlaceArm(Vector2 shoulder, float scale)
        {
            arm.anchoredPosition = PixelTween.Snap(shoulder);
            arm.localScale = new Vector3(scale, 1f, 1f);
        }

        public void Clear()
        {
            StopAllCoroutines();
            reach = swing = reaction = celebrate = null;
            RestArm();
            mallet.localEulerAngles = new Vector3(0, 0, malletRest);
            mochi.localScale = Vector3.one;
            mochiImage.sprite = mochiNormal;
            flipped = rainbow = recoiling = armLocked = shownThisFlip = pendingFlip = pendingBonus = false;
            pendingCue = null;
            if (lifted && fieldParent != null)
            {
                grandma.SetParent(fieldParent, false);
                pile.SetParent(fieldParent, false);
                lifted = false;
            }
            grandma.anchoredPosition = grandmaHome;
            grandma.localEulerAngles = Vector3.zero;
            mortar.anchoredPosition = mortarHome;
            mochi.gameObject.SetActive(true);
            face.sprite = faceNormal;
            ceilingMochi.gameObject.SetActive(false);
            ceilingMochi.localScale = Vector3.one;
            stuckRoutine = null;
            pile.anchoredPosition = pileHome;
            pile.localScale = pileScale;
            pile.gameObject.SetActive(false);
            flash.color = Color.clear;
            bubble.text = "";
            TintCake();
            ShowKind();
            foreach (Image speck in flour) speck.gameObject.SetActive(false);
        }

        // The hand goes into the mortar and the cake turns as the beat arrives. A feint leaves the hand in.
        public void Reach(BeatCue cue, float seconds)
        {
            // A late glare keeps the hand snatched until the yank finishes. The next flip waits its turn.
            FinishPendingFlip();
            pendingCue = cue;
            pendingSeconds = Mathf.Max(.05f, seconds);
            shownThisFlip = false;
            pendingFlip = cue.Kind != CueKind.Forbidden;
            pendingBonus = cue.Kind == CueKind.Bonus;
            if (armLocked) return;
            BeginReach();
        }

        private void BeginReach()
        {
            if (pendingCue == null) return;
            if (reach != null) StopCoroutine(reach);
            reach = StartCoroutine(ReachRoutine(pendingCue, pendingSeconds));
        }

        private IEnumerator ReachRoutine(BeatCue cue, float seconds)
        {
            bool feint = cue.Kind == CueKind.Forbidden;
            Vector2 from = arm.anchoredPosition;
            float fromScale = arm.localScale.x;
            // Most of the lead is a soft lift, so the cake settles onto the beat instead of snapping up.
            float approach = Mathf.Max(.25f, seconds * .9f);
            yield return PixelTween.Run(approach, t => PlaceArm(Vector2.Lerp(from, armReach, PixelTween.InOutSine(t)), Mathf.Lerp(fromScale, reachScale, PixelTween.InOutSine(t))));
            float remain = seconds - approach;
            if (remain > 0f) yield return new WaitForSecondsRealtime(remain);
            FinishPendingFlip();
            if (feint) yield return new WaitForSecondsRealtime(.22f);
            yield return PullArm(.1f);
            reach = null;
        }

        private void FinishPendingFlip()
        {
            if (!pendingFlip) return;
            bool bonus = pendingBonus;
            pendingFlip = pendingBonus = false;
            ShowCake(bonus);
        }

        private void StopReaction()
        {
            if (reaction != null) { StopCoroutine(reaction); reaction = null; }
        }

        // A cake on the ceiling lives on its own routine: a face reaction can never leave it up there,
        // and it comes down on its own, on the next flip, or on the next good strike.
        private void ReleaseCeiling()
        {
            if (stuckRoutine != null) { StopCoroutine(stuckRoutine); stuckRoutine = null; }
            if (!ceilingMochi.gameObject.activeSelf) return;
            bubble.text = "";
            ceilingMochi.gameObject.SetActive(false);
            ceilingMochi.localScale = Vector3.one;
            mochiImage.sprite = rainbow ? mochiRainbow : mochiNormal;
            flipped = false;
            TintCake();
            ShowKind();
            mochi.gameObject.SetActive(true);
        }

        // Plain bean-powder cake, its other side, or the rainbow one once a perfect run has earned it.
        private void ShowCake(bool bonus)
        {
            // She can only turn a cake that is in the mortar: a flip peels it off the ceiling.
            ReleaseCeiling();
            if (shownThisFlip && !rainbow && !bonus) return;
            shownThisFlip = true;
            flipped = !flipped;
            mochiImage.sprite = rainbow || bonus ? mochiRainbow : flipped ? mochiFlipped : mochiNormal;
            TintCake();
            ShowKind();
            StartCoroutine(PixelTween.Punch(mochi, .12f, .1f));
        }

        public void Strike(bool perfect, bool rainbowCake)
        {
            pendingFlip = pendingBonus = false;
            if (reach != null) { StopCoroutine(reach); reach = null; }
            if (rainbowCake) rainbow = true;
            ShowCake(false);
            RestArm();
            StopReaction();
            recoiling = armLocked = false;
            reaction = StartCoroutine(Squash(perfect));
        }

        // Late: she snatches her hand back and glares.
        public void TooLate(string text)
        {
            if (reach != null) { StopCoroutine(reach); reach = null; }
            StopReaction();
            recoiling = armLocked = true;
            reaction = StartCoroutine(Recoil(text));
        }

        private IEnumerator PullArm(float seconds)
        {
            Vector2 start = arm.anchoredPosition;
            float startScale = arm.localScale.x;
            yield return PixelTween.Run(seconds, t => PlaceArm(Vector2.Lerp(start, armRest, PixelTween.InCubic(t)), Mathf.Lerp(startScale, 1f, PixelTween.InCubic(t))));
        }

        private IEnumerator Recoil(string text)
        {
            face.sprite = faceGlare;
            bubble.text = text;
            bubble.color = Palette.Ink;
            Vector2 snatched = armRest + new Vector2(-48f, 36f);
            Vector2 start = arm.anchoredPosition;
            float startScale = arm.localScale.x;
            yield return PixelTween.Run(.07f, t => PlaceArm(Vector2.Lerp(start, snatched, t), Mathf.Lerp(startScale, 1f, t)));
            yield return PixelTween.Run(.08f, t => PlaceArm(Vector2.Lerp(snatched, armRest, t), 1f));
            armLocked = false;
            if (pendingFlip) BeginReach();
            yield return new WaitForSecondsRealtime(.4f);
            if (!recoiling) yield break;
            face.sprite = faceNormal;
            bubble.text = "";
            recoiling = false;
            reaction = null;
        }

        // The cake flies up and sticks to the ceiling (plan: "打快了年糕粘到天花板上").
        public void TooEarly(string text)
        {
            pendingFlip = pendingBonus = false;
            armLocked = false;
            if (reach != null) { StopCoroutine(reach); reach = null; }
            RestArm();
            StopReaction();
            ReleaseCeiling();
            stuckRoutine = StartCoroutine(Stuck(text));
        }

        // Struck during a feint: the mallet lands on Grandma's hand.
        public void HandHit(string text)
        {
            pendingFlip = pendingBonus = false;
            armLocked = false;
            if (reach != null) { StopCoroutine(reach); reach = null; }
            RestArm();
            React(faceOuch, text, Palette.DangerText, shake: true);
        }

        public void FeintAvoided() { React(faceHappy, "", Palette.Ink, shake: false); }

        // All Perfect: the cakes pile up and Grandma dances until the next round.
        public void Celebrate()
        {
            Transform top = fieldParent != null ? fieldParent.parent : null;
            if (top != null)
            {
                grandma.SetParent(top, true);
                pile.SetParent(top, true);
                pile.SetAsLastSibling();
                grandma.SetAsLastSibling();
                lifted = true;
                danceHome = grandma.anchoredPosition;
                pile.anchoredPosition += new Vector2(0, 110);
            }
            else danceHome = grandmaHome;
            RaiseMountain();
            pile.localScale = pileScale * 1.7f;
            pile.gameObject.SetActive(true);
            face.sprite = faceHappy;
            celebrate = StartCoroutine(Dance());
        }

        private void Swing()
        {
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            yield return PixelTween.Run(.06f, t => mallet.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(malletRest, malletStrike, t)));
            yield return PixelTween.Run(.18f, t => mallet.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(malletStrike, malletRest, PixelTween.OutCubic(t))));
            swing = null;
        }

        private IEnumerator Squash(bool perfect)
        {
            face.sprite = faceHappy;
            ShowFlour(perfect ? flour.Length : flour.Length / 2);
            StartCoroutine(Shake(mortar, mortarHome, 6));
            // Sticky stretch: flatten, then spring back taller than round before settling.
            yield return PixelTween.Run(.06f, t => mochi.localScale = new Vector3(Mathf.Lerp(1, 1.35f, t), Mathf.Lerp(1, .6f, t), 1));
            yield return PixelTween.Run(.22f, t => mochi.localScale = new Vector3(Mathf.Lerp(1.35f, 1, PixelTween.OutBack(t)), Mathf.Lerp(.6f, 1, PixelTween.OutBack(t)), 1));
            yield return new WaitForSecondsRealtime(.15f);
            foreach (Image speck in flour) speck.gameObject.SetActive(false);
            face.sprite = faceNormal;
            reaction = null;
        }

        private IEnumerator Stuck(string text)
        {
            bubble.text = text;
            bubble.color = Palette.Ink;
            mochi.gameObject.SetActive(false);
            ceilingMochi.SetAsLastSibling();
            ceilingMochi.localScale = new Vector3(1.35f, 1.35f, 1f);
            Image stuck = ceilingMochi.GetComponent<Image>();
            if (stuck != null) stuck.sprite = mochiImage.sprite;
            ceilingMochi.gameObject.SetActive(true);
            TintCake();
            yield return PixelTween.Punch(ceilingMochi, .35f, .22f);
            yield return new WaitForSecondsRealtime(.7f);
            stuckRoutine = null;
            ceilingMochi.gameObject.SetActive(false);
            ceilingMochi.localScale = Vector3.one;
            mochiImage.sprite = rainbow ? mochiRainbow : mochiNormal;
            flipped = false;
            TintCake();
            ShowKind();
            mochi.gameObject.SetActive(true);
            bubble.text = "";
        }

        private void React(Sprite expression, string text, Color color, bool shake)
        {
            StopReaction();
            reaction = StartCoroutine(ReactRoutine(expression, text, color, shake));
        }

        private IEnumerator ReactRoutine(Sprite expression, string text, Color color, bool shake)
        {
            face.sprite = expression;
            bubble.text = text;
            bubble.color = color;
            if (shake)
            {
                flash.color = new Color(Palette.Coral.r, Palette.Coral.g, Palette.Coral.b, .45f);
                StartCoroutine(Shake(grandma, grandmaHome, 8));
            }
            yield return new WaitForSecondsRealtime(.45f);
            flash.color = Color.clear;
            face.sprite = faceNormal;
            bubble.text = "";
            reaction = null;
        }

        private IEnumerator Shake(RectTransform target, Vector2 home, float amount)
        {
            for (int i = 0; i < 4; i++)
            {
                target.anchoredPosition = home + new Vector2(i % 2 == 0 ? amount : -amount, 0);
                yield return new WaitForSecondsRealtime(.03f);
            }
            target.anchoredPosition = home;
        }

        // Bean-powder cakes and rainbow cakes stacked above the three already in the pile.
        private void RaiseMountain()
        {
            if (mountain || pile.childCount == 0) return;
            mountain = true;
            RectTransform seed = pile.GetChild(0) as RectTransform;
            Vector2[] spots = {
                new Vector2(-56, 58), new Vector2(58, 62), new Vector2(-16, 112), new Vector2(24, 118), new Vector2(0, 168)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                RectTransform copy = Instantiate(seed.gameObject, pile).GetComponent<RectTransform>();
                copy.anchoredPosition = spots[i];
                Image image = copy.GetComponent<Image>();
                if (image == null) continue;
                bool rainbowCake = i % 2 == 0;
                image.sprite = rainbowCake ? mochiRainbow : mochiNormal;
                image.color = rainbowCake ? Color.white : Bean;
            }
        }

        private IEnumerator Dance()
        {
            for (float t = 0; ; t += Time.unscaledDeltaTime)
            {
                grandma.anchoredPosition = danceHome + new Vector2(PixelTween.Snap(Mathf.Sin(t * 4) * 28), PixelTween.Snap(Mathf.Abs(Mathf.Sin(t * 8)) * 48));
                grandma.localEulerAngles = new Vector3(0, 0, PixelTween.Steps(Mathf.Sin(t * 4), 2) * 14);
                if (lifted)
                {
                    pile.SetAsLastSibling();
                    grandma.SetAsLastSibling();
                }
                yield return null;
            }
        }

        private void ShowFlour(int count)
        {
            for (int i = 0; i < flour.Length; i++)
            {
                bool shown = i < count;
                flour[i].gameObject.SetActive(shown);
                if (!shown) continue;
                float angle = (i / (float)flour.Length) * Mathf.PI + .2f;
                flour[i].rectTransform.anchoredPosition = mochi.anchoredPosition + PixelTween.Snap(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 96);
            }
        }

        private void TintCake()
        {
            mochiImage.color = mochiImage.sprite == mochiRainbow ? Color.white : Bean;
            Image stuck = ceilingMochi.GetComponent<Image>();
            if (stuck != null) stuck.color = Bean;
        }

        private void ShowKind()
        {
            if (kindLabel == null) return;
            kindLabel.text = Strings.T(rainbow ? "mochi.rainbow" : "mochi.bean");
        }

        private void EnsureKindLabel()
        {
            if (bubble == null) return;
            var go = new GameObject("MochiKind", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(80, -48);
            rect.sizeDelta = new Vector2(360, 40);
            kindLabel = go.AddComponent<Text>();
            kindLabel.font = bubble.font;
            kindLabel.fontSize = 28;
            kindLabel.alignment = TextAnchor.MiddleCenter;
            kindLabel.color = Palette.Ink;
            kindLabel.raycastTarget = false;
            kindLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void OnDisable() { grandma.localEulerAngles = Vector3.zero; }
    }
}

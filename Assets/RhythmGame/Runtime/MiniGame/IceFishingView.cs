using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Ice, three holes, one fish underneath, and the bucket the catches stack in.
    public sealed class IceFishingView : MonoBehaviour
    {
        // The smithy hammer: cocked above the ice, then the head swings down onto the tile.
        private const float Raised = 180f, Down = 270f;
        [SerializeField] private RectTransform fisher, fish, bucket, hammer;
        [SerializeField] private Image fisherImage, fishImage, splash;
        [SerializeField] private Sprite fisherIdle, fisherWet;
        [SerializeField] private Sprite[] fishes;
        [SerializeField] private RectTransform[] holes;
        [SerializeField] private Image[] pile, dex, openings, cracks;
        [SerializeField] private Text caughtText, dexCaption;
        private Vector2 fisherHome, fishHome, hammerHome;
        private float hammerAngle;
        private int shown = -1, stacked, restPad = IceChart.Center, nextSide = IceChart.Right;
        private const float RestSeconds = .6f;
        private static float HalfBeat { get { return (float)(IceChart.BeatSeconds * .5); } }
        private Coroutine swing, blow, step, plunge;
        private float settledUntil;
        public event Action<int> Pressed;

        private void Awake()
        {
            fisherHome = fisher.anchoredPosition;
            fishHome = fish.anchoredPosition;
            if (hammer != null)
            {
                hammerHome = Above(Hole(IceChart.Center));
                hammer.anchoredPosition = hammerHome;
                SetHammer(Raised);
            }
            for (int i = 0; i < holes.Length; i++)
            {
                IceHole hole = holes[i].GetComponent<IceHole>();
                if (hole != null) hole.Pressed += OnHole;
            }
            Clear(new bool[IceChart.SpeciesCount]);
        }

        private void OnHole(int pad) { if (Pressed != null) Pressed(pad); }

        public void Clear(bool[] known)
        {
            if (swinging()) StopAllCoroutines();
            swing = blow = step = plunge = null;
            shown = -1;
            stacked = 0;
            restPad = IceChart.Center;
            nextSide = IceChart.Right;
            settledUntil = 0f;
            fisher.anchoredPosition = fisherHome;
            fish.anchoredPosition = Inside(IceChart.Center);
            fish.gameObject.SetActive(true);
            swing = StartCoroutine(Wander());
            if (hammer != null)
            {
                hammer.anchoredPosition = hammerHome;
                SetHammer(Raised);
            }
            if (fisherImage != null && fisherIdle != null) fisherImage.sprite = fisherIdle;
            if (splash != null) splash.gameObject.SetActive(false);
            for (int i = 0; openings != null && i < openings.Length; i++)
                if (openings[i] != null) openings[i].gameObject.SetActive(false);
            for (int i = 0; cracks != null && i < cracks.Length; i++)
                if (cracks[i] != null) cracks[i].gameObject.SetActive(false);
            if (pile != null)
                foreach (Image icon in pile) icon.gameObject.SetActive(false);
            ShowDex(known);
            if (dexCaption != null) dexCaption.text = Strings.T("ice.dex");
            SetCaught(0);
        }

        public void ShowDex(bool[] known)
        {
            if (dex == null) return;
            for (int i = 0; i < dex.Length; i++)
                if (dex[i] != null) dex[i].color = Color.white;
        }

        public void Approach(BeatCue cue, float secondsToBeat)
        {
            float half = WindowHalf(cue);
            if (cue.Variant == shown && fish.gameObject.activeSelf && cue.Kind != CueKind.Forbidden)
            {
                settledUntil = Mathf.Max(settledUntil, Time.unscaledTime + Mathf.Max(0f, secondsToBeat) + half);
                return;
            }
            shown = cue.Variant;
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(Swim(cue, Mathf.Max(.2f, secondsToBeat)));
        }

        public void Chop(int pad)
        {
            RectTransform hole = Hole(pad);
            if (hole != null) StartCoroutine(PixelTween.Punch(hole, .2f, .12f));
            StartCoroutine(Break(pad));
            if (step != null) StopCoroutine(step);
            step = StartCoroutine(StepTo(hole));
            if (hammer == null) return;
            if (blow != null) StopCoroutine(blow);
            blow = StartCoroutine(HammerDown(hole));
        }

        // The hammer lands: the ice cracks, the crack opens into a hole, and water jumps out of it.
        private IEnumerator Break(int pad)
        {
            int index = IceChart.HoleIndex(pad);
            Image crack = index >= 0 && cracks != null && index < cracks.Length ? cracks[index] : null;
            Image opening = index >= 0 && openings != null && index < openings.Length ? openings[index] : null;
            if (crack != null) crack.gameObject.SetActive(true);
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .09f) yield return null;
            if (opening != null)
            {
                opening.gameObject.SetActive(true);
                opening.transform.SetAsLastSibling();
                if (crack != null) crack.transform.SetAsLastSibling();
                StartCoroutine(PixelTween.Punch(opening.rectTransform, .18f, .1f));
            }
            Splash(pad);
            start = Time.unscaledTime;
            while (Time.unscaledTime < start + .75f) yield return null;
            // The cold closes it again, ready for the next ice.
            if (crack != null) crack.gameObject.SetActive(false);
            if (opening != null) opening.gameObject.SetActive(false);
        }

        private void Splash(int pad)
        {
            if (splash == null) return;
            RectTransform hole = Hole(pad);
            Vector2 at = hole != null ? hole.anchoredPosition : Vector2.zero;
            splash.rectTransform.anchoredPosition = at + new Vector2(0, 30f);
            splash.gameObject.SetActive(true);
            splash.transform.SetAsLastSibling();
            StartCoroutine(Fade(splash, .4f));
        }

        private IEnumerator Fade(Image image, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + seconds)
            {
                image.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(start, start + seconds, Time.unscaledTime));
                yield return null;
            }
            image.color = Color.white;
            image.gameObject.SetActive(false);
        }

        // Struck the wrong ice: the fisher shudders before he goes through.
        public void Shiver(int pad)
        {
            if (fisher != null) StartCoroutine(PixelTween.Punch(fisher, .3f, .14f));
            Splash(pad);
        }

        public void Flinch(int pad)
        {
            if (fish.gameObject.activeSelf) StartCoroutine(PixelTween.Punch(fish, .25f, .12f));
        }

        public void Catch(int pad, int species, int count)
        {
            shown = -1;
            settledUntil = 0f;
            if (swing != null) StopCoroutine(swing);
            swing = null;
            fish.gameObject.SetActive(false);
            fish.anchoredPosition = Inside(pad);
            restPad = pad;
            swing = StartCoroutine(Resurface());
            if (pile != null && stacked < pile.Length)
            {
                Image icon = pile[stacked++];
                if (fishImage != null && fishImage.sprite != null) icon.sprite = fishImage.sprite;
                icon.color = Color.white;
                icon.gameObject.SetActive(true);
                icon.transform.SetAsLastSibling();
                StartCoroutine(PixelTween.Punch(icon.rectTransform, .3f, .16f));
            }
            SetCaught(count);
        }

        public void Escape(int pad)
        {
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(Flee());
        }

        private IEnumerator Flee()
        {
            yield return DartAway();
            yield return Wander();
        }

        // One catch out of the way, the next fish shows up on a neighbouring ice.
        private IEnumerator Resurface()
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .5f) yield return null;
            restPad = Neighbour();
            fish.anchoredPosition = Inside(restPad);
            fish.gameObject.SetActive(true);
            yield return Wander();
        }

        // rise: he shivers in the water and climbs back out while the fish keeps swimming.
        // Otherwise he stays in, and the fish stops with the run.
        public void Fall(int pad, bool rise)
        {
            if (!rise)
            {
                if (swing != null) StopCoroutine(swing);
                swing = null;
            }
            if (step != null) StopCoroutine(step);
            step = null;
            if (plunge != null) StopCoroutine(plunge);
            if (fisherImage != null && fisherWet != null) fisherImage.sprite = fisherWet;
            plunge = StartCoroutine(rise ? FallAndClimb(Hole(pad)) : Drop(Hole(pad)));
        }

        // He climbs out onto the ice he broke, where the hammer is, not back to where his step began.
        private IEnumerator FallAndClimb(RectTransform hole)
        {
            Vector2 back = hole != null ? Stand(hole) : fisher.anchoredPosition;
            yield return Drop(hole);
            Vector2 into = fisher.anchoredPosition;
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .6f)
            {
                float t = Time.unscaledTime - start;
                fisher.anchoredPosition = into + new Vector2(Mathf.Sin(t * 70f) * 6f, 0f);
                yield return null;
            }
            fisher.anchoredPosition = into;
            if (splash != null) splash.gameObject.SetActive(false);
            start = Time.unscaledTime;
            while (Time.unscaledTime < start + .35f)
            {
                float t = PixelTween.OutCubic(Mathf.InverseLerp(start, start + .35f, Time.unscaledTime));
                fisher.anchoredPosition = Vector2.Lerp(into, back, t);
                yield return null;
            }
            fisher.anchoredPosition = back;
            if (hammer != null && blow == null && hole != null)
            {
                hammer.anchoredPosition = Above(hole);
                SetHammer(Raised);
            }
            if (fisherImage != null && fisherIdle != null) fisherImage.sprite = fisherIdle;
            plunge = null;
        }

        private void SetCaught(int count)
        {
            if (caughtText != null) caughtText.text = Strings.T("ice.caught") + count;
        }

        public int ShownVariant { get { return shown; } }

        // Only the ice the fish is actually over. Any other ice is empty, and breaking it drops the fisher in.
        public bool FishAt(int pad)
        {
            if (fish == null || !fish.gameObject.activeSelf) return false;
            RectTransform hole = Hole(pad);
            if (hole == null) return false;
            Vector2 delta = fish.anchoredPosition - hole.anchoredPosition;
            return Mathf.Abs(delta.x) <= hole.sizeDelta.x * .5f && Mathf.Abs(delta.y) <= hole.sizeDelta.y * .5f;
        }

        public RectTransform HoleRect(int pad) { return Hole(pad); }

        private RectTransform Hole(int pad)
        {
            int index = IceChart.HoleIndex(pad);
            if (holes == null || index < 0 || index >= holes.Length) return null;
            return holes[index];
        }

        private bool swinging() { return swing != null || blow != null || step != null || plunge != null; }

        private IEnumerator StepTo(RectTransform hole)
        {
            if (fisher == null || hole == null) yield break;
            Vector2 from = fisher.anchoredPosition;
            Vector2 to = Stand(hole);
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .12f)
            {
                float t = Mathf.InverseLerp(start, start + .12f, Time.unscaledTime);
                fisher.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            fisher.anchoredPosition = to;
            step = null;
        }

        // Where the fisher stands to strike that ice.
        private static Vector2 Stand(RectTransform hole)
        {
            return new Vector2(hole.anchoredPosition.x, hole.anchoredPosition.y + hole.sizeDelta.y * .5f);
        }

        // Pivot sits on the ice surface, to the right. Cocked, the head stays above the ice.
        private Vector2 Above(RectTransform hole)
        {
            if (hole == null || hammer == null) return hammerHome;
            float surface = hole.anchoredPosition.y + hole.sizeDelta.y * .5f;
            return new Vector2(hole.anchoredPosition.x + hammer.sizeDelta.y * .77f, surface);
        }

        private IEnumerator HammerDown(RectTransform hole)
        {
            Vector2 from = hammer.anchoredPosition;
            Vector2 to = hole != null ? Above(hole) : hammerHome;
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .1f)
            {
                float t = Mathf.InverseLerp(start, start + .1f, Time.unscaledTime);
                hammer.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            hammer.anchoredPosition = to;
            yield return SwingHammer(hammerAngle, Down, .08f);
            SetHammer(Down);
            start = Time.unscaledTime;
            while (Time.unscaledTime < start + .06f) yield return null;
            yield return SwingHammer(Down, Raised, .14f);
            SetHammer(Raised);
            hammer.anchoredPosition = to;
            blow = null;
        }

        private IEnumerator SwingHammer(float from, float to, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + seconds)
            {
                float t = Mathf.InverseLerp(start, start + seconds, Time.unscaledTime);
                SetHammer(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetHammer(to);
        }

        private void SetHammer(float degrees)
        {
            hammerAngle = degrees;
            hammer.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private Vector2 Inside(int pad)
        {
            RectTransform hole = Hole(pad);
            return hole != null ? hole.anchoredPosition : fishHome;
        }

        // The next ice on the route: the middle leads out to a side, a side leads back to the middle.
        private int Neighbour()
        {
            if (restPad != IceChart.Center) return IceChart.Center;
            return nextSide = nextSide == IceChart.Right ? IceChart.Left : IceChart.Right;
        }

        private static float WindowHalf(BeatCue cue)
        {
            return cue != null && cue.GoodMs > 0 ? (float)(cue.GoodMs / 1000.0) : RestSeconds * .5f;
        }

        private IEnumerator Swim(BeatCue cue, float seconds)
        {
            float half = WindowHalf(cue);
            float beatAt = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < settledUntil) yield return null;
            float remain = Mathf.Max(.08f, beatAt - Time.unscaledTime);
            float travel = Mathf.Max(.08f, remain - half);
            Vector2 target = Inside(cue.Pad);
            if (!fish.gameObject.activeSelf) fish.anchoredPosition = Inside(restPad);
            Vector2 from = fish.anchoredPosition;
            fish.gameObject.SetActive(true);
            fish.localScale = new Vector3(target.x >= from.x ? 1f : -1f, 1f, 1f);
            // The tell: over the last half beat the fish slows right down before it stops.
            float slow = Mathf.Min(HalfBeat, travel * .5f);
            Vector2 edge = Vector2.Lerp(from, target, .86f);
            yield return Glide(from, edge, travel - slow);
            yield return Glide(edge, target, slow);
            restPad = cue.Pad;
            nextSide = cue.Pad == IceChart.Right ? IceChart.Right : IceChart.Left;
            settledUntil = Mathf.Max(settledUntil, beatAt + half);
            while (Time.unscaledTime < settledUntil) yield return null;
            if (cue.Kind == CueKind.Forbidden) yield return DartAway();
            yield return Wander();
        }

        // Between fish the one under the ice keeps playing: middle, side, middle, other side.
        private IEnumerator Wander()
        {
            while (true)
            {
                float start = Time.unscaledTime;
                while (Time.unscaledTime < start + RestSeconds) yield return null;
                int next = Neighbour();
                Vector2 from = fish.anchoredPosition, to = Inside(next);
                fish.localScale = new Vector3(to.x >= from.x ? 1f : -1f, 1f, 1f);
                yield return Glide(from, to, .4f);
                restPad = next;
            }
        }

        private IEnumerator Glide(Vector2 from, Vector2 to, float seconds)
        {
            float start = Time.unscaledTime;
            while (seconds > 0f && Time.unscaledTime < start + seconds)
            {
                float t = Mathf.InverseLerp(start, start + seconds, Time.unscaledTime);
                fish.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            fish.anchoredPosition = to;
        }

        // A fish that got away slips to the next ice on the route and keeps playing there.
        private IEnumerator DartAway()
        {
            int next = Neighbour();
            Vector2 from = fish.anchoredPosition;
            Vector2 to = Inside(next);
            fish.gameObject.SetActive(true);
            fish.localScale = new Vector3(to.x >= from.x ? 1f : -1f, 1f, 1f);
            yield return Glide(from, to, .28f);
            restPad = next;
        }

        private IEnumerator Drop(RectTransform hole)
        {
            Vector2 from = fisher.anchoredPosition;
            Vector2 into = hole != null ? hole.anchoredPosition + new Vector2(0, -80f) : from + new Vector2(0, -160f);
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .45f)
            {
                float t = Mathf.InverseLerp(start, start + .45f, Time.unscaledTime);
                fisher.anchoredPosition = Vector2.Lerp(from, into, t * t);
                yield return null;
            }
            fisher.anchoredPosition = into;
            if (splash != null)
            {
                splash.rectTransform.anchoredPosition = into + new Vector2(0, 24f);
                splash.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (holes == null) return;
            foreach (RectTransform hole in holes)
            {
                if (hole == null) continue;
                IceHole ice = hole.GetComponent<IceHole>();
                if (ice != null) ice.Pressed -= OnHole;
            }
        }
    }

    // One ice hole. The pad index is the drum pad for that side.
    public sealed class IceHole : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private int pad;
        public int Pad { get { return pad; } }
        public event Action<int> Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Pressed != null) Pressed(pad);
        }
    }
}

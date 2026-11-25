using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // A cloth on a block, a club in each hand, and Mother beside them.
    // Both clubs rest on the cloth. The next blow lifts that hand half a beat early, then the key
    // brings it back down. The same hand twice throws that club away and Mother stares.
    // An all-perfect run shines the cloth flat.
    public sealed class FabricView : MonoBehaviour
    {
        // One blow, end to end: whatever is left of the lift, a settle at the top, then the fall onto
        // the cloth. The controller shortens it toward the least when this hand is called again soon.
        public const float SwingSeconds = .2f, LeastSwingSeconds = .1f;
        private const float Dip = 6f;       // how far under the cloth's top face the head settles
        private const float Inset = 12f;    // the head stays this far inside the cloth's near edge
        private const float Lift = 64f;     // how far above the cloth a cocked head sits, so the next hand reads at a glance
        [SerializeField] private RectTransform cloth, leftClub, rightClub, mother;
        [SerializeField] private Image motherImage;
        [SerializeField] private Image[] sparkles;
        [SerializeField] private Sprite motherIdle, motherLook, motherGlad;
        [SerializeField] private Text bubble;
        private Vector2 clothHome, clothHalf, leftHome, rightHome, motherHome;
        private Vector3 clothScale;
        private Transform fieldParent;
        private Coroutine leftMove, rightMove, look, celebrate, press;
        private float leftRest, rightRest, leftAngle, rightAngle, smooth;
        private bool lifted, leftAway, rightAway;

        private void Awake()
        {
            clothHome = cloth.anchoredPosition;
            clothScale = cloth.localScale;
            // The saved rect, not the flattening one: the blow must land on the same angle all run.
            clothHalf = new Vector2(cloth.sizeDelta.x * Mathf.Abs(clothScale.x) * .5f,
                                    cloth.sizeDelta.y * Mathf.Abs(clothScale.y) * .5f);
            leftHome = leftClub.anchoredPosition;
            rightHome = rightClub.anchoredPosition;
            // Rest is on the cloth, not the old raised lean. Only the next blow leaves the surface.
            leftRest = StrikeAngle(true);
            rightRest = StrikeAngle(false);
            SetAngle(true, leftRest);
            SetAngle(false, rightRest);
            motherHome = mother.anchoredPosition;
            fieldParent = mother.parent;
        }

        public void Clear()
        {
            StopAllCoroutines();
            leftMove = rightMove = look = celebrate = press = null;
            if (lifted && fieldParent != null)
            {
                cloth.SetParent(fieldParent, false);
                mother.SetParent(fieldParent, false);
                if (sparkles != null)
                    foreach (Image speck in sparkles)
                        if (speck != null) speck.rectTransform.SetParent(fieldParent, false);
                lifted = false;
            }
            cloth.anchoredPosition = clothHome;
            cloth.localScale = clothScale;
            leftClub.anchoredPosition = leftHome;
            rightClub.anchoredPosition = rightHome;
            mother.anchoredPosition = motherHome;
            mother.localEulerAngles = Vector3.zero;
            SetAngle(true, leftRest);
            SetAngle(false, rightRest);
            leftAway = rightAway = false;
            smooth = 0f;
            Face(motherIdle);
            if (bubble != null) bubble.text = "";
            if (sparkles != null)
                foreach (Image speck in sparkles) if (speck != null) speck.gameObject.SetActive(false);
        }

        // The other hand. A press on one key must not leave this club mid-swing.
        // A club that has left her grip finishes its flight instead of snapping back.
        public void Plant(bool left)
        {
            if (IsAway(left)) return;
            if (left)
            {
                if (leftMove != null) StopCoroutine(leftMove);
                leftMove = null;
            }
            else
            {
                if (rightMove != null) StopCoroutine(rightMove);
                rightMove = null;
            }
            RectTransform club = left ? leftClub : rightClub;
            club.anchoredPosition = Rest(left);
            SetAngle(left, Idle(left));
        }

        // The next hand leaves the cloth and holds the raised angle until the blow or a miss.
        public void Raise(bool left, float seconds)
        {
            if (IsAway(left)) return;
            PlayClub(left, LiftClub(left, seconds));
        }

        // The blow: one unbroken motion up to the top of the lift and down onto the cloth, where it stays.
        public RectTransform Club(bool left) { return left ? leftClub : rightClub; }

        public void Strike(bool left, float seconds = SwingSeconds)
        {
            PlayClub(left, Blow(left, Mathf.Max(LeastSwingSeconds, seconds)));
        }

        // A missed blow: the raised club sinks back to the cloth without striking.
        public void Lower(bool left, float seconds = SwingSeconds)
        {
            if (IsAway(left)) return;
            PlayClub(left, Sink(left, Mathf.Max(LeastSwingSeconds, seconds)));
        }

        // Same hand twice. The club leaves her grip, then comes home on the same lean.
        public void Fling(bool left)
        {
            PlayClub(left, Fly(left));
            if (bubble != null)
            {
                bubble.text = Strings.T("laundry.fly");
                bubble.color = Palette.DangerText;
            }
        }

        public void Stare()
        {
            if (look != null) StopCoroutine(look);
            look = StartCoroutine(Look());
        }

        // Each alternating blow spreads the cloth at once. The scale is never reset to 1.
        public void Flatten()
        {
            smooth = Mathf.Min(1f, smooth + .08f);
            if (press != null) StopCoroutine(press);
            press = StartCoroutine(PressCloth());
        }

        private IEnumerator PressCloth()
        {
            ApplyCloth(false);
            Vector3 spread = cloth.localScale;
            Vector3 struck = new Vector3(spread.x * 1.04f, spread.y * .9f, 1f);
            yield return PixelTween.Run(.07f, t => cloth.localScale = Vector3.Lerp(struck, spread, PixelTween.OutCubic(t)));
            ApplyCloth(false);
            press = null;
        }

        // The whole run on the beat: the cloth shines and lies flat, in front of the result.
        public void Celebrate()
        {
            smooth = 1f;
            ApplyCloth(true);
            Face(motherGlad);
            if (bubble != null)
            {
                bubble.text = Strings.T("laundry.spread");
                bubble.color = Palette.SunText;
            }
            Transform top = fieldParent != null ? fieldParent.parent : null;
            if (top != null)
            {
                cloth.SetParent(top, true);
                mother.SetParent(top, true);
                if (sparkles != null)
                    foreach (Image speck in sparkles)
                        if (speck != null) speck.rectTransform.SetParent(top, true);
                lifted = true;
            }
            celebrate = StartCoroutine(Shine());
        }

        private void ApplyCloth(bool full)
        {
            float x = Mathf.Lerp(1f, full ? 1.75f : 1.28f, smooth);
            float y = Mathf.Lerp(1f, full ? .36f : .74f, smooth);
            cloth.localScale = new Vector3(clothScale.x * x, clothScale.y * y, 1f);
        }

        // The club rises over most of its lead-in, easing out of the cloth and into the top, then holds
        // there waiting for the key.
        private IEnumerator LiftClub(bool left, float seconds)
        {
            SetAway(left, false);
            RectTransform club = left ? leftClub : rightClub;
            club.SetAsLastSibling();
            club.anchoredPosition = Rest(left);
            float from = left ? leftAngle : rightAngle, top = Raised(left);
            yield return PixelTween.Run(Mathf.Clamp(seconds * .7f, .1f, .3f), t =>
            {
                club.anchoredPosition = Rest(left);
                SetAngle(left, Mathf.LerpAngle(from, top, PixelTween.InOutSine(t)));
            });
        }

        // Pressed on the way up, the rest of the lift belongs to this same swing, so the arc never kinks.
        // The lift slows into the top and the fall turns straight back down from there. The fall moves
        // from its first frame and only gathers speed toward the cloth, so it never hangs and then drops.
        private IEnumerator Blow(bool left, float seconds)
        {
            SetAway(left, false);
            RectTransform club = left ? leftClub : rightClub;
            club.SetAsLastSibling();
            club.anchoredPosition = Rest(left);
            float top = Raised(left), rest = Idle(left), from = left ? leftAngle : rightAngle;
            float span = Mathf.Abs(Mathf.DeltaAngle(rest, top));
            float risen = span > 1f ? Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(rest, from)) / span) : 1f;
            float up = (1f - risen) * seconds * .8f;
            float total = up + seconds;
            float upTo = up / total;
            yield return PixelTween.Run(total, t =>
            {
                float angle = t < upTo ? Mathf.LerpAngle(from, top, OutQuad(t / upTo))
                    : Mathf.LerpAngle(top, rest, Fall((t - upTo) / (1f - upTo)));
                club.anchoredPosition = Rest(left);
                SetAngle(left, angle);
            });
        }

        // No strike: the head drifts back down to the cloth.
        private IEnumerator Sink(bool left, float seconds)
        {
            RectTransform club = left ? leftClub : rightClub;
            float from = left ? leftAngle : rightAngle, rest = Idle(left);
            yield return PixelTween.Run(Mathf.Max(.12f, seconds), t =>
            {
                club.anchoredPosition = Rest(left);
                SetAngle(left, Mathf.LerpAngle(from, rest, PixelTween.InOutSine(t)));
            });
        }

        private static float OutQuad(float t) { return 1f - (1f - t) * (1f - t); }

        // Already moving at the start, quickest at the cloth. Half way through the time it is a third of
        // the way down, where a cubic would still be an eighth.
        private static float Fall(float t) { return .45f * t + .55f * t * t; }

        // Same hand twice: the club tumbles up and away from the cloth, then returns to its lean.
        private IEnumerator Fly(bool left)
        {
            SetAway(left, true);
            RectTransform club = left ? leftClub : rightClub;
            club.SetAsLastSibling();
            Vector2 home = Rest(left);
            float homeAngle = Idle(left);
            Vector2 away = home + new Vector2(left ? -170f : 170f, 230f);
            float spun = homeAngle + (left ? 220f : -220f);
            yield return Travel(left, club.anchoredPosition, away, left ? leftAngle : rightAngle, spun, .16f);
            yield return new WaitForSecondsRealtime(.42f);
            yield return Travel(left, club.anchoredPosition, home, left ? leftAngle : rightAngle, homeAngle, .2f);
            club.anchoredPosition = home;
            SetAngle(left, homeAngle);
            SetAway(left, false);
        }

        private IEnumerator Travel(bool left, Vector2 from, Vector2 to, float fromAngle, float toAngle, float seconds)
        {
            RectTransform club = left ? leftClub : rightClub;
            yield return PixelTween.Run(seconds, t =>
            {
                float k = PixelTween.OutCubic(t);
                club.anchoredPosition = PixelTween.Snap(Vector2.Lerp(from, to, k));
                SetAngle(left, Mathf.LerpAngle(fromAngle, toAngle, k));
            });
        }

        private bool IsAway(bool left) { return left ? leftAway : rightAway; }

        private void SetAway(bool left, bool away)
        {
            if (left) leftAway = away;
            else rightAway = away;
        }

        // Both hands rest with the head on the cloth. A blow leaves that pose only while it is the next one.
        private float Idle(bool left) { return left ? leftRest : rightRest; }

        private Vector2 Rest(bool left) { return left ? leftHome : rightHome; }

        // Positive rotation swings a hanging head to the right, so the left club cocks that way.
        private float Inward(bool left)
        {
            return Rest(left).x <= clothHome.x ? 1f : -1f;
        }

        // The wind-up: the head sits well above the cloth, so the lifted hand is the pad to play.
        private float Raised(bool left)
        {
            float length = Length(left);
            if (length <= 1f) return Idle(left);
            float inward = Inward(left);
            float targetY = clothHome.y + clothHalf.y + Lift;
            float angle = Mathf.Acos(Mathf.Clamp((Rest(left).y - targetY) / length, -1f, 1f)) * Mathf.Rad2Deg;
            return FlatAngle(inward * angle);
        }

        // The end of the blow, the same turn every time: the head sits just under the cloth's top
        // face and stays inside its near edge, so it never swings off the cloth.
        private float StrikeAngle(bool left)
        {
            float length = Length(left);
            if (length <= 1f) return Idle(left);
            float inward = Inward(left);
            float turn = Mathf.Acos(Mathf.Clamp((Rest(left).y - (clothHome.y + clothHalf.y - Dip)) / length, -1f, 1f));
            // A shallower turn drops the head toward the outer edge; hold it on the cloth.
            float nearest = (clothHome.x - inward * (clothHalf.x - Inset) - Rest(left).x) * inward / length;
            float least = Mathf.Asin(Mathf.Clamp(nearest, -1f, 1f));
            return FlatAngle(inward * Mathf.Max(turn, least) * Mathf.Rad2Deg);
        }

        private float Length(bool left)
        {
            RectTransform club = left ? leftClub : rightClub;
            return club.pivot.y * club.sizeDelta.y * Mathf.Abs(club.localScale.y);
        }

        private static float FlatAngle(float degrees)
        {
            return Mathf.Repeat(degrees + 180f, 360f) - 180f;
        }

        private IEnumerator Look()
        {
            Face(motherLook);
            Vector2 home = mother.anchoredPosition;
            mother.anchoredPosition = home + new Vector2(-6f, 0f);
            yield return new WaitForSecondsRealtime(1.15f);
            mother.anchoredPosition = home;
            if (motherImage != null && motherImage.sprite == motherLook) Face(motherIdle);
            if (bubble != null && bubble.text == Strings.T("laundry.fly")) bubble.text = "";
            look = null;
        }

        private IEnumerator Shine()
        {
            if (sparkles == null) yield break;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < sparkles.Length; i++)
                {
                    Image speck = sparkles[i];
                    if (speck == null) continue;
                    float angle = t * 2.2f + i * (Mathf.PI * 2f / sparkles.Length);
                    float radius = 70f + Mathf.Sin(t * 3f + i) * 18f;
                    speck.gameObject.SetActive(true);
                    speck.rectTransform.anchoredPosition = cloth.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    speck.color = new Color(1f, .92f, .55f, .45f + .55f * Mathf.Abs(Mathf.Sin(t * 6f + i)));
                }
                if (lifted)
                {
                    cloth.SetAsLastSibling();
                    if (sparkles != null)
                        foreach (Image speck in sparkles)
                            if (speck != null) speck.rectTransform.SetAsLastSibling();
                    mother.SetAsLastSibling();
                }
                yield return null;
            }
        }

        private IEnumerator Glide(bool left, float angle, float seconds, bool whip = false)
        {
            RectTransform club = left ? leftClub : rightClub;
            float fromAngle = left ? leftAngle : rightAngle;
            club.anchoredPosition = Rest(left);
            yield return PixelTween.Run(Mathf.Max(.03f, seconds), t =>
            {
                float k = whip ? t : PixelTween.OutCubic(t);
                club.anchoredPosition = Rest(left);
                SetAngle(left, Mathf.LerpAngle(fromAngle, angle, k));
            });
        }

        private void PlayClub(bool left, IEnumerator routine)
        {
            if (left)
            {
                if (leftMove != null) StopCoroutine(leftMove);
                leftMove = StartCoroutine(routine);
            }
            else
            {
                if (rightMove != null) StopCoroutine(rightMove);
                rightMove = StartCoroutine(routine);
            }
        }

        private void SetAngle(bool left, float angle)
        {
            if (left) { leftAngle = angle; leftClub.localEulerAngles = new Vector3(0f, 0f, angle); }
            else { rightAngle = angle; rightClub.localEulerAngles = new Vector3(0f, 0f, angle); }
        }

        private void Face(Sprite sprite)
        {
            if (motherImage != null && sprite != null) motherImage.sprite = sprite;
        }
    }
}

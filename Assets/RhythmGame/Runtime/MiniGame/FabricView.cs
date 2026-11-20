using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // A cloth on a block, a club in each hand, and Mother beside them.
    // A key raises that club to one fixed angle and turns it down onto the cloth. The same hand
    // twice throws that club away and Mother stares. An all-perfect run shines the cloth flat.
    public sealed class FabricView : MonoBehaviour
    {
        private const float Lean = 60f;   // the resting lean, mirrored. Both hands share this one angle.
        private const float Cock = 60f;   // the one raised angle, mirrored per hand. Never relative to the lean.
        private const float Dip = 6f;     // how far under the cloth's top face the head settles
        private const float Inset = 12f;  // the head stays this far inside the cloth's near edge
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
            leftRest = Lean;
            rightRest = -Lean;
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

        // The club comes down on the cloth, then rests.
        public void Strike(bool left)
        {
            PlayClub(left, Blow(left));
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

        // Every blow is the same arc, first press included: cocked, then down onto the cloth, then rest.
        private IEnumerator Blow(bool left)
        {
            SetAway(left, false);
            RectTransform club = left ? leftClub : rightClub;
            club.SetAsLastSibling();
            club.anchoredPosition = Rest(left);
            SetAngle(left, Cocked(left));
            yield return Glide(left, StrikeAngle(left), .04f, true);
            yield return new WaitForSecondsRealtime(.02f);
            yield return Glide(left, Idle(left), .06f);
        }

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

        // Both hands rest on the same lean, mirrored. A blow leaves it and comes back to it.
        private float Idle(bool left) { return left ? leftRest : rightRest; }

        private Vector2 Rest(bool left) { return left ? leftHome : rightHome; }

        // Positive rotation swings a hanging head to the right, so the left club cocks that way.
        private float Inward(bool left)
        {
            return Rest(left).x <= clothHome.x ? 1f : -1f;
        }

        // Where every blow starts: a fixed 60 degrees in toward the cloth, the same on both hands.
        private float Cocked(bool left)
        {
            return Inward(left) * Cock;
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

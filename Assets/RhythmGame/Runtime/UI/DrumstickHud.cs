using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Chestnut total pinned to the top of the 1280x720 playfield. Each award launches "+n" from the
    // score; it fades as it reaches the total, and the number then counts up and pops.
    public sealed class DrumstickHud : MonoBehaviour
    {
        private static readonly Color32 Chestnut = new Color32(122, 78, 46, 255);
        private static readonly Color32 Cream = new Color32(255, 236, 214, 255);
        private static readonly Color32 Edge = new Color32(168, 164, 158, 255);

        private PadFlashPool pool;
        private RectTransform space, score;
        private Image pill;
        private Text total, notice;
        private long shown, goal;
        private float punch;
        private int epoch, airborne;
        private bool awards = true;

        public static DrumstickHud Create(PadFlashPool pool, Font font)
        {
            DrumstickHud hud = pool.gameObject.AddComponent<DrumstickHud>();
            hud.Build(pool, font);
            return hud;
        }

        public void FollowScore(RectTransform score) { this.score = score; }

        public void Awards(bool on) { awards = on; }

        // Snap the printed total. "+n" still in the air from an older round no longer changes it.
        public void Reset(long amount)
        {
            epoch++;
            airborne = 0;
            shown = goal = amount;
            punch = 0f;
            if (total != null) total.rectTransform.localScale = Vector3.one;
            if (notice != null) notice.text = Strings.T("minigame.stick_pad_only");
            Paint();
            if (pill != null) pill.gameObject.SetActive(true);
            if (notice != null) notice.transform.parent.gameObject.SetActive(true);
        }

        public void Award(long gain)
        {
            if (!awards || gain <= 0 || pool == null) return;
            int lane = airborne;
            airborne++;
            int token = epoch;
            pool.Rise(Launch(lane), PillPoint(), "+" + gain.ToString("N0"), () =>
            {
                airborne = Mathf.Max(0, airborne - 1);
                if (token != epoch) return;
                goal += gain;
                punch = .32f;
            });
        }

        private void Update()
        {
            if (total == null) return;
            if (shown < goal)
            {
                long gap = goal - shown;
                long step = System.Math.Max(1L, (long)System.Math.Ceiling(gap * Time.unscaledDeltaTime / .4f));
                shown += step;
                if (shown > goal) shown = goal;
                Paint();
            }
            if (punch > 0f)
            {
                punch -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(punch / .32f);
                float scale = 1f + .28f * Mathf.Sin(k * Mathf.PI);
                total.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            else if (total.rectTransform.localScale != Vector3.one)
                total.rectTransform.localScale = Vector3.one;
        }

        private void Build(PadFlashPool pool, Font font)
        {
            this.pool = pool;
            space = pool.GetComponent<RectTransform>();
            var pillObject = new GameObject("StickPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pillObject.transform.SetParent(pool.transform, false);
            pill = pillObject.GetComponent<Image>();
            pill.color = Chestnut;
            pill.raycastTarget = false;
            RectTransform rect = pill.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(340f, 48f);
            rect.anchoredPosition = new Vector2(0f, 300f);
            FlatRim(rect);
            total = Label("Total", pill.transform, font, 22, Cream);
            Stretch(total.rectTransform);

            var noteBar = new GameObject("StickNote", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            noteBar.transform.SetParent(pool.transform, false);
            Image bar = noteBar.GetComponent<Image>();
            bar.color = Chestnut;
            bar.raycastTarget = false;
            RectTransform noteRect = bar.rectTransform;
            noteRect.anchorMin = noteRect.anchorMax = new Vector2(.5f, .5f);
            noteRect.pivot = new Vector2(.5f, .5f);
            noteRect.sizeDelta = new Vector2(1100f, 32f);
            noteRect.anchoredPosition = new Vector2(0f, -334f);
            notice = Label("Line", noteBar.transform, font, 15, Cream);
            Stretch(notice.rectTransform);
            notice.text = Strings.T("minigame.stick_pad_only");
        }

        // A column under the total, starting down at the score's height so each "+n" rises into the pill.
        private Vector2 Launch(int lane)
        {
            float y = 160f;
            if (score != null && space != null)
            {
                Vector3 local = space.InverseTransformPoint(score.TransformPoint(score.rect.center));
                y = local.y - 80f;
            }
            return new Vector2(PillPoint().x, y - lane * 96f);
        }

        private Vector2 PillPoint()
        {
            return pill != null ? pill.rectTransform.anchoredPosition + new Vector2(0f, -8f) : new Vector2(0f, 300f);
        }

        private void Paint()
        {
            if (total == null) return;
            total.text = Strings.T("minigame.stick_total") + shown.ToString("N0");
        }

        // One art pixel of the same gray on every side.
        private static void FlatRim(RectTransform parent)
        {
            float edge = PixelTween.Pixel;
            Rim(parent, "Top", Edge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -edge), Vector2.zero);
            Rim(parent, "Right", Edge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-edge, 0f), Vector2.zero);
            Rim(parent, "Left", Edge, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(edge, 0f));
            Rim(parent, "Bottom", Edge, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, edge));
        }

        private static void Rim(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Text Label(string name, Transform parent, Font font, int size, Color color)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}

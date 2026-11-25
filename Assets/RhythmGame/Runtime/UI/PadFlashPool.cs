using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Hit flashes and drumstick "+n" labels share one pool. Play() and Rise() only restart a slot,
    // so a strike never allocates and never waits on the effect.
    public sealed class PadFlashPool : MonoBehaviour
    {
        public enum Kind { Perfect, Good, Miss }

        private const int Flashes = 12, Floaters = 6;
        private Image[] slot;
        private float[] age, length, width;
        private Color[] tint;
        private int live;
        private RectTransform[] floaterRoot;
        private CanvasGroup[] floaterGroup;
        private Text[] floater, floaterMid, floaterDeep;
        private float[] floaterAge, floaterLength;
        private Vector2[] floaterFrom, floaterTo;
        private Action[] floaterDone;
        private int floaterLive;
        private static readonly Color32 FaceGold = new Color32(255, 236, 64, 255);
        private static readonly Color32 Extrude = new Color32(232, 112, 16, 255);
        private static readonly Color32 ExtrudeDeep = new Color32(92, 36, 8, 255);
        private Font font;
        private static Sprite burst;

        private static readonly Color Core = new Color32(244, 154, 160, 255);
        private static readonly Color Ray = new Color32(242, 166, 92, 255);

        public static PadFlashPool Create(Transform parent)
        {
            var root = new GameObject("PadFlashes", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var space = (RectTransform)root.transform;
            space.anchorMin = space.anchorMax = new Vector2(.5f, .5f);
            space.pivot = new Vector2(.5f, .5f);
            space.sizeDelta = new Vector2(1280f, 720f);
            space.anchoredPosition = Vector2.zero;
            root.transform.SetAsLastSibling();
            return root.AddComponent<PadFlashPool>();
        }

        public void SetFont(Font font) { this.font = font; }

        private void Awake()
        {
            slot = new Image[Flashes];
            age = new float[Flashes];
            length = new float[Flashes];
            width = new float[Flashes];
            tint = new Color[Flashes];
            Sprite sprite = Burst();
            for (int i = 0; i < Flashes; i++)
            {
                var image = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(transform, false);
                image.sprite = sprite;
                image.raycastTarget = false;
                image.enabled = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                slot[i] = image;
                age[i] = 1f;
            }
            floaterRoot = new RectTransform[Floaters];
            floaterGroup = new CanvasGroup[Floaters];
            floater = new Text[Floaters];
            floaterMid = new Text[Floaters];
            floaterDeep = new Text[Floaters];
            floaterAge = new float[Floaters];
            floaterLength = new float[Floaters];
            floaterFrom = new Vector2[Floaters];
            floaterTo = new Vector2[Floaters];
            floaterDone = new Action[Floaters];
            for (int i = 0; i < Floaters; i++)
            {
                var block = new GameObject("Plus", typeof(RectTransform), typeof(CanvasGroup));
                block.transform.SetParent(transform, false);
                RectTransform root = (RectTransform)block.transform;
                root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
                root.pivot = new Vector2(.5f, .5f);
                root.sizeDelta = new Vector2(420f, 120f);
                floaterRoot[i] = root;
                floaterGroup[i] = block.GetComponent<CanvasGroup>();
                floaterGroup[i].blocksRaycasts = false;
                floaterGroup[i].interactable = false;
                // Two extrusion steps, then the bright face, so the digits read as a chunk of gold.
                floaterDeep[i] = FloaterLayer(root, ExtrudeDeep, new Vector2(8f, -8f));
                floaterMid[i] = FloaterLayer(root, Extrude, new Vector2(4f, -4f));
                floater[i] = FloaterLayer(root, FaceGold, Vector2.zero);
                block.SetActive(false);
                floaterAge[i] = 1f;
            }
        }

        public void Play(RectTransform target, Kind kind)
        {
            if (target == null) return;
            Vector3 local = ((RectTransform)transform).InverseTransformPoint(target.TransformPoint(target.rect.center));
            Play(new Vector2(local.x, local.y), kind);
        }

        public void Play(Vector2 anchored, Kind kind)
        {
            if (slot == null) return;
            transform.SetAsLastSibling();
            int index = 0;
            float oldest = -1f;
            for (int i = 0; i < Flashes; i++)
            {
                if (age[i] >= 1f) { index = i; break; }
                if (age[i] > oldest) { oldest = age[i]; index = i; }
            }
            bool wasIdle = age[index] >= 1f;
            Style(kind, out width[index], out length[index], out tint[index]);
            age[index] = 0f;
            RectTransform rect = slot[index].rectTransform;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(width[index], width[index]);
            rect.localScale = new Vector3(.35f, .35f, 1f);
            slot[index].color = tint[index];
            slot[index].enabled = true;
            if (wasIdle) live++;
        }

        // "+n" travels from the score up to the total and fades as it arrives. `done` runs once, at the total.
        public void Rise(Vector2 from, Vector2 to, string line, Action done)
        {
            if (floater == null || string.IsNullOrEmpty(line)) return;
            transform.SetAsLastSibling();
            int index = 0;
            float oldest = -1f;
            for (int i = 0; i < Floaters; i++)
            {
                if (floaterAge[i] >= 1f) { index = i; break; }
                if (floaterAge[i] > oldest) { oldest = floaterAge[i]; index = i; }
            }
            bool wasIdle = floaterAge[index] >= 1f;
            if (!wasIdle && floaterDone[index] != null)
            {
                Action early = floaterDone[index];
                floaterDone[index] = null;
                early();
            }
            float distance = Vector2.Distance(from, to);
            floaterAge[index] = 0f;
            floaterLength[index] = Mathf.Max(.5f, distance / 280f);
            floaterFrom[index] = from;
            floaterTo[index] = to;
            floaterDone[index] = done;
            RectTransform block = floaterRoot[index];
            block.gameObject.SetActive(true);
            block.anchoredPosition = from;
            block.localScale = new Vector3(.4f, .4f, 1f);
            floaterGroup[index].alpha = 1f;
            PaintFloater(index, line);
            if (wasIdle) floaterLive++;
        }

        // Gold face with two pixel steps down-right, the arcade extrusion.
        private static Text FloaterLayer(RectTransform parent, Color color, Vector2 offset)
        {
            var text = new GameObject("Digit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 72;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(420f, 120f);
            rect.anchoredPosition = offset;
            return text;
        }

        private void PaintFloater(int index, string line)
        {
            Text[] layers = { floaterDeep[index], floaterMid[index], floater[index] };
            for (int i = 0; i < layers.Length; i++)
            {
                if (font != null) layers[i].font = font;
                layers[i].text = line;
            }
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime;
            if (live > 0)
            {
                for (int i = 0; i < Flashes; i++)
                {
                    if (age[i] >= 1f) continue;
                    age[i] += step / length[i];
                    float t = age[i] < 1f ? age[i] : 1f;
                    float spread = t < .3f ? Mathf.Lerp(.35f, 1.08f, t / .3f) : Mathf.Lerp(1.08f, 1f, (t - .3f) / .7f);
                    slot[i].rectTransform.localScale = new Vector3(spread, spread, 1f);
                    Color color = tint[i];
                    color.a *= t < .4f ? 1f : 1f - (t - .4f) / .6f;
                    slot[i].color = color;
                    if (t < 1f) continue;
                    slot[i].enabled = false;
                    live--;
                }
            }
            if (floaterLive == 0) return;
            for (int i = 0; i < Floaters; i++)
            {
                if (floaterAge[i] >= 1f) continue;
                floaterAge[i] += step / floaterLength[i];
                float t = floaterAge[i] < 1f ? floaterAge[i] : 1f;
                Vector2 at = Vector2.Lerp(floaterFrom[i], floaterTo[i], t);
                floaterRoot[i].anchoredPosition = at;
                float pop = t < .16f ? Mathf.Lerp(.4f, 1.22f, t / .16f) : Mathf.Lerp(1.22f, 1f, (t - .16f) / .84f);
                floaterRoot[i].localScale = new Vector3(pop, pop, 1f);
                float remain = Vector2.Distance(at, floaterTo[i]);
                floaterGroup[i].alpha = remain > 110f ? 1f : Mathf.Clamp01(remain / 110f);
                if (t < 1f) continue;
                floaterRoot[i].gameObject.SetActive(false);
                floaterLive--;
                Action done = floaterDone[i];
                floaterDone[i] = null;
                if (done != null) done();
            }
        }

        private static void Style(Kind kind, out float size, out float seconds, out Color color)
        {
            if (kind == Kind.Perfect)
            {
                size = 220f;
                seconds = .32f;
                color = Color.white;
                return;
            }
            if (kind == Kind.Good)
            {
                size = 150f;
                seconds = .26f;
                color = new Color(1f, 1f, 1f, .95f);
                return;
            }
            size = 96f;
            seconds = .2f;
            color = new Color(.55f, .55f, .58f, .8f);
        }

        // Coral centre with orange rays, matching the strike-effect reference.
        private static Sprite Burst()
        {
            if (burst != null) return burst;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (n - 1) * .5f;
            const int rays = 12;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - center, dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                    if (dist <= .34f)
                    {
                        tex.SetPixel(x, y, Core);
                        continue;
                    }
                    float turns = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * rays;
                    float spoke = Mathf.Abs(turns - Mathf.Round(turns));
                    bool ray = dist < .96f && spoke < .16f;
                    tex.SetPixel(x, y, ray ? Ray : new Color(1f, 1f, 1f, 0f));
                }
            tex.Apply();
            burst = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(.5f, .5f), n);
            return burst;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Bevel, a 2-art-pixel drop shadow, and a sunken press for every screen that already
    // carries MenuTypography: login, home, the mini-game list, and each game.
    public static class PixelChrome
    {
        // One art pixel is PixelTween.Pixel canvas units. The shadow is two of those, straight down.
        public static readonly Vector2 Drop = new Vector2(0f, -PixelTween.Pixel * 2f);
        private static readonly Color ShadowColor = new Color(34f / 255f, 34f / 255f, 34f / 255f, 120f / 255f);
        private static Sprite bevel;

        public static void Apply(Transform root)
        {
            if (root == null) return;
            Sprite frame = Bevel();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null) continue;
                if (IsFrame(image))
                {
                    image.sprite = frame;
                    image.type = Image.Type.Sliced;
                    EnsureShadow(image.gameObject);
                }
                else if (IsCharacter(image))
                    EnsureShadow(image.gameObject);
                Button button = image.GetComponent<Button>();
                if (button != null && button.GetComponent<PixelPress>() == null)
                    button.gameObject.AddComponent<PixelPress>();
            }
        }

        private static bool IsFrame(Image image)
        {
            return image.sprite != null && image.sprite.name.StartsWith("px_frame");
        }

        private static bool IsCharacter(Image image)
        {
            if (image.GetComponent<Button>() != null || !image.preserveAspect) return false;
            string name = image.name;
            return name == "Guide" || name == "Portrait" || name.StartsWith("Guide");
        }

        private static void EnsureShadow(GameObject target)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null) shadow = target.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = Drop;
            shadow.useGraphicAlpha = true;
        }

        // 12x12, 3px slice. Light falls from the top: three bright steps, a mid body, three dark steps.
        public static Sprite Bevel()
        {
            if (bevel != null) return bevel;
            const int n = 12;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool corner = (x == 0 || x == n - 1) && (y == 0 || y == n - 1);
                    if (corner)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float tone = Tone(x, y, n);
                    tex.SetPixel(x, y, new Color(tone, tone, tone, 1f));
                }
            tex.Apply();
            bevel = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3f, 3f, 3f, 3f));
            bevel.name = "px_frame";
            return bevel;
        }

        // y is a Unity texture row: 0 is the bottom of the sprite.
        public static float Tone(int x, int y, int n)
        {
            float tone;
            if (y == n - 1) tone = 1f;
            else if (y == n - 2) tone = .94f;
            else if (y == n - 3) tone = .88f;
            else if (y == 0) tone = .34f;
            else if (y == 1) tone = .5f;
            else if (y == 2) tone = .66f;
            else tone = .84f;
            if (x == 0) tone = Mathf.Min(1f, tone + .06f);
            else if (x == 1) tone = Mathf.Min(1f, tone + .03f);
            else if (x == n - 1) tone *= .78f;
            else if (x == n - 2) tone *= .9f;
            return tone;
        }
    }

    // A press covers its own shadow, so the button reads as pushed into the panel.
    public sealed class PixelPress : MonoBehaviour, IPointerDownHandler
    {
        private RectTransform rect;
        private Shadow shadow;
        private Vector2 home;
        private bool sunk;

        private void Awake()
        {
            rect = (RectTransform)transform;
            shadow = GetComponent<Shadow>();
        }

        private void OnDisable() { Rise(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Button button = GetComponent<Button>();
            if (button != null && !button.interactable) return;
            if (sunk || rect == null) return;
            home = rect.anchoredPosition;
            rect.anchoredPosition = home + PixelChrome.Drop;
            if (shadow != null) shadow.effectDistance = Vector2.zero;
            sunk = true;
        }

        private void Update()
        {
            if (!sunk) return;
            if (Pointer.current != null && Pointer.current.press.isPressed) return;
            Rise();
        }

        private void Rise()
        {
            if (!sunk || rect == null) return;
            rect.anchoredPosition = home;
            if (shadow != null) shadow.effectDistance = PixelChrome.Drop;
            sunk = false;
        }
    }
}

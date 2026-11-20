using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Rebuilds the main menu canvas in place with pixel-art styling. The MainMenuController component
    // (and its popup prefab references) is kept; only the canvas children are regenerated.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.PixelMenuBuilder.Rebuild
    public static class PixelMenuBuilder
    {
        private const string ArtDir = "Assets/RhythmGame/Art/UI";
        private const string FramePath = ArtDir + "/px_frame.png";
        internal const string GridPath = ArtDir + "/px_grid.png";
        internal const string ScanlinePath = ArtDir + "/px_scanlines.png";
        internal const string SolidPath = ArtDir + "/px_solid.png";
        // Official Hos!m! wordmark (client reply v5 §11-2): black for light backgrounds.
        internal const string LogoPath = ArtDir + "/logo_black.png";
        private const string MenuMusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/MenuBeat.wav";
        private const float Px = PixelTween.Pixel;
        internal const float FrameFill = Palette.FrameFill;
        private static Font font;

        [MenuItem("Rhythm Game/Rebuild Pixel Main Menu")]
        public static void Rebuild()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf");
            Sprite frame = CreateArt();
            var scene = EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            var controller = Object.FindFirstObjectByType<MainMenuController>();
            Transform canvas = controller.transform;
            for (int i = canvas.childCount - 1; i >= 0; i--) Object.DestroyImmediate(canvas.GetChild(i).gameObject);

            Stretch(Box("Background", canvas, Palette.Background, null).rectTransform).GetComponent<Image>().raycastTarget = false;
            RawImage grid = Raw("Grid", canvas, AssetDatabase.LoadAssetAtPath<Texture2D>(GridPath), new Rect(0, 0, 1280 / 64f, 720 / 64f));

            RectTransform menu = Rect("Menu", canvas, Vector2.zero, new Vector2(1200, 680));
            CanvasGroup group = menu.gameObject.AddComponent<CanvasGroup>();

            // Client reply v5 §4. Left: the official logo, the guide character large with a one-line
            // speech bubble, and beat bars on the menu BGM. Right: three big modes, two secondary
            // buttons and three small ones; drumsticks pinned top right.
            Image logo = Box("Logo", menu, Color.white, ImportLogo());
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Place(logo.rectTransform, new Vector2(-404, 300), new Vector2(384, 84));
            Image bubble = Box("Bubble", menu, Palette.Panel, frame);
            Place(bubble.rectTransform, new Vector2(-380, 196), new Vector2(456, 72));
            bubble.raycastTarget = false;
            Shadow(bubble.gameObject, Palette.DropShadow, new Vector2(Px, -Px));
            Text bubbleText = Label("BubbleText", bubble.transform, "", Vector2.zero, new Vector2(424, 56), 24, Palette.Ink, TextAnchor.MiddleCenter);
            Image guide = Box("Guide", menu, Color.white, null);
            guide.preserveAspect = true;
            guide.raycastTarget = false;
            Place(guide.rectTransform, new Vector2(-380, -24), new Vector2(336, 336));
            guide.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RhythmGame/Resources/RhythmGame/Portraits/whale.png");
            var bars = new RectTransform[18];
            for (int i = 0; i < bars.Length; i++)
            {
                Image bar = Box("BeatBar" + i, menu, Palette.Rainbow[i % Palette.Rainbow.Length], null);
                bar.raycastTarget = false;
                bar.rectTransform.pivot = new Vector2(.5f, 0);
                float height = PixelTween.Snap(24 + Mathf.Abs(Mathf.Sin(i * .67f)) * 56, Px * 2);
                Place(bar.rectTransform, new Vector2(-584 + i * 24, -300), new Vector2(12, height));
                bars[i] = bar.rectTransform;
            }

            Image pill = Box("Balance", menu, Palette.Lift(Palette.Orange), frame);
            Place(pill.rectTransform, new Vector2(436, 300), new Vector2(328, 56));
            pill.raycastTarget = false;
            Text balance = Label("BalanceText", pill.transform, "", Vector2.zero, new Vector2(304, 48), 24, Palette.Ink, TextAnchor.MiddleCenter);

            // Names are what scripts and tests look up.
            Button lessons = PixelButton("LessonButton", menu, frame, "鼓练习", new Vector2(300, 188), new Vector2(560, 96), Palette.Purple, Palette.Ink, 32);
            Button start = PixelButton("StartGameButton", menu, frame, "节奏游戏", new Vector2(300, 76), new Vector2(560, 96), Palette.Red, Palette.Ink, 32);
            Button miniGames = PixelButton("MiniGamesButton", menu, frame, "鼓点小游戏", new Vector2(300, -36), new Vector2(560, 96), Palette.Blue, Palette.Ink, 32);
            Button characters = PixelButton("CharacterButton", menu, frame, "角色", new Vector2(156, -140), new Vector2(272, 72), Palette.Panel, Palette.Ink, 24);
            Button records = PixelButton("RecordsButton", menu, frame, "战绩", new Vector2(444, -140), new Vector2(272, 72), Palette.Panel, Palette.Ink, 24);
            Button settings = PixelButton("SettingsButton", menu, frame, "设置", new Vector2(104, -232), new Vector2(176, 64), Palette.Well, Palette.Ink, 20);
            Button inquiry = PixelButton("InquiryButton", menu, frame, "咨询", new Vector2(300, -232), new Vector2(176, 64), Palette.Well, Palette.Ink, 20);
            Button store = PixelButton("StoreButton", menu, frame, "官方商城", new Vector2(496, -232), new Vector2(176, 64), Palette.Well, Palette.Ink, 20);
            Text status = Label("Status", menu, "", new Vector2(300, -292), new Vector2(560, 32), 20, Palette.DangerText, TextAnchor.MiddleCenter);

            AudioSource music = new GameObject("Music", typeof(AudioSource)).GetComponent<AudioSource>();
            music.transform.SetParent(canvas, false);
            music.playOnAwake = false;
            GameSceneBuilder.GenerateAudio(MenuMusicPath, 32);
            AssetDatabase.ImportAsset(MenuMusicPath);
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath);
            MenuTypography typography = controller.GetComponent<MenuTypography>();
            if (typography == null) typography = controller.gameObject.AddComponent<MenuTypography>();
            Assign(typography, "fontOverride", font);

            RectTransform popupRoot = Stretch(Rect("PopupRoot", canvas, Vector2.zero, Vector2.zero));
            RawImage scanlines = Raw("Scanlines", canvas, AssetDatabase.LoadAssetAtPath<Texture2D>(ScanlinePath), new Rect(0, 0, 1, 720 / 4f));
            scanlines.raycastTarget = false;

            // Shutter bars for the reveal / exit; inactive in the scene so the editor view stays clear.
            RectTransform wipe = Stretch(Rect("Wipe", canvas, Vector2.zero, Vector2.zero));
            var wipeBars = new RectTransform[12];
            for (int i = 0; i < wipeBars.Length; i++)
            {
                Image bar = Box("Bar" + i, wipe, Palette.Shutter, null);
                bar.raycastTarget = false;
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0, i / 12f); rect.anchorMax = new Vector2(1, (i + 1) / 12f);
                rect.pivot = new Vector2(i % 2, .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                bar.gameObject.SetActive(false);
                wipeBars[i] = rect;
            }

            Assign(controller, "lessonButton", lessons, "startButton", start, "miniGamesButton", miniGames,
                "characterButton", characters, "recordsButton", records, "settingsButton", settings, "inquiryButton", inquiry,
                "storeButton", store, "menuGroup", group, "popupRoot", popupRoot, "balanceText", balance, "statusText", status,
                "bubbleText", bubbleText, "guidePortrait", guide, "music", music);
            MainMenuAnimator animator = controller.GetComponent<MainMenuAnimator>();
            if (animator == null) animator = controller.gameObject.AddComponent<MainMenuAnimator>();
            Assign(animator, "grid", grid, "logo", logo.rectTransform, "guide", guide.rectTransform, "bubble", bubble.rectTransform, "music", music);
            AssignArray(animator, "wipeBars", wipeBars);
            AssignArray(animator, "beatBars", bars);
            AssignArray(animator, "buttons", new Object[] { lessons.transform, start.transform, miniGames.transform, characters.transform,
                records.transform, settings.transform, inquiry.transform, store.transform });

            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            if (events != null) events.firstSelectedGameObject = start.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_MENU_READY");
        }

        [MenuItem("Rhythm Game/Capture Main Menu Preview")]
        public static void CapturePreview() { Capture(MainMenuBuilder.ScenePath, "main-menu-preview.png"); }

        [MenuItem("Rhythm Game/Capture Game Preview")]
        public static void CaptureGamePreview()
        {
            Capture(GameSceneBuilder.ScenePath, "game-preview.png");
            Capture(GameSceneBuilder.ScenePath, "game-play-preview.png", "Modal");
        }

        // Renders a scene's resting layout (no animation) to <project>/<file>, optionally with one
        // root-canvas child hidden. The scene is reopened from disk each time and never saved.
        public static void Capture(string scenePath, string file, string hide = null)
        {
            EditorSceneManager.OpenScene(scenePath);
            if (hide != null) Object.FindFirstObjectByType<Canvas>().transform.Find(hide).gameObject.SetActive(false);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Camera camera = Camera.main;
            var target = new RenderTexture(1920, 1080, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            Debug.Log("MENU_PREVIEW_READY");
        }

        // ---- Generated pixel art ----

        internal static Sprite ImportLogo()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        }

        internal static Sprite CreateArt()
        {
            Directory.CreateDirectory(ArtDir);
            // 12x12 frame: 1px soft outline with notched corners and a bottom-right shade; the fill is FrameFill.
            var frame = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 11 || y == 11;
                    bool corner = (x == 0 || x == 11) && (y == 0 || y == 11);
                    float grey = edge ? .24f : (y == 1 || x == 10) ? .86f : FrameFill;
                    frame.SetPixel(x, y, corner ? Color.clear : new Color(grey, grey, grey, 1));
                }
            WritePng(frame, FramePath);
            // 16x16 grid cell (64 units): faint 1px lines on two edges, tiled across the background.
            var grid = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    grid.SetPixel(x, y, x == 0 || y == 0 ? new Color(Palette.GridLine.r, Palette.GridLine.g, Palette.GridLine.b, .3f) : Color.clear);
            WritePng(grid, GridPath);
            var scan = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            scan.SetPixel(0, 0, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, .03f)); scan.SetPixel(0, 1, Color.clear);
            WritePng(scan, ScanlinePath);

            // 4x4 solid white: crisp fills for bars (Filled images need a sprite).
            var solid = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) solid.SetPixel(x, y, Color.white);
            WritePng(solid, SolidPath);

            Import(FramePath, true, new Vector4(3, 3, 3, 3));
            Import(SolidPath, true, Vector4.zero);
            Import(GridPath, false, Vector4.zero);
            Import(ScanlinePath, false, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        }

        private static void WritePng(Texture2D texture, string path)
        {
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Import(string path, bool sprite, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) { importer.spriteImportMode = SpriteImportMode.Single; importer.spriteBorder = border; importer.spritePixelsPerUnit = 100; }
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = sprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // ---- UI helpers (1280x720 reference units, sizes multiples of one art pixel) ----

        internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, position, size);
            return rect;
        }

        internal static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        internal static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image Box(string name, Transform parent, Color color, Sprite sprite)
        {
            Image image = Rect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                // Sprite texel = 1 unit at 100 PPU; 0.25 makes each texel one 4-unit art pixel.
                image.pixelsPerUnitMultiplier = 1 / Px;
            }
            return image;
        }

        internal static RawImage Raw(string name, Transform parent, Texture2D texture, Rect uv)
        {
            RawImage image = Stretch(Rect(name, parent, Vector2.zero, Vector2.zero)).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            Text label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = font != null ? font : (font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        internal static void Shadow(GameObject target, Color color, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        // Hard drop shadow, no hover/press motion (mobile); press tint switches instantly.
        internal static Button PixelButton(string name, Transform parent, Sprite frame, string text, Vector2 position, Vector2 size,
            Color fill, Color foreground, int fontSize)
        {
            Image image = Box(name, parent, new Color(Mathf.Min(1, fill.r / FrameFill), Mathf.Min(1, fill.g / FrameFill), Mathf.Min(1, fill.b / FrameFill)), frame);
            Place(image.rectTransform, position, size);
            Shadow(image.gameObject, Palette.DropShadow, new Vector2(Px, -Px));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.85f, .85f, .85f);
            colors.disabledColor = new Color(1, 1, 1, .45f);   // faded, not darkened
            colors.fadeDuration = 0;
            button.colors = colors;
            Text label = Label("Label", image.transform, text, new Vector2(0, Px / 2), size, fontSize, foreground, TextAnchor.MiddleCenter);
            if (foreground.grayscale > .6f) Shadow(label.gameObject, new Color(0, 0, 0, .35f), new Vector2(2, -2));   // light text only
            return button;
        }

        internal static void Assign(Object target, params object[] fields)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i += 2)
                serialized.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AssignArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

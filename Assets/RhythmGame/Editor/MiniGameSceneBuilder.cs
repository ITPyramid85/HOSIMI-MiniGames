using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Everything a mini-game scene shares: camera, canvas, background, HUD (time bar, score, combo,
    // judgement, countdown, cycle), the start / result modal with its pad diagram, screen wipe, audio
    // and the MiniGameController wiring. A game builder calls Begin, fills Field, then Finish and Save.
    internal static class MiniGameSceneBuilder
    {
        private const float Px = PixelTween.Pixel;
        // The physical drum pad (plan §1): two top corners, three middle, three bottom. Index = pad.
        internal static readonly Vector2[] PadLayout = {
            new Vector2(-400, 140), new Vector2(400, 140),
            new Vector2(-216, -16), new Vector2(0, -16), new Vector2(216, -16),
            new Vector2(-216, -196), new Vector2(0, -196), new Vector2(216, -196) };
        internal static readonly string[] PadKeys = { "Q", "E", "A", "S", "D", "Z", "X", "C" };

        internal sealed class Scene
        {
            public UnityEngine.SceneManagement.Scene Unity;
            public GameObject Canvas;
            public Transform Root;
            public RectTransform Field;   // the game's own area, between background and HUD
            public Sprite Frame, Solid;
            public RawImage Grid;
        }

        internal static Scene Begin(string canvasName, Color background)
        {
            var s = new Scene { Frame = PixelMenuBuilder.CreateArt() };
            s.Solid = AssetDatabase.LoadAssetAtPath<Sprite>(PixelMenuBuilder.SolidPath);
            s.Unity = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = background;
            camera.transform.position = new Vector3(0, 0, -10);
            s.Canvas = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(MenuTypography));
            s.Canvas.layer = 5;
            // Without an override MenuTypography falls back to the OS font at runtime.
            PixelMenuBuilder.Assign(s.Canvas.GetComponent<MenuTypography>(), "fontOverride",
                AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            s.Canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = s.Canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            s.Root = s.Canvas.transform;
            PixelMenuBuilder.Stretch(PixelMenuBuilder.Box("Background", s.Root, background, null).rectTransform).GetComponent<Image>().raycastTarget = false;
            s.Grid = PixelMenuBuilder.Raw("Grid", s.Root, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.GridPath), new Rect(0, 0, 1280 / 64f, 720 / 64f));
            s.Grid.color = new Color(1, 1, 1, .5f);
            s.Field = PixelMenuBuilder.Rect("Field", s.Root, Vector2.zero, new Vector2(1280, 720));
            return s;
        }

        // Adds HUD, modal, wipe and audio, then the controller with the shared fields assigned.
        // Assign the game's own fields on the returned controller, then call Save.
        internal static T Finish<T>(Scene s, string musicPath, int musicSeconds) where T : MiniGameController
        {
            GameSceneBuilder.GenerateAudio(musicPath, musicSeconds);
            AssetDatabase.ImportAsset(musicPath);
            Transform root = s.Root;

            // HUD: progress through the current stage across the top, score and lives left, combo centre
            // (plan §3), stage and elapsed time right. Runs have no time limit (client reply v5 §1).
            Image stageBack = PixelMenuBuilder.Box("StageBack", root, Palette.Panel, s.Solid);
            PixelMenuBuilder.Place(stageBack.rectTransform, new Vector2(0, 336), new Vector2(1184, Px * 2));
            Image stageFill = PixelMenuBuilder.Box("StageFill", root, Palette.Sky, s.Solid);
            PixelMenuBuilder.Place(stageFill.rectTransform, new Vector2(0, 336), new Vector2(1184, Px * 2));
            stageFill.type = Image.Type.Filled; stageFill.fillMethod = Image.FillMethod.Horizontal; stageFill.fillAmount = 0;
            stageBack.raycastTarget = stageFill.raycastTarget = false;
            Text scoreCaption = PixelMenuBuilder.Label("ScoreCaption", root, "S C O R E", new Vector2(-456, 300), new Vector2(240, 24), 16, Palette.Ink, TextAnchor.MiddleLeft);
            Text score = PixelMenuBuilder.Label("Score", root, "000000", new Vector2(-456, 264), new Vector2(240, 48), 48, Palette.Ink, TextAnchor.MiddleLeft);
            PixelMenuBuilder.Shadow(score.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            Text stage = PixelMenuBuilder.Label("Stage", root, "", new Vector2(456, 284), new Vector2(240, 40), 32, Palette.Ink, TextAnchor.MiddleRight);
            stage.gameObject.SetActive(false);
            PixelMenuBuilder.Shadow(stage.gameObject, Palette.TextShadow, new Vector2(Px / 2, -Px / 2));
            Text timer = PixelMenuBuilder.Label("Timer", root, "0:00", new Vector2(456, 244), new Vector2(240, 32), 32, Palette.Ink, TextAnchor.MiddleRight);
            Sprite heart = HeartSprite();
            var lives = new Image[MiniGameScore.MaxLives];
            for (int i = 0; i < lives.Length; i++)
                lives[i] = Art("Life" + (i + 1), root, heart, new Vector2(-560 + i * 40, 216), 4);
            Text combo = PixelMenuBuilder.Label("Combo", root, "", new Vector2(0, 268), new Vector2(320, 96), 48, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(combo.gameObject, Palette.Panel, new Vector2(Px, -Px));
            Text judgement = PixelMenuBuilder.Label("Judgement", root, "", new Vector2(0, 196), new Vector2(480, 64), 32, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(judgement.gameObject, Palette.Panel, new Vector2(Px / 2, -Px / 2));
            Text countdown = PixelMenuBuilder.Label("Countdown", root, "", new Vector2(0, 64), new Vector2(320, 96), 64, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(countdown.gameObject, Palette.TextShadow, new Vector2(Px, -Px));

            foreach (Text number in new[] { scoreCaption, score, stage, timer, combo, judgement, countdown }) PixelText.Mark(number);

            // Start / result modal with the pad diagram (plan §3: the prompt lights the pads in use).
            Image modal = PixelMenuBuilder.Box("Modal", root, Palette.Dim, null);
            PixelMenuBuilder.Stretch(modal.rectTransform);
            Image card = PixelMenuBuilder.Box("Card", modal.transform, Palette.Lift(Palette.Panel), s.Frame);
            PixelMenuBuilder.Place(card.rectTransform, Vector2.zero, new Vector2(720, 464));
            PixelMenuBuilder.Shadow(card.gameObject, Palette.DropShadow, new Vector2(Px * 2, -Px * 2));
            Text title = PixelMenuBuilder.Label("Title", card.transform, "", new Vector2(0, 180), new Vector2(640, 56), 48, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(title.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            Text body = PixelMenuBuilder.Label("Body", card.transform, "", new Vector2(0, 92), new Vector2(640, 96), 16, Palette.Ink, TextAnchor.MiddleCenter);
            body.lineSpacing = 1.4f;
            RectTransform diagram = PixelMenuBuilder.Rect("PadDiagram", card.transform, new Vector2(0, -36), new Vector2(300, 120));
            var diagramPads = new Image[8];
            for (int pad = 0; pad < 8; pad++)
            {
                diagramPads[pad] = PixelMenuBuilder.Box("Pad" + (pad + 1), diagram, Palette.Lift(Palette.Well), s.Frame);
                PixelMenuBuilder.Place(diagramPads[pad].rectTransform, PadLayout[pad] * .3f + new Vector2(0, 8), new Vector2(48, 40));
                diagramPads[pad].raycastTarget = false;
                PixelText.Mark(PixelMenuBuilder.Label("Number", diagramPads[pad].transform, (pad + 1).ToString(), Vector2.zero, new Vector2(48, 40), 16, Palette.Ink, TextAnchor.MiddleCenter));
            }
            Button menu = PixelMenuBuilder.PixelButton("MenuButton", card.transform, s.Frame, "返回", new Vector2(-200, -172), new Vector2(208, 64), Palette.Well, Palette.Ink, 16);
            Button primary = PixelMenuBuilder.PixelButton("PrimaryButton", card.transform, s.Frame, "开始", new Vector2(104, -172), new Vector2(368, 80), Palette.Sun, Palette.Ink, 32);

            PixelMenuBuilder.Raw("Scanlines", root, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.ScanlinePath), new Rect(0, 0, 1, 720 / 4f));
            RectTransform[] wipeBars = BuildWipe(root);

            AudioSource music = new GameObject("Music", typeof(AudioSource)).GetComponent<AudioSource>();
            music.transform.SetParent(root, false); music.playOnAwake = false;
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
            AudioSource effects = new GameObject("SoundEffects", typeof(AudioSource)).GetComponent<AudioSource>();
            effects.transform.SetParent(root, false); effects.playOnAwake = false;

            T controller = s.Canvas.AddComponent<T>();
            PixelMenuBuilder.Assign(controller, "music", music, "effects", effects,
                "hitSound", Sound("pluck_001"), "bonusSound", Sound("confirmation_002"), "forbiddenSound", Sound("error_004"),
                "missSound", Sound("scratch_002"), "comboSound", Sound("maximize_004"),
                "scoreText", score, "comboText", combo, "judgementText", judgement, "stageText", stage, "timerText", timer,
                "countdownText", countdown, "stageFill", stageFill, "modal", modal.gameObject, "modalTitle", title, "modalBody", body,
                "primaryButton", primary, "menuButton", menu);
            PixelMenuBuilder.AssignArray(controller, "padDiagram", diagramPads);
            PixelMenuBuilder.AssignArray(controller, "lifeIcons", lives);
            GameUIAnimator animator = s.Canvas.AddComponent<GameUIAnimator>();
            PixelMenuBuilder.Assign(animator, "grid", s.Grid, "track", s.Field, "judgement", judgement, "countdown", countdown, "score", score);
            PixelMenuBuilder.AssignArray(animator, "wipeBars", wipeBars);
            PixelMenuBuilder.AssignArray(animator, "leftColumn", new Object[] { scoreCaption.transform, score.transform });
            PixelMenuBuilder.AssignArray(animator, "rightColumn", new Object[] { stage.transform, timer.transform });
            return controller;
        }

        // 8x7 white heart, tinted per life by the controller.
        private static Sprite HeartSprite()
        {
            var palette = new Dictionary<char, Color32> { { 'k', new Color32(34, 34, 34, 255) }, { 'w', new Color32(255, 255, 255, 255) } };
            return PixelArt.Save("Assets/RhythmGame/Art/UI", "heart", new[] {
                ".kk..kk.",
                "kwwkkwwk",
                "kwwwwwwk",
                "kwwwwwwk",
                ".kwwwwk.",
                "..kwwk..",
                "...kk..." }, palette);
        }

        internal static void Save(Scene s, string scenePath)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(s.Unity, scenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(e => e.path == scenePath)) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        // Pixel image from string art (one char per pixel) at an integer scale of art pixels.
        internal static Image Art(string name, Transform parent, Sprite sprite, Vector2 position, float scale)
        {
            Image image = PixelMenuBuilder.Box(name, parent, Color.white, sprite);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            PixelMenuBuilder.Place(image.rectTransform, position, new Vector2(sprite.rect.width, sprite.rect.height) * scale);
            return image;
        }

        private static RectTransform[] BuildWipe(Transform root)
        {
            RectTransform wipe = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("Wipe", root, Vector2.zero, Vector2.zero));
            var bars = new RectTransform[12];
            for (int i = 0; i < bars.Length; i++)
            {
                Image bar = PixelMenuBuilder.Box("Bar" + i, wipe, Palette.Shutter, null);
                bar.raycastTarget = false;
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0, i / 12f); rect.anchorMax = new Vector2(1, (i + 1) / 12f);
                rect.pivot = new Vector2(i % 2, .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                bar.gameObject.SetActive(false);
                bars[i] = rect;
            }
            return bars;
        }

        internal static AudioClip Sound(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RhythmGame/kenney_interface-sounds/Audio/" + name + ".ogg");
            if (clip == null) throw new System.InvalidOperationException("Missing sound: " + name);
            return clip;
        }
    }

    // String-art pixel sprites: one character per pixel, '.' transparent, any size.
    internal static class PixelArt
    {
        internal static string[] Overlay(string[] art, (int row, int col, string pixels)[] patches)
        {
            var result = (string[])art.Clone();
            foreach (var (row, col, pixels) in patches)
                result[row] = result[row].Substring(0, col) + pixels + result[row].Substring(col + pixels.Length);
            return result;
        }

        internal static Sprite Save(string dir, string name, string[] art, Dictionary<char, Color32> palette)
        {
            System.IO.Directory.CreateDirectory(dir);
            int height = art.Length, width = art[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int row = 0; row < height; row++)
            {
                if (art[row].Length != width) throw new System.InvalidOperationException(name + " row " + row + " is not " + width + " pixels.");
                for (int col = 0; col < width; col++)
                {
                    char key = art[row][col];
                    texture.SetPixel(col, height - 1 - row, key == '.' ? new Color32(0, 0, 0, 0) : palette[key]);
                }
            }
            texture.Apply();
            string path = dir + "/" + name + ".png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

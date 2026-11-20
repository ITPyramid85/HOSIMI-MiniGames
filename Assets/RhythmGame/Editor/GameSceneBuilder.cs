using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmGame.Editor
{
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        private const string AudioPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/TrainingBeat.wav";
        private static readonly Color Bg = Palette.Background;
        private static readonly Color Panel = Palette.Panel;
        private static readonly Color Accent = Palette.Sky;
        private static readonly Color Muted = Palette.Muted;

        [MenuItem("Rhythm Game/Open Game Scene")]
        public static void OpenGame()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); else BuildAssets();
        }

        public static void BuildAssets()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Game scene already exists. Edit it directly.");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/RhythmGame/Prefabs");
            GenerateTrainingAudio();
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Bg;
            camera.transform.position = new Vector3(0, 0, -10);
            GameObject canvasObject = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MenuTypography));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Stretch(Box("Background", root, Bg, 0, 0, 0, 0).rectTransform);
            RectTransform layout = Rect("Layout", root, 0, 0, 1700, 980);
            Label("Brand", layout, "F I R S T   B E A T  /  0 1", -650, 460, 400, 45, 20, Accent);
            Text song = Label("Song", layout, "First Beat\n120 BPM  /  PRACTICE  /  00:42", -650, 362, 400, 110, 27, Color.white);
            Text character = Label("Character", layout, "角色展示位", -650, -30, 340, 350, 26, Color.white);
            Box("CharacterCard", layout, Panel, -650, -30, 400, 430).transform.SetSiblingIndex(character.transform.GetSiblingIndex());
            Box("CharacterAccent", layout, Accent, -848, -30, 4, 430);
            Text skill = Label("SkillStatus", layout, "", -650, -294, 400, 88, 22, Accent);
            Text hint = Label("Hint", layout, "", -650, -427, 400, 110, 20, Muted);

            RectTransform track = Box("Track", layout, new Color32(19, 28, 45, 255), 0, 50, 620, 820).rectTransform;
            track.gameObject.AddComponent<RectMask2D>();
            Text score = Label("Score", layout, "000000", 650, 254, 400, 95, 64, Color.white);
            Label("ScoreCaption", layout, "S C O R E", 650, 326, 400, 40, 20, Muted);
            Text combo = Label("Combo", layout, "0\nCOMBO", 650, 30, 400, 150, 40, Accent);
            Text hp = Label("Health", layout, "100 / 100", 650, -160, 400, 50, 28, Color.white);
            Label("HealthCaption", layout, "H P", 650, -112, 400, 32, 18, Muted);
            Box("HealthBack", layout, Panel, 650, -208, 400, 18);
            Image hpFill = Box("HealthFill", layout, Accent, 650, -208, 400, 18); SetFill(hpFill);
            Box("ProgressBack", layout, Panel, 0, -420, 620, 5);
            Image progress = Box("ProgressFill", layout, Accent, 0, -420, 620, 5); SetFill(progress); progress.fillAmount = 0;
            Label("Footer", layout, "L I G H T   U P .   T A P   T H E   B E A T .", 0, -469, 700, 36, 16, Muted);
            Button pause = Button("PauseButton", layout, "暂停", 750, 440, 200, 64, Panel);

            Image modal = Box("Modal", root, new Color(.015f, .025f, .06f, .94f), 0, 0, 0, 0);
            Stretch(modal.rectTransform); modal.raycastTarget = true;
            RectTransform card = Box("Card", modal.transform, Panel, 0, 0, 900, 880).rectTransform;
            Box("Accent", card, Accent, 0, 437, 900, 6);
            Text title = Label("Title", card, "", 0, 330, 790, 90, 44, Color.white);
            Text body = Label("Body", card, "", 0, 20, 790, 510, 24, Muted);
            Button primary = Button("PrimaryButton", card, "开始演奏", 190, -345, 340, 78, Accent);
            primary.GetComponentInChildren<Text>().color = Bg;
            Button menu = Button("MenuButton", card, "返回主界面", -190, -345, 340, 78, Bg);
            GameController controller = canvasObject.AddComponent<GameController>();
            AudioSource music = new GameObject("Music", typeof(AudioSource)).GetComponent<AudioSource>();
            music.transform.SetParent(root, false); music.playOnAwake = false;
            AudioSource sfx = new GameObject("SoundEffects", typeof(AudioSource)).GetComponent<AudioSource>();
            sfx.transform.SetParent(root, false); sfx.playOnAwake = false;
            Assign(controller, "healthFill", hpFill, "progressFill", progress,
                "scoreText", score, "comboText", combo, "healthText", hp,
                "characterText", character, "stageText", skill, "songText", song, "hintText", hint,
                "modal", modal.gameObject, "modalTitle", title, "modalBody", body,
                "primaryButton", primary, "menuButton", menu, "pauseButton", pause,
                "music", music, "effects", sfx, "tapSound", Sound("click_001"),
                "missSound", Sound("error_001"));
            ConfigureButtonArea(controller, track);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Created button-tap Game scene and original 42-second training audio.");
        }

        // Targeted migration: replaces only the old track contents, preserving HUD and audio bindings.
        public static void UpgradeToButtonMode() { UpgradeToPadMode(); }

        public static void UpgradeToPadMode()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameController controller = Object.FindFirstObjectByType<GameController>();
            Transform layout = controller.transform.Find("Layout");
            RectTransform track = (RectTransform)layout.Find("Track");
            while (track.childCount > 0) Object.DestroyImmediate(track.GetChild(0).gameObject);
            foreach (string name in new[] { "RailLeft", "RailRight" })
            {
                Transform rail = layout.Find(name);
                if (rail != null) Object.DestroyImmediate(rail.gameObject);
            }
            layout.Find("Footer").GetComponent<Text>().text = "L I G H T   U P .   T A P   T H E   B E A T .";
            ConfigureButtonArea(controller, track);
            var settings = new SerializedObject(controller);
            settings.FindProperty("chartId").stringValue = GameController.DefaultChartId;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BUTTON_MODE_SCENE_READY");
        }

        private static void ConfigureButtonArea(GameController controller, RectTransform track)
        {
            Label("ModeTitle", track, "T A P   T H E   L I G H T", 0, 365, 570, 40, 22, Muted);
            Vector2[] positions = {
                new Vector2(-206, 175), new Vector2(206, 175),
                new Vector2(-150, 0), new Vector2(0, 0), new Vector2(150, 0),
                new Vector2(-206, -175), new Vector2(0, -175), new Vector2(206, -175)
            };
            string[] keys = { "Q", "E", "A", "S", "D", "Z", "X", "C" };
            var views = new DrumPadView[BeatmapData.PadCount];
            Sprite shape = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            for (int pad = 0; pad < views.Length; pad++)
            {
                Vector2 at = positions[pad];
                RectTransform group = Rect("Pad" + (pad + 1), track, at.x, at.y, 138, 138);
                Image border = Box("Border", group, new Color32(57, 84, 111, 255), 0, 0, 138, 138);
                border.sprite = shape; border.type = Image.Type.Sliced;
                Image input = Box("Button", group, new Color32(37, 54, 76, 255), 0, 0, 130, 130);
                input.sprite = shape; input.type = Image.Type.Sliced; input.raycastTarget = true;
                GameInputArea area = input.gameObject.AddComponent<GameInputArea>();
                Assign(area, "controller", controller);
                var inputSettings = new SerializedObject(area);
                inputSettings.FindProperty("padIndex").intValue = pad;
                inputSettings.ApplyModifiedPropertiesWithoutUndo();
                Label("Key", input.transform, (pad + 1) + "  /  " + keys[pad], 0, 43, 120, 28, 17, Color.white);
                Text caption = Label("LightLabel", input.transform, "", 0, 0, 122, 48, 26, Muted);
                Text feedback = Label("Feedback", group, "", 0, -43, 124, 27, 16, Accent);
                Image approach = Box("CueOutline", group, Color.clear, 0, 0, 146, 146);
                approach.sprite = shape; approach.type = Image.Type.Sliced;
                approach.transform.SetAsFirstSibling();
                views[pad] = group.gameObject.AddComponent<DrumPadView>();
                Assign(views[pad], "body", input, "label", caption, "feedback", feedback, "approach", approach, "input", area);
            }
            Label("PadCount", track, "8  P A D S", 0, 175, 200, 40, 20, Muted);
            Text inputLabel = Label("InputHint", track, "", 0, -324, 580, 60, 19, Muted);
            Text judgement = Label("Judgement", track, "", 0, 295, 580, 65, 38, Color.white);
            Text countdown = Label("Countdown", track, "", 0, 295, 550, 100, 64, Color.white);
            RawImage effect = Rect("HitEffect", track, 0, 0, 100, 100).gameObject.AddComponent<RawImage>();
            effect.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RhythmGame/kenney_particle-pack/PNG (Transparent)/star_04.png");
            effect.raycastTarget = false; effect.color = Color.clear;
            Assign(controller, "inputLabel", inputLabel, "judgementText", judgement, "countdownText", countdown, "hitEffect", effect);
            var controllerSettings = new SerializedObject(controller);
            var padArray = controllerSettings.FindProperty("pads");
            padArray.arraySize = views.Length;
            for (int pad = 0; pad < views.Length; pad++) padArray.GetArrayElementAtIndex(pad).objectReferenceValue = views[pad];
            controllerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void UpgradeToCasualMode()
        {
            GenerateAudio("Assets/RhythmGame/Resources/RhythmGame/Audio/CasualBeat.wav", 60);
            AssetDatabase.Refresh();
            UpgradeToPadMode();
            var scene = EditorSceneManager.GetActiveScene();
            var controller = Object.FindFirstObjectByType<GameController>();
            Transform layout = controller.transform.Find("Layout");
            layout.Find("HealthCaption").GetComponent<Text>().text = "MISS";
            layout.Find("Health").GetComponent<Text>().text = "0 / 10";
            layout.Find("Character").GetComponent<Text>().text = "";
            layout.Find("SkillStatus").GetComponent<Text>().text = "";
            Transform card = controller.transform.Find("Modal/Card");
            var old = card.Find("VisualGuide");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            BuildGuide(card);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CASUAL_MODE_READY");
        }

        private static void BuildGuide(Transform card)
        {
            RectTransform root = Rect("VisualGuide", card, 0, 0, 790, 500);
            Text instruction = Label("Instruction", root, "", 0, 200, 750, 60, 34, Color.white);
            // Temporary friendly drum avatar, drawn from native UI shapes; replace with final character art later.
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform avatar = Rect("GuideAvatar", root, -235, 0, 230, 240);
            Image face = Box("Face", avatar, Accent, 0, 20, 185, 185); face.sprite = circle;
            foreach (int side in new[] { -1, 1 })
            {
                Image eye = Box("Eye", avatar, Bg, side * 34, 50, 12, 16); eye.sprite = circle;
                Image cheek = Box("Cheek", avatar, new Color32(245, 153, 150, 255), side * 55, 25, 22, 12); cheek.sprite = circle;
                Box("Stick", avatar, new Color32(255, 209, 102, 255), side * 100, -8, 12, 78).transform.localRotation = Quaternion.Euler(0, 0, side * 28);
            }
            Box("Smile", avatar, Bg, 0, 22, 18, 5);
            Image drum = Box("Drum", avatar, new Color32(228, 114, 116, 255), 0, -55, 150, 90);
            drum.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); drum.type = Image.Type.Sliced;
            Image rim = Box("DrumTop", avatar, new Color32(255, 226, 170, 255), 0, -20, 150, 38); rim.sprite = circle;
            Text name = Label("CharacterName", root, "", -235, -160, 260, 50, 28, Accent);
            RectTransform board = Rect("DemoBoard", root, 155, 10, 380, 300);
            Vector2[] positions = { new Vector2(-130, 100), new Vector2(130, 100),
                new Vector2(-95, 0), new Vector2(0, 0), new Vector2(95, 0),
                new Vector2(-130, -100), new Vector2(0, -100), new Vector2(130, -100) };
            var lights = new Image[8];
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i] = Box("DemoPad" + (i + 1), board, Panel, positions[i].x, positions[i].y, 74, 74);
                lights[i].sprite = drum.sprite; lights[i].type = Image.Type.Sliced;
                Label("Number", lights[i].transform, (i + 1).ToString(), 0, 0, 65, 60, 22, Color.white);
            }
            RectTransform finger = Rect("TapHand", board, 0, 0, 36, 60);
            Image palm = Box("Palm", finger, Color.white, 0, -12, 30, 33); palm.sprite = circle;
            Image tip = Box("Finger", finger, Color.white, -7, 8, 12, 42); tip.sprite = drum.sprite; tip.type = Image.Type.Sliced;
            Text feedback = Label("Feedback", root, "", 155, -205, 350, 50, 26, new Color32(255, 209, 102, 255));
            MiniGameGuide guide = root.gameObject.AddComponent<MiniGameGuide>();
            Assign(guide, "finger", finger, "instruction", instruction, "characterName", name, "feedback", feedback);
            var serialized = new SerializedObject(guide);
            var array = serialized.FindProperty("demoPads"); array.arraySize = lights.Length;
            for (int i = 0; i < lights.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Original synthesized 120 BPM test groove. Notes use absolute seconds against the same DSP clock.
        private static void GenerateTrainingAudio() { GenerateAudio(AudioPath, 42); }

        internal static void GenerateAudio(string path, int seconds)
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const int rate = 44100;
            var random = new System.Random(120);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + rate * seconds * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(rate * seconds * 2);
                double[] bass = { 65.406, 65.406, 77.782, 58.270 };
                for (int i = 0; i < rate * seconds; i++)
                {
                    double t = (double)i / rate;
                    double beat = t % .5, half = t % .25;
                    int step = (int)(t / .5);
                    double noise = random.NextDouble() * 2 - 1;
                    double kick = .43 * Math.Sin(2 * Math.PI * (48 * beat + 9 * (1 - Math.Exp(-beat * 35)))) * Math.Exp(-beat * 17);
                    double snare = (step % 2 == 1 ? .21 : .035) * noise * Math.Exp(-beat * 30);
                    double hat = .07 * noise * Math.Exp(-half * 95);
                    double low = .10 * Math.Sin(2 * Math.PI * bass[(step / 8) % 4] * beat) * (1 - Math.Exp(-beat * 150)) * Math.Exp(-beat * 7);
                    double fade = Math.Min(1, Math.Max(0, (seconds - t) / 1.5));
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, kick + snare + hat + low)) * fade * short.MaxValue));
                }
            }
        }

        private static AudioClip Sound(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RhythmGame/kenney_interface-sounds/Audio/" + name + ".ogg");
            if (clip == null) throw new InvalidOperationException("Missing sound: " + name);
            return clip;
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            RectTransform r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            return r;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Image Box(string name, Transform parent, Color color, float x, float y, float w, float h)
        {
            Image image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private static void SetFill(Image image)
        {
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; image.fillAmount = 1;
            // Filled images need a sprite; the built-in UI sprite is persistent and ships with Unity.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
        private static Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color color)
        {
            Text text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"); text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        private static Button Button(string name, Transform parent, string value, float x, float y, float w, float h, Color color)
        {
            Image image = Box(name, parent, color, x, y, w, h); image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label("Label", image.transform, value, 0, 0, w, h, 26, Color.white);
            return button;
        }
        private static void Assign(Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2) serialized.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

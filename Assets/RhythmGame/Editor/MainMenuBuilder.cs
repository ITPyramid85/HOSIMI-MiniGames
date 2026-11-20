using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        public const string LatencyPath = "Assets/RhythmGame/Prefabs/LatencyPanel.prefab";
        public const string ManagerPath = "Assets/RhythmGame/Resources/RhythmGame/GameManager.prefab";
        private static readonly Color Background = Palette.Background;
        private static readonly Color Panel = Palette.Panel;
        private static readonly Color Accent = Palette.Sky;
        private static readonly Color Muted = Palette.Muted;

        [MenuItem("Rhythm Game/Open Main Menu")]
        public static void OpenMainMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildAssets();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // Also callable using Unity -batchmode -executeMethod RhythmGame.Editor.MainMenuBuilder.BuildAssets.
        public static void BuildAssets()
        {
            // Never overwrite an existing scene or customized prefab.
            if (File.Exists(ScenePath) || File.Exists(LatencyPath) || File.Exists(ManagerPath))
                throw new System.InvalidOperationException("Main menu assets already exist; open and edit them directly.");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/RhythmGame/Prefabs");
            Directory.CreateDirectory("Assets/RhythmGame/Resources/RhythmGame");
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject manager = new GameObject("GameManager", typeof(GameManager));
            GameObject managerPrefab = PrefabUtility.SaveAsPrefabAsset(manager, ManagerPath);
            Object.DestroyImmediate(manager);
            PrefabUtility.InstantiatePrefab(managerPrefab, scene);

            LatencyCalibrationPopup popupPrefab = BuildSettingsPrefab();
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.transform.position = new Vector3(0, 0, -10);

            GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MenuTypography));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Image background = Box("Background", canvasObject.transform, Background, Vector2.zero, Vector2.zero);
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            RectTransform layout = Rect("Menu", canvasObject.transform, Vector2.zero, new Vector2(1600, 920));
            CanvasGroup group = layout.gameObject.AddComponent<CanvasGroup>();
            Label("Brand", layout, "R H Y T H M   /   0 1", new Vector2(-530, 400), new Vector2(540, 48), 22, Accent);
            Label("Title", layout, "FIRST\nBEAT", new Vector2(-420, 180), new Vector2(760, 320), 96, Color.white);
            Text subtitle = Label("Subtitle", layout, "准备好，跟上节拍。", new Vector2(-500, -35), new Vector2(600, 64), 28, Muted);
            Box("AccentLine", layout, Accent, new Vector2(-756, -107), new Vector2(88, 5)).raycastTarget = false;
            for (int i = 0; i < 20; i++)
            {
                float h = 32 + Mathf.Abs(Mathf.Sin(i * 0.67f)) * 140;
                Box("BeatBar" + i, layout, new Color(0.23f, 0.5f, 0.54f, 0.28f),
                    new Vector2(-784 + i * 25, -290 + h * 0.5f), new Vector2(9, h)).raycastTarget = false;
            }
            Label("Footer", layout, "F I N D   Y O U R   R H Y T H M", new Vector2(-460, -410), new Vector2(680, 36), 17, Muted);

            Button settings = Button("SettingsButton", layout, "设置", new Vector2(680, 400), new Vector2(240, 64), Panel, Color.white, 24);
            Button start = Button("StartGameButton", layout, "开始游戏", new Vector2(440, 80), new Vector2(640, 112), Accent, Background, 34);
            Button characters = Button("CharacterButton", layout, "角色", new Vector2(275, -62), new Vector2(310, 88), Panel, Color.white, 28);
            Button shop = Button("ShopButton", layout, "商城", new Vector2(605, -62), new Vector2(310, 88), Panel, Color.white, 28);
            Button quit = Button("QuitButton", layout, "退出游戏", new Vector2(680, -410), new Vector2(240, 64), Panel, Muted, 23);
            Text status = Label("Status", layout, "", new Vector2(410, -198), new Vector2(700, 100), 20, new Color(1, 0.65f, 0.62f));
            status.alignment = TextAnchor.MiddleCenter;
            RectTransform popupRoot = Rect("PopupRoot", canvasObject.transform, Vector2.zero, Vector2.zero);
            Stretch(popupRoot);
            MainMenuController controller = canvasObject.AddComponent<MainMenuController>();
            Assign(controller, "settingsButton", settings, "startButton", start, "characterButton", characters,
                "shopButton", shop, "quitButton", quit, "menuGroup", group, "popupRoot", popupRoot,
                "settingsPrefab", popupPrefab, "characterPrefab", CharacterMenuBuilder.GetOrCreatePrefab(),
                "subtitle", subtitle, "statusText", status);

            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            events.GetComponent<EventSystem>().firstSelectedGameObject = start.gameObject;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.path != ScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Created MainMenu.unity, SettingsPanel.prefab, and persistent GameManager.prefab.");
        }

        // Targeted upgrade: rebuilds only the Card under the existing root, so the prefab GUID,
        // the LatencyCalibrationPopup fileID and the main menu's reference to it are all kept.
        [MenuItem("Rhythm Game/Rebuild Settings Panel")]
        public static void UpgradeBluetoothSettings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LatencyPath);
            try
            {
                Transform old = root.transform.Find("Card");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                BuildSettingsCard((RectTransform)root.transform, root.GetComponent<LatencyCalibrationPopup>());
                PrefabUtility.SaveAsPrefabAsset(root, LatencyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt SettingsPanel.prefab with the Bluetooth tap test.");
        }

        // One-time migration after a script update: old prefabs have no tap pad wired up.
        [InitializeOnLoadMethod]
        private static void UpgradeOutdatedSettingsPanel()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LatencyPath);
                LatencyCalibrationPopup popup = prefab != null ? prefab.GetComponent<LatencyCalibrationPopup>() : null;
                if (popup == null || new SerializedObject(popup).FindProperty("tapPad").objectReferenceValue != null) return;
                UpgradeBluetoothSettings();
            };
        }

        private static LatencyCalibrationPopup BuildSettingsPrefab()
        {
            RectTransform root = Rect("SettingsPanel", null, Vector2.zero, Vector2.zero);
            Stretch(root);
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0.015f, 0.025f, 0.06f, 0.88f);
            root.gameObject.AddComponent<MenuTypography>();
            BuildSettingsCard(root, root.gameObject.AddComponent<LatencyCalibrationPopup>());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, LatencyPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<LatencyCalibrationPopup>();
        }

        // Laid out for the 1280x720 reference canvas: tap test on the left, result and offset on the right.
        private static void BuildSettingsCard(RectTransform root, LatencyCalibrationPopup popup)
        {
            RectTransform card = Box("Card", root, Panel, Vector2.zero, new Vector2(960, 620)).rectTransform;
            Box("Accent", card, Accent, new Vector2(0, 307), new Vector2(960, 6)).raycastTarget = false;
            Text title = Label("Title", card, "蓝牙耳机延迟校准", new Vector2(0, 268), new Vector2(900, 44), 32, Color.white);
            title.alignment = TextAnchor.MiddleCenter;
            Text status = Label("OutputStatus", card, "", new Vector2(0, 224), new Vector2(900, 40), 16, Color.white);
            status.alignment = TextAnchor.MiddleCenter;
            Box("Divider", card, Background, new Vector2(0, 0), new Vector2(2, 360)).raycastTarget = false;

            // Left: beat light + tap pad.
            Image light = Box("BeatLight", card, Background, new Vector2(-230, 150), new Vector2(88, 88));
            light.raycastTarget = false;
            Text lightCaption = Label("LightCaption", card, "", new Vector2(-230, 92), new Vector2(440, 22), 14, Muted);
            lightCaption.alignment = TextAnchor.MiddleCenter;
            Image pad = Box("TapPad", card, Background, new Vector2(-230, -16), new Vector2(440, 170));
            Text padLabel = Label("Label", pad.transform, "点 击", Vector2.zero, new Vector2(440, 170), 48, Accent);
            padLabel.alignment = TextAnchor.MiddleCenter;
            CalibrationTapPad tapPad = pad.gameObject.AddComponent<CalibrationTapPad>();
            Text prompt = Label("TapPrompt", card, "", new Vector2(-230, -122), new Vector2(440, 26), 16, Color.white);
            prompt.alignment = TextAnchor.MiddleCenter;
            Text log = Label("TapLog", card, "", new Vector2(-230, -152), new Vector2(440, 24), 14, Muted);
            log.alignment = TextAnchor.MiddleCenter;

            // Right: result, test controls, saved offset.
            Text resultHeading = Label("ResultHeading", card, "测量结果", new Vector2(230, 178), new Vector2(420, 22), 14, Muted);
            Text resultValue = Label("ResultValue", card, "-- ms", new Vector2(230, 142), new Vector2(420, 48), 40, Accent);
            Text resultDetail = Label("ResultDetail", card, "", new Vector2(230, 84), new Vector2(420, 60), 14, Muted);
            resultDetail.alignment = TextAnchor.UpperLeft;
            Button test = Button("TestButton", card, "开始测试", new Vector2(125, 26), new Vector2(200, 50), Accent, Background, 18);
            Button apply = Button("ApplyResultButton", card, "使用测量值", new Vector2(335, 26), new Vector2(200, 50), Background, Accent, 18);
            Button enable = Button("EnableBluetoothButton", card, "蓝牙补偿：关", new Vector2(230, -36), new Vector2(420, 48), Background, Accent, 18);
            Button minus = Button("MinusButton", card, "−10", new Vector2(50, -98), new Vector2(60, 44), Background, Color.white, 16);
            Button plus = Button("PlusButton", card, "+10", new Vector2(410, -98), new Vector2(60, 44), Background, Color.white, 16);
            RectTransform sliderRoot = Rect("DelaySlider", card, new Vector2(230, -98), new Vector2(280, 44));
            Box("Track", sliderRoot, Background, Vector2.zero, new Vector2(270, 8));
            RectTransform handleArea = Rect("HandleArea", sliderRoot, Vector2.zero, new Vector2(256, 44));
            // Slider stretches the handle vertically to its container.
            Image handle = Box("Handle", handleArea, Accent, Vector2.zero, new Vector2(18, -12));
            Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle; slider.handleRect = handle.rectTransform;
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            Text delay = Label("DelayValue", card, "0 ms", new Vector2(150, -150), new Vector2(240, 36), 24, Accent);
            Button reset = Button("ResetButton", card, "归零", new Vector2(380, -150), new Vector2(100, 36), Background, Muted, 14);

            Text message = Label("Message", card, "", new Vector2(0, -212), new Vector2(900, 44), 14, Muted);
            message.alignment = TextAnchor.MiddleCenter;
            Button close = Button("CloseButton", card, "取消", new Vector2(-160, -270), new Vector2(260, 52), Background, Color.white, 18);
            Button save = Button("SaveButton", card, "保存并关闭", new Vector2(160, -270), new Vector2(260, 52), Accent, Background, 18);
            Assign(popup, "closeButton", close, "title", title, "message", message, "delayText", delay,
                "statusText", status, "delaySlider", slider, "enableButton", enable, "minusButton", minus,
                "plusButton", plus, "resetButton", reset, "saveButton", save,
                "testButton", test, "applyButton", apply, "tapPad", tapPad, "beatLight", light,
                "lightCaption", lightCaption, "tapPrompt", prompt, "tapLog", log,
                "resultHeading", resultHeading, "resultValue", resultValue, "resultDetail", resultDetail);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Box(string name, Transform parent, Color color, Vector2 position, Vector2 size)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            Text label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size, Color background, Color foreground, int fontSize)
        {
            Image image = Box(name, parent, background, position, size);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.73f, 0.8f, 0.85f);
            button.colors = colors;
            Text label = Label("Label", image.transform, text, Vector2.zero, size, fontSize, foreground);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void Assign(Object target, params object[] fields)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i += 2)
                serialized.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

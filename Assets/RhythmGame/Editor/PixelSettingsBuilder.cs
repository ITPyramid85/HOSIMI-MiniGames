using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Builds the settings screen prefab (client reply v5 §5 items) and points the
    // main menu's Settings button at it. Safe to rerun: the prefab is regenerated, its GUID kept.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.PixelSettingsBuilder.Build
    public static class PixelSettingsBuilder
    {
        public const string PrefabPath = "Assets/RhythmGame/Prefabs/SettingsPanel.prefab";
        private const float Px = PixelTween.Pixel;
        private const float Left = -408;   // card content's left edge

        [MenuItem("Rhythm Game/Build Settings Panel (Pixel)")]
        public static void Build()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(PixelMenuBuilder.SolidPath);
            RectTransform root = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("SettingsPanel", null, Vector2.zero, Vector2.zero));
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = Palette.Dim;
            PixelMenuBuilder.Assign(root.gameObject.AddComponent<MenuTypography>(), "fontOverride",
                AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            SettingsPopup popup = root.gameObject.AddComponent<SettingsPopup>();

            Image card = PixelMenuBuilder.Box("Card", root, Palette.Lift(Palette.Panel), frame);
            PixelMenuBuilder.Place(card.rectTransform, Vector2.zero, new Vector2(880, 656));
            PixelMenuBuilder.Shadow(card.gameObject, Palette.DropShadow, new Vector2(Px * 2, -Px * 2));
            Transform c = card.transform;

            // Client reply v5 §5: one row per item, label left, control right.
            Text title = Text("Title", c, "设置", Left, 272, 400, 56, 40, Palette.Ink);
            Button close = PixelMenuBuilder.PixelButton("CloseButton", c, frame, "关闭", new Vector2(336, 272), new Vector2(144, 56), Palette.Well, Palette.Ink, 24);
            string[] rows = { "Pad", "Music", "Effects", "Language", "Google", "Latency", "Inquiry" };
            var labels = new Dictionary<string, Text>();
            for (int i = 0; i < rows.Length; i++)
            {
                labels[rows[i]] = Text(rows[i] + "Label", c, rows[i], Left, RowY(i), 200, 48, 24, Palette.Ink);
                if (i > 0)
                {
                    Image line = PixelMenuBuilder.Box("Line" + i, c, Palette.Well, null);
                    PixelMenuBuilder.Place(line.rectTransform, new Vector2(0, RowY(i) + 36), new Vector2(816, Px / 2));
                    line.raycastTarget = false;
                }
            }
            Button pad = RowButton(c, frame, "PadButton", RowY(0), Palette.Blue);
            Slider music = Slider("MusicSlider", c, frame, solid, new Vector2(72, RowY(1)), 432);
            Text musicValue = Text("MusicValue", c, "100%", 312, RowY(1), 96, 40, 24, Palette.Ink);
            musicValue.alignment = TextAnchor.MiddleRight;
            Slider effects = Slider("EffectsSlider", c, frame, solid, new Vector2(72, RowY(2)), 432);
            Text effectsValue = Text("EffectsValue", c, "100%", 312, RowY(2), 96, 40, 24, Palette.Ink);
            effectsValue.alignment = TextAnchor.MiddleRight;
            var languages = new Button[PlayerSettings.DisplayOrder.Length];
            for (int i = 0; i < languages.Length; i++)
            {
                string code = PlayerSettings.CodeOf(PlayerSettings.DisplayOrder[i]).Substring(0, 2);
                languages[i] = PixelMenuBuilder.PixelButton("Language_" + code, c, frame, code, new Vector2(-128 + i * 152, RowY(3)),
                    new Vector2(144, 48), Palette.Well, Palette.Ink, 20);
            }
            // Client reply v6 §3-5: the account row has Google and Apple sign-in and account deletion.
            Button google = PixelMenuBuilder.PixelButton("GoogleButton", c, frame, "", new Vector2(-64, RowY(4)), new Vector2(176, 48), Palette.Well, Palette.Ink, 20);
            Button apple = PixelMenuBuilder.PixelButton("AppleButton", c, frame, "", new Vector2(120, RowY(4)), new Vector2(176, 48), Palette.Ink, Palette.Panel, 20);
            Button deleteAccount = PixelMenuBuilder.PixelButton("DeleteAccountButton", c, frame, "", new Vector2(320, RowY(4)), new Vector2(176, 48), Palette.Well, Palette.DangerText, 20);
            Button latency = RowButton(c, frame, "LatencyButton", RowY(5), Palette.Blue);
            Button inquiry = RowButton(c, frame, "InquiryButton", RowY(6), Palette.Well);
            Text status = Text("Status", c, "", Left, -296, 816, 32, 20, Palette.MintText);
            status.alignment = TextAnchor.MiddleCenter;

            PixelMenuBuilder.Assign(popup, "closeButton", close, "padButton", pad, "googleButton", google, "appleButton", apple, "deleteAccountButton", deleteAccount, "latencyButton", latency,
                "inquiryButton", inquiry, "musicSlider", music, "effectsSlider", effects,
                "title", title, "padLabel", labels["Pad"], "musicLabel", labels["Music"], "effectsLabel", labels["Effects"],
                "languageLabel", labels["Language"], "googleLabel", labels["Google"], "latencyLabel", labels["Latency"],
                "inquiryLabel", labels["Inquiry"], "musicValue", musicValue, "effectsValue", effectsValue, "status", status,
                "padTestPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PadTestBuilder.PrefabPath).GetComponent<PadTestPopup>(),
                "latencyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuBuilder.LatencyPath).GetComponent<LatencyCalibrationPopup>());
            PixelMenuBuilder.AssignArray(popup, "languageButtons", languages);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);

            var scene = EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            PixelMenuBuilder.Assign(Object.FindFirstObjectByType<MainMenuController>(), "settingsPrefab", prefab.GetComponent<SettingsPopup>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_SETTINGS_READY");
        }

        private static float RowY(int row) { return 200 - row * 72; }

        private static Button RowButton(Transform parent, Sprite frame, string name, float y, Color fill)
        {
            return PixelMenuBuilder.PixelButton(name, parent, frame, "", new Vector2(304, y), new Vector2(208, 48), fill, Palette.Ink, 20);
        }

        // Left-aligned label whose left edge sits at `left`.
        private static Text Text(string name, Transform parent, string value, float left, float y, float w, float h, int size, Color color)
        {
            return PixelMenuBuilder.Label(name, parent, value, new Vector2(left + w / 2, y), new Vector2(w, h), size, color, TextAnchor.MiddleLeft);
        }


        // Pixel slider: framed track, solid fill, framed handle taller than the track.
        private static Slider Slider(string name, Transform parent, Sprite frame, Sprite solid, Vector2 position, float width)
        {
            RectTransform root = PixelMenuBuilder.Rect(name, parent, position, new Vector2(width, Px * 6));
            Image track = PixelMenuBuilder.Box("Track", root, Palette.Lift(Palette.Well), frame);
            PixelMenuBuilder.Stretch(track.rectTransform);
            track.raycastTarget = true;
            RectTransform fillArea = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("FillArea", root, Vector2.zero, Vector2.zero));
            fillArea.offsetMin = new Vector2(Px, Px); fillArea.offsetMax = new Vector2(-Px, -Px);
            Image fill = PixelMenuBuilder.Box("Fill", fillArea, Palette.Sky, solid);
            fill.type = Image.Type.Simple;
            fill.raycastTarget = false;
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("HandleArea", root, Vector2.zero, Vector2.zero));
            handleArea.offsetMin = new Vector2(Px * 3, 0); handleArea.offsetMax = new Vector2(-Px * 3, 0);
            Image handle = PixelMenuBuilder.Box("Handle", handleArea, Palette.Lift(Palette.Sun), frame);
            handle.rectTransform.sizeDelta = new Vector2(Px * 6, Px * 4);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 10; slider.wholeNumbers = true; slider.value = 10;
            return slider;
        }
    }
}

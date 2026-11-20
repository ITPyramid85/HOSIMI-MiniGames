using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the mini-game list scene: ten cards in plan order. Fully generated, so rebuilding is safe.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.MiniGamesMenuBuilder.Build
    public static class MiniGamesMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGames.unity";
        private const float Px = PixelTween.Pixel;

        [MenuItem("Rhythm Game/Build Mini-Game List")]
        public static void Build()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Palette.Background;
            camera.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("MiniGamesCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(MenuTypography));
            canvasObject.layer = 5;
            PixelMenuBuilder.Assign(canvasObject.GetComponent<MenuTypography>(), "fontOverride",
                AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Transform root = canvasObject.transform;
            PixelMenuBuilder.Stretch(PixelMenuBuilder.Box("Background", root, Palette.Background, null).rectTransform).GetComponent<Image>().raycastTarget = false;
            PixelMenuBuilder.Raw("Grid", root, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.GridPath), new Rect(0, 0, 1280 / 64f, 720 / 64f));

            Button back = PixelMenuBuilder.PixelButton("BackButton", root, frame, "返回", new Vector2(-520, 296), new Vector2(176, 64), Palette.Well, Palette.Ink, 24);
            Text title = PixelMenuBuilder.Label("Title", root, "", new Vector2(-176, 296), new Vector2(448, 56), 40, Palette.Ink, TextAnchor.MiddleLeft);
            Image pill = PixelMenuBuilder.Box("Balance", root, Palette.Lift(Palette.Orange), frame);
            PixelMenuBuilder.Place(pill.rectTransform, new Vector2(436, 296), new Vector2(328, 56));
            pill.raycastTarget = false;
            Text balance = PixelMenuBuilder.Label("BalanceText", pill.transform, "", Vector2.zero, new Vector2(304, 48), 24, Palette.Ink, TextAnchor.MiddleCenter);

            var cards = new Button[MiniGamesMenu.Games.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                bool built = MiniGamesMenu.Games[i].Scene != null;
                Vector2 position = new Vector2(-480 + (i % 5) * 240, i < 5 ? 96 : -176);
                Button card = PixelMenuBuilder.PixelButton("Game_" + MiniGamesMenu.Games[i].Id, root, frame, "", position, new Vector2(224, 248),
                    built ? Palette.Rainbow[i % Palette.Rainbow.Length] : Palette.Well, Palette.Ink, 24);
                Object.DestroyImmediate(card.transform.Find("Label").gameObject);
                PixelText.Mark(PixelMenuBuilder.Label("Number", card.transform, (i + 1).ToString("00"), new Vector2(-72, 96), new Vector2(64, 40), 32, Palette.Ink, TextAnchor.MiddleLeft));
                PixelMenuBuilder.Label("Name", card.transform, "", new Vector2(0, 8), new Vector2(200, 80), 28, Palette.Ink, TextAnchor.MiddleCenter);
                PixelMenuBuilder.Label("Best", card.transform, "", new Vector2(0, -84), new Vector2(208, 48), 20, Palette.Ink, TextAnchor.MiddleCenter);
                cards[i] = card;
            }

            MiniGamesMenu menu = canvasObject.AddComponent<MiniGamesMenu>();
            PixelMenuBuilder.Assign(menu, "backButton", back, "title", title, "balance", balance);
            PixelMenuBuilder.AssignArray(menu, "cards", cards);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("MINI_GAMES_READY");
        }
    }
}

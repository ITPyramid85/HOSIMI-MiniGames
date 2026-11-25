using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the login scene and makes it the first scene of the build. Fully generated, so
    // rebuilding is safe. Headless: unity run <project> -- -executeMethod RhythmGame.Editor.LoginBuilder.Build
    public static class LoginBuilder
    {
        public const string ScenePath = "Assets/Scenes/Login.unity";
        private const string MenuMusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/MenuBeat.wav";
        private const string Portraits = "Assets/RhythmGame/Resources/RhythmGame/Portraits/";
        private const float Px = PixelTween.Pixel;

        [MenuItem("Rhythm Game/Build Login Scene")]
        public static void Build()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Palette.Background;
            camera.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("LoginCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
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

            RectTransform panel = PixelMenuBuilder.Rect("Login", root, Vector2.zero, new Vector2(1200, 680));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

            // Official wordmark, two of the drum animals either side, sign-in buttons in the middle and
            // the four languages along the bottom.
            Image logo = PixelMenuBuilder.Box("Logo", panel, Color.white, PixelMenuBuilder.ImportLogo());
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            PixelMenuBuilder.Place(logo.rectTransform, new Vector2(0, 220), new Vector2(560, 128));
            Text tagline = PixelMenuBuilder.Label("Tagline", panel, "", new Vector2(0, 124), new Vector2(800, 48), 28, Palette.Muted, TextAnchor.MiddleCenter);
            Guide("GuideLeft", panel, "whale", new Vector2(-420, -40));
            Guide("GuideRight", panel, "lion", new Vector2(420, -40));

            // Client reply v6 §4: Google is the biggest, main button; Apple the same size (iOS only);
            // guest stays as a small secondary button with a note that its records stay on this device.
            Button google = PixelMenuBuilder.PixelButton("GoogleButton", panel, frame, "Google", new Vector2(0, LoginController.GoogleY), new Vector2(560, 96), Palette.Orange, Palette.Ink, 30);
            Button apple = PixelMenuBuilder.PixelButton("AppleButton", panel, frame, "Apple", new Vector2(0, LoginController.AppleY), new Vector2(560, 96), Palette.Ink, Palette.Panel, 30);
            Button guest = PixelMenuBuilder.PixelButton("GuestButton", panel, frame, "Guest", new Vector2(0, LoginController.GuestY), new Vector2(280, 52), Palette.Well, Palette.Ink, 22);
            Button start = PixelMenuBuilder.PixelButton("StartButton", panel, frame, "Start", new Vector2(0, 36), new Vector2(560, 104), Palette.Orange, Palette.Ink, 36);
            Text account = PixelMenuBuilder.Label("Account", panel, "", new Vector2(0, -48), new Vector2(640, 36), 22, Palette.Muted, TextAnchor.MiddleCenter);
            Text status = PixelMenuBuilder.Label("Status", panel, "", new Vector2(0, 100), new Vector2(800, 30), 20, Palette.DangerText, TextAnchor.MiddleCenter);
            Text note = PixelMenuBuilder.Label("Note", panel, "", new Vector2(0, LoginController.GuestY - 54), new Vector2(600, 52), 18, Palette.Muted, TextAnchor.MiddleCenter);

            var languages = new Button[PlayerSettings.DisplayOrder.Length];
            for (int i = 0; i < languages.Length; i++)
            {
                string code = PlayerSettings.CodeOf(PlayerSettings.DisplayOrder[i]).Substring(0, 2);
                languages[i] = PixelMenuBuilder.PixelButton("Language_" + code, panel, frame, code, new Vector2(-234 + i * 156, -290),
                    new Vector2(140, 52), Palette.Well, Palette.Ink, 22);
            }

            AudioSource music = new GameObject("Music", typeof(AudioSource)).GetComponent<AudioSource>();
            music.transform.SetParent(root, false);
            music.playOnAwake = false;
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath) == null) GameSceneBuilder.GenerateAudio(MenuMusicPath, 32);
            AssetDatabase.ImportAsset(MenuMusicPath);
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath);

            LoginController controller = canvasObject.AddComponent<LoginController>();
            PixelMenuBuilder.Assign(controller, "googleButton", google, "appleButton", apple, "guestButton", guest, "startButton", start,
                "tagline", tagline, "accountText", account, "statusText", status, "noteText", note, "group", group, "music", music);
            PixelMenuBuilder.AssignArray(controller, "languageButtons", languages);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, ScenePath);
            MakeFirstScene();
            AssetDatabase.SaveAssets();
            Debug.Log("LOGIN_READY");
        }

        private static void Guide(string name, Transform parent, string id, Vector2 position)
        {
            Image guide = PixelMenuBuilder.Box(name, parent, Color.white, null);
            guide.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Portraits + id + ".png");
            guide.preserveAspect = true;
            guide.raycastTarget = false;
            PixelMenuBuilder.Place(guide.rectTransform, position, new Vector2(288, 288));
        }

        // The app opens on the login screen; MainMenuBuilder calls this too so it stays first.
        internal static void MakeFirstScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.path != ScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

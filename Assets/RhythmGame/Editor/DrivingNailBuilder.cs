using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the nail-driving scene on the shared mini-game frame. Fully generated, so rebuilding is safe.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.DrivingNailBuilder.Build
    public static class DrivingNailBuilder
    {
        public const string ScenePath = "Assets/Scenes/DrivingNail.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Nails";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/NailBeat.wav";
        private const string RimPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/NailRim.wav";
        private const string SceneVersion = "2";
        private const float Px = PixelTween.Pixel;
        private const float NailScale = 5f, HammerScale = 6f, FigureScale = 8f;
        private static readonly Color Workshop = new Color32(255, 246, 236, 255);
        private static readonly Color Wood = new Color32(222, 176, 122, 255);
        private static readonly Color Grain = new Color32(196, 140, 86, 255);
        private static readonly Color Lip = new Color32(120, 78, 48, 255);
        private static readonly Color Dust = new Color32(232, 214, 186, 255);

        [MenuItem("Rhythm Game/Build Nail Driving Prototype")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("NailCanvas", Workshop);
            Transform field = s.Field;

            Image touch = PixelMenuBuilder.Box("TouchArea", field, Color.clear, null);
            PixelMenuBuilder.Stretch(touch.rectTransform);
            touch.raycastTarget = true;

            RectTransform board = PixelMenuBuilder.Rect("Board", field, new Vector2(40, -36), new Vector2(700, 248));
            Image plank = PixelMenuBuilder.Box("Plank", board, Wood, s.Solid);
            PixelMenuBuilder.Stretch(plank.rectTransform);
            plank.raycastTarget = false;
            for (int i = 0; i < 6; i++)
            {
                Image grain = PixelMenuBuilder.Box("Grain" + i, board, Grain, s.Solid);
                PixelMenuBuilder.Place(grain.rectTransform, new Vector2(0, 88 - i * 32), new Vector2(660, Px));
                grain.raycastTarget = false;
            }
            Image lip = PixelMenuBuilder.Box("Lip", board, Lip, s.Solid);
            PixelMenuBuilder.Place(lip.rectTransform, new Vector2(0, 112), new Vector2(700, Px * 2));
            lip.raycastTarget = false;

            // One nail stays in the scene. DrivingNailView copies it for each nail that comes up.
            Image nail = MiniGameSceneBuilder.Art("Nail", board, art["nail"], Vector2.zero, NailScale);
            nail.rectTransform.pivot = new Vector2(.5f, 1f);
            PixelMenuBuilder.Place(nail.rectTransform, new Vector2(0, 132), nail.rectTransform.sizeDelta);
            Image hammer = MiniGameSceneBuilder.Art("Hammer", board, art["hammer"], Vector2.zero, HammerScale);
            hammer.rectTransform.pivot = new Vector2(.5f, 1f);
            PixelMenuBuilder.Place(hammer.rectTransform, new Vector2(-250, 132 + hammer.rectTransform.sizeDelta.y), hammer.rectTransform.sizeDelta);

            var dust = new Image[6];
            for (int i = 0; i < dust.Length; i++)
            {
                dust[i] = PixelMenuBuilder.Box("Dust" + i, board, Dust, s.Solid);
                PixelMenuBuilder.Place(dust[i].rectTransform, Vector2.zero, new Vector2(Px * 2, Px * 2));
                dust[i].raycastTarget = false;
                dust[i].gameObject.SetActive(false);
            }

            Image carpenter = MiniGameSceneBuilder.Art("Carpenter", field, art["carpenter"], new Vector2(-470, -24), FigureScale);
            Image furniture = MiniGameSceneBuilder.Art("Furniture", field, art["chair"], new Vector2(470, -8), FigureScale);
            var collected = new Image[3];
            collected[0] = MiniGameSceneBuilder.Art("GotChair", field, art["chair"], new Vector2(390, -268), 4);
            collected[1] = MiniGameSceneBuilder.Art("GotDesk", field, art["desk"], new Vector2(470, -268), 4);
            collected[2] = MiniGameSceneBuilder.Art("GotHouse", field, art["house"], new Vector2(550, -268), 4);
            foreach (Image icon in collected) icon.color = new Color(1, 1, 1, .28f);

            Text count = PixelMenuBuilder.Label("Count", field, "", new Vector2(40, 156), new Vector2(420, 72), 56, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(count.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            Text caption = PixelMenuBuilder.Label("Remaining", field, "", new Vector2(40, 108), new Vector2(420, 36), 24, Palette.Ink, TextAnchor.MiddleCenter);
            Text bubble = PixelMenuBuilder.Label("Bubble", field, "", new Vector2(-470, 132), new Vector2(280, 48), 28, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(bubble.gameObject, Palette.Panel, new Vector2(Px / 2, -Px / 2));
            PixelText.Mark(PixelMenuBuilder.Label("Key", field, "7 / X", new Vector2(40, -312), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter));
            PixelText.Mark(count);
            PixelText.Mark(caption);

            DrivingNailView view = touch.gameObject.AddComponent<DrivingNailView>();
            PixelMenuBuilder.Assign(view, "board", board, "hammer", hammer.rectTransform, "carpenter", carpenter.rectTransform, "furniture", furniture.rectTransform,
                "carpenterImage", carpenter, "furnitureImage", furniture, "countText", count, "caption", caption, "bubble", bubble,
                "nailStraight", art["nail"], "nailBent", art["bent"], "carpenterIdle", art["carpenter"], "carpenterScratch", art["scratch"],
                "chair", art["chair"], "desk", art["desk"], "house", art["house"], "nailTemplate", nail);
            PixelMenuBuilder.AssignArray(view, "dust", dust);
            PixelMenuBuilder.AssignArray(view, "collected", collected);

            DrivingNailController controller = MiniGameSceneBuilder.Finish<DrivingNailController>(s, MusicPath, 64);
            PixelMenuBuilder.Assign(controller, "view", view, "hitSound", Rimshot());
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("DRIVING_NAIL_READY");
        }

        // A short hammer rimshot: a click plus a fast noise burst, used as the pad-8 strike.
        private static AudioClip Rimshot()
        {
            const int rate = 44100;
            int samples = (int)(rate * .09);
            Directory.CreateDirectory(Path.GetDirectoryName(RimPath));
            var random = new System.Random(8);
            using (var writer = new BinaryWriter(File.Create(RimPath)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double noise = random.NextDouble() * 2 - 1;
                    double click = Math.Sin(2 * Math.PI * 1680 * t) * Math.Exp(-t * 70);
                    double body = noise * Math.Exp(-t * 46);
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, .55 * click + .7 * body)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(RimPath, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RimPath);
            if (clip == null) throw new InvalidOperationException("Missing rimshot: " + RimPath);
            return clip;
        }

        // The open editor cannot take a second Unity process, so the first import builds the scene itself.
        private static bool buildQueued;

        [InitializeOnLoadMethod]
        private static void ScheduleBuild()
        {
            if (buildQueued) return;
            buildQueued = true;
            EditorApplication.delayCall += TryBuild;
        }

        private static void TryBuild()
        {
            buildQueued = false;
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DrivingNailMenu.built"));
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == SceneVersion) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                ScheduleBuild();
                return;
            }
            EditorSceneManager.SaveOpenScenes();
            Build();
            Directory.CreateDirectory(Path.GetDirectoryName(marker));
            File.WriteAllText(marker, SceneVersion);
        }

        private static readonly string[] HammerArt = {
            "..kkkk..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..koOk..",
            "..kook..",
            "kkkkkkkk",
            "kssssssk",
            "kshhhssk",
            "kssssssk",
            "kshhhssk",
            "kssssssk",
            "kkkkkkkk" };

        private static readonly string[] NailArt = {
            ".kkkkkk.",
            "kssssssk",
            "kshhhhsk",
            "kssssssk",
            ".kkkkkk.",
            "...kk...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...kk...",
            "..kssk..",
            "...kk...",
            "...kk..." };

        private static readonly string[] BentArt = {
            ".kkkkkk.",
            "kssssssk",
            "kshhhhsk",
            "kssssssk",
            ".kkkkkk.",
            "..kk....",
            ".ss.....",
            "ss......",
            ".ss.....",
            "..ss....",
            "...ss...",
            "...ss...",
            "...ss...",
            "...ss...",
            "...kk...",
            "..kssk..",
            "...kk...",
            "...kk..." };

        private static readonly string[] CarpenterArt = {
            "......kkkk......",
            ".....kYYYYk.....",
            "....kYYhhYYk....",
            "....kYekkYek....",
            "....kYYmmYYk....",
            ".....kYYYYk.....",
            "......kkkk......",
            "....kkppppkk....",
            "...kppppppppk...",
            "...kppppppppk...",
            "....kppppppk....",
            ".....kpppkk.....",
            ".....kppppk.....",
            "......kkkk......",
            "......k..k......",
            "......k..k......",
            ".....kk..kk.....",
            ".....kk..kk....." };

        private static readonly string[] ScratchArt = {
            "......kkkk.kk...",
            ".....kYYYYYkk...",
            "....kYYhhYYkYk..",
            "....kYekkYekk...",
            "....kYYmmYYk....",
            ".....kYYYYk.....",
            "......kkkk......",
            "....kkppppkk....",
            "...kppppppppk...",
            "...kppppppppk...",
            "....kppppppk....",
            ".....kpppkk.....",
            ".....kppppk.....",
            "......kkkk......",
            "......k..k......",
            "......k..k......",
            ".....kk..kk.....",
            ".....kk..kk....." };

        private static readonly string[] ChairArt = {
            "............",
            "....kkkk....",
            "...kpppkk...",
            "...kppppk...",
            "...kppppk...",
            "kkkkkkkkkkkk",
            "kYYYYYYYYYYk",
            "kYYYYYYYYYYk",
            "kkkkkkkkkkkk",
            "k..k....k..k",
            "k..k....k..k",
            "kkkk....kkkk" };

        private static readonly string[] DeskArt = {
            "kkkkkkkkkkkkkkkk",
            "kYYYYYYYYYYYYYYk",
            "kYhhhhhhhhhhhhYk",
            "kYYYYYYYYYYYYYYk",
            "kkkkkkkkkkkkkkkk",
            "....kk......kk..",
            "....kk......kk..",
            "....kk......kk..",
            "...kkkk....kkkk.",
            "...kkkk....kkkk." };

        private static readonly string[] HouseArt = {
            "......kk......",
            ".....krrk.....",
            "....krrrrk....",
            "...krrrrrrk...",
            "..krrrrrrrrk..",
            ".krrrrrrrrrrk.",
            "krrrrrrrrrrrrk",
            "kggggggggggggk",
            "kggkwwkwkggggk",
            "kgggkookgggggk",
            "kgggkookgggggk",
            "kgggkookgggggk",
            "kggggggggggggk",
            "kkkkkkkkkkkkkk" };

        private static Dictionary<string, Sprite> CreateArt()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(34, 34, 34, 255) }, { 'o', new Color32(214, 160, 104, 255) }, { 'O', new Color32(176, 116, 70, 255) },
                { 's', new Color32(214, 220, 226, 255) }, { 'h', new Color32(255, 255, 255, 255) },
                { 'Y', new Color32(248, 199, 154, 255) }, { 'p', new Color32(120, 168, 214, 255) },
                { 'e', new Color32(34, 34, 34, 255) }, { 'm', new Color32(204, 92, 96, 255) },
                { 'r', new Color32(196, 92, 74, 255) }, { 'g', new Color32(186, 214, 150, 255) },
                { 'w', new Color32(120, 176, 214, 255) } };
            return new Dictionary<string, Sprite> {
                { "hammer", PixelArt.Save(ArtDir, "hammer", HammerArt, palette) },
                { "nail", PixelArt.Save(ArtDir, "nail", NailArt, palette) },
                { "bent", PixelArt.Save(ArtDir, "bent", BentArt, palette) },
                { "carpenter", PixelArt.Save(ArtDir, "carpenter", CarpenterArt, palette) },
                { "scratch", PixelArt.Save(ArtDir, "scratch", ScratchArt, palette) },
                { "chair", PixelArt.Save(ArtDir, "chair", ChairArt, palette) },
                { "desk", PixelArt.Save(ArtDir, "desk", DeskArt, palette) },
                { "house", PixelArt.Save(ArtDir, "house", HouseArt, palette) } };
        }
    }
}

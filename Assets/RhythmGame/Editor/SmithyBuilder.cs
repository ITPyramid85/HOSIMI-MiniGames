using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 5, the smithy, on the same frame as nail driving: light workshop, one prop, the worker beside it.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.SmithyBuilder.Build
    public static class SmithyBuilder
    {
        public const string ScenePath = "Assets/Scenes/Smithy.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Smithy";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/SmithBeat.wav";
        private const string SceneVersion = "9";
        private const string StackPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/SmithStack.wav";
        private const string CrashPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/SmithCrash.wav";
        private const float Px = PixelTween.Pixel;
        private static readonly Color Workshop = new Color32(255, 244, 232, 255);
        private static readonly Color Soot = new Color32(88, 78, 72, 255);
        private static readonly Color Horn = new Color32(120, 124, 132, 255);
        private static readonly Color Ember = new Color32(255, 168, 64, 255);

        [MenuItem("Rhythm Game/Build Smithy")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("SmithCanvas", Workshop);
            Transform field = s.Field;

            Image touch = PixelMenuBuilder.Box("TouchArea", field, Color.clear, null);
            PixelMenuBuilder.Stretch(touch.rectTransform);
            touch.raycastTarget = true;

            RectTransform bench = PixelMenuBuilder.Rect("Bench", field, new Vector2(20, -72), new Vector2(520, 240));
            Image block = PixelMenuBuilder.Box("Block", bench, Soot, s.Solid);
            PixelMenuBuilder.Place(block.rectTransform, new Vector2(0, -28), new Vector2(360, 28));
            block.raycastTarget = false;
            Image face = PixelMenuBuilder.Box("Face", bench, Horn, s.Solid);
            PixelMenuBuilder.Place(face.rectTransform, new Vector2(0, 8), new Vector2(440, 72));
            face.raycastTarget = false;
            Image anvil = MiniGameSceneBuilder.Art("Anvil", bench, art["anvil"], new Vector2(0, 8), 8);
            Image glow = PixelMenuBuilder.Box("Glow", bench, Ember, s.Solid);
            PixelMenuBuilder.Place(glow.rectTransform, new Vector2(0, 78), new Vector2(120, 48));
            glow.raycastTarget = false;
            Image iron = MiniGameSceneBuilder.Art("Iron", bench, art["iron"], new Vector2(0, 78), 6);

            Image hammer = MiniGameSceneBuilder.Art("Hammer", bench, art["hammer"], Vector2.zero, 6);
            hammer.rectTransform.pivot = new Vector2(.5f, 1f);
            PixelMenuBuilder.Place(hammer.rectTransform, new Vector2(80, 78 + hammer.rectTransform.sizeDelta.y), hammer.rectTransform.sizeDelta);
            hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var sparks = new Image[8];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = PixelMenuBuilder.Box("Spark" + i, field, Ember, s.Solid);
                PixelMenuBuilder.Place(sparks[i].rectTransform, Vector2.zero, new Vector2(Px * 2, Px * 2));
                sparks[i].raycastTarget = false;
                sparks[i].gameObject.SetActive(false);
            }

            Image smith = MiniGameSceneBuilder.Art("Smith", field, art["smith"], new Vector2(500, -56), 6);
            Image blade = MiniGameSceneBuilder.Art("Blade", field, art["sword7"], new Vector2(-490, -36), 8);
            blade.enabled = false;
            var pips = new Image[7];
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = MiniGameSceneBuilder.Art("Pip" + (i + 1), field, art["sword" + (i + 1)], new Vector2(-600 + i * 48, -312), 3);
                pips[i].color = new Color(1f, 1f, 1f, .28f);
            }

            Text count = PixelMenuBuilder.Label("Count", field, "", new Vector2(68, 196), new Vector2(420, 72), 56, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(count.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            Text caption = PixelMenuBuilder.Label("Remaining", field, "", new Vector2(68, 148), new Vector2(420, 36), 24, Palette.Ink, TextAnchor.MiddleCenter);
            Text bubble = PixelMenuBuilder.Label("Bubble", field, "", new Vector2(500, 148), new Vector2(300, 48), 28, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(bubble.gameObject, Palette.Panel, new Vector2(Px / 2, -Px / 2));
            PixelText.Mark(PixelMenuBuilder.Label("Key", field, "2 / E", new Vector2(20, -312), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter));
            PixelText.Mark(count);
            PixelText.Mark(caption);

            SmithyView view = touch.gameObject.AddComponent<SmithyView>();
            PixelMenuBuilder.Assign(view, "anvil", bench, "hammer", hammer.rectTransform, "smith", smith.rectTransform, "blade", blade.rectTransform,
                "iron", iron, "glow", glow, "smithImage", smith, "bladeImage", blade,
                "ironBar", art["iron"], "ironCracked", art["cracked"], "smithIdle", art["smith"], "smithGlad", art["glad"], "smithSigh", art["sigh"],
                "countText", count, "caption", caption, "bubble", bubble);
            PixelMenuBuilder.AssignArray(view, "sparks", sparks);
            PixelMenuBuilder.AssignArray(view, "pips", pips);
            PixelMenuBuilder.AssignArray(view, "swords", new UnityEngine.Object[] {
                art["sword1"], art["sword2"], art["sword3"], art["sword4"], art["sword5"], art["sword6"], art["sword7"] });

            WriteBellows(MusicPath, 48);
            SmithyController controller = MiniGameSceneBuilder.Finish<SmithyController>(s, MusicPath, 48);
            Nudge(s.Root.Find("Judgement") as RectTransform, 48f, 28f);
            Nudge(s.Root.Find("Countdown") as RectTransform, 48f, 28f);
            PixelMenuBuilder.Assign(controller, "view", view, "hitSound", Cymbal(StackPath, .16, true), "crashSound", Cymbal(CrashPath, .8, false));
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("SMITHY_READY");
        }

        private static void Nudge(RectTransform rect, float x, float y)
        {
            if (rect != null) rect.anchoredPosition += new Vector2(x, y);
        }

        // A pump of air on every beat. This is the only rhythmic guide; the red iron is the visual one.
        private static void WriteBellows(string path, int seconds)
        {
            const int rate = 44100;
            int samples = rate * seconds;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var random = new System.Random(12);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double beat = t % .5;
                    double attack = 1 - Math.Exp(-beat * 48);
                    double env = attack * Math.Exp(-beat * 5.5);
                    double air = (random.NextDouble() * 2 - 1) * env;
                    double flap = Math.Sin(2 * Math.PI * 78 * beat) * Math.Exp(-beat * 16);
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, .5 * air + .2 * flap)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        // Stack is short and dirty. Crash is the long wash used only for the finishing blow.
        private static AudioClip Cymbal(string path, double seconds, bool stack)
        {
            const int rate = 44100;
            int samples = (int)(rate * seconds);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var random = new System.Random(stack ? 3 : 9);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double noise = (random.NextDouble() * 2 - 1);
                    double tone = stack
                        ? noise * Math.Exp(-t * 34) * .55
                          + Math.Sin(2 * Math.PI * 2100 * t) * Math.Exp(-t * 30) * .22
                          + Math.Sin(2 * Math.PI * 4800 * t) * Math.Exp(-t * 42) * .16
                        : noise * Math.Exp(-t * 5.5) * .34
                          + Math.Sin(2 * Math.PI * 520 * t) * Math.Exp(-t * 3.2) * .18
                          + Math.Sin(2 * Math.PI * 1480 * t) * Math.Exp(-t * 4.2) * .2
                          + Math.Sin(2 * Math.PI * 3600 * t) * Math.Exp(-t * 6) * .12;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing cymbal: " + path);
            return clip;
        }

        private static void WriteWavHeader(BinaryWriter writer, int samples, int rate)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
        }

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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Smithy.built"));
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
            "..kkkk..", "..kook..", "..kook..", "..kook..", "..kook..", "..kook..", "..koOk..", "..kook..",
            "kkkkkkkk", "kssssssk", "kshhhssk", "kssssssk", "kshhhssk", "kssssssk", "kkkkkkkk" };

        private static readonly string[] AnvilArt = {
            "kkkkkkkkkkkkkkkkkkkk",
            "kssssssssssssssssssk",
            "kshhhhhhhhhhhhhhhssk",
            "kkkkkkkkkkkkkkkkkkkk",
            "......kkkkkkkk......",
            "......kssssssk......",
            "......kssssssk......",
            "......kkkkkkkk......" };

        private static readonly string[] IronArt = {
            ".kkkkkkkkkkkk.",
            "kssssssssssssk",
            "kshhhhhhhhhhsk",
            "kssssssssssssk",
            "kshhhhhhhhhhsk",
            ".kkkkkkkkkkkk." };

        private static readonly string[] CrackedArt = {
            ".kkkkkkkkkkkk.",
            "ksss.kk.sssssk",
            "kshh.kk.hhhssk",
            "ksss.kk.sssssk",
            "kshhhhhhhhhhsk",
            ".kkkkkkkkkkkk." };

        // One width, seven blades. Each heat replaces the one on the bench with the next.
        private static readonly string[] Sword1 = {
            "............", "............", ".....kk.....", "....kssk....", "....kssk....", "....kssk....",
            "....kssk....", "....kssk....", "....kkkk....", "....kook....", "....kook....", "....kook....",
            "....kkkk....", "............", "............", "............" };
        private static readonly string[] Sword2 = {
            ".....kk.....", "....kssk....", "....kssk....", "....khhk....", "....kssk....", "....khhk....",
            "....kssk....", "....kssk....", "....kssk....", "....kkkk....", "....kook....", "....kook....",
            "....koOk....", "....kook....", "....kkkk....", "............" };
        private static readonly string[] Sword3 = {
            ".....kk.....", "....kssk....", "....khhk....", "....kssk....", "....khhk....", "....kssk....",
            "....khhk....", "....kssk....", "...kkkkkk...", "..kksssskk..", "...kkkkkk...", "....kook....",
            "....koOk....", "....kook....", "....kook....", "....kkkk...." };
        private static readonly string[] Sword4 = {
            ".....kk.....", "....kssk....", "....khhk....", "....kssk....", "....khhk....", "....kssk....",
            "....khhk....", "....kssk....", "..kkkkkkkk..", ".kkrrssrrkk.", "..kkkkkkkk..", "....kook....",
            "....koOk....", "....kook....", "....kook....", "....kkkk...." };
        private static readonly string[] Sword5 = {
            ".....kk.....", "....kggk....", "....kghk....", "....kggk....", "....kghk....", "....kggk....",
            "....kghk....", "....kggk....", "..kkggggkk..", ".kkgrgggrkk.", "..kkggggkk..", "....kggk....",
            "....koOk....", "....kook....", "....kghk....", "....kkkk...." };
        private static readonly string[] Sword6 = {
            ".....kk.....", "....kggk....", "....kghk....", "....kggk....", "....kghk....", "....kggk....",
            "....kghk....", "....kggk....", ".kkkkkkkkkk.", "kkgrhgghrgkk", ".kkkkkkkkkk.", "....kggk....",
            "....koOk....", "....kggk....", "....kghk....", "....kkkk...." };
        private static readonly string[] Sword7 = {
            ".....kk.....", "....khhk....", "....kggk....", "....kghk....", "....kggk....", "....kghk....",
            "....kggk....", "....kghk....", "kkkkkkkkkkkk", "kgrghgghgrgk", "kkkkkkkkkkkk", "....kggk....",
            "....khhk....", "....kggk....", "....koOk....", "....kkkk...." };

        // Reference portrait, drawn so the face can change. Topknot, headband, beard, apron, fists, wraps.
        // Rows 6, 7 and 10 are the brow, the eyes and the mouth.
        private static readonly string[] SmithIdle = {
            "............kknnnnkk............",
            "..........kkNNnnnnNNkk..........",
            "........kkNNnnnnnnnnNNkk........",
            "....kkWWWWWWWWWWWWWWWWWWkkWWW...",
            "..kkNNnnnnnnnnnnnnnnnnnnnnNNkk..",
            ".kkNYYYYYYYYYYYYYYYYYYYYYYYYNkk.",
            ".kkNYYkYYYYYYYYYYYYYYYYkYYYNNkk.",
            ".kkNYYYkhhkYYYYYYYYYkhhkYYYNNkk.",
            ".kkNYYYYbbYYYYYYYYYYYbbYYYYNNkk.",
            ".kkNYYYYYYnnnnnnnnnnnYYYYYYNNkk.",
            ".kkNYYYYYnnnshhhhhsnnnYYYYYNNkk.",
            "..kkNnnnnnnnnnnnnnnnnnnnnnnNkk..",
            "....kkNnnnnnnnnnnnnnnnnnnNkk....",
            "......kkYYYYYYYYYYYYYYYYkk......",
            "....kkppppppppppppppppppppkk....",
            "kYYYYkppaaaaaaaaaaaaaaappkYYYYYk",
            "kYYYukkppaaaaaaaaaaaaaaappkuYYYk",
            "kkYYYYkkaaaaaaaaaaaaaaaaakkYYYkk",
            ".kkkaaaaaaaaaaaaaaaaaaaaaaaakkk.",
            ".kkkaaaaaaaaaaaaaaaaaaaaaaaakkk.",
            ".kkkaaaaaaakkkkkkkkkaaaaaaaakkk.",
            ".kkkaaaaaaaaaaaaaaaaaaaaaaaakkk.",
            ".kkkaaaaaaaaaaaaaaaaaaaaaaaakkk.",
            ".kkkaaaaaaaaaaaaaaaaaaaaaaaakkk.",
            ".kkddddddddddddddddddddddddddkk.",
            "kkdddddddk............kdddddddkk",
            "kkdddddddk............kdddddddkk",
            "kkWWWWWWWkk..........kkWWWWWWWkk",
            "kkWWWWWWWkk..........kkWWWWWWWkk",
            "kkOOOOOOOOk.........kOOOOOOOOOkk",
            "kkkkkkkkkkkkkk....kkkkkkkkkkkkkk" };

        private static Dictionary<string, Sprite> CreateArt()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(34, 34, 34, 255) }, { 'o', new Color32(214, 160, 104, 255) }, { 'O', new Color32(176, 116, 70, 255) },
                { 's', new Color32(186, 192, 200, 255) }, { 'h', new Color32(244, 246, 248, 255) },
                { 'g', new Color32(232, 184, 64, 255) }, { 'r', new Color32(214, 48, 64, 255) },
                { 'n', new Color32(110, 64, 36, 255) }, { 'N', new Color32(72, 40, 24, 255) },
                { 'W', new Color32(244, 236, 214, 255) }, { 'Y', new Color32(255, 206, 166, 255) },
                { 'b', new Color32(255, 142, 162, 255) }, { 'm', new Color32(180, 48, 52, 255) },
                { 'a', new Color32(168, 96, 48, 255) }, { 'p', new Color32(236, 220, 190, 255) },
                { 'd', new Color32(78, 52, 40, 255) }, { 'u', new Color32(92, 58, 42, 255) },
                { 'w', new Color32(220, 230, 240, 255) } };
            string[] glad = PixelArt.Overlay(SmithIdle, new (int, int, string)[] {
                (6, 0, ".kkNYYYYYYYYYYYYYYYYYYYYYYYYNkk."),
                (7, 0, ".kkNYYYYkYYkYYYYYYYYkYYkYYYNNkk."),
                (10, 0, ".kkNYYYYYnnnsshhhssnnnYYYYYNNkk.") });
            string[] sigh = PixelArt.Overlay(SmithIdle, new (int, int, string)[] {
                (4, 0, "..kkNNnnnnnnnnnnnnnnnnnnnnNNww.."),
                (6, 0, ".kkNYYYYYYYYYYYYYYYYYYYYYYYYNkk."),
                (7, 0, ".kkNYYYYkkkkYYYYYYYkkkkYYYYNNkk."),
                (10, 0, ".kkNYYYYYYnnnnmmmmnnnnYYYYwNNkk.") });
            return new Dictionary<string, Sprite> {
                { "hammer", PixelArt.Save(ArtDir, "hammer", HammerArt, palette) },
                { "anvil", PixelArt.Save(ArtDir, "anvil", AnvilArt, palette) },
                { "iron", PixelArt.Save(ArtDir, "iron", IronArt, palette) },
                { "cracked", PixelArt.Save(ArtDir, "cracked", CrackedArt, palette) },
                { "smith", PixelArt.Save(ArtDir, "smith", SmithIdle, palette) },
                { "glad", PixelArt.Save(ArtDir, "smith_glad", glad, palette) },
                { "sigh", PixelArt.Save(ArtDir, "smith_sigh", sigh, palette) },
                { "sword1", PixelArt.Save(ArtDir, "sword1", Sword1, palette) },
                { "sword2", PixelArt.Save(ArtDir, "sword2", Sword2, palette) },
                { "sword3", PixelArt.Save(ArtDir, "sword3", Sword3, palette) },
                { "sword4", PixelArt.Save(ArtDir, "sword4", Sword4, palette) },
                { "sword5", PixelArt.Save(ArtDir, "sword5", Sword5, palette) },
                { "sword6", PixelArt.Save(ArtDir, "sword6", Sword6, palette) },
                { "sword7", PixelArt.Save(ArtDir, "sword7", Sword7, palette) } };
        }

    }
}

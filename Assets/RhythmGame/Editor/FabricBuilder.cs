using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 7, fabric beating, on the same frame as the smithy: a light court, the cloth, Mother beside it.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.FabricBuilder.Build
    public static class FabricBuilder
    {
        public const string ScenePath = "Assets/Scenes/FabricBeating.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Fabric";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricBeat.wav";
        private const string TomLeftPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricTom1.wav";
        private const string TomRightPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricTom2.wav";
        private const string WhooshPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricWhoosh.wav";
        private const string CoughPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricCough.wav";
        private const string SpreadPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/FabricSpread.wav";
        private const string SceneVersion = "2";
        private const float Px = PixelTween.Pixel;
        private const float Lean = 42;   // resting lean of a club, mirrored on the other hand
        private static readonly Color Court = new Color32(255, 236, 214, 255);
        private static readonly Color Wood = new Color32(146, 96, 58, 255);
        private static readonly Color Gold = new Color32(255, 214, 96, 255);

        [MenuItem("Rhythm Game/Build Fabric Beating")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("FabricCanvas", Court);
            Transform field = s.Field;

            Image touch = PixelMenuBuilder.Box("TouchArea", field, Color.clear, null);
            PixelMenuBuilder.Stretch(touch.rectTransform);
            touch.raycastTarget = true;

            Image block = PixelMenuBuilder.Box("Block", field, Wood, s.Solid);
            PixelMenuBuilder.Place(block.rectTransform, new Vector2(0, -96), new Vector2(460, 36));
            block.raycastTarget = false;

            Image cloth = MiniGameSceneBuilder.Art("Cloth", field, art["cloth"], new Vector2(0, -28), 8);
            // Mirrored: the same lean and the same distance from the cloth on both hands.
            Image left = Club("LeftClub", field, art["club"], new Vector2(-93, 71));
            Image right = Club("RightClub", field, art["club"], new Vector2(93, 71));
            right.rectTransform.localEulerAngles = new Vector3(0, 0, -Lean);

            Image mother = MiniGameSceneBuilder.Art("Mother", field, art["mother"], new Vector2(430, -36), 6);
            var sparkles = new Image[8];
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = PixelMenuBuilder.Box("Spark" + i, field, Gold, s.Solid);
                PixelMenuBuilder.Place(sparkles[i].rectTransform, Vector2.zero, new Vector2(Px * 2, Px * 2));
                sparkles[i].raycastTarget = false;
                sparkles[i].gameObject.SetActive(false);
            }

            Text bubble = PixelMenuBuilder.Label("Bubble", field, "", new Vector2(430, 150), new Vector2(280, 48), 28, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(bubble.gameObject, Palette.Panel, new Vector2(Px / 2, -Px / 2));
            Text key = PixelMenuBuilder.Label("Key", field, "4 / S", new Vector2(0, -300), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter);
            PixelText.Mark(key);
            key.gameObject.SetActive(false);

            FabricView view = touch.gameObject.AddComponent<FabricView>();
            PixelMenuBuilder.Assign(view, "cloth", cloth.rectTransform, "leftClub", left.rectTransform, "rightClub", right.rectTransform,
                "mother", mother.rectTransform, "motherImage", mother, "bubble", bubble,
                "motherIdle", art["mother"], "motherLook", art["look"], "motherGlad", art["glad"]);
            PixelMenuBuilder.AssignArray(view, "sparkles", sparkles);

            WriteMusic(MusicPath, 48);
            WriteTom(TomLeftPath, 146);
            WriteTom(TomRightPath, 196);
            WriteWhoosh(WhooshPath);
            WriteCough(CoughPath);
            WriteSpread(SpreadPath);
            FabricController controller = MiniGameSceneBuilder.Finish<FabricController>(s, MusicPath, 48);
            PixelMenuBuilder.Assign(controller, "view", view,
                "tomLeft", LoadClip(TomLeftPath), "tomRight", LoadClip(TomRightPath),
                "whooshSound", LoadClip(WhooshPath), "coughSound", LoadClip(CoughPath), "spreadSound", LoadClip(SpreadPath));
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("FABRIC_READY");
        }

        private static Image Club(string name, Transform field, Sprite sprite, Vector2 top)
        {
            Image club = MiniGameSceneBuilder.Art(name, field, sprite, top, 6);
            club.rectTransform.pivot = new Vector2(.5f, 1f);
            PixelMenuBuilder.Place(club.rectTransform, top, club.rectTransform.sizeDelta);
            club.rectTransform.localEulerAngles = new Vector3(0, 0, Lean);
            return club;
        }

        private static AudioClip LoadClip(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing clip: " + path);
            return clip;
        }

        // A pentatonic line whose pulse tightens across the loop. The raised club is still the cue.
        private static void WriteMusic(string path, int seconds)
        {
            const int rate = 44100;
            int samples = rate * seconds;
            double[] scale = { 196.00, 220.00, 261.63, 293.66, 329.63, 392.00, 440.00 };
            int step = 0;
            double phase = 0, notePhase = 0, noteAge = 4;
            var random = new System.Random(7);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double beatsPerSecond = 2 + 2.4 * (t / seconds);
                    phase += beatsPerSecond / rate;
                    if (phase >= 1)
                    {
                        phase -= 1;
                        step = (step + 1 + (step % 4 == 2 ? 1 : 0)) % scale.Length;
                        notePhase = 0;
                        noteAge = 0;
                    }
                    noteAge += 1.0 / rate;
                    notePhase += scale[step] / rate;
                    double env = Math.Exp(-noteAge * (7 + 8 * t / seconds));
                    double tone = Math.Sin(2 * Math.PI * notePhase) * env * .2
                        + Math.Sin(2 * Math.PI * notePhase * 1.5) * env * .05
                        + (random.NextDouble() * 2 - 1) * Math.Exp(-noteAge * 36) * .1;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteTom(string path, double pitch)
        {
            const int rate = 44100;
            int samples = (int)(rate * .22);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double tone = Math.Sin(2 * Math.PI * pitch * t) * Math.Exp(-t * 14) * .55
                        + Math.Sin(2 * Math.PI * pitch * 2.02 * t) * Math.Exp(-t * 22) * .16
                        + Math.Sin(2 * Math.PI * 1800 * t) * Math.Exp(-t * 80) * .12;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
        }

        // The club leaving her hand: a falling rush, then the floor.
        private static void WriteWhoosh(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * .42);
            var random = new System.Random(4);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double rush = (random.NextDouble() * 2 - 1) * Math.Exp(-t * 6) * .28
                        + Math.Sin(2 * Math.PI * (640 - 900 * t) * t) * Math.Exp(-t * 5) * .22;
                    double thud = t > .24 ? Math.Sin(2 * Math.PI * 86 * (t - .24)) * Math.Exp(-(t - .24) * 16) * .55 : 0;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, rush + thud)) * short.MaxValue));
                }
            }
        }

        private static void WriteCough(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * .46);
            var random = new System.Random(11);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double noise = random.NextDouble() * 2 - 1;
                    double tone = Burst(t, .04, noise) + Burst(t, .2, noise) * .85;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
        }

        private static double Burst(double t, double at, double noise)
        {
            double d = t - at;
            if (d < 0 || d > .11) return 0;
            return noise * Math.Sin(Math.PI * d / .11) * .55 + Math.Sin(2 * Math.PI * 180 * d) * Math.Sin(Math.PI * d / .11) * .2;
        }

        // The cloth opening out in one stroke.
        private static void WriteSpread(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * .55);
            var random = new System.Random(15);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double tone = (random.NextDouble() * 2 - 1) * Math.Exp(-t * 3.2) * .32
                        + Math.Sin(2 * Math.PI * 980 * t) * Math.Exp(-t * 5) * .16
                        + Math.Sin(2 * Math.PI * 1560 * t) * Math.Exp(-t * 4.2) * .12;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/FabricBeating.built"));
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

        private static readonly string[] ClubArt = {
            ".kkkk.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kook.",
            ".kOOk.",
            "kOOOOk",
            "kOOOOk",
            "kOOOOk",
            ".kkkk." };

        private static readonly string[] ClothArt = {
            "kkkkkkkkkkkkkkkkkkkk",
            "krrrrrrrrrrrrrrrrrrk",
            "krRrrrrrrrrrrrrrrRrk",
            "krrrrwwwwwwwwwwrrrrk",
            "krRrrrrrrrrrrrrrrRrk",
            "krrrrrrrrrrrrrrrrrrk",
            "kkkkkkkkkkkkkkkkkkkk" };

        // Hair, brows, eyes and mouth sit on fixed rows so the other faces can swap them.
        private static readonly string[] MotherArt = {
            "......hhhh......",
            "...kkhhhhhhkk...",
            "..khsssssssshk..",
            "..khsskkkksshk..",
            "..khsseessessk..",
            "..khsssssssshk..",
            "..khssmmmmsshk..",
            "...kksssssskk...",
            "..kppppppppppk..",
            ".kppwwwwwwwwppk.",
            ".kppwwwwwwwwppk.",
            ".kppppppppppppk.",
            "..kddddddddddk..",
            "..kDddddddddDk..",
            "..kddddkkddddk..",
            "...kkkk..kkkk...",
            "..kooo....ooook.",
            "..kkkk....kkkk.." };

        private static Dictionary<string, Sprite> CreateArt()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(42, 32, 48, 255) }, { 'h', new Color32(72, 40, 28, 255) },
                { 's', new Color32(255, 214, 180, 255) }, { 'e', new Color32(40, 28, 36, 255) },
                { 'm', new Color32(176, 64, 80, 255) }, { 'p', new Color32(236, 220, 196, 255) },
                { 'w', new Color32(255, 250, 244, 255) }, { 'd', new Color32(42, 64, 138, 255) },
                { 'D', new Color32(86, 118, 186, 255) }, { 'o', new Color32(214, 160, 104, 255) },
                { 'O', new Color32(150, 92, 48, 255) }, { 'r', new Color32(186, 42, 58, 255) },
                { 'R', new Color32(232, 110, 118, 255) } };
            string[] look = PixelArt.Overlay(MotherArt, new[] {
                (3, 0, "..khkkkkkkkkkhk."),
                (4, 0, "..khsseekkeessk."),
                (6, 0, "..khsskkkksshk..") });
            string[] glad = PixelArt.Overlay(MotherArt, new[] {
                (4, 0, "..khssekkeessk.."),
                (6, 0, "..khsmmmmmmshk..") });
            return new Dictionary<string, Sprite> {
                { "club", PixelArt.Save(ArtDir, "club", ClubArt, palette) },
                { "cloth", PixelArt.Save(ArtDir, "cloth", ClothArt, palette) },
                { "mother", PixelArt.Save(ArtDir, "mother", MotherArt, palette) },
                { "look", PixelArt.Save(ArtDir, "mother_look", look, palette) },
                { "glad", PixelArt.Save(ArtDir, "mother_glad", glad, palette) } };
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 8. Flat illustration matching the reference: two heads over wide drums, a cool band below.
    public static class EchoBuilder
    {
        public const string ScenePath = "Assets/Scenes/TappingBeat.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Echo";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/EchoBeat.wav";
        private const string SceneVersion = "7";
        private static readonly Color Court = new Color32(240, 245, 245, 255);
        private static readonly Color Table = new Color32(224, 229, 239, 255);
        private static readonly Color Skin = new Color32(255, 215, 180, 255);
        private static readonly Color HairLeft = new Color32(24, 34, 49, 255);
        private static readonly Color HairRight = new Color32(69, 79, 99, 255);
        private static readonly Color Cream = new Color32(240, 230, 195, 255);
        private static readonly Color Maroon = new Color32(134, 44, 44, 255);
        private static readonly Color Ink = new Color32(25, 35, 50, 255);
        private static readonly Color Gold = new Color32(232, 200, 106, 255);
        private const float LeftX = -316f;
        private const float RightX = 310f;

        [MenuItem("Rhythm Game/Build Tapping Beat")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("EchoCanvas", Court);
            Transform field = s.Field;

            Image table = PixelMenuBuilder.Box("Table", field, Table, s.Solid);
            PixelMenuBuilder.Place(table.rectTransform, new Vector2(0f, -226f), new Vector2(1280f, 154f));
            table.raycastTarget = false;

            Seat("Player", field, LeftX, art["smile"], HairLeft, art["circle"], art["stadium"]);
            Image rival = Seat("Rival", field, RightX, art["frown"], HairRight, art["circle"], art["stadium"]);
            Image player = field.Find("Player/Face").GetComponent<Image>();

            Image bubbleBack = Shape("Bubble", field, art["stadium"], new Vector2(468f, 176f), new Vector2(150f, 58f), Color.white);
            Text bubble = PixelMenuBuilder.Label("Huh", bubbleBack.transform, "", Vector2.zero, new Vector2(140f, 58f), 28, Ink, TextAnchor.MiddleCenter);
            bubbleBack.gameObject.SetActive(false);

            // The free column between the two drummers, clear of their heads and of the shared HUD.
            Text beats = PixelMenuBuilder.Label("Beats", field, "", new Vector2(0f, 128f), new Vector2(360f, 72f), 24, Ink, TextAnchor.MiddleCenter);
            beats.lineSpacing = 1.3f;
            PixelText.Mark(beats);
            Text turn = PixelMenuBuilder.Label("Turn", field, "", new Vector2(0f, 30f), new Vector2(300f, 72f), 26, Ink, TextAnchor.MiddleCenter);
            turn.lineSpacing = 1.3f;

            var drums = new Image[8];
            Vector2[] kit = {
                new Vector2(-130f, -176f), new Vector2(130f, -176f),
                new Vector2(-200f, -214f), new Vector2(-68f, -214f), new Vector2(68f, -214f),
                new Vector2(-130f, -270f), Vector2.zero, new Vector2(130f, -270f) };
            int[] used = { 0, 1, 2, 3, 4, 5, 7 };
            foreach (int pad in used)
            {
                bool cymbal = pad <= 1;
                drums[pad] = KitPad(field, art, "Drum" + (pad + 1), kit[pad], cymbal, MiniGameSceneBuilder.PadKeys[pad]);
                bool open = System.Array.IndexOf(EchoChart.PadsIn(1), pad) >= 0;
                drums[pad].GetComponent<CanvasGroup>().alpha = open ? 1f : .34f;
                Button button = drums[pad].gameObject.AddComponent<Button>();
                button.targetGraphic = drums[pad];
                ColorBlock colors = button.colors;
                colors.disabledColor = Color.white;
                colors.fadeDuration = 0f;
                button.colors = colors;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }

            EchoView view = field.gameObject.AddComponent<EchoView>();
            PixelMenuBuilder.Assign(view, "rival", rival, "player", player, "bubble", bubble, "bubbleBack", bubbleBack, "beats", beats, "turn", turn,
                "calm", art["frown"], "smug", art["smug"], "haughty", art["haughty"],
                "fluster", art["fluster"], "laugh", art["laugh"], "playerFace", art["smile"]);
            PixelMenuBuilder.AssignArray(view, "drums", drums);

            WriteMusic(MusicPath, 32);
            WriteTom(PathOf("EchoTomA"), 220);
            WriteTom(PathOf("EchoTomS"), 174);
            WriteTom(PathOf("EchoTomD"), 146);
            WriteTom(PathOf("EchoTomZ"), 110);
            WriteTom(PathOf("EchoTomC"), 82);
            WriteCymbal(PathOf("EchoCymbalQ"), 1800);
            WriteCymbal(PathOf("EchoCymbalE"), 1400);
            WriteHuh(PathOf("EchoHuh"));
            WriteLaugh(PathOf("EchoLaugh"));

            EchoController controller = MiniGameSceneBuilder.Finish<EchoController>(s, MusicPath, 32);
            // The clock's stage ladder is not this game's three stages. The five lives stay, and they fall.
            string[] unused = { "Scanlines", "Stage", "StageBack", "StageFill", "Countdown" };
            foreach (string name in unused)
            {
                Transform spare = s.Root.Find(name);
                if (spare != null) spare.gameObject.SetActive(false);
            }
            PixelMenuBuilder.Assign(controller, "view", view,
                "tomA", Load(PathOf("EchoTomA")), "tomS", Load(PathOf("EchoTomS")), "tomD", Load(PathOf("EchoTomD")),
                "tomZ", Load(PathOf("EchoTomZ")), "tomC", Load(PathOf("EchoTomC")),
                "cymbalQ", Load(PathOf("EchoCymbalQ")), "cymbalE", Load(PathOf("EchoCymbalE")),
                "huhSound", Load(PathOf("EchoHuh")), "laughSound", Load(PathOf("EchoLaugh")));
            MiniGameSceneBuilder.Save(s, ScenePath);
        }

        private static string PathOf(string name)
        {
            return "Assets/RhythmGame/Resources/RhythmGame/Audio/" + name + ".wav";
        }

        // Head 138 on a 256 texture. Features sit where the reference face does: dots, then a thick arc.
        private const float FaceScale = 138f / 256f;

        private static Dictionary<string, Sprite> CreateArt()
        {
            var art = new Dictionary<string, Sprite>();
            art["circle"] = SaveSmooth("circle", Circle(256));
            art["stadium"] = SaveSmooth("stadium", Stadium(512, 210));
            art["pad"] = SaveSmooth("pad", Stadium(256, 134));
            art["smile"] = SaveSmooth("face-smile", Features(Eyes.Dot, Mouth.Smile));
            art["frown"] = SaveSmooth("face-frown", Features(Eyes.Dot, Mouth.Frown));
            art["smug"] = SaveSmooth("face-smug", Features(Eyes.Narrow, Mouth.Smirk));
            art["haughty"] = SaveSmooth("face-haughty", Features(Eyes.Flat, Mouth.Smile));
            art["fluster"] = SaveSmooth("face-fluster", Features(Eyes.Wide, Mouth.Open));
            art["laugh"] = SaveSmooth("face-laugh", Features(Eyes.Shut, Mouth.Laugh));
            return art;
        }

        private static Image Seat(string name, Transform parent, float x, Sprite face, Color hair, Sprite circle, Sprite stadium)
        {
            RectTransform seat = PixelMenuBuilder.Rect(name, parent, Vector2.zero, Vector2.zero);
            Shape("Body", seat, stadium, new Vector2(x, -81f), new Vector2(312f, 128f), Maroon);
            Shape("Head", seat, circle, new Vector2(x, 4f), new Vector2(323f, 123f), Cream);
            Shape("Skin", seat, circle, new Vector2(x, 133f), new Vector2(138f, 138f), Skin);
            Shape("Hair", seat, circle, new Vector2(x, 186f), new Vector2(122f, 50f), hair);
            return Shape("Face", seat, face, new Vector2(x, 133f), new Vector2(138f, 138f), Color.white);
        }

        private static Image KitPad(Transform parent, Dictionary<string, Sprite> art, string name, Vector2 position, bool cymbal, string key)
        {
            Image hit = Shape(name, parent, art["circle"], position, new Vector2(96f, 64f), new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            hit.gameObject.AddComponent<CanvasGroup>();
            if (cymbal)
                Shape("Disc", hit.transform, art["circle"], Vector2.zero, new Vector2(96f, 30f), Gold);
            else
            {
                Shape("Body", hit.transform, art["pad"], new Vector2(0f, -8f), new Vector2(84f, 44f), Maroon);
                Shape("Top", hit.transform, art["circle"], new Vector2(0f, 10f), new Vector2(90f, 32f), Cream);
            }
            PixelText.Mark(PixelMenuBuilder.Label("Key", hit.transform, key, new Vector2(0f, cymbal ? 0f : 10f), new Vector2(48f, 24f), 16, Ink, TextAnchor.MiddleCenter));
            return hit;
        }

        private static Image Shape(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            Image image = PixelMenuBuilder.Box(name, parent, color, null);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            PixelMenuBuilder.Place(image.rectTransform, position, size);
            return image;
        }

        private enum Eyes { Dot, Narrow, Flat, Wide, Shut }
        private enum Mouth { Smile, Frown, Smirk, Open, Laugh }

        private static Texture2D Features(Eyes eyes, Mouth mouth)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[n * n]);
            var ink = new Color32(25, 35, 50, 255);
            float left = Tex(128f - 23f);
            float right = Tex(128f + 23f);
            float eyeY = Tex(128f + 9f);
            if (eyes == Eyes.Shut)
            {
                StrokeArc(tex, left, eyeY - 6f, 22f, -150f, -30f, 12f, ink);
                StrokeArc(tex, right, eyeY - 6f, 22f, -150f, -30f, 12f, ink);
            }
            else
            {
                float ry = eyes == Eyes.Flat ? 8f : eyes == Eyes.Narrow ? 12f : eyes == Eyes.Wide ? 24f : 17f;
                float rx = eyes == Eyes.Wide ? 24f : 17f;
                FillEllipse(tex, left, eyeY, rx, ry, ink);
                FillEllipse(tex, right, eyeY, rx, ry, ink);
            }
            if (eyes == Eyes.Flat) Brow(tex, left, right, eyeY + 28f, -16f, ink);
            if (eyes == Eyes.Wide) Brow(tex, left, right, eyeY + 34f, 14f, ink);
            if (mouth == Mouth.Open) FillEllipse(tex, 128f, Tex(128f - 34f), 16f, 18f, ink);
            else if (mouth == Mouth.Laugh) FillEllipse(tex, 128f, Tex(128f - 36f), 40f, 28f, ink);
            else if (mouth == Mouth.Frown) StrokeArc(tex, 128f, Tex(128f - 58f), 52f, 20f, 160f, 16f, ink);
            else if (mouth == Mouth.Smirk) StrokeArc(tex, 128f, Tex(128f - 6f), 40f, -150f, -30f, 14f, ink);
            else StrokeArc(tex, 128f, Tex(128f - 8f), 52f, -160f, -20f, 16f, ink);
            tex.Apply();
            return tex;
        }

        // 128 + a canvas offset, in texture pixels of the 138-unit head.
        private static float Tex(float headSpace)
        {
            return 128f + (headSpace - 128f) / FaceScale;
        }

        private static void Brow(Texture2D tex, float left, float right, float y, float tilt, Color color)
        {
            StrokeLine(tex, left - 18f, y - tilt, left + 16f, y + tilt, 8f, color);
            StrokeLine(tex, right - 16f, y + tilt, right + 18f, y - tilt, 8f, color);
        }

        private static Texture2D Circle(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float c = n * .5f;
            float radius = c - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + .8f)));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D Stadium(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float radius = height * .5f;
            float cy = radius;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float px = x + .5f;
                    float clamped = Mathf.Clamp(px, radius, width - radius);
                    float dx = px - clamped;
                    float dy = y + .5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + .8f)));
                }
            tex.Apply();
            return tex;
        }

        private static void FillEllipse(Texture2D tex, float cx, float cy, float rx, float ry, Color color)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 2f), x1 = Mathf.CeilToInt(cx + rx + 2f);
            int y0 = Mathf.FloorToInt(cy - ry - 2f), y1 = Mathf.CeilToInt(cy + ry + 2f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x + .5f - cx) / rx;
                    float ny = (y + .5f - cy) / ry;
                    Cover(tex, x, y, Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) + .8f / Mathf.Min(rx, ry)), color);
                }
        }

        private static void StrokeArc(Texture2D tex, float cx, float cy, float radius, float startDeg, float endDeg, float thickness, Color color)
        {
            float sweep = Mathf.DeltaAngle(startDeg, endDeg);
            float pad = thickness * .5f / radius * Mathf.Rad2Deg;
            int x0 = Mathf.FloorToInt(cx - radius - thickness), x1 = Mathf.CeilToInt(cx + radius + thickness);
            int y0 = Mathf.FloorToInt(cy - radius - thickness), y1 = Mathf.CeilToInt(cy + radius + thickness);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + .5f - cx, dy = y + .5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float along = Mathf.DeltaAngle(startDeg, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    bool inside = sweep >= 0f ? along >= -pad && along <= sweep + pad : along <= pad && along >= sweep - pad;
                    if (!inside) continue;
                    Cover(tex, x, y, Mathf.Clamp01(thickness * .5f - Mathf.Abs(dist - radius) + .8f), color);
                }
        }

        private static void StrokeLine(Texture2D tex, float x0, float y0, float x1, float y1, float thickness, Color color)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < .001f) return;
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - thickness), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + thickness);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - thickness), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + thickness);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + .5f - x0, py = y + .5f - y0;
                    float t = Mathf.Clamp01((px * dx + py * dy) / (len * len));
                    float ox = px - dx * t, oy = py - dy * t;
                    Cover(tex, x, y, Mathf.Clamp01(thickness * .5f - Mathf.Sqrt(ox * ox + oy * oy) + .8f), color);
                }
        }

        private static void Cover(Texture2D tex, int x, int y, float coverage, Color color)
        {
            if (coverage <= 0f || x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            Color under = tex.GetPixel(x, y);
            float a = Mathf.Clamp01(coverage) * color.a;
            float outA = a + under.a * (1f - a);
            if (outA <= 0f) return;
            Color over = (color * a + under * under.a * (1f - a)) / outA;
            over.a = outA;
            tex.SetPixel(x, y, over);
        }

        private static Sprite SaveSmooth(string name, Texture2D texture)
        {
            Directory.CreateDirectory(ArtDir);
            string path = ArtDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static AudioClip Load(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing clip: " + path);
            return clip;
        }

        private static void WriteMusic(string path, int seconds)
        {
            const int rate = 44100;
            int samples = rate * seconds;
            double[] scale = { 196.00, 220.00, 261.63, 293.66, 329.63, 392.00 };
            int step = 0;
            double phase = 0, notePhase = 0, noteAge = 4;
            var random = new System.Random(8);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    phase += 1.6 / rate;
                    if (phase >= 1)
                    {
                        phase -= 1;
                        step = (step + 1) % scale.Length;
                        notePhase = 0;
                        noteAge = 0;
                    }
                    noteAge += 1.0 / rate;
                    notePhase += scale[step] / rate;
                    double env = Math.Exp(-noteAge * 6);
                    double tone = Math.Sin(2 * Math.PI * notePhase) * env * .16
                        + Math.Sin(2 * Math.PI * notePhase * 2) * env * .04
                        + (random.NextDouble() * 2 - 1) * Math.Exp(-noteAge * 30) * .04;
                    writer.Write((short)(Math.Max(-.9, Math.Min(.9, tone)) * short.MaxValue));
                }
            }
        }

        private static void WriteTom(string path, double pitch)
        {
            const int rate = 44100;
            int samples = (int)(rate * .28);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double tone = Math.Sin(2 * Math.PI * pitch * t) * Math.Exp(-t * 10) * .6
                        + Math.Sin(2 * Math.PI * pitch * 2.01 * t) * Math.Exp(-t * 16) * .16;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
        }

        private static void WriteCymbal(string path, double pitch)
        {
            const int rate = 44100;
            int samples = (int)(rate * .4);
            var random = new System.Random((int)pitch);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double tone = Math.Sin(2 * Math.PI * pitch * t) * Math.Exp(-t * 4) * .22
                        + (random.NextDouble() * 2 - 1) * Math.Exp(-t * 5) * .28;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
        }

        private static void WriteHuh(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * .28);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double pitch = 320 + 180 * t;
                    double tone = Math.Sin(2 * Math.PI * pitch * t) * Math.Exp(-t * 8) * .4;
                    writer.Write((short)(Math.Max(-.95, Math.Min(.95, tone)) * short.MaxValue));
                }
            }
        }

        private static void WriteLaugh(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * .7);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double burst = Math.Abs(Math.Sin(2 * Math.PI * 6 * t));
                    double tone = Math.Sin(2 * Math.PI * 280 * t) * burst * Math.Exp(-t * 2) * .45;
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TappingBeat.built"));
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
    }
}

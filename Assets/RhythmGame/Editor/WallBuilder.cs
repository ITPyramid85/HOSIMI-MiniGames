using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 10. The reference layout: a light court, a slate floor, a wave creeping in from the left,
    // a headband runner with a hammer, and a staggered brick wall on the right.
    public static class WallBuilder
    {
        public const string ScenePath = "Assets/Scenes/BreakingBricks.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Wall";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/WallBeat.wav";
        private const string SceneVersion = "13";
        // 40 beats at 100 BPM, so the tense loop lands on a beat.
        private const int MusicSeconds = 24;
        private const float FloorTop = -130f;
        private const int BrickRows = 5, BrickColumns = 2;
        private static readonly Vector2 BrickSize = new Vector2(110f, 50f);
        private static readonly Color Court = new Color32(240, 245, 245, 255);
        private static readonly Color Floor = new Color32(108, 122, 156, 255);
        private static readonly Color Skin = new Color32(255, 215, 180, 255);
        private static readonly Color Cream = new Color32(240, 230, 195, 255);
        private static readonly Color Maroon = new Color32(134, 44, 44, 255);
        private static readonly Color Ink = new Color32(25, 35, 50, 255);
        private static readonly Color Gold = new Color32(232, 200, 106, 255);
        private static readonly Color Band = new Color32(240, 196, 64, 255);
        private static readonly Color Shirt = new Color32(220, 84, 84, 255);
        private static readonly Color Steel = new Color32(140, 152, 176, 255);
        private static readonly Color Handle = new Color32(92, 58, 36, 255);
        private static readonly Color Thin = new Color32(245, 212, 100, 255);
        private static readonly Color Mid = new Color32(240, 140, 64, 255);
        private static readonly Color Thick = new Color32(214, 84, 84, 255);
        private static readonly Color WaterBlue = new Color32(62, 124, 204, 255);
        private static readonly Color FishBlue = new Color32(120, 196, 232, 255);

        [MenuItem("Rhythm Game/Build Breaking Bricks")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("WallCanvas", Court);
            Transform field = s.Field;
            Image floor = PixelMenuBuilder.Box("Floor", field, Floor, s.Solid);
            PixelMenuBuilder.Place(floor.rectTransform, new Vector2(0f, (FloorTop - 360f) * 0.5f), new Vector2(1280f, FloorTop + 360f));
            floor.raycastTarget = false;

            RectTransform hero = Hero(field, art);
            RectTransform hammer = (RectTransform)hero.Find("Hammer");

            RectTransform wall = PixelMenuBuilder.Rect("Wall", field, new Vector2(300f, FloorTop), Vector2.zero);
            var bricks = new Image[BrickRows * BrickColumns];
            for (int row = 0; row < BrickRows; row++)
                for (int column = 0; column < BrickColumns; column++)
                {
                    float x = (column - 0.5f) * (BrickSize.x + 4f) + (row % 2 == 1 ? BrickSize.x * 0.5f : 0f);
                    float y = BrickSize.y * 0.5f + row * (BrickSize.y + 4f);
                    bricks[row * BrickColumns + column] = Shape("Brick" + row + column, wall, art["round"], new Vector2(x, y), BrickSize, Thin);
                }
            Vector2 middle = new Vector2(28f, BrickRows * (BrickSize.y + 4f) * 0.5f);
            Image chest = Shape("Chest", wall, art["round"], middle, new Vector2(150f, 110f), Gold);
            Shape("Lid", chest.transform, art["round"], new Vector2(0f, 24f), new Vector2(150f, 22f), new Color32(176, 132, 48, 255));
            Shape("Latch", chest.transform, art["circle"], new Vector2(0f, 2f), new Vector2(24f, 24f), Ink);
            chest.gameObject.SetActive(false);
            Image crack = Shape("Crack", wall, art["crack"], middle, new Vector2(230f, 250f), Color.white);
            crack.color = new Color(1f, 1f, 1f, 0f);
            Text hits = PixelMenuBuilder.Label("Hits", wall, "", middle, new Vector2(90f, 64f), 44, Cream, TextAnchor.MiddleCenter);
            PixelText.Mark(hits);

            // Pivot on the floor's left edge so the wave sits on top of the slate and grows up from there.
            Image water = Shape("Water", field, art["wave"], Vector2.zero, new Vector2(160f, 220f), WaterBlue);
            water.rectTransform.pivot = Vector2.zero;
            water.rectTransform.anchoredPosition = new Vector2(-640f, FloorTop);
            RectTransform fish = Fish(field, art);

            Image pill = Shape("MeterPill", field, art["stadium"], new Vector2(0f, 122f), new Vector2(180f, 52f), Cream);
            Text meters = PixelMenuBuilder.Label("Meters", pill.transform, "0m", Vector2.zero, new Vector2(160f, 44f), 32, Ink, TextAnchor.MiddleCenter);
            PixelText.Mark(meters);

            var drums = new Image[8];
            drums[WallChart.ThinPad] = Drum(field, art, "DrumS", new Vector2(-140f, -238f), "S", Thin, false);
            drums[WallChart.MidPad] = Drum(field, art, "DrumD", new Vector2(0f, -238f), "D", Mid, false);
            drums[WallChart.ThickPad] = Drum(field, art, "DrumC", new Vector2(140f, -238f), "C", Thick, false);
            drums[WallChart.ChestPad] = Drum(field, art, "DrumQ", new Vector2(290f, -234f), "Q", Gold, true);
            drums[WallChart.ChestPad].gameObject.SetActive(false);

            WallView view = field.gameObject.AddComponent<WallView>();
            PixelMenuBuilder.Assign(view, "water", water.rectTransform, "wall", wall, "chest", chest, "crack", crack,
                "hero", hero, "fish", fish, "hammer", hammer, "hits", hits, "meters", meters);
            PixelMenuBuilder.AssignArray(view, "bricks", bricks);
            PixelMenuBuilder.AssignArray(view, "drums", drums);

            WriteMusic(MusicPath, MusicSeconds);
            WriteTom(PathOf("WallTomThin"), 196);
            WriteTom(PathOf("WallTomMid"), 147);
            WriteTom(PathOf("WallTomThick"), 110);
            WriteCrash(PathOf("WallCrash"));
            WriteRubble(PathOf("WallRubble"));
            WriteChime(PathOf("WallChime"));
            WriteSplash(PathOf("WallSplash"));
            WriteGurgle(PathOf("WallGurgle"));
            WriteWater(PathOf("WallWater"));

            WallController controller = MiniGameSceneBuilder.Finish<WallController>(s, MusicPath, MusicSeconds);
            Transform scan = s.Root.Find("Scanlines");
            if (scan != null) scan.gameObject.SetActive(false);
            AudioSource flood = new GameObject("Flood", typeof(AudioSource)).GetComponent<AudioSource>();
            flood.transform.SetParent(s.Root, false);
            flood.playOnAwake = false;
            flood.loop = true;
            flood.spatialBlend = 0f;
            flood.volume = 0.02f;
            flood.clip = Load(PathOf("WallWater"));
            PixelMenuBuilder.Assign(controller, "view", view, "floodAudio", flood,
                "tomThin", Load(PathOf("WallTomThin")), "tomMid", Load(PathOf("WallTomMid")),
                "tomThick", Load(PathOf("WallTomThick")), "crash", Load(PathOf("WallCrash")),
                "rubble", Load(PathOf("WallRubble")), "chime", Load(PathOf("WallChime")),
                "splash", Load(PathOf("WallSplash")), "gurgle", Load(PathOf("WallGurgle")));
            MiniGameSceneBuilder.Save(s, ScenePath);
        }

        private static string PathOf(string name)
        {
            return "Assets/RhythmGame/Resources/RhythmGame/Audio/" + name + ".wav";
        }

        // The reference runner: round face, yellow headband, a surprised mouth and a red body.
        // The hammer pivots on its grip; rest is raised behind, a strike swings the head into the wall.
        private static RectTransform Hero(Transform parent, Dictionary<string, Sprite> art)
        {
            RectTransform root = PixelMenuBuilder.Rect("Hero", parent, new Vector2(50f, FloorTop), Vector2.zero);
            RectTransform hammer = PixelMenuBuilder.Rect("Hammer", root, new Vector2(70f, 60f), Vector2.zero);
            hammer.localEulerAngles = new Vector3(0f, 0f, 50f);
            Shape("Handle", hammer, art["round"], new Vector2(0f, 80f), new Vector2(14f, 160f), Handle);
            Shape("Head", hammer, art["round"], new Vector2(0f, 165f), new Vector2(76f, 46f), Steel);
            Shape("Shadow", root, art["stadium"], new Vector2(0f, 2f), new Vector2(120f, 14f), new Color(0f, 0f, 0f, 0.22f));
            Shape("Body", root, art["round"], new Vector2(0f, 42f), new Vector2(96f, 84f), Shirt);
            Shape("Head", root, art["circle"], new Vector2(0f, 128f), new Vector2(96f, 96f), Skin);
            Shape("Band", root, art["round"], new Vector2(0f, 156f), new Vector2(102f, 26f), Band);
            Shape("EyeL", root, art["circle"], new Vector2(-16f, 130f), new Vector2(10f, 10f), Ink);
            Shape("EyeR", root, art["circle"], new Vector2(16f, 130f), new Vector2(10f, 10f), Ink);
            Shape("Mouth", root, art["circle"], new Vector2(0f, 104f), new Vector2(16f, 20f), Ink);
            return root;
        }

        private static RectTransform Fish(Transform parent, Dictionary<string, Sprite> art)
        {
            RectTransform root = PixelMenuBuilder.Rect("Fish", parent, new Vector2(50f, -40f), Vector2.zero);
            Shape("Band", root, art["round"], new Vector2(16f, 24f), new Vector2(40f, 12f), Band);
            Image tail = Shape("Tail", root, art["stadium"], new Vector2(-48f, 0f), new Vector2(30f, 38f), Gold);
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 38f);
            Shape("Body", root, art["stadium"], Vector2.zero, new Vector2(96f, 42f), FishBlue);
            Shape("Eye", root, art["circle"], new Vector2(28f, 6f), new Vector2(12f, 12f), Cream);
            Shape("Pupil", root, art["circle"], new Vector2(30f, 6f), new Vector2(6f, 6f), Ink);
            root.gameObject.SetActive(false);
            return root;
        }

        private static Image Drum(Transform parent, Dictionary<string, Sprite> art, string name, Vector2 position, string key, Color top, bool cymbal)
        {
            Image hit = Shape(name, parent, art["circle"], position, cymbal ? new Vector2(116f, 52f) : new Vector2(96f, 64f), new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            hit.gameObject.AddComponent<CanvasGroup>();
            Shape("Body", hit.transform, art["stadium"], new Vector2(0f, -8f), cymbal ? new Vector2(100f, 18f) : new Vector2(84f, 44f), cymbal ? Gold : Maroon);
            Shape("Top", hit.transform, art["circle"], new Vector2(0f, cymbal ? 0f : 10f), cymbal ? new Vector2(108f, 28f) : new Vector2(90f, 32f), top);
            PixelText.Mark(PixelMenuBuilder.Label("Key", hit.transform, key, new Vector2(0f, cymbal ? 0f : 10f), new Vector2(48f, 24f), 16, Ink, TextAnchor.MiddleCenter));
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
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

        private static Dictionary<string, Sprite> CreateArt()
        {
            var art = new Dictionary<string, Sprite>();
            art["circle"] = SaveSmooth("circle", Circle(256));
            art["stadium"] = SaveSmooth("stadium", Stadium(512, 210));
            art["round"] = SaveSmooth("round", Rounded(256, 128, 22));
            art["wave"] = SaveSmooth("wave", Wave(512, 256));
            art["crack"] = SaveSmooth("crack", Crack());
            return art;
        }

        private static Texture2D Circle(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float c = n * 0.5f;
            float radius = c - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.8f)));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D Stadium(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float radius = height * 0.5f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float clamped = Mathf.Clamp(px, radius, width - radius);
                    float dx = px - clamped;
                    float dy = y + 0.5f - radius;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.8f)));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D Rounded(int width, int height, float radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = px - Mathf.Clamp(px, radius, width - radius);
                    float dy = py - Mathf.Clamp(py, radius, height - radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.8f)));
                }
            tex.Apply();
            return tex;
        }

        // Solid water with one and a half gentle swells along the top edge, like the reference wave.
        private static Texture2D Wave(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float top = height - 22f + 14f * Mathf.Sin(u * Mathf.PI * 3f + 0.6f);
                for (int y = 0; y < height; y++)
                {
                    float alpha = Mathf.Clamp01(top - (y + 0.5f) + 0.8f);
                    float shade = y > top - 26f ? 1f : 0.92f;
                    tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D Crack()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var clear = new Color32[n * n];
            tex.SetPixels32(clear);
            Stroke(tex, 24, 108, 64, 64);
            Stroke(tex, 64, 64, 108, 96);
            Stroke(tex, 64, 64, 78, 18);
            Stroke(tex, 36, 36, 96, 28);
            tex.Apply();
            return tex;
        }

        private static void Stroke(Texture2D tex, int x0, int y0, int x1, int y1)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float u = steps == 0 ? 0f : (float)i / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, u));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, u));
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int px = x + ox, py = y + oy;
                        if (px >= 0 && py >= 0 && px < tex.width && py < tex.height) tex.SetPixel(px, py, Color.white);
                    }
            }
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
            const double beat = 0.6;
            int samples = rate * seconds;
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                int bar = (int)(t / beat);
                double into = t - bar * beat;
                double kick = Math.Sin(2 * Math.PI * 52 * t) * Math.Exp(-into * 16);
                double pulse = Math.Sin(2 * Math.PI * 104 * t) * Math.Exp(-Math.Abs(into - 0.3) * 22) * 0.35;
                double drone = Math.Sin(2 * Math.PI * 55 * t) * 0.08 + Math.Sin(2 * Math.PI * 82 * t) * 0.04;
                double strain = bar % 8 == 7 ? Math.Sin(2 * Math.PI * 220 * t) * into * 0.12 : 0;
                return (kick * 0.55 + pulse + drone + strain) * 0.62;
            });
        }

        private static void WriteTom(string path, double pitch)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.28);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                return Math.Sin(2 * Math.PI * pitch * t) * Math.Exp(-t * 10) * 0.55;
            });
        }

        private static void WriteCrash(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.7);
            var random = new System.Random(11);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double noise = random.NextDouble() * 2 - 1;
                return (noise * 0.55 + Math.Sin(2 * Math.PI * 1400 * t) * 0.22) * Math.Exp(-t * 3.5);
            });
        }

        private static void WriteRubble(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.45);
            var random = new System.Random(4);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double noise = random.NextDouble() * 2 - 1;
                double env = Math.Exp(-t * 8) + 0.7 * Math.Exp(-Math.Abs(t - 0.08) * 14) + 0.45 * Math.Exp(-Math.Abs(t - 0.16) * 14);
                return noise * env * 0.5;
            });
        }

        private static void WriteChime(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.7);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                return (Math.Sin(2 * Math.PI * 880 * t) * Math.Exp(-t * 3) + Math.Sin(2 * Math.PI * 1320 * t) * Math.Exp(-t * 4) * 0.6) * 0.32;
            });
        }

        private static void WriteSplash(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.4);
            var random = new System.Random(6);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                return (random.NextDouble() * 2 - 1) * Math.Exp(-t * 6) * 0.6;
            });
        }

        private static void WriteGurgle(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.55);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double bubble = Math.Sin(2 * Math.PI * 8 * t);
                return Math.Sin(2 * Math.PI * (230 + 50 * bubble) * t) * Math.Max(0, bubble) * Math.Exp(-t * 2.4) * 0.4;
            });
        }

        private static void WriteWater(string path)
        {
            const int rate = 44100;
            int samples = rate * 2;
            var random = new System.Random(12);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double surge = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 0.35 * t);
                double noise = (random.NextDouble() * 2 - 1) * 0.12;
                double lap = Math.Sin(2 * Math.PI * 2.2 * t) * 0.14 + Math.Sin(2 * Math.PI * 3.1 * t) * 0.09;
                double rush = Math.Sin(2 * Math.PI * 0.8 * t) * Math.Sin(2 * Math.PI * 11 * t) * 0.06;
                return (noise + lap + rush) * surge;
            });
        }

        private static void WriteTone(string path, int samples, Func<int, double> sample)
        {
            const int rate = 44100;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                    writer.Write((short)(Math.Max(-0.95, Math.Min(0.95, sample(i))) * short.MaxValue));
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BreakingBricks.built"));
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

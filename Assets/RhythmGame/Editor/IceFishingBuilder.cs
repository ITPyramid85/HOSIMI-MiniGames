using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 6, ice fishing, on the same frame as the smithy: pixel props, a light field, one job.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.IceFishingBuilder.Build
    public static class IceFishingBuilder
    {
        public const string ScenePath = "Assets/Scenes/IceFishing.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/IceFishing";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/IceBeat.wav";
        private const string AudioDir = "Assets/RhythmGame/Resources/RhythmGame/Audio/";
        private const string SceneVersion = "21";
        private const float Px = PixelTween.Pixel;
        private static readonly Color Sky = new Color32(236, 246, 252, 255);
        private static readonly Color Band = new Color32(198, 224, 240, 255);
        private static readonly Color Tile = new Color32(226, 241, 248, 255);
        private static readonly Color TileEdge = new Color32(186, 214, 230, 255);
        private static readonly int[] HoleX = { -270, 0, 270 };
        private const float TileY = -24f, TileW = 230f, TileH = 156f;

        [MenuItem("Rhythm Game/Build Ice Fishing")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("IceCanvas", Sky);
            Transform field = s.Field;

            float surface = TileY + TileH * .5f;
            const float waterDepth = 300f;
            Image water = PixelMenuBuilder.Box("Water", field, Band, s.Solid);
            PixelMenuBuilder.Place(water.rectTransform, new Vector2(0, surface - waterDepth * .5f), new Vector2(1280, waterDepth));
            water.raycastTarget = false;

            Image fish = MiniGameSceneBuilder.Art("Fish", field, art["perch"], new Vector2(HoleX[1], TileY), 1);
            fish.gameObject.SetActive(false);

            var holes = new RectTransform[3];
            var openings = new Image[3];
            var cracks = new Image[3];
            int[] pads = { IceChart.Left, IceChart.Center, IceChart.Right };
            string[] keys = { "4 / S", "5 / D", "8 / C" };
            for (int i = 0; i < holes.Length; i++)
            {
                Image hole = MiniGameSceneBuilder.Art("Ice" + (i + 1), field, art["tile"], new Vector2(HoleX[i], TileY), 1);
                hole.raycastTarget = true;
                IceHole hit = hole.gameObject.AddComponent<IceHole>();
                var settings = new SerializedObject(hit);
                settings.FindProperty("pad").intValue = pads[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                holes[i] = hole.rectTransform;
                openings[i] = MiniGameSceneBuilder.Art("Open" + (i + 1), hole.transform, art["hole"], Vector2.zero, 1);
                openings[i].gameObject.SetActive(false);
                cracks[i] = MiniGameSceneBuilder.Art("Crack" + (i + 1), hole.transform, art["crack"], Vector2.zero, 1);
                cracks[i].gameObject.SetActive(false);
                PixelText.Mark(PixelMenuBuilder.Label("Key" + (i + 1), field, keys[i], new Vector2(HoleX[i], TileY - TileH * .5f - 22f), new Vector2(120, 28), 16, Palette.Ink, TextAnchor.MiddleCenter));
            }
            fish.transform.SetAsLastSibling();

            Image fisher = MiniGameSceneBuilder.Art("Fisher", field, art["fisher"], new Vector2(HoleX[1], surface), 1);
            fisher.rectTransform.pivot = new Vector2(.5f, 0f);
            PixelMenuBuilder.Place(fisher.rectTransform, new Vector2(HoleX[1], surface), fisher.rectTransform.sizeDelta);
            fisher.raycastTarget = false;

            Image hammer = MiniGameSceneBuilder.Art("Hammer", field, art["hammer"], Vector2.zero, 6);
            hammer.rectTransform.pivot = new Vector2(.5f, 1f);
            PixelMenuBuilder.Place(hammer.rectTransform, new Vector2(HoleX[1] + hammer.rectTransform.sizeDelta.y * .77f, surface), hammer.rectTransform.sizeDelta);
            hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            hammer.raycastTarget = false;

            Image bucket = MiniGameSceneBuilder.Art("Bucket", field, art["bucket"], new Vector2(-520, 120), 1);
            var pile = new Image[7];
            for (int i = 0; i < pile.Length; i++)
            {
                pile[i] = MiniGameSceneBuilder.Art("Catch" + (i + 1), bucket.transform, art["perch"], new Vector2(i % 2 == 0 ? -8 : 8, -34 + i * 9), .38f);
                pile[i].gameObject.SetActive(false);
            }

            Text caught = PixelMenuBuilder.Label("Caught", field, "", new Vector2(0, 286), new Vector2(520, 48), 32, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(caught.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            PixelText.Mark(caught);
            Text guide = PixelMenuBuilder.Label("Guide", field, "", new Vector2(-560, -300), new Vector2(160, 28), 16, Palette.Ink, TextAnchor.MiddleRight);
            PixelText.Mark(guide);

            var dex = new Image[IceChart.SpeciesCount];
            for (int i = 0; i < dex.Length; i++)
            {
                dex[i] = MiniGameSceneBuilder.Art("Dex" + (i + 1), field, art[IceChart.Species[i]], new Vector2(-360 + i * 72, -300), .38f);
                dex[i].color = Color.white;
            }

            Image splash = MiniGameSceneBuilder.Art("Splash", field, art["splash"], Vector2.zero, 1);
            splash.gameObject.SetActive(false);

            var viewObject = new GameObject("IceView", typeof(RectTransform));
            viewObject.transform.SetParent(field, false);
            IceFishingView view = viewObject.AddComponent<IceFishingView>();
            PixelMenuBuilder.Assign(view, "fisher", fisher.rectTransform, "fish", fish.rectTransform, "bucket", bucket.rectTransform, "hammer", hammer.rectTransform,
                "fisherImage", fisher, "fishImage", fish, "splash", splash,
                "fisherIdle", art["fisher"], "fisherWet", art["wet"], "caughtText", caught, "dexCaption", guide);
            PixelMenuBuilder.AssignArray(view, "holes", holes);
            PixelMenuBuilder.AssignArray(view, "fishes", new Object[] {
                art["perch"], art["trout"], art["carp"], art["pike"], art["salmon"], art["char"] });
            PixelMenuBuilder.AssignArray(view, "pile", pile);
            PixelMenuBuilder.AssignArray(view, "dex", dex);
            PixelMenuBuilder.AssignArray(view, "openings", openings);
            PixelMenuBuilder.AssignArray(view, "cracks", cracks);

            WriteWinter(MusicPath, 48);
            IceFishingController controller = MiniGameSceneBuilder.Finish<IceFishingController>(s, MusicPath, 48);
            PixelMenuBuilder.Assign(controller, "view", view,
                "crackSound", Crack("IceCrack"), "splashSound", Splash("IceSplash"),
                "bucketSound", Thud("IceBucket"), "fleeSound", Gurgle("IceGurgle"));
            PixelMenuBuilder.AssignArray(controller, "padSounds", new Object[] {
                Tom("IceTom1", 104), Tom("IceTom2", 146), Tom("IceTom3", 196) });
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("ICE_FISHING_READY");
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/IceFishing.built"));
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

        private static Dictionary<string, Sprite> CreateArt()
        {
            var art = new Dictionary<string, Sprite>();
            art["tile"] = Save(Rounded((int)TileW, (int)TileH, 28, Tile, TileEdge), "tile");
            art["fisher"] = Save(Person(false), "fisher");
            art["wet"] = Save(Person(true), "wet");
            art["hammer"] = PixelArt.Save(ArtDir, "hammer", HammerArt, new Dictionary<char, Color32> {
                { 'k', new Color32(34, 34, 34, 255) }, { 'o', new Color32(214, 160, 104, 255) }, { 'O', new Color32(176, 116, 70, 255) },
                { 's', new Color32(186, 192, 200, 255) }, { 'h', new Color32(244, 246, 248, 255) }
            });
            art["bucket"] = Save(OpenBucket(), "bucket");
            art["splash"] = Save(Splash(), "splash");
            art["crack"] = Save(Cracks(), "crack");
            art["hole"] = Save(Hole(), "hole");
            Color32 navy = new Color32(26, 48, 102, 255);
            art["perch"] = Save(Fish(navy), "perch");
            art["trout"] = Save(Fish(navy), "trout");
            art["carp"] = Save(Fish(navy), "carp");
            art["pike"] = Save(Fish(navy), "pike");
            art["salmon"] = Save(Fish(navy), "salmon");
            art["char"] = Save(Fish(navy), "char");
            return art;
        }

        private static Texture2D Rounded(int width, int height, int radius, Color32 fill, Color32 edge)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float halfW = width * .5f, halfH = height * .5f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float px = Mathf.Abs(x + .5f - halfW) - (halfW - radius);
                    float py = Mathf.Abs(y + .5f - halfH) - (halfH - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f))
                        + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    if (outside > 0f) texture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                    else if (outside > -3f) texture.SetPixel(x, y, edge);
                    else texture.SetPixel(x, y, fill);
                }
            return texture;
        }

        // Flat orange fish, facing right, with the little mark above it from the reference.
        private static Texture2D Fish(Color32 body)
        {
            var texture = Clear(120, 72);
            FillEllipse(texture, 74, 30, 28, 16, body);
            FillTriangle(texture, 46, 30, 24, 16, 24, 44, body);
            FillEllipse(texture, 86, 36, 7, 7, new Color32(255, 255, 255, 255));
            FillEllipse(texture, 88, 36, 3, 3, new Color32(48, 56, 64, 255));
            FillEllipse(texture, 70, 58, 5, 3, new Color32(255, 255, 255, 255));
            return texture;
        }

        // Round face and a large shirt. The shirt bottom is the sprite bottom, which rests on the ice.
        private static Texture2D Person(bool wet)
        {
            var texture = Clear(120, 168);
            Color32 shirt = wet ? new Color32(46, 110, 168, 255) : new Color32(78, 156, 224, 255);
            Color32 skin = new Color32(248, 206, 160, 255);
            FillRounded(texture, 8, 0, 104, 96, 28, shirt);
            FillEllipse(texture, 60, 122, 34, 34, skin);
            FillRounded(texture, 28, 136, 64, 20, 8, new Color32(226, 84, 84, 255));
            if (wet)
            {
                FillRect(texture, 44, 124, 14, 4, new Color32(48, 56, 64, 255));
                FillRect(texture, 64, 124, 14, 4, new Color32(48, 56, 64, 255));
            }
            else
            {
                FillEllipse(texture, 48, 124, 4, 4, new Color32(48, 56, 64, 255));
                FillEllipse(texture, 72, 124, 4, 4, new Color32(48, 56, 64, 255));
                FillEllipse(texture, 60, 110, 8, 4, new Color32(196, 96, 96, 255));
            }
            return texture;
        }

        // Same hammer as the smithy: handle on top, head at the bottom.
        private static readonly string[] HammerArt = {
            "..kkkk..", "..kook..", "..kook..", "..kook..", "..kook..", "..kook..", "..koOk..", "..kook..",
            "kkkkkkkk", "kssssssk", "kshhhssk", "kssssssk", "kshhhssk", "kssssssk", "kkkkkkkk" };

        // A solid tub: dark walls and rim around a filled inside, so the catch piled on it reads as being in the bucket.
        private const int BucketW = 128, BucketH = 110;

        private static Texture2D OpenBucket()
        {
            Texture2D texture = Clear(BucketW, BucketH);
            Color32 wall = new Color32(72, 96, 118, 255);
            Color32 inner = new Color32(148, 170, 188, 255);
            float center = BucketW * .5f;
            for (int y = 0; y < BucketH; y++)
            {
                float half = Mathf.Lerp(46f, 62f, y / (float)(BucketH - 1));
                for (int x = 0; x < BucketW; x++)
                {
                    float edge = Mathf.Abs(x + .5f - center);
                    if (edge > half) continue;
                    bool shell = edge > half - 6f || y < 6 || y >= BucketH - 9;
                    texture.SetPixel(x, y, shell ? wall : inner);
                }
            }
            return texture;
        }

        // Cracks run out of the spot the hammer landed, drawn on top of the ice.
        private static Texture2D Cracks()
        {
            Texture2D texture = Clear((int)TileW, (int)TileH);
            Color32 line = new Color32(142, 174, 196, 255);
            int cx = (int)TileW / 2, cy = (int)TileH / 2;
            int[,] arms = { { -86, 26 }, { -40, -54 }, { 34, 50 }, { 88, -18 }, { 10, -62 }, { -96, -40 } };
            for (int i = 0; i < arms.GetLength(0); i++)
            {
                int ex = cx + arms[i, 0], ey = cy + arms[i, 1];
                int bend = (i % 2 == 0 ? 14 : -14);
                Streak(texture, cx, cy, (cx + ex) / 2 + bend, (cy + ey) / 2 - bend, line);
                Streak(texture, (cx + ex) / 2 + bend, (cy + ey) / 2 - bend, ex, ey, line);
            }
            return texture;
        }

        private static void Streak(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                FillRect(texture, x, y, 3, 3, color);
            }
        }

        // The water that shows through once the ice gives way.
        private static Texture2D Hole()
        {
            Texture2D texture = Clear((int)TileW, (int)TileH);
            int cx = (int)TileW / 2, cy = (int)TileH / 2;
            FillEllipse(texture, cx, cy, 82, 52, new Color32(122, 164, 196, 255));
            FillEllipse(texture, cx, cy, 74, 44, new Color32(58, 104, 146, 255));
            FillEllipse(texture, cx - 26, cy + 18, 16, 7, new Color32(146, 188, 214, 255));
            return texture;
        }

        private static Texture2D Splash()
        {
            var texture = Clear(96, 48);
            FillEllipse(texture, 48, 16, 36, 10, new Color32(255, 255, 255, 230));
            FillEllipse(texture, 24, 28, 8, 8, new Color32(255, 255, 255, 210));
            FillEllipse(texture, 72, 30, 10, 8, new Color32(255, 255, 255, 210));
            return texture;
        }

        // Quiet winter music: a slow bell every two beats over a soft low pad, with a faint wind.
        private static void WriteWinter(string path, int seconds)
        {
            const int rate = 44100;
            int samples = rate * seconds;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var random = new System.Random(31);
            double[] bell = { 1046.5, 1318.5, 1568.0, 1318.5 };
            double[] pad = { 130.81, 110.0, 123.47, 98.0 };
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                double breeze = 0;
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double bar = t % 4, note = t % 1;
                    int step = (int)(t % 8 / 1);
                    double chime = .11 * System.Math.Sin(2 * System.Math.PI * bell[step % 4] * note) * System.Math.Exp(-note * 4.5)
                        + .05 * System.Math.Sin(2 * System.Math.PI * bell[step % 4] * 2 * note) * System.Math.Exp(-note * 7);
                    double low = .09 * System.Math.Sin(2 * System.Math.PI * pad[(int)(t % 16 / 4)] * t) * (.7 + .3 * System.Math.Sin(2 * System.Math.PI * .25 * t));
                    breeze = breeze * .995 + (random.NextDouble() * 2 - 1) * .005;
                    double wind = breeze * .5 * (.6 + .4 * System.Math.Sin(2 * System.Math.PI * .07 * t));
                    double fade = System.Math.Min(1, System.Math.Min(t / 2, (seconds - t) / 2));
                    double mix = (chime + low + wind) * System.Math.Max(0, fade) * (1 - .1 * System.Math.Sin(2 * System.Math.PI * bar));
                    writer.Write((short)(System.Math.Max(-.95, System.Math.Min(.95, mix)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        // One tom per ice: low on the left, middle in the middle, high on the right.
        private static AudioClip Tom(string name, double pitch)
        {
            return Render(name, .4, (t, random) =>
            {
                double drop = pitch * (1 + 1.1 * System.Math.Exp(-t * 26));
                double skin = System.Math.Sin(2 * System.Math.PI * drop * t) * System.Math.Exp(-t * 9.5) * .62;
                double slap = (random.NextDouble() * 2 - 1) * System.Math.Exp(-t * 70) * .2;
                return skin + slap;
            });
        }

        // The dry snap of ice splitting.
        private static AudioClip Crack(string name)
        {
            return Render(name, .25, (t, random) =>
            {
                double noise = random.NextDouble() * 2 - 1;
                return noise * System.Math.Exp(-t * 110) * .7
                    + System.Math.Sin(2 * System.Math.PI * 2600 * t) * System.Math.Exp(-t * 90) * .2
                    + System.Math.Sin(2 * System.Math.PI * 820 * t) * System.Math.Exp(-t * 40) * .12;
            });
        }

        // Something hits the water.
        private static AudioClip Splash(string name)
        {
            return Render(name, .55, (t, random) =>
            {
                double noise = random.NextDouble() * 2 - 1;
                double plop = System.Math.Sin(2 * System.Math.PI * (180 - 110 * System.Math.Exp(-t * 6)) * t) * System.Math.Exp(-t * 7) * .32;
                return plop + noise * System.Math.Exp(-t * 11) * .3 * (1 - System.Math.Exp(-t * 60));
            });
        }

        // The catch landing in the bucket.
        private static AudioClip Thud(string name)
        {
            return Render(name, .3, (t, random) =>
            {
                double body = System.Math.Sin(2 * System.Math.PI * 96 * t) * System.Math.Exp(-t * 16) * .5;
                double knock = (random.NextDouble() * 2 - 1) * System.Math.Exp(-t * 60) * .16;
                return body + knock;
            });
        }

        // A fish getting away: bubbles going down.
        private static AudioClip Gurgle(string name)
        {
            return Render(name, .5, (t, random) =>
            {
                double bubble = 0;
                for (int b = 0; b < 4; b++)
                {
                    double at = t - b * .1;
                    if (at < 0) continue;
                    bubble += System.Math.Sin(2 * System.Math.PI * (620 - 90 * b) * at) * System.Math.Exp(-at * 34) * .22;
                }
                return bubble + (random.NextDouble() * 2 - 1) * System.Math.Exp(-t * 16) * .07;
            });
        }

        private static AudioClip Render(string name, double seconds, System.Func<double, System.Random, double> voice)
        {
            const int rate = 44100;
            int samples = (int)(rate * seconds);
            string path = AudioDir + name + ".wav";
            Directory.CreateDirectory(AudioDir);
            var random = new System.Random(name.Length * 17 + 5);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double value = voice((double)i / rate, random);
                    writer.Write((short)(System.Math.Max(-.95, System.Math.Min(.95, value)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new System.InvalidOperationException("Missing ice sound: " + path);
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

        private static Texture2D Clear(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            texture.SetPixels32(pixels);
            return texture;
        }

        private static void FillEllipse(Texture2D texture, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float nx = (x - cx) / (float)rx, ny = (y - cy) / (float)ry;
                    if (nx * nx + ny * ny <= 1f) Plot(texture, x, y, color);
                }
        }

        private static void FillTriangle(Texture2D texture, int x0, int y0, int x1, int y1, int x2, int y2, Color32 color)
        {
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2)), maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2)), maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (InsideTriangle(x, y, x0, y0, x1, y1, x2, y2)) Plot(texture, x, y, color);
        }

        private static bool InsideTriangle(int x, int y, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float d0 = (x - x1) * (y0 - y1) - (x0 - x1) * (y - y1);
            float d1 = (x - x2) * (y1 - y2) - (x1 - x2) * (y - y2);
            float d2 = (x - x0) * (y2 - y0) - (x2 - x0) * (y - y0);
            return !((d0 < 0 || d1 < 0 || d2 < 0) && (d0 > 0 || d1 > 0 || d2 > 0));
        }

        private static void FillRounded(Texture2D texture, int x, int y, int width, int height, int radius, Color32 color)
        {
            float halfW = width * .5f, halfH = height * .5f;
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    float dx = Mathf.Abs(px + .5f - halfW) - (halfW - radius);
                    float dy = Mathf.Abs(py + .5f - halfH) - (halfH - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                        + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                    if (outside <= 0f) Plot(texture, x + px, y + py, color);
                }
        }

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++) Plot(texture, x + px, y + py, color);
        }

        private static void Plot(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, y, color);
        }

        private static Sprite Save(Texture2D texture, string name)
        {
            texture.Apply();
            Directory.CreateDirectory(ArtDir);
            string path = ArtDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
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
    }
}

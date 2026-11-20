using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Game 9. Flat runners on a course band, the same light grid and drum kit as Tapping Beat.
    public static class RaceBuilder
    {
        public const string ScenePath = "Assets/Scenes/RabbitTurtle.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Race";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/RaceBeat.wav";
        private const string SceneVersion = "2";
        // 80 beats at 100 BPM, five times the 16-beat tune, so the loop is seamless.
        private const int MusicSeconds = 48;
        private static readonly Color Court = new Color32(240, 245, 245, 255);
        private static readonly Color Skin = new Color32(255, 215, 180, 255);
        private static readonly Color HairLeft = new Color32(24, 34, 49, 255);
        private static readonly Color HairRight = new Color32(69, 79, 99, 255);
        private static readonly Color Cream = new Color32(240, 230, 195, 255);
        private static readonly Color Maroon = new Color32(134, 44, 44, 255);
        private static readonly Color Ink = new Color32(25, 35, 50, 255);
        private static readonly Color Gold = new Color32(232, 200, 106, 255);
        private static readonly Color FlagRed = new Color32(214, 69, 69, 255);
        private static readonly Color TurtleGreen = new Color32(90, 168, 86, 255);
        private static readonly Color ShellGreen = new Color32(46, 122, 58, 255);
        private static readonly Color RabbitCream = new Color32(244, 239, 228, 255);
        private static readonly Color EarPink = new Color32(240, 183, 196, 255);
        private static readonly Color MeadowSky = new Color32(201, 230, 195, 255);
        private static readonly Color MeadowTrack = new Color32(228, 196, 138, 255);

        [MenuItem("Rhythm Game/Build Rabbit Turtle")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("RaceCanvas", Court);
            Image sky = PixelMenuBuilder.Box("Sky", s.Root, MeadowSky, s.Solid);
            PixelMenuBuilder.Stretch(sky.rectTransform);
            sky.raycastTarget = false;
            sky.transform.SetSiblingIndex(1);

            Transform field = s.Field;
            Image track = Shape("Track", field, art["stadium"], new Vector2(0f, -36f), new Vector2(1180f, 168f), MeadowTrack);
            Image finish = PixelMenuBuilder.Box("Finish", field, Ink, s.Solid);
            PixelMenuBuilder.Place(finish.rectTransform, new Vector2(448f, -16f), new Vector2(16f, 188f));
            finish.raycastTarget = false;
            Image banner = PixelMenuBuilder.Box("Banner", finish.transform, FlagRed, s.Solid);
            PixelMenuBuilder.Place(banner.rectTransform, new Vector2(22f, 74f), new Vector2(28f, 18f));
            banner.raycastTarget = false;

            Image stand = Shape("Stand", field, art["stadium"], new Vector2(0f, 78f), new Vector2(1220f, 70f), new Color(1f, 1f, 1f, 0.45f));
            stand.raycastTarget = false;
            var flags = new Image[8];
            for (int i = 0; i < flags.Length; i++) flags[i] = Crowd(field, art, s.Solid, i);

            RectTransform turtle = Turtle(field, art);
            RectTransform rabbit = Rabbit(field, art);
            Image coursePill = Shape("CoursePill", field, art["stadium"], new Vector2(-470f, -150f), new Vector2(200f, 40f), Cream);
            Text course = PixelMenuBuilder.Label("Course", coursePill.transform, "", Vector2.zero, new Vector2(184f, 36f), 22, Ink, TextAnchor.MiddleCenter);
            PixelText.Mark(course);
            Image stepsPill = Shape("StepsPill", field, art["stadium"], new Vector2(90f, -150f), new Vector2(168f, 40f), Cream);
            Text steps = PixelMenuBuilder.Label("Steps", stepsPill.transform, "", Vector2.zero, new Vector2(150f, 36f), 22, Ink, TextAnchor.MiddleCenter);
            PixelText.Mark(steps);
            Text sprint = PixelMenuBuilder.Label("Sprint", field, "", new Vector2(0f, 128f), new Vector2(280f, 40f), 26, new Color32(204, 112, 40, 255), TextAnchor.MiddleCenter);
            PixelText.Mark(sprint);
            sprint.gameObject.SetActive(false);

            var drums = new Image[8];
            drums[RaceChart.LeftPad] = Drum(field, art, "DrumS", new Vector2(-200f, -268f), "S", false);
            drums[RaceChart.RightPad] = Drum(field, art, "DrumD", new Vector2(-20f, -268f), "D", false);
            drums[RaceChart.SprintPad] = Drum(field, art, "DrumZ", new Vector2(160f, -300f), "Z", true);
            drums[RaceChart.SprintPad].GetComponent<CanvasGroup>().alpha = 0.38f;
            Shape("Drummer", field, art["circle"], new Vector2(-110f, -176f), new Vector2(72f, 72f), Skin);
            Shape("DrummerHair", field, art["circle"], new Vector2(-110f, -146f), new Vector2(58f, 22f), HairLeft);
            RectTransform mallet = Shape("Mallet", field, art["stadium"], new Vector2(-160f, -210f), new Vector2(16f, 78f), Ink).rectTransform;
            mallet.pivot = new Vector2(0.5f, 1f);
            mallet.localEulerAngles = new Vector3(0f, 0f, -18f);

            RaceView view = field.gameObject.AddComponent<RaceView>();
            PixelMenuBuilder.Assign(view, "sky", sky, "track", track, "turtle", turtle, "rabbit", rabbit, "mallet", mallet,
                "rabbitOpen", rabbit.Find("Eyes").GetComponent<Image>(),
                "rabbitShut", rabbit.Find("Shut").GetComponent<Image>(),
                "rabbitMouth", rabbit.Find("Mouth").GetComponent<Image>(),
                "ribbon", turtle.Find("Ribbon").GetComponent<Image>(),
                "flower", turtle.Find("Flower").GetComponent<Image>(),
                "star", turtle.Find("Star").GetComponent<Image>(),
                "bubble", rabbit.Find("Bubble/Huh").GetComponent<Text>(),
                "bubbleBack", rabbit.Find("Bubble").GetComponent<Image>(),
                "steps", steps, "course", course, "sprint", sprint,
                "zzz", rabbit.Find("Zzz").GetComponent<Text>());
            PixelMenuBuilder.AssignArray(view, "flags", flags);
            PixelMenuBuilder.AssignArray(view, "drums", drums);

            WriteMusic(MusicPath, MusicSeconds);
            WriteFoot(PathOf("RaceFoot"));
            WriteTom(PathOf("RaceTom"), 168);
            WriteSnare(PathOf("RaceSnare"));
            WriteSnore(PathOf("RaceSnore"));
            WriteWake(PathOf("RaceWake"));
            WriteWhistle(PathOf("RaceWhistle"));
            WriteTrumpet(PathOf("RaceTrumpet"));
            WriteNyah(PathOf("RaceNyah"));

            RaceController controller = MiniGameSceneBuilder.Finish<RaceController>(s, MusicPath, MusicSeconds);
            Transform scan = s.Root.Find("Scanlines");
            if (scan != null) scan.gameObject.SetActive(false);
            AudioSource snore = new GameObject("Snore", typeof(AudioSource)).GetComponent<AudioSource>();
            snore.transform.SetParent(s.Root, false);
            snore.playOnAwake = false;
            snore.loop = true;
            snore.spatialBlend = 0f;
            snore.volume = 0.45f;
            snore.clip = Load(PathOf("RaceSnore"));
            PixelMenuBuilder.Assign(controller, "view", view, "snore", snore,
                "foot", Load(PathOf("RaceFoot")), "tom", Load(PathOf("RaceTom")), "snare", Load(PathOf("RaceSnare")),
                "wake", Load(PathOf("RaceWake")), "whistle", Load(PathOf("RaceWhistle")),
                "trumpet", Load(PathOf("RaceTrumpet")), "nyah", Load(PathOf("RaceNyah")));
            MiniGameSceneBuilder.Save(s, ScenePath);
        }

        private static string PathOf(string name)
        {
            return "Assets/RhythmGame/Resources/RhythmGame/Audio/" + name + ".wav";
        }

        // One fan holding a flag on a stick. The view swings the stick around the hand to call the beat.
        // The left four call S and the right four call D. All eight call Z.
        private static Image Crowd(Transform parent, Dictionary<string, Sprite> art, Sprite solid, int index)
        {
            float x = -490f + index * 140f;
            RectTransform fan = PixelMenuBuilder.Rect("Fan" + index, parent, new Vector2(x, 86f), Vector2.zero);
            Color shirt = index % 3 == 0 ? Gold : index % 3 == 1 ? new Color32(106, 143, 216, 255) : FlagRed;
            Shape("Body", fan, art["stadium"], new Vector2(0f, -28f), new Vector2(42f, 30f), shirt);
            Shape("Head", fan, art["circle"], new Vector2(0f, 0f), new Vector2(32f, 32f), Skin);
            Shape("Hair", fan, art["circle"], new Vector2(0f, 12f), new Vector2(28f, 12f), index % 2 == 0 ? HairLeft : HairRight);
            RectTransform stick = PixelMenuBuilder.Rect("Stick", fan, new Vector2(18f, -18f), Vector2.zero);
            Shape("Pole", stick, solid, new Vector2(0f, 32f), new Vector2(5f, 64f), Ink);
            Image cloth = Shape("Flag", stick, solid, new Vector2(2f, 64f), new Vector2(50f, 32f), index < 4 ? FlagRed : Gold);
            cloth.rectTransform.pivot = new Vector2(0f, 1f);
            return cloth;
        }

        private static RectTransform Turtle(Transform parent, Dictionary<string, Sprite> art)
        {
            RectTransform root = PixelMenuBuilder.Rect("Turtle", parent, new Vector2(-480f, -58f), Vector2.zero);
            Shape("Shadow", root, art["stadium"], new Vector2(10f, -36f), new Vector2(120f, 18f), new Color(0f, 0f, 0f, 0.18f));
            Shape("Shell", root, art["circle"], Vector2.zero, new Vector2(92f, 80f), ShellGreen);
            Image ribbon = Shape("Ribbon", root, art["stadium"], new Vector2(0f, 6f), new Vector2(78f, 14f), FlagRed);
            Image flower = Shape("Flower", root, art["circle"], new Vector2(0f, 26f), new Vector2(22f, 22f), EarPink);
            Shape("FlowerHeart", flower.transform, art["circle"], Vector2.zero, new Vector2(10f, 10f), Gold);
            Image star = Shape("Star", root, art["star"], Vector2.zero, new Vector2(28f, 28f), Gold);
            ribbon.gameObject.SetActive(false);
            flower.gameObject.SetActive(false);
            star.gameObject.SetActive(false);
            Shape("Head", root, art["circle"], new Vector2(52f, 8f), new Vector2(40f, 40f), TurtleGreen);
            Shape("Eye", root, art["circle"], new Vector2(62f, 14f), new Vector2(8f, 8f), Ink);
            return root;
        }

        private static RectTransform Rabbit(Transform parent, Dictionary<string, Sprite> art)
        {
            RectTransform root = PixelMenuBuilder.Rect("Rabbit", parent, new Vector2(-480f, 8f), Vector2.zero);
            Shape("Shadow", root, art["stadium"], new Vector2(8f, -28f), new Vector2(110f, 16f), new Color(0f, 0f, 0f, 0.18f));
            Shape("Body", root, art["stadium"], Vector2.zero, new Vector2(96f, 48f), RabbitCream);
            Shape("Tail", root, art["circle"], new Vector2(-48f, 8f), new Vector2(18f, 18f), Cream);
            Image earL = Shape("EarL", root, art["stadium"], new Vector2(28f, 40f), new Vector2(14f, 36f), RabbitCream);
            Image earR = Shape("EarR", root, art["stadium"], new Vector2(48f, 44f), new Vector2(14f, 36f), RabbitCream);
            earL.rectTransform.localEulerAngles = new Vector3(0f, 0f, 16f);
            earR.rectTransform.localEulerAngles = new Vector3(0f, 0f, -12f);
            Shape("EarInL", earL.transform, art["stadium"], Vector2.zero, new Vector2(6f, 22f), EarPink);
            Shape("EarInR", earR.transform, art["stadium"], Vector2.zero, new Vector2(6f, 22f), EarPink);
            Shape("Head", root, art["circle"], new Vector2(46f, 14f), new Vector2(40f, 40f), RabbitCream);
            Shape("Eyes", root, art["circle"], new Vector2(56f, 20f), new Vector2(8f, 8f), Ink);
            Image shut = Shape("Shut", root, art["stadium"], new Vector2(56f, 20f), new Vector2(14f, 5f), Ink);
            Image mouth = Shape("Mouth", root, art["circle"], new Vector2(66f, 6f), new Vector2(12f, 8f), EarPink);
            shut.gameObject.SetActive(false);
            mouth.gameObject.SetActive(false);
            Image bubble = Shape("Bubble", root, art["stadium"], new Vector2(78f, 58f), new Vector2(120f, 40f), Cream);
            PixelText.Mark(PixelMenuBuilder.Label("Huh", bubble.transform, "", Vector2.zero, new Vector2(110f, 36f), 20, Ink, TextAnchor.MiddleCenter));
            bubble.gameObject.SetActive(false);
            Text zzz = PixelMenuBuilder.Label("Zzz", root, "Zzz", new Vector2(78f, 52f), new Vector2(64f, 28f), 18, HairRight, TextAnchor.MiddleCenter);
            zzz.gameObject.SetActive(false);
            return root;
        }

        private static Image Drum(Transform parent, Dictionary<string, Sprite> art, string name, Vector2 position, string key, bool snare)
        {
            Image hit = Shape(name, parent, art["circle"], position, new Vector2(96f, 64f), new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            hit.gameObject.AddComponent<CanvasGroup>();
            Shape("Body", hit.transform, art["stadium"], new Vector2(0f, -8f), snare ? new Vector2(84f, 28f) : new Vector2(84f, 44f), Maroon);
            Shape("Top", hit.transform, art["circle"], new Vector2(0f, snare ? 2f : 10f), new Vector2(90f, snare ? 26f : 32f), snare ? Gold : Cream);
            PixelText.Mark(PixelMenuBuilder.Label("Key", hit.transform, key, new Vector2(0f, snare ? 2f : 10f), new Vector2(48f, 24f), 16, Ink, TextAnchor.MiddleCenter));
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
            art["star"] = SaveSmooth("star", Star());
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

        private static Texture2D Star()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[n * n]);
            Vector2 c = new Vector2(64f, 64f);
            var outer = new Vector2[5];
            var inner = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = (90f - i * 72f) * Mathf.Deg2Rad;
                outer[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 58f;
                float b = a - 36f * Mathf.Deg2Rad;
                inner[i] = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 24f;
            }
            for (int i = 0; i < 5; i++)
            {
                FillTri(tex, outer[i], inner[i], inner[(i + 4) % 5]);
                FillTri(tex, c, inner[i], inner[(i + 4) % 5]);
            }
            tex.Apply();
            return tex;
        }

        private static void FillTri(Texture2D tex, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Edge(b, c, a);
            if (Mathf.Abs(area) < 0.001f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(tex.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(tex.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            var ink = new Color(1f, 1f, 1f, 1f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(b, c, p) / area;
                    float w1 = Edge(c, a, p) / area;
                    float w2 = Edge(a, b, p) / area;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f) tex.SetPixel(x, y, ink);
                }
        }

        private static float Edge(Vector2 u, Vector2 v, Vector2 p)
        {
            return (p.x - u.x) * (v.y - u.y) - (p.y - u.y) * (v.x - u.x);
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
            // Chinese race tune on the D gong pentatonic (D E F# A B): a reedy suona-like lead in a
            // galloping "horse race" figure, a big tanggu drum on the beat, a bangzi woodblock gallop,
            // bo cymbals on the backbeat and a gong at the top of every two bars.
            const int rate = 44100;
            const double beat = 0.6, eighth = beat / 2, sixteenth = beat / 4;
            int samples = rate * seconds;
            int[] tune = {
                7, 7, 9, 7, 4, 2, 0, 2,  4, 7, 9, 12, 9, 7, 4, 7,
                12, 12, 14, 12, 9, 7, 9, 12,  9, 7, 4, 2, 4, 2, 0, -1 };
            var random = new System.Random(9);
            double phase = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                WriteWavHeader(writer, samples, rate);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    int b = (int)(t / beat);
                    double intoBeat = t - b * beat;
                    int e = (int)(t / eighth);
                    double intoEighth = t - e * eighth;
                    int s = (int)(t / sixteenth);
                    double intoSixteenth = t - s * sixteenth;
                    double phrase = t % (beat * 8);
                    double noise = random.NextDouble() * 2 - 1;

                    int degree = tune[e % tune.Length];
                    double lead = 0;
                    if (degree >= 0)
                    {
                        double freq = 293.66 * Math.Pow(2, degree / 12.0);
                        double vibrato = 1 + 0.007 * Math.Sin(2 * Math.PI * 5.5 * t);
                        phase += freq * vibrato / rate;
                        double p = 2 * Math.PI * phase;
                        double reed = Math.Sin(p) * 0.5 + Math.Sin(2 * p) * 0.32 + Math.Sin(3 * p) * 0.22 + Math.Sin(4 * p) * 0.12;
                        double attack = Math.Min(1, intoEighth / 0.012);
                        double release = Math.Min(1, (eighth - intoEighth) / 0.03);
                        lead = reed * attack * release * (0.65 + 0.35 * Math.Exp(-intoEighth * 6)) * 0.17;
                    }

                    double drumPitch = 62 + 40 * Math.Exp(-intoBeat * 30);
                    double tanggu = Math.Sin(2 * Math.PI * drumPitch * intoBeat) * Math.Exp(-intoBeat * 11) * 0.36;
                    int inBeat = s % 4;
                    double bangzi = inBeat == 2 || inBeat == 3
                        ? Math.Sin(2 * Math.PI * 1750 * intoSixteenth) * Math.Exp(-intoSixteenth * 70) * 0.12 : 0;
                    double bo = b % 2 == 1
                        ? (noise * 0.7 + Math.Sin(2 * Math.PI * 3100 * intoBeat) * 0.3) * Math.Exp(-intoBeat * 9) * 0.13 : 0;
                    double gongPitch = 1 - 0.04 * Math.Min(1, phrase / 1.5);
                    double gong = (Math.Sin(2 * Math.PI * 110 * gongPitch * phrase) * 0.55
                        + Math.Sin(2 * Math.PI * 163 * gongPitch * phrase) * 0.3
                        + Math.Sin(2 * Math.PI * 231 * phrase) * 0.15)
                        * Math.Min(1, phrase / 0.01) * Math.Exp(-phrase * 1.1) * 0.2;

                    double tone = lead + tanggu + bangzi + bo + gong;
                    writer.Write((short)(Math.Max(-0.9, Math.Min(0.9, tone)) * short.MaxValue));
                }
            }
        }

        private static void WriteFoot(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.22);
            var random = new System.Random(3);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double tick = Math.Exp(-Math.Abs(t) * 40) + Math.Exp(-Math.Abs(t - 0.08) * 40);
                return (random.NextDouble() * 2 - 1) * tick * 0.55;
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

        private static void WriteSnare(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.18);
            var random = new System.Random(5);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                return ((random.NextDouble() * 2 - 1) * 0.7 + Math.Sin(2 * Math.PI * 180 * t) * 0.3) * Math.Exp(-t * 18);
            });
        }

        private static void WriteSnore(string path)
        {
            const int rate = 44100;
            int samples = rate * 2;
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double wobble = 0.35 + 0.65 * Math.Sin(2 * Math.PI * t / 2);
                double edge = Math.Min(1, t / 0.05) * Math.Min(1, (2 - t) / 0.05);
                return Math.Sin(2 * Math.PI * 92 * t) * wobble * edge * 0.35;
            });
        }

        private static void WriteWake(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.22);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                return Math.Sin(2 * Math.PI * (220 + 500 * t) * t) * Math.Exp(-t * 8) * 0.45;
            });
        }

        private static void WriteWhistle(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.42);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                double a = Math.Exp(-Math.Abs(t - 0.06) * 22);
                double b = Math.Exp(-Math.Abs(t - 0.26) * 22);
                return Math.Sin(2 * Math.PI * 1480 * t) * (a + b) * 0.28;
            });
        }

        private static void WriteTrumpet(string path)
        {
            const int rate = 44100;
            int samples = rate;
            double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                int step = Math.Min(notes.Length - 1, (int)(t / 0.22));
                double into = t - step * 0.22;
                return Math.Sin(2 * Math.PI * notes[step] * t) * Math.Exp(-into * 3.5) * 0.4;
            });
        }

        private static void WriteNyah(string path)
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.7);
            WriteTone(path, samples, delegate (int i)
            {
                double t = (double)i / rate;
                int step = t < 0.32 ? 0 : 1;
                double into = t - step * 0.32;
                double pitch = 520 - into * 700;
                double gate = into < 0.28 ? 1 : 0;
                return Math.Sin(2 * Math.PI * pitch * t) * gate * Math.Exp(-into * 4) * 0.4;
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RabbitTurtle.built"));
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

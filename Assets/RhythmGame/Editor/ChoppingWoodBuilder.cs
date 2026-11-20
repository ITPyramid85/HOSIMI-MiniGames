using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the wood-chopping scene on the shared mini-game frame.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.ChoppingWoodBuilder.Build
    public static class ChoppingWoodBuilder
    {
        public const string ScenePath = "Assets/Scenes/ChoppingWood.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Wood";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/WoodBeat.wav";
        private const string SceneVersion = "27";
        private static readonly Color BeltFront = new Color32(168, 92, 48, 255);
        private static readonly Color BeltDeck = new Color32(244, 184, 48, 255);
        private static readonly Color Cleat = new Color32(62, 36, 28, 255);
        private static readonly Color Rail = new Color32(48, 32, 28, 255);
        private static readonly Color Roller = new Color32(88, 96, 108, 255);
        private static readonly Color Hub = new Color32(214, 220, 228, 255);

        [MenuItem("Rhythm Game/Build Chopping Wood")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("WoodCanvas", Palette.Background);
            s.Grid.color = Color.white;
            Transform field = s.Field;

            Image touch = PixelMenuBuilder.Box("TouchArea", field, Color.clear, null);
            PixelMenuBuilder.Stretch(touch.rectTransform);
            touch.raycastTarget = true;

            const float beltHeight = 64f;
            RectTransform belt = PixelMenuBuilder.Rect("Belt", field, new Vector2(0, -56), new Vector2(960, beltHeight));
            Image beltBody = PixelMenuBuilder.Box("BeltBody", belt, BeltFront, s.Solid);
            PixelMenuBuilder.Stretch(beltBody.rectTransform);
            beltBody.raycastTarget = false;
            Image deck = PixelMenuBuilder.Box("BeltDeck", belt, BeltDeck, s.Solid);
            PixelMenuBuilder.Place(deck.rectTransform, new Vector2(0, beltHeight * .5f - 19f), new Vector2(960, 26));
            deck.raycastTarget = false;
            Image rail = PixelMenuBuilder.Box("BeltRail", belt, Rail, s.Solid);
            PixelMenuBuilder.Place(rail.rectTransform, new Vector2(0, beltHeight * .5f - 3f), new Vector2(960, 6));
            rail.raycastTarget = false;
            var stripes = new Image[10];
            for (int i = 0; i < stripes.Length; i++)
            {
                stripes[i] = PixelMenuBuilder.Box("Cleat" + i, belt, Cleat, s.Solid);
                PixelMenuBuilder.Place(stripes[i].rectTransform, new Vector2(-432 + i * 96, beltHeight * .5f - 19f), new Vector2(18, 26));
                stripes[i].raycastTarget = false;
            }
            var rollers = new RectTransform[2];
            for (int i = 0; i < rollers.Length; i++)
            {
                Image roller = PixelMenuBuilder.Box(i == 0 ? "RollerL" : "RollerR", belt, Roller, s.Solid);
                PixelMenuBuilder.Place(roller.rectTransform, new Vector2(i == 0 ? -468 : 468, 0), new Vector2(56, 56));
                roller.raycastTarget = false;
                Image hub = PixelMenuBuilder.Box("Hub", roller.transform, Hub, s.Solid);
                PixelMenuBuilder.Place(hub.rectTransform, Vector2.zero, new Vector2(22, 22));
                hub.raycastTarget = false;
                rollers[i] = roller.rectTransform;
            }

            const float boardX = 280f, boardLift = 8f;
            float boardHeight = art["board"].rect.height * 6f;
            Image board = MiniGameSceneBuilder.Art("Board", belt, art["board"], new Vector2(boardX, beltHeight * .5f - boardHeight * .5f + boardLift), 6);
            float logHeight = art["log"].rect.height * 6f;
            Vector2 onBelt = new Vector2(boardX, beltHeight * .5f + logHeight * .5f + boardLift);
            Image piece = MiniGameSceneBuilder.Art("Piece", belt, art["log"], onBelt, 6);
            Image left = MiniGameSceneBuilder.Art("HalfL", belt, art["log"], onBelt, 6);
            Image right = MiniGameSceneBuilder.Art("HalfR", belt, art["log"], onBelt, 6);
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);

            Image axe = MiniGameSceneBuilder.Art("Axe", field, art["axe"], Vector2.zero, 6);
            float logWidth = art["log"].rect.width * 6f;
            float beltTop = belt.anchoredPosition.y + beltHeight * .5f;
            float edge = logHeight;
            float reach = edge * art["axe"].rect.width / art["axe"].rect.height;
            axe.rectTransform.pivot = new Vector2(1f, .5f);
            axe.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            PixelMenuBuilder.Place(axe.rectTransform, new Vector2(belt.anchoredPosition.x + boardX - logWidth * .5f + reach, beltTop + edge * .5f + 48f + boardLift), new Vector2(reach, edge));
            axe.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            float personScale = 8f;
            float personHeight = art["person"].rect.height * personScale;
            Image person = MiniGameSceneBuilder.Art("Person", field, art["person"], new Vector2(528f, beltTop + personHeight * .5f), personScale);

            Text bubble = PixelMenuBuilder.Label("Bubble", field, "", new Vector2(0, 168), new Vector2(360, 48), 28, Palette.Ink, TextAnchor.MiddleCenter);
            PixelText.Mark(PixelMenuBuilder.Label("Key", field, "8 / C", new Vector2(0, -250), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter));
            PixelText.Mark(bubble);

            WoodView view = touch.gameObject.AddComponent<WoodView>();
            PixelMenuBuilder.Assign(view, "piece", piece.rectTransform, "axe", axe.rectTransform, "board", board.rectTransform,
                "leftHalf", left.rectTransform, "rightHalf", right.rectTransform,
                "pieceImage", piece, "axeImage", axe, "leftImage", left, "rightImage", right,
                "log", art["log"], "thickLog", art["thick"], "cracked", art["cracked"], "half", art["half"],
                "melon", art["melon"], "stone", art["stone"],
                "axePlain", art["axe"], "axeGold", art["gold"], "bubble", bubble,
                "personImage", person, "personIdle", art["person"], "personHappy", art["personHappy"], "personFrown", art["personFrown"]);
            PixelMenuBuilder.AssignArray(view, "stripes", stripes);
            PixelMenuBuilder.AssignArray(view, "rollers", rollers);

            WoodController controller = MiniGameSceneBuilder.Finish<WoodController>(s, MusicPath, 64);
            PixelMenuBuilder.Assign(controller, "view", view);
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("CHOPPING_WOOD_READY");
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
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ChoppingWood.built"));
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

        private static readonly string[] LogArt = {
            ".kkkkkk.",
            "kddddddk",
            "kdwwwwdk",
            "kdwhhwdk",
            "kdwwwwdk",
            "kdwwwwdk",
            "kdwhhwdk",
            "kdwwwwdk",
            "kdwwwwdk",
            "kdwhhwdk",
            "kdwwwwdk",
            "kdwwwwdk",
            "kdwhhwdk",
            "kdwwwwdk",
            "kddddddk",
            ".kkkkkk." };

        private static readonly string[] ThickArt = {
            ".kkkkkkkkkk.",
            "kddddddddddk",
            "kdwwwwwwwwdk",
            "kdwwhhhhwwdk",
            "kdwwwwwwwwdk",
            "kdwwwwwwwwdk",
            "kdwwhhhhwwdk",
            "kdwwwwwwwwdk",
            "kdwwwwwwwwdk",
            "kdwwhhhhwwdk",
            "kdwwwwwwwwdk",
            "kdwwwwwwwwdk",
            "kdwwhhhhwwdk",
            "kdwwwwwwwwdk",
            "kdwwwwwwwwdk",
            "kdwwhhhhwwdk",
            "kddddddddddk",
            ".kkkkkkkkkk." };

        private static readonly string[] CrackedArt = {
            ".kkkkkkkkkk.",
            "kddddkkddddk",
            "kdwwwkkwwwdk",
            "kdwhhkkhhwdk",
            "kdwwwkkwwwdk",
            "kdwwwkkwwwdk",
            "kdwhhkkhhwdk",
            "kdwwwkkwwwdk",
            "kdwwwkkwwwdk",
            "kdwhhkkhhwdk",
            "kdwwwkkwwwdk",
            "kdwwwkkwwwdk",
            "kdwhhkkhhwdk",
            "kdwwwkkwwwdk",
            "kdwwwkkwwwdk",
            "kdwhhkkhhwdk",
            "kddddkkddddk",
            ".kkkkkkkkkk." };

        private static readonly string[] HalfArt = {
            ".kkkkk",
            "kddddk",
            "kdwwwk",
            "kdwhhk",
            "kdwwwk",
            "kdwwwk",
            "kdwhhk",
            "kdwwwk",
            "kdwwwk",
            "kdwhhk",
            "kdwwwk",
            "kdwwwk",
            "kdwhhk",
            "kdwwwk",
            "kddddk",
            ".kkkkk" };

        private static readonly string[] MelonArt = {
            "....kkkk....",
            "..kggggggk..",
            ".kggrrrrggk.",
            "kgrrrrrrrrrk",
            "kgrrrhhrrrrk",
            "kgrrrrrrrrrk",
            "kgrrrhhrrrrk",
            "kgrrrrrrrrrk",
            ".krrrrrrrrk.",
            "..kggggggk..",
            "....kkkk...." };

        private static readonly string[] BoardArt = {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkk",
            "kddddddddddddddddddddddddddk",
            "kdwwwwwwwwwwwkkwwwwwwwwwwwdk",
            "kdwwwwwwwwwwwkkwwwwwwwwwwwdk",
            "kddddddddddddddddddddddddddk",
            ".kkkkkkkkkkkkkkkkkkkkkkkkkk." };

        private static readonly string[] PersonArt = {
            "......kkkkkkkkkkkk......",
            "....krrrrrrrrrrrrrrk....",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "kkkkkkkkkkkkkkkkkkkkkkkk",
            "kkddYYYYYYYYYYYYYYYYddkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kkYYYYkhhhYYYYkhhhYYYYkk",
            "kkYYYYbbYYYYYYYYbbYYYYkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kkYYYYYYYmmmmmmYYYYYYYkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            ".kkYYYYYYYYYYYYYYYYYYkk.",
            "..kkYYYYYYYYYYYYYYYYkk..",
            "....kkYYYYYYYYYYYYkk....",
            ".kkppppppppppppppppppkk.",
            ".kkppooooooooooooooppkk.",
            "kYYYkppooooooooooppkYYYk",
            ".kkppooooooooooooooppkk.",
            ".kkppppppppppppppppppkk.",
            "...kddddddddddddddddk...",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kddddddk........kddddddk",
            "kddddddk........kddddddk",
            ".kkkkkkk........kkkkkkk." };

        private static readonly string[] PersonHappyArt = {
            "......kkkkkkkkkkkk......",
            "....krrrrrrrrrrrrrrk....",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "kkkkkkkkkkkkkkkkkkkkkkkk",
            "kkddYYYYYYYYYYYYYYYYddkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kkYYYYYkkkYYYYkkkYYYYYkk",
            "kkYYYbbYYYYYYYYYYbbYYYkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kkYYYYmmhwwwwwwhmmYYYYkk",
            "kkYYYYYYYmmmmmmYYYYYYYkk",
            ".kkYYYYYYYYYYYYYYYYYYkk.",
            "..kkYYYYYYYYYYYYYYYYkk..",
            "kYYYkkYYYYYYYYYYYYkkYYYk",
            ".kkppppppppppppppppppkk.",
            ".kkppooooooooooooooppkk.",
            ".kkppooooooooooooooppkk.",
            ".kkppooooooooooooooppkk.",
            ".kkppppppppppppppppppkk.",
            "...kddddddddddddddddk...",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kddddddk........kddddddk",
            "kddddddk........kddddddk",
            ".kkkkkkk........kkkkkkk." };

        private static readonly string[] PersonFrownArt = {
            "......kkkkkkkkkkkk......",
            "....krrrrrrrrrrrrrrk....",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "..krrrrrrrrrrrrrrrrrrk..",
            "kkkkkkkkkkkkkkkkkkkkkkkk",
            "kkddYYYYYYYYYYYYYYYYddkk",
            "kkYYYYYYYkkkkkkYYYYYYYkk",
            "kkYYYYYkhkYYYYkhkYYYYYkk",
            "kkYYYYYbbYYYYYYbbYYYYpkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            "kkYYYYYYYmmmmmmYYYYYYYkk",
            "kkYYmmYYYYYYYYYYYYmmYYkk",
            "kYYYYYYYYYYYYYYYYYYYYYYk",
            ".kkYYYYYYYYYYYYYYYYYYkk.",
            "..kkYYYYYYYYYYYYYYYYkk..",
            "....kkYYYYYYYYYYYYkk....",
            ".kkppppppppppppppppppkk.",
            ".kkppooooooooooooooppkk.",
            "kYYYkppooooooooooppkYYYk",
            ".kkppooooooooooooooppkk.",
            ".kkppppppppppppppppppkk.",
            "...kddddddddddddddddk...",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kkdddddk........kdddddkk",
            "kddddddk........kddddddk",
            "kddddddk........kddddddk",
            ".kkkkkkk........kkkkkkk." };

        private static readonly string[] StoneArt = {
            ".kkkkkkkk.",
            "kssssssssk",
            "kshhhhhhsk",
            "kssssssssk",
            "kshhhhhhsk",
            "kssssssssk",
            ".kkkkkkkk." };

        // The axe pictures are the supplied sprite. Build must not redraw them.
        private static Dictionary<string, Sprite> CreateArt()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(34, 34, 34, 255) }, { 'd', new Color32(92, 52, 32, 255) },
                { 'w', new Color32(236, 214, 170, 255) }, { 'e', new Color32(160, 96, 16, 255) },
                { 'g', new Color32(72, 168, 78, 255) }, { 'r', new Color32(214, 48, 64, 255) },
                { 'h', new Color32(255, 244, 214, 255) }, { 's', new Color32(148, 152, 160, 255) },
                { 'Y', new Color32(255, 206, 166, 255) }, { 'p', new Color32(86, 156, 214, 255) },
                { 'm', new Color32(214, 64, 86, 255) }, { 'o', new Color32(242, 126, 42, 255) },
                { 'b', new Color32(255, 142, 162, 255) } };
            return new Dictionary<string, Sprite> {
                { "log", PixelArt.Save(ArtDir, "log", LogArt, palette) },
                { "thick", PixelArt.Save(ArtDir, "thick", ThickArt, palette) },
                { "cracked", PixelArt.Save(ArtDir, "cracked", CrackedArt, palette) },
                { "half", PixelArt.Save(ArtDir, "half", HalfArt, palette) },
                { "melon", PixelArt.Save(ArtDir, "melon", MelonArt, palette) },
                { "stone", PixelArt.Save(ArtDir, "stone", StoneArt, palette) },
                { "board", PixelArt.Save(ArtDir, "board", BoardArt, palette) },
                { "person", PixelArt.Save(ArtDir, "person", PersonArt, palette) },
                { "personHappy", PixelArt.Save(ArtDir, "person_happy", PersonHappyArt, palette) },
                { "personFrown", PixelArt.Save(ArtDir, "person_frown", PersonFrownArt, palette) },
                { "axe", ImportSprite(ArtDir + "/axe.png") },
                { "gold", ImportSprite(ArtDir + "/gold.png") } };
        }

        private static Sprite ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException("Missing sprite: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

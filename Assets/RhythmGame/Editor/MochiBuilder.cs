using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the rice-cake pounding scene: the shared mini-game frame plus a kitchen with Grandma,
    // the mortar, the rice cake and the mallet, all as code-drawn pixel art (placeholders until the
    // artist's sprites replace the PNGs). Fully generated, so rebuilding is always safe. Headless:
    // unity run <project> -- -executeMethod RhythmGame.Editor.MochiBuilder.Build
    public static class MochiBuilder
    {
        public const string ScenePath = "Assets/Scenes/Mochi.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Mochi";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/MochiBeat.wav";
        private const float Px = PixelTween.Pixel;
        private const float Scale = 12;   // screen units per art pixel (3 UI pixels of 4 units)
        private static readonly Color Kitchen = new Color32(255, 236, 205, 255);
        private static readonly Color Floor = new Color32(222, 170, 118, 255);
        private static readonly Color Beam = new Color32(176, 120, 78, 255);

        [MenuItem("Rhythm Game/Build Rice-Cake Pounding Prototype")]
        public static void Build()
        {
            Dictionary<string, Sprite> art = CreateArt();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("MochiCanvas", Kitchen);
            Transform field = s.Field;

            // Kitchen: ceiling beam (the cake sticks here) and a wooden floor.
            Image beam = PixelMenuBuilder.Box("Beam", field, Beam, s.Solid);
            PixelMenuBuilder.Place(beam.rectTransform, new Vector2(0, 312), new Vector2(1280, 96));
            Image floor = PixelMenuBuilder.Box("Floor", field, Floor, s.Solid);
            PixelMenuBuilder.Place(floor.rectTransform, new Vector2(0, -296), new Vector2(1280, 128));
            beam.raycastTarget = floor.raycastTarget = false;

            // The whole field is the touch target: a tap anywhere is pad 8.
            Image touch = PixelMenuBuilder.Box("TouchArea", field, Color.clear, null);
            PixelMenuBuilder.Stretch(touch.rectTransform);
            touch.raycastTarget = true;

            Image grandma = MiniGameSceneBuilder.Art("Grandma", field, art["grandma"], new Vector2(-144, -136), Scale);
            Image face = MiniGameSceneBuilder.Art("Face", grandma.transform, art["face_normal"], Vector2.zero, Scale);
            Image flash = PixelMenuBuilder.Box("Flash", grandma.transform, Color.clear, null);
            PixelMenuBuilder.Stretch(flash.rectTransform);
            flash.raycastTarget = false;
            Image arm = MiniGameSceneBuilder.Art("Arm", field, art["arm"], Vector2.zero, Scale);
            Vector2 armRest = new Vector2(-150, -150), armReach = new Vector2(10, -112);
            arm.rectTransform.anchoredPosition = armRest;

            Image mortar = MiniGameSceneBuilder.Art("Mortar", field, art["mortar"], new Vector2(80, -200), Scale);
            Image mochi = MiniGameSceneBuilder.Art("Mochi", field, art["mochi"], new Vector2(80, -112), Scale);
            RectTransform pile = PixelMenuBuilder.Rect("Pile", field, new Vector2(80, -40), new Vector2(240, 160));
            for (int i = 0; i < 3; i++)
                MiniGameSceneBuilder.Art("Cake" + i, pile, art[i == 1 ? "mochi_rainbow" : "mochi"], new Vector2(i == 2 ? 0 : (i == 0 ? -40 : 40), i == 2 ? 56 : 0), Scale * .75f);
            Image ceiling = MiniGameSceneBuilder.Art("CeilingMochi", field, art["mochi"], new Vector2(80, 252), Scale);
            ceiling.rectTransform.localEulerAngles = new Vector3(0, 0, 180);

            // Mallet pivots at the bottom of its handle and swings left onto the cake.
            Image mallet = MiniGameSceneBuilder.Art("Mallet", field, art["mallet"], new Vector2(344, -280), Scale);
            mallet.rectTransform.pivot = new Vector2(.5f, 0);
            mallet.rectTransform.anchoredPosition = new Vector2(344, -280);
            mallet.rectTransform.localEulerAngles = new Vector3(0, 0, -20);

            var flour = new Image[8];
            for (int i = 0; i < flour.Length; i++)
            {
                flour[i] = PixelMenuBuilder.Box("Flour" + i, field, Palette.Panel, s.Solid);
                PixelMenuBuilder.Place(flour[i].rectTransform, Vector2.zero, new Vector2(Px * 3, Px * 3));
                flour[i].raycastTarget = false;
                flour[i].gameObject.SetActive(false);
            }
            Text bubble = PixelMenuBuilder.Label("Bubble", field, "", new Vector2(-144, 32), new Vector2(320, 48), 32, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(bubble.gameObject, Palette.Panel, new Vector2(Px / 2, -Px / 2));
            PixelText.Mark(PixelMenuBuilder.Label("Key", field, "8 / C", new Vector2(80, -300), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter));

            MochiView view = touch.gameObject.AddComponent<MochiView>();
            PixelMenuBuilder.Assign(view, "arm", arm.rectTransform, "mallet", mallet.rectTransform, "mochi", mochi.rectTransform,
                "mortar", mortar.rectTransform, "grandma", grandma.rectTransform, "ceilingMochi", ceiling.rectTransform, "pile", pile,
                "face", face, "mochiImage", mochi, "flash", flash, "bubble", bubble,
                "faceNormal", art["face_normal"], "faceHappy", art["face_happy"], "faceGlare", art["face_glare"], "faceOuch", art["face_ouch"],
                "mochiNormal", art["mochi"], "mochiFlipped", art["mochi_flipped"], "mochiRainbow", art["mochi_rainbow"]);
            PixelMenuBuilder.AssignArray(view, "flour", flour);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("armRest").vector2Value = armRest;
            serialized.FindProperty("armReach").vector2Value = armReach;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ceiling.gameObject.SetActive(false);
            pile.gameObject.SetActive(false);

            MochiController controller = MiniGameSceneBuilder.Finish<MochiController>(s, MusicPath, 64);
            PixelMenuBuilder.Assign(controller, "view", view);
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("MOCHI_READY");
        }

        // ---- Pixel art (placeholders) ----

        private static readonly string[] GrandmaArt = {
            "......kkkk......",
            ".....khhhhk.....",
            "....kkhhhhkk....",
            "...khhhhhhhhk...",
            "..khhhhhhhhhhk..",
            "..khsssssssshk..",
            "..kssssssssssk..",
            "..kssessssessk..",
            "..kcsssmmssssk..",
            "...kssssssssk...",
            "..kkppppppppkk..",
            ".kppppwwwwppppk.",
            ".kppwwwwwwwwppk.",
            ".kppwwwwwwwwppk.",
            ".kppwwwwwwwwppk.",
            ".kppwwwWWwwwppk.",
            ".kppwwwwwwwwppk.",
            ".kppppppppppppk.",
            "..kppppkkppppk..",
            "..kkkkk..kkkkk.." };

        private static readonly string[] ArmArt = {
            "kkkkkkkkk.",
            "pppppksssk",
            "pppppksssk",
            "kkkkkkkkkk" };

        private static readonly string[] MortarArt = {
            "kkkkkkkkkkkkkkkkkkkk",
            "kddddddddddddddddddk",
            "kBBBBBBBBBBBBBBBBBBk",
            ".kbbbbbbbbbbbbbbbbk.",
            ".kbBbbbbbbbbbbbbBbk.",
            ".kbbbbbbbbbbbbbbbbk.",
            "..kbbbbbbbbbbbbbbk..",
            "..kbBbbbbbbbbbbBbk..",
            "...kbbbbbbbbbbbbk...",
            "...kbbbbbbbbbbbbk...",
            "..kbbbbbbbbbbbbbbk..",
            "..kkkkkkkkkkkkkkkk.." };

        private static readonly string[] MochiArt = {
            "...kkkkkk...",
            ".kkwwwwwwkk.",
            "kwwwwwwwwwwk",
            "kwwwwwwwwwwk",
            "kWwwwwwwwwWk",
            ".kkkkkkkkkk." };

        private static readonly string[] MalletArt = {
            "kkkkkkkk",
            "kooooook",
            "koOOOOok",
            "kooooook",
            "koOOOOok",
            "kooooook",
            "kkkkkkkk",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kook..",
            "..kkkk.." };

        private static Dictionary<string, Sprite> CreateArt()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(61, 44, 78, 255) }, { 'h', new Color32(214, 214, 226, 255) }, { 's', new Color32(255, 214, 180, 255) },
                { 'e', new Color32(40, 30, 40, 255) }, { 'c', new Color32(255, 150, 160, 255) }, { 'm', new Color32(200, 90, 100, 255) },
                { 'p', new Color32(155, 123, 234, 255) }, { 'w', new Color32(255, 255, 255, 255) }, { 'W', new Color32(225, 225, 238, 255) },
                { 'd', new Color32(92, 56, 36, 255) }, { 'B', new Color32(200, 140, 90, 255) }, { 'b', new Color32(176, 116, 70, 255) },
                { 'o', new Color32(214, 160, 104, 255) }, { 'O', new Color32(176, 116, 70, 255) },
                { 'r', new Color32(255, 122, 92, 255) }, { 'y', new Color32(255, 201, 60, 255) }, { 'g', new Color32(95, 211, 166, 255) },
                { 'u', new Color32(76, 195, 240, 255) }, { 'v', new Color32(155, 123, 234, 255) } };
            var art = new Dictionary<string, Sprite>();
            // The body has a blank face; the face is a separate overlay so expressions swap without redrawing.
            string[] blankFace = PixelArt.Overlay(GrandmaArt, new[] { (7, 5, "s"), (7, 10, "s"), (8, 3, "s"), (8, 7, "ss"), (8, 12, "s") });
            art["grandma"] = PixelArt.Save(ArtDir, "grandma", blankFace, palette);
            art["face_normal"] = PixelArt.Save(ArtDir, "face_normal", Face(new[] { (7, 5, "e"), (7, 10, "e"), (8, 3, "c"), (8, 12, "c"), (8, 7, "mm") }), palette);
            art["face_happy"] = PixelArt.Save(ArtDir, "face_happy", Face(new[] { (7, 4, "kk"), (7, 10, "kk"), (8, 3, "c"), (8, 12, "c"), (8, 6, "mmmm") }), palette);
            art["face_glare"] = PixelArt.Save(ArtDir, "face_glare", Face(new[] { (6, 4, "kk"), (6, 10, "kk"), (7, 5, "e"), (7, 10, "e"), (8, 6, "kkkk") }), palette);
            art["face_ouch"] = PixelArt.Save(ArtDir, "face_ouch", Face(new[] { (7, 4, "kk"), (7, 10, "kk"), (8, 6, "mmmm"), (9, 6, "mmmm"), (8, 3, "c"), (8, 12, "c") }), palette);
            art["arm"] = PixelArt.Save(ArtDir, "arm", ArmArt, palette);
            art["mortar"] = PixelArt.Save(ArtDir, "mortar", MortarArt, palette);
            art["mochi"] = PixelArt.Save(ArtDir, "mochi", MochiArt, palette);
            art["mochi_flipped"] = PixelArt.Save(ArtDir, "mochi_flipped", PixelArt.Overlay(MochiArt, new[] { (1, 3, "WWWWWW"), (4, 1, "wwwwwwwwww") }), palette);
            // Rainbow cake: the bonus, and the top of the collection (plan: soybean -> rainbow).
            art["mochi_rainbow"] = PixelArt.Save(ArtDir, "mochi_rainbow", PixelArt.Overlay(MochiArt, new[] {
                (1, 3, "rrrrrr"), (2, 1, "yyyyyyyyyy"), (3, 1, "gggggggggg"), (4, 1, "uuuuuuuuuu") }), palette);
            art["mallet"] = PixelArt.Save(ArtDir, "mallet", MalletArt, palette);
            return art;
        }

        // A transparent sheet the size of the body with only the face pixels drawn.
        private static string[] Face((int row, int col, string pixels)[] patches)
        {
            var blank = new string[GrandmaArt.Length];
            for (int i = 0; i < blank.Length; i++) blank[i] = new string('.', GrandmaArt[0].Length);
            return PixelArt.Overlay(blank, patches);
        }
    }
}

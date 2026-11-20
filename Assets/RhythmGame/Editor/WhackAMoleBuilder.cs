using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Generates the whack-a-mole scene: the shared mini-game frame plus eight holes and pixel moles.
    // The scene is fully generated, so rebuilding it is always safe. Headless:
    // unity run <project> -- -executeMethod RhythmGame.Editor.WhackAMoleBuilder.Build
    public static class WhackAMoleBuilder
    {
        public const string ScenePath = "Assets/Scenes/WhackAMole.unity";
        private const string ArtDir = "Assets/RhythmGame/Art/Moles";
        private const string MusicPath = "Assets/RhythmGame/Resources/RhythmGame/Audio/WhackBeat.wav";
        private const float Px = PixelTween.Pixel;
        private static readonly Color Meadow = new Color32(184, 230, 138, 255);   // a garden, not a night screen
        private static readonly Color Earth = new Color32(176, 116, 70, 255);
        private static readonly Color Pit = new Color32(60, 34, 22, 255);
        private static readonly Color Spark = Palette.Sun;

        [MenuItem("Rhythm Game/Build Whack-a-Mole Prototype")]
        public static void Build()
        {
            Dictionary<string, Sprite> moles = CreateMoles();
            MiniGameSceneBuilder.Scene s = MiniGameSceneBuilder.Begin("WhackCanvas", Meadow);
            // Holes in pad order, so holes[i] is pad i + 1.
            var holes = new MoleHoleView[8];
            for (int pad = 0; pad < holes.Length; pad++) holes[pad] = BuildHole(s.Field, pad, s.Frame, s.Solid, moles);
            WhackAMoleController controller = MiniGameSceneBuilder.Finish<WhackAMoleController>(s, MusicPath, 64);
            PixelMenuBuilder.AssignArray(controller, "holes", holes);
            MiniGameSceneBuilder.Save(s, ScenePath);
            Debug.Log("WHACK_A_MOLE_READY");
        }

        // Rim (touch area) > pit > masked mouth the mole rises through > front lip > sparks, bubble, key.
        private static MoleHoleView BuildHole(Transform field, int pad, Sprite frame, Sprite solid, Dictionary<string, Sprite> moles)
        {
            RectTransform hole = PixelMenuBuilder.Rect("Hole" + (pad + 1), field, MiniGameSceneBuilder.PadLayout[pad], new Vector2(176, 152));
            Image rim = PixelMenuBuilder.Box("Rim", hole, Palette.Lift(Earth), frame);
            PixelMenuBuilder.Stretch(rim.rectTransform);
            rim.raycastTarget = true;
            Image pit = PixelMenuBuilder.Box("Pit", hole, Pit, solid);
            PixelMenuBuilder.Place(pit.rectTransform, new Vector2(0, -28), new Vector2(144, 32));
            pit.raycastTarget = false;
            RectTransform mouth = PixelMenuBuilder.Rect("Mouth", hole, new Vector2(0, 36), new Vector2(144, 128));
            mouth.gameObject.AddComponent<RectMask2D>();
            Image mole = PixelMenuBuilder.Box("Mole", mouth, Color.white, moles["mole"]);
            mole.type = Image.Type.Simple;
            mole.raycastTarget = false;
            RectTransform moleRect = mole.rectTransform;
            moleRect.anchorMin = moleRect.anchorMax = new Vector2(.5f, 0);
            moleRect.pivot = new Vector2(.5f, 0);
            moleRect.sizeDelta = new Vector2(128, 128);
            moleRect.anchoredPosition = new Vector2(0, -128);
            Image lip = PixelMenuBuilder.Box("Lip", hole, Pit, solid);
            PixelMenuBuilder.Place(lip.rectTransform, new Vector2(0, -40), new Vector2(152, 16));
            lip.raycastTarget = false;
            var sparks = new Image[6];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = PixelMenuBuilder.Box("Spark" + i, hole, Spark, solid);
                PixelMenuBuilder.Place(sparks[i].rectTransform, Vector2.zero, new Vector2(Px * 3, Px * 3));
                sparks[i].raycastTarget = false;
                sparks[i].gameObject.SetActive(false);
            }
            Text bubble = PixelMenuBuilder.Label("Bubble", hole, "", new Vector2(0, 112), new Vector2(176, 32), 16, Palette.Ink, TextAnchor.MiddleCenter);
            PixelMenuBuilder.Shadow(bubble.gameObject, Palette.Panel, new Vector2(2, -2));
            PixelText.Mark(PixelMenuBuilder.Label("Key", hole, (pad + 1) + " / " + MiniGameSceneBuilder.PadKeys[pad], new Vector2(0, -60), new Vector2(160, 24), 16, Palette.Ink, TextAnchor.MiddleCenter));

            MoleHoleView view = hole.gameObject.AddComponent<MoleHoleView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("pad").intValue = pad;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PixelMenuBuilder.Assign(view, "mole", moleRect, "moleImage", mole, "holeImage", rim, "bubble", bubble,
                "normal", moles["mole"], "gold", moles["gold"], "flower", moles["flower"], "tongue", moles["tongue"]);
            PixelMenuBuilder.AssignArray(view, "sparks", sparks);
            PixelMenuBuilder.AssignArray(view, "disguises", new Object[] { moles["hat"], moles["glasses"] });
            return view;
        }


        // ---- 16x16 pixel moles ----

        private static readonly string[] MoleArt = {
            "................",
            ".....kkkkkk.....",
            "....kbbbbbbk....",
            "...kbbbbbbbbk...",
            "..kbbbbbbbbbbk..",
            "..kbbwebbwebbk..",
            "..kbbeebbeebbk..",
            "..kbcbbnnbbcbk..",
            "..kbbbbttbbbbk..",
            "..kbbllllllbbk..",
            ".kbbllllllllbbk.",
            ".kbllllllllllbk.",
            ".kbllllllllllbk.",
            ".kbbllllllllbbk.",
            ".kbbbbbbbbbbbbk.",
            ".kbbbbbbbbbbbbk." };

        private static Dictionary<string, Sprite> CreateMoles()
        {
            var palette = new Dictionary<char, Color32> {
                { 'k', new Color32(42, 24, 16, 255) }, { 'b', new Color32(139, 90, 60, 255) }, { 'l', new Color32(216, 168, 120, 255) },
                { 'w', new Color32(255, 255, 255, 255) }, { 'e', new Color32(26, 15, 10, 255) }, { 'n', new Color32(240, 138, 156, 255) },
                { 'c', new Color32(232, 106, 122, 255) }, { 't', new Color32(255, 250, 240, 255) }, { 'r', new Color32(214, 64, 84, 255) },
                { 'p', new Color32(255, 140, 190, 255) }, { 'y', new Color32(255, 220, 80, 255) }, { 'h', new Color32(30, 30, 44, 255) },
                { 'g', new Color32(20, 20, 28, 255) }, { 's', new Color32(120, 200, 255, 255) } };
            var goldPalette = new Dictionary<char, Color32>(palette) {
                ['k'] = new Color32(107, 74, 16, 255), ['b'] = new Color32(232, 184, 48, 255), ['l'] = new Color32(255, 242, 168, 255) };

            var sprites = new Dictionary<string, Sprite>();
            sprites["mole"] = PixelArt.Save(ArtDir, "mole", MoleArt, palette);
            sprites["gold"] = PixelArt.Save(ArtDir, "gold", MoleArt, goldPalette);
            // Flower on the head: the one you must not hit.
            sprites["flower"] = PixelArt.Save(ArtDir, "flower", PixelArt.Overlay(MoleArt, new[] { (0, 2, "ppp"), (1, 2, "pyp"), (2, 2, "ppp") }), palette);
            // Taunt face for a missed mole: squeezed eyes and tongue out.
            sprites["tongue"] = PixelArt.Save(ArtDir, "tongue", PixelArt.Overlay(MoleArt, new[] {
                (5, 5, "bb"), (5, 9, "bb"), (6, 5, "kk"), (6, 9, "kk"), (8, 7, "rr"), (9, 7, "rr") }), palette);
            // Disguises (visual only, plan §4-02): a top hat and sunglasses.
            sprites["hat"] = PixelArt.Save(ArtDir, "hat", PixelArt.Overlay(MoleArt, new[] {
                (0, 5, "hhhhhh"), (1, 5, "hhhhhh"), (2, 5, "rrrrrr"), (3, 3, "hhhhhhhhhh") }), palette);
            sprites["glasses"] = PixelArt.Save(ArtDir, "glasses", PixelArt.Overlay(MoleArt, new[] {
                (5, 4, "ggggggggg"), (6, 4, "gsg"), (6, 9, "gsg") }), palette);
            return sprites;
        }




    }
}

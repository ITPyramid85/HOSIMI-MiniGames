using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Moves screens that were built incrementally (Game scene, Settings panel, note prefab) from the
    // old dark palette onto Palette. Fills map by colour; text picks Ink or white from what it sits on.
    // Screens with a full builder (menu, characters, whack-a-mole) are rebuilt instead.
    // Safe to rerun: new colours are not keys of the old map. Headless:
    // unity run <project> -- -executeMethod RhythmGame.Editor.PaletteRemap.Apply
    public static class PaletteRemap
    {
        private const float OldFrameFill = .78f;
        private static readonly Dictionary<Color32, Color> Fills = new Dictionary<Color32, Color>
        {
            { C(13, 19, 34), Palette.Background }, { C(7, 10, 18), Palette.Background }, { C(10, 14, 26), Palette.Background },
            { C(27, 37, 56), Palette.Panel }, { C(34, 48, 72), Palette.Panel }, { C(52, 64, 88), Palette.Panel },
            { C(57, 84, 111), Palette.Panel }, { C(44, 63, 107), Palette.Panel }, { C(70, 84, 112), Palette.Panel },
            { C(19, 28, 45), Palette.Well }, { C(37, 54, 76), Palette.Well }, { C(45, 92, 104), Palette.Well }, { C(226, 232, 240), Palette.Well },
            { C(96, 232, 200), Palette.Sky }, { C(103, 208, 255), Palette.Sky }, { C(24, 92, 96), Palette.Sky },
            { C(255, 209, 102), Palette.Sun }, { C(154, 171, 194), Palette.Muted }, { C(235, 103, 125), Palette.Coral },
        };
        private static readonly Dictionary<Color32, Color> Inks = new Dictionary<Color32, Color>
        {
            { C(154, 171, 194), Palette.Muted }, { C(96, 232, 200), Palette.SkyText }, { C(103, 208, 255), Palette.SkyText },
            { C(255, 209, 102), Palette.SunText }, { C(235, 103, 125), Palette.DangerText }, { C(255, 178, 125), Palette.WarnText },
            { C(13, 19, 34), Palette.Ink }, { C(7, 10, 18), Palette.Ink },
        };

        // The candy palette (v1 review deck) to the pastel one (client reply v5 §8), matched by RGB; alpha is kept.
        private static readonly Dictionary<Color32, Color> CandyToPastel = new Dictionary<Color32, Color>
        {
            { C(255, 244, 217), Palette.Background }, { C(240, 210, 160), Palette.GridLine }, { C(255, 233, 194), Palette.Well },
            { C(61, 44, 78), Palette.Ink }, { C(138, 122, 156), Palette.Muted },
            { C(255, 201, 60), Palette.Sun }, { C(76, 195, 240), Palette.Sky }, { C(95, 211, 166), Palette.Mint },
            { C(255, 127, 168), Palette.Pink }, { C(255, 122, 92), Palette.Coral }, { C(155, 123, 234), Palette.Grape },
            { C(24, 140, 196), Palette.SkyText }, { C(32, 150, 108), Palette.MintText }, { C(214, 132, 0), Palette.SunText },
            { C(214, 64, 96), Palette.DangerText }, { C(224, 112, 32), Palette.WarnText },
        };

        [MenuItem("Rhythm Game/Apply Palette To Game And Settings")]
        public static void Apply()
        {
            PixelMenuBuilder.CreateArt();   // frame, grid and scanline textures in the new palette
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.backgroundColor = Palette.Background;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.transform.parent == null) Remap(canvas.transform);
            foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.name == "Brand") text.text = "H O S I M I";
                // Numbers keep the pixel font; everything else turns readable (client reply v5 §4-4).
                if (text.name == "Score" || text.name == "Combo" || text.name == "Countdown" || text.name == "Judgement") PixelText.Mark(text);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (string path in new[] { MainMenuBuilder.LatencyPath, "Assets/RhythmGame/Prefabs/RhythmNote.prefab" })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try { Remap(root.transform); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PALETTE_REMAP_READY");
        }

        private static void Remap(Transform root)
        {
            // Fills first, so text can then look at what it now sits on.
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is Text) continue;
                if (graphic.name.StartsWith("Bar") && graphic.transform.parent != null && graphic.transform.parent.name == "Wipe")
                    graphic.color = Palette.Shutter;
                else if (IsBlocker(graphic.color)) graphic.color = Palette.Dim;
                else graphic.color = Map(Fills, graphic.color, graphic is Image && ((Image)graphic).type == Image.Type.Sliced);
            }
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                bool light = OnLightSurface(text.transform);
                Color32 c = text.color;
                if (c.r > 230 && c.g > 230 && c.b > 230) text.color = new Color(light ? Palette.Ink.r : 1, light ? Palette.Ink.g : 1, light ? Palette.Ink.b : 1, text.color.a);
                else text.color = Map(Inks, text.color, false);
            }
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.color = Map(CandyToPastel, graphic.color, false);
            foreach (Shadow shadow in root.GetComponentsInChildren<Shadow>(true))
            {
                shadow.effectColor = Map(CandyToPastel, shadow.effectColor, false);
                Color32 c = shadow.effectColor;
                bool onText = shadow.GetComponent<Text>() != null;
                if (c.r == 24 && c.g == 92 && c.b == 96) shadow.effectColor = Palette.TextShadow;
                else if (c.r < 10 && c.g < 10 && c.b < 10) shadow.effectColor = onText ? Palette.Panel : Palette.DropShadow;
            }
        }

        // Exact match on the stored colour, or on it with the old 0.78 frame lift undone.
        private static Color Map(Dictionary<Color32, Color> map, Color color, bool lifted)
        {
            Color mapped;
            if (TryFind(map, color, false, out mapped) || (lifted && TryFind(map, color, true, out mapped)))
                return new Color(mapped.r, mapped.g, mapped.b, color.a);
            return color;
        }

        private static bool TryFind(Dictionary<Color32, Color> map, Color color, bool lifted, out Color mapped)
        {
            Color32 c = color;
            foreach (KeyValuePair<Color32, Color> entry in map)
            {
                Color32 key = lifted ? (Color32)new Color(Mathf.Min(1, entry.Key.r / 255f / OldFrameFill),
                    Mathf.Min(1, entry.Key.g / 255f / OldFrameFill), Mathf.Min(1, entry.Key.b / 255f / OldFrameFill)) : entry.Key;
                if (Mathf.Abs(key.r - c.r) <= 3 && Mathf.Abs(key.g - c.g) <= 3 && Mathf.Abs(key.b - c.b) <= 3)
                {
                    mapped = entry.Value;
                    return true;
                }
            }
            mapped = default(Color);
            return false;
        }

        // The old near-black modal dim, or an earlier run's Dim (so retuning its alpha reapplies).
        private static bool IsBlocker(Color c)
        {
            bool oldDim = c.a > .5f && c.r < .05f && c.g < .05f && c.b < .1f;
            bool dim = Mathf.Abs(c.r - Palette.Dim.r) < .02f && Mathf.Abs(c.g - Palette.Dim.g) < .02f && Mathf.Abs(c.b - Palette.Dim.b) < .02f;
            return c.a < 1 && (oldDim || dim);
        }

        // The nearest opaque fill behind the text; the screen background counts as light.
        private static bool OnLightSurface(Transform text)
        {
            for (Transform t = text.parent; t != null; t = t.parent)
            {
                Image image = t.GetComponent<Image>();
                if (image != null && image.color.a > .5f) return image.color.grayscale > .5f;
            }
            return true;
        }

        private static Color32 C(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }
    }
}

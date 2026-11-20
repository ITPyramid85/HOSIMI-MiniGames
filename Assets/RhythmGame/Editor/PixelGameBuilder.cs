using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Restyles the existing Game scene in place for the pixel look and wires GameUIAnimator.
    // Object names, paths and bindings are untouched, so GameController and tests keep working.
    // Safe to run repeatedly. Headless: unity run <project> -- -executeMethod RhythmGame.Editor.PixelGameBuilder.Restyle
    public static class PixelGameBuilder
    {
        private const float Px = PixelTween.Pixel;
        private static readonly Color Ink = Palette.Shutter;   // wipe bars
        private static readonly Color AccentDark = Palette.TextShadow;
        private static readonly string[] LeftColumn = { "Brand", "Song", "CharacterCard", "Character", "CharacterAccent", "SkillStatus", "Hint" };
        private static readonly string[] RightColumn = { "PauseButton", "ScoreCaption", "Score", "Combo", "HealthCaption", "Health", "HealthBack", "HealthFill" };

        [MenuItem("Rhythm Game/Restyle Game Scene (Pixel)")]
        public static void Restyle()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(PixelMenuBuilder.SolidPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf");
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            GameController controller = Object.FindFirstObjectByType<GameController>();
            Transform root = controller.transform;
            Transform layout = root.Find("Layout");
            foreach (string name in new[] { "Grid", "Scanlines", "Wipe" })
            {
                Transform old = root.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // Align every fixed-anchor element to the art grid.
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == root || rect.anchorMin != rect.anchorMax) continue;
                rect.anchoredPosition = PixelTween.Snap(rect.anchoredPosition);
                rect.sizeDelta = PixelTween.Snap(rect.sizeDelta);
            }
            // MonoBit is a 16px bitmap design: multiples of 16 keep glyph edges crisp.
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
                text.fontSize = text.fontSize < 24 ? 16 : text.fontSize < 40 ? 32 : text.fontSize < 56 ? 48 : 64;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            Frame(layout.Find("Track"), frame);
            Frame(layout.Find("CharacterCard"), frame);
            Transform card = root.Find("Modal/Card");
            Frame(card, frame);
            Shadow(card, new Color(0, 0, 0, .5f), new Vector2(Px * 2, -Px * 2));
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                Frame(button.transform, frame);
                Shadow(button.transform, new Color(0, 0, 0, .55f), new Vector2(Px, -Px));
                ColorBlock colors = button.colors;
                colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
                colors.pressedColor = new Color(.7f, .7f, .7f);
                colors.fadeDuration = 0;
                button.colors = colors;
            }
            foreach (DrumPadView pad in root.GetComponentsInChildren<DrumPadView>(true))
                foreach (string part in new[] { "Border", "Button", "CueOutline" }) Frame(pad.transform.Find(part), frame);
            MiniGameGuide guide = root.GetComponentInChildren<MiniGameGuide>(true);
            if (guide != null)
                foreach (Transform child in guide.transform.Find("DemoBoard"))
                    if (child.name.StartsWith("DemoPad")) Frame(child, frame);
            foreach (string name in new[] { "HealthBack", "ProgressBack", "HealthFill", "ProgressFill" })
            {
                Image bar = layout.Find(name).GetComponent<Image>();
                bar.sprite = solid;
                Vector2 size = bar.rectTransform.sizeDelta;
                bar.rectTransform.sizeDelta = new Vector2(size.x, name.StartsWith("Health") ? Px * 3 : Px);
            }
            Shadow(layout.Find("Score"), AccentDark, new Vector2(Px, -Px));
            Shadow(layout.Find("Track/Countdown"), AccentDark, new Vector2(Px, -Px));

            // Equipped character's portrait: on the left card, and in the start guide in place of the drum avatar.
            PixelCharacterBuilder.ImportPortraits();
            RectTransform characterCard = (RectTransform)layout.Find("CharacterCard");
            Image cardPortrait = PortraitImage(characterCard, Vector2.zero, characterCard.sizeDelta - new Vector2(Px * 6, Px * 16), new Vector2(0, Px * 4));
            RectTransform characterName = (RectTransform)layout.Find("Character");
            characterName.anchoredPosition = characterCard.anchoredPosition - new Vector2(0, characterCard.sizeDelta.y / 2 - Px * 7);
            characterName.sizeDelta = new Vector2(characterCard.sizeDelta.x, Px * 10);
            characterName.GetComponent<Text>().fontSize = 32;
            characterName.SetAsLastSibling();
            PixelMenuBuilder.Assign(controller, "characterPortrait", cardPortrait);
            if (guide != null)
            {
                RectTransform avatar = (RectTransform)guide.transform.Find("GuideAvatar");
                Image guidePortrait = PortraitImage(guide.transform, avatar.anchoredPosition + new Vector2(0, Px * 4), new Vector2(Px * 48, Px * 72), Vector2.zero);
                PixelMenuBuilder.Assign(guide, "portrait", guidePortrait, "placeholderAvatar", avatar.gameObject);
                avatar.gameObject.SetActive(false);
            }

            // Background grid right above the solid background; scanlines and shutters on top.
            RawImage grid = PixelMenuBuilder.Raw("Grid", root, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.GridPath),
                new Rect(0, 0, 1280 / 64f, 720 / 64f));
            grid.transform.SetSiblingIndex(root.Find("Background").GetSiblingIndex() + 1);
            grid.color = new Color(1, 1, 1, .5f);   // quieter than the menu: the pads are the focus
            PixelMenuBuilder.Raw("Scanlines", root, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.ScanlinePath),
                new Rect(0, 0, 1, 720 / 4f));
            RectTransform wipe = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("Wipe", root, Vector2.zero, Vector2.zero));
            var wipeBars = new RectTransform[12];
            for (int i = 0; i < wipeBars.Length; i++)
            {
                Image bar = PixelMenuBuilder.Box("Bar" + i, wipe, Ink, null);
                bar.raycastTarget = false;
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0, i / 12f); rect.anchorMax = new Vector2(1, (i + 1) / 12f);
                rect.pivot = new Vector2(i % 2, .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                bar.gameObject.SetActive(false);
                wipeBars[i] = rect;
            }

            GameUIAnimator animator = controller.GetComponent<GameUIAnimator>();
            if (animator == null) animator = controller.gameObject.AddComponent<GameUIAnimator>();
            PixelMenuBuilder.Assign(animator, "grid", grid, "track", layout.Find("Track"),
                "judgement", layout.Find("Track/Judgement").GetComponent<Text>(),
                "countdown", layout.Find("Track/Countdown").GetComponent<Text>(),
                "combo", layout.Find("Combo").GetComponent<Text>(), "score", layout.Find("Score").GetComponent<Text>());
            PixelMenuBuilder.AssignArray(animator, "wipeBars", wipeBars);
            PixelMenuBuilder.AssignArray(animator, "leftColumn", Find(layout, LeftColumn));
            PixelMenuBuilder.AssignArray(animator, "rightColumn", Find(layout, RightColumn));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_GAME_READY");
        }

        // Swaps a flat or rounded panel for the pixel frame, lifting its colour once so the
        // 0.78-grey frame fill lands on the original colour.
        private static void Frame(Transform target, Sprite frame)
        {
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || image.sprite == frame) return;
            Color c = image.color;
            float k = 1 / PixelMenuBuilder.FrameFill;
            image.color = new Color(Mathf.Min(1, c.r * k), Mathf.Min(1, c.g * k), Mathf.Min(1, c.b * k), c.a);
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1 / Px;
        }

        // Replaces any previous "Portrait" child so reruns don't stack images.
        private static Image PortraitImage(Transform parent, Vector2 position, Vector2 size, Vector2 offset)
        {
            Transform old = parent.Find("Portrait");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Image image = PixelMenuBuilder.Box("Portrait", parent, Color.white, null);
            PixelMenuBuilder.Place(image.rectTransform, position + offset, size);
            image.preserveAspect = true;
            image.raycastTarget = false;
            // Default art for the editor view; GameController / MiniGameGuide swap in the equipped character.
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RhythmGame/Resources/RhythmGame/Portraits/whale.png");
            return image;
        }

        private static void Shadow(Transform target, Color color, Vector2 distance)
        {
            if (target == null) return;
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null) { PixelMenuBuilder.Shadow(target.gameObject, color, distance); return; }
            shadow.effectColor = color; shadow.effectDistance = distance;
        }

        private static Object[] Find(Transform layout, string[] names)
        {
            var found = new List<Object>();
            foreach (string name in names)
            {
                Transform item = layout.Find(name);
                if (item != null) found.Add(item);
            }
            return found.ToArray();
        }
    }
}

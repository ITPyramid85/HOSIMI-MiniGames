using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Rebuilds the character popup's Card in place: one page per character (plan 2-2), flipped
    // left/right, with a story card and three buttons. The prefab root and its CharacterPopup are
    // kept so the main menu's reference survives. Headless:
    // unity run <project> -- -executeMethod RhythmGame.Editor.PixelCharacterBuilder.Rebuild
    public static class PixelCharacterBuilder
    {
        private const string PortraitDir = "Assets/RhythmGame/Resources/RhythmGame/Portraits";
        private const float Px = PixelTween.Pixel;

        [MenuItem("Rhythm Game/Rebuild Character Panel (Pixel)")]
        public static void Rebuild()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            ImportPortraits();
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterMenuBuilder.PrefabPath);
            try
            {
                Transform old = root.transform.Find("Card");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                BuildCard(root.transform, root.GetComponent<CharacterPopup>(), frame);
                PrefabUtility.SaveAsPrefabAsset(root, CharacterMenuBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_CHARACTERS_READY");
        }

        // Portraits are large pixel art shown at about a third of source size; mipmapped bilinear keeps them clean.
        internal static void ImportPortraits()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitDir }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        // Plan 2-2 laid out for landscape: the character's world on the left, the story card and the
        // three fixed buttons on the right, page count and drumsticks along the top.
        private static void BuildCard(Transform root, CharacterPopup popup, Sprite frame)
        {
            PixelMenuBuilder.Assign(root.GetComponent<MenuTypography>(), "fontOverride",
                AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            root.GetComponent<Image>().color = Palette.Dim;
            Image card = Framed("Card", root, Palette.Panel, frame);
            PixelMenuBuilder.Place(card.rectTransform, Vector2.zero, new Vector2(1120, 608));
            PixelMenuBuilder.Shadow(card.gameObject, Palette.DropShadow, new Vector2(Px * 2, -Px * 2));
            Transform c = card.transform;

            Text title = PixelMenuBuilder.Label("Title", c, "角色 1 / 6", new Vector2(-304, 252), new Vector2(448, 48), 32, Palette.Ink, TextAnchor.MiddleLeft);
            PixelMenuBuilder.Shadow(title.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            Image pill = Framed("BalancePill", c, Palette.Sun, frame);
            PixelMenuBuilder.Place(pill.rectTransform, new Vector2(232, 252), new Vector2(304, 48));
            Text balance = PixelMenuBuilder.Label("Balance", pill.transform, "", Vector2.zero, new Vector2(288, 32), 16, Palette.Ink, TextAnchor.MiddleCenter);
            Button close = PixelMenuBuilder.PixelButton("CloseButton", c, frame, "返回", new Vector2(464, 252), new Vector2(144, 56), Palette.Well, Palette.Ink, 16);

            // Left: the character's world, clipped so pages slide in from the edge.
            Image world = Framed("World", c, Palette.Sky, frame);
            PixelMenuBuilder.Place(world.rectTransform, new Vector2(-256, -20), new Vector2(544, 464));
            world.gameObject.AddComponent<RectMask2D>().padding = new Vector4(Px * 2, Px * 2, Px * 2, Px * 2);
            RawImage grid = PixelMenuBuilder.Raw("Grid", world.transform, AssetDatabase.LoadAssetAtPath<Texture2D>(PixelMenuBuilder.GridPath),
                new Rect(0, 0, 544 / 64f, 464 / 64f));
            grid.color = new Color(1, 1, 1, .6f);
            Image ground = PixelMenuBuilder.Box("Ground", world.transform, new Color(1, 1, 1, .35f), null);
            PixelMenuBuilder.Place(ground.rectTransform, new Vector2(0, -184), new Vector2(544, 96));
            ground.raycastTarget = false;

            RectTransform stage = PixelMenuBuilder.Rect("Stage", world.transform, Vector2.zero, new Vector2(544, 464));
            Image footShadow = PixelMenuBuilder.Box("FootShadow", stage, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, .18f), null);
            PixelMenuBuilder.Place(footShadow.rectTransform, new Vector2(0, -152), new Vector2(192, 16));
            footShadow.raycastTarget = false;
            Image portrait = PixelMenuBuilder.Box("Portrait", stage, Color.white, null);
            PixelMenuBuilder.Place(portrait.rectTransform, new Vector2(0, 8), new Vector2(336, 336));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            Image chip = Framed("WorldChip", world.transform, new Color(1, 1, 1, .85f), frame);
            PixelMenuBuilder.Place(chip.rectTransform, new Vector2(-256, 200), new Vector2(304, 32));
            chip.rectTransform.pivot = new Vector2(0, .5f);
            Text worldLabel = PixelMenuBuilder.Label("WorldLabel", chip.transform, "", Vector2.zero, new Vector2(272, 24), 16, Palette.Ink, TextAnchor.MiddleLeft);
            Button previous = PixelMenuBuilder.PixelButton("PreviousButton", world.transform, frame, "<", new Vector2(-232, 0), new Vector2(48, 64), Palette.Panel, Palette.Ink, 32);
            Button next = PixelMenuBuilder.PixelButton("NextButton", world.transform, frame, ">", new Vector2(232, 0), new Vector2(48, 64), Palette.Panel, Palette.Ink, 32);

            RectTransform dotsRow = PixelMenuBuilder.Rect("Dots", world.transform, new Vector2(0, -208), new Vector2(256, 16));
            var dotsLayout = dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 8;
            dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            dotsLayout.childControlWidth = dotsLayout.childControlHeight = false;
            dotsLayout.childForceExpandWidth = dotsLayout.childForceExpandHeight = false;
            Image dot = PixelMenuBuilder.Box("DotTemplate", dotsRow, Palette.Muted, null);
            dot.rectTransform.sizeDelta = new Vector2(Px * 3, Px * 3);
            dot.raycastTarget = false;
            dot.gameObject.SetActive(false);

            // Right: story card.
            Text name = PixelMenuBuilder.Label("CharacterName", c, "", new Vector2(288, 204), new Vector2(448, 48), 32, Palette.Ink, TextAnchor.MiddleLeft);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16; name.resizeTextMaxSize = 32;
            name.verticalOverflow = VerticalWrapMode.Truncate;   // best fit only shrinks text that would not fit
            PixelMenuBuilder.Shadow(name.gameObject, Palette.TextShadow, new Vector2(Px, -Px));
            RectTransform tagRow = PixelMenuBuilder.Rect("Tags", c, new Vector2(288, 160), new Vector2(448, 32));
            var tags = new Image[3];
            for (int i = 0; i < tags.Length; i++)
            {
                tags[i] = Framed("Tag" + i, tagRow, Palette.Well, frame);
                RectTransform rect = tags[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
                rect.sizeDelta = new Vector2(96, 32);
                PixelMenuBuilder.Stretch(PixelMenuBuilder.Label("Text", tags[i].transform, "", Vector2.zero, Vector2.zero, 16, Palette.Ink, TextAnchor.MiddleCenter).rectTransform);
            }
            Text story = PixelMenuBuilder.Label("Story", c, "", new Vector2(288, 64), new Vector2(448, 144), 16, Palette.Ink, TextAnchor.UpperLeft);
            story.lineSpacing = 1.25f;
            Image divider = PixelMenuBuilder.Box("Divider", c, new Color(Palette.Muted.r, Palette.Muted.g, Palette.Muted.b, .3f), null);
            PixelMenuBuilder.Place(divider.rectTransform, new Vector2(288, -24), new Vector2(448, Px));
            divider.raycastTarget = false;
            Text ownership = PixelMenuBuilder.Label("Ownership", c, "", new Vector2(144, -48), new Vector2(160, 24), 16, Palette.Muted, TextAnchor.MiddleLeft);
            Text prices = PixelMenuBuilder.Label("Prices", c, "", new Vector2(368, -48), new Vector2(288, 24), 16, Palette.Muted, TextAnchor.MiddleRight);

            Button primary = PixelMenuBuilder.PixelButton("PrimaryButton", c, frame, "", new Vector2(288, -104), new Vector2(448, 64), Palette.Sun, Palette.Ink, 16);
            Button claim = PixelMenuBuilder.PixelButton("ClaimButton", c, frame, "", new Vector2(288, -176), new Vector2(448, 64), Palette.Mint, Palette.Ink, 16);
            Button merch = PixelMenuBuilder.PixelButton("MerchButton", c, frame, "", new Vector2(288, -248), new Vector2(448, 64), Palette.Sky, Palette.Ink, 16);
            foreach (Button button in new[] { primary, claim, merch })
            {
                ColorBlock colors = button.colors;
                colors.disabledColor = new Color(1, 1, 1, .45f);
                button.colors = colors;
            }

            Text status = PixelMenuBuilder.Label("Status", c, "", new Vector2(-192, -280), new Vector2(416, 24), 16, Palette.MintText, TextAnchor.MiddleCenter);
            Button retry = PixelMenuBuilder.PixelButton("RetrySaveButton", c, frame, "重试保存", new Vector2(-464, -280), new Vector2(160, 40), Palette.Well, Palette.WarnText, 16);
            retry.gameObject.SetActive(false);

            PixelMenuBuilder.Assign(popup, "closeButton", close, "primaryButton", primary, "claimButton", claim, "merchButton", merch,
                "previousButton", previous, "nextButton", next, "retrySaveButton", retry,
                "title", title, "balance", balance, "worldLabel", worldLabel, "characterName", name, "story", story,
                "ownership", ownership, "prices", prices, "status", status,
                "world", world, "portrait", portrait, "dotTemplate", dot, "stage", stage);
            PixelMenuBuilder.AssignArray(popup, "tags", tags);
        }

        private static Image Framed(string name, Transform parent, Color color, Sprite frame)
        {
            Color lifted = Palette.Lift(color);
            Image image = PixelMenuBuilder.Box(name, parent, lifted, frame);
            image.raycastTarget = false;
            return image;
        }
    }
}

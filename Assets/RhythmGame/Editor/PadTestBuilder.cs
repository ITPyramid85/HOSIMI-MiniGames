using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    // Builds the drum test screen (PadTestPanel.prefab) and the code-drawn pad layout it shows: the eight
    // pads of the BX-2 (G3003 manual) where they sit on the product (PAD1-2 top, PAD3-5 middle, PAD6-8
    // bottom), without the drum body (client reply v6 §6-1). Faces are separate sprites so each can light up.
    // Headless: unity run <project> -- -executeMethod RhythmGame.Editor.PadTestBuilder.Build
    public static class PadTestBuilder
    {
        public const string PrefabPath = "Assets/RhythmGame/Prefabs/PadTestPanel.prefab";
        private const string ArtDir = "Assets/RhythmGame/Art/Drum";
        private const float Px = PixelTween.Pixel;
        public const int ArtW = 168, ArtH = 140, ArtScale = 3;

        // (x, y, r) in art pixels, y down; index = PAD1..PAD8.
        public static readonly int[,] Pads = {
            { 30, 42, 17 }, { 138, 42, 17 },
            { 47, 72, 14 }, { 84, 70, 14 }, { 121, 72, 14 },
            { 30, 108, 19 }, { 84, 106, 16 }, { 138, 108, 19 } };
        private const float FaceRatio = .62f;

        [MenuItem("Rhythm Game/Build Drum Test Panel")]
        public static void Build()
        {
            Sprite frame = PixelMenuBuilder.CreateArt();
            Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(PixelMenuBuilder.SolidPath);
            Sprite body = DrawBody();
            RectTransform root = PixelMenuBuilder.Stretch(PixelMenuBuilder.Rect("PadTestPanel", null, Vector2.zero, Vector2.zero));
            root.gameObject.AddComponent<Image>().color = Palette.Dim;
            PixelMenuBuilder.Assign(root.gameObject.AddComponent<MenuTypography>(), "fontOverride",
                AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/MonoBit_CN_16px.ttf"));
            PadTestPopup popup = root.gameObject.AddComponent<PadTestPopup>();

            Image card = PixelMenuBuilder.Box("Card", root, Palette.Lift(Palette.Background), frame);
            PixelMenuBuilder.Place(card.rectTransform, Vector2.zero, new Vector2(1232, 688));
            PixelMenuBuilder.Shadow(card.gameObject, Palette.DropShadow, new Vector2(Px * 2, -Px * 2));
            Transform c = card.transform;

            Text title = Left("Title", c, "鼓垫测试", -576, 292, 480, 56, 40, Palette.Ink);
            Button close = PixelMenuBuilder.PixelButton("CloseButton", c, frame, "关闭", new Vector2(520, 292), new Vector2(144, 56), Palette.Well, Palette.Ink, 24);

            // Connection line, Bluetooth search, audio route.
            Image pill = PixelMenuBuilder.Box("Connection", c, Palette.Lift(Palette.Panel), frame);
            PixelMenuBuilder.Place(pill.rectTransform, new Vector2(-276, 220), new Vector2(600, 56));
            pill.raycastTarget = false;
            Image dot = PixelMenuBuilder.Box("Dot", pill.transform, Palette.Well, null);
            PixelMenuBuilder.Place(dot.rectTransform, new Vector2(-268, 0), new Vector2(Px * 5, Px * 5));
            dot.raycastTarget = false;
            Text connection = Left("ConnectionText", pill.transform, "", -244, 0, 532, 48, 22, Palette.Ink);
            Button scan = PixelMenuBuilder.PixelButton("ScanButton", c, frame, "搜索", new Vector2(116, 220), new Vector2(144, 56), Palette.Blue, Palette.Ink, 22);
            Text audio = Left("AudioText", c, "", -576, 172, 720, 32, 20, Palette.Muted);

            // The drum.
            RectTransform drum = PixelMenuBuilder.Rect("Drum", c, new Vector2(-300, -26), new Vector2(ArtW * ArtScale, ArtH * ArtScale));
            Image bodyImage = drum.gameObject.AddComponent<Image>();
            bodyImage.sprite = body;
            bodyImage.raycastTarget = false;
            var faces = new Image[DrumKit.InputCount];
            var labels = new Text[DrumKit.InputCount];
            for (int i = 0; i < DrumKit.PadCount; i++)
            {
                int r = FaceRadius(Pads[i, 2]);
                Image face = PixelMenuBuilder.Box("Pad" + (i + 1), drum, Color.white, null);
                face.sprite = DrawDisc("face_" + r, r);
                PixelMenuBuilder.Place(face.rectTransform, ArtToLocal(Pads[i, 0], Pads[i, 1]), Vector2.one * (2 * r + 1) * ArtScale);
                face.gameObject.AddComponent<CalibrationTapPad>();
                Text number = PixelMenuBuilder.Label("Number", face.transform, (i + 1).ToString(), new Vector2(0, 2), new Vector2(64, 40), 28, Palette.Panel, TextAnchor.MiddleCenter);
                PixelText.Mark(number);
                faces[i] = face; labels[i] = number;
            }
            string[] pedalNames = { "踩镲踏板", "底鼓" };
            for (int p = 0; p < 2; p++)
            {
                Image pedal = PixelMenuBuilder.Box(p == 0 ? "PedalHiHat" : "PedalKick", c, Color.white, frame);
                PixelMenuBuilder.Place(pedal.rectTransform, new Vector2(-416 + p * 232, -302), new Vector2(208, 48));
                PixelMenuBuilder.Shadow(pedal.gameObject, Palette.DropShadow, new Vector2(Px, -Px));
                pedal.gameObject.AddComponent<CalibrationTapPad>();
                labels[DrumKit.HiHatPedal + p] = PixelMenuBuilder.Label("Label", pedal.transform, pedalNames[p], Vector2.zero, new Vector2(196, 44), 22, Palette.Panel, TextAnchor.MiddleCenter);
                faces[DrumKit.HiHatPedal + p] = pedal;
            }

            // Last hits.
            Image hits = PixelMenuBuilder.Box("Hits", c, Palette.Lift(Palette.Panel), frame);
            PixelMenuBuilder.Place(hits.rectTransform, new Vector2(320, 34), new Vector2(528, 264));
            hits.raycastTarget = false;
            Transform h = hits.transform;
            Text hitsTitle = Left("HitsTitle", h, "刚才敲的", -236, 96, 400, 44, 28, Palette.Ink);
            var swatches = new Image[PadTestPopup.LogRows]; var bars = new Image[PadTestPopup.LogRows];
            var names = new Text[PadTestPopup.LogRows]; var values = new Text[PadTestPopup.LogRows];
            for (int row = 0; row < PadTestPopup.LogRows; row++)
            {
                float y = 44 - row * 48;
                swatches[row] = PixelMenuBuilder.Box("Swatch" + row, h, Palette.Orange, frame);
                PixelMenuBuilder.Place(swatches[row].rectTransform, new Vector2(-224, y), new Vector2(Px * 6, Px * 6));
                swatches[row].raycastTarget = false;
                names[row] = Left("Name" + row, h, "", -196, y, 200, 40, 24, Palette.Ink);
                values[row] = Left("Value" + row, h, "", 8, y, 120, 40, 22, Palette.Muted);
                bars[row] = PixelMenuBuilder.Box("Bar" + row, h, Palette.Orange, solid);
                bars[row].type = Image.Type.Simple;
                bars[row].raycastTarget = false;
                bars[row].rectTransform.anchorMin = bars[row].rectTransform.anchorMax = new Vector2(.5f, .5f);
                bars[row].rectTransform.pivot = new Vector2(0, .5f);
                bars[row].rectTransform.anchoredPosition = new Vector2(132, y);
                bars[row].rectTransform.sizeDelta = new Vector2(120, Px * 4);
            }

            // Calibration.
            Image calibration = PixelMenuBuilder.Box("Calibration", c, Palette.Lift(Palette.Panel), frame);
            PixelMenuBuilder.Place(calibration.rectTransform, new Vector2(320, -216), new Vector2(528, 208));
            calibration.raycastTarget = false;
            Transform k = calibration.transform;
            Text calibrationTitle = Left("CalibrationTitle", k, "延迟校准", -236, 68, 300, 44, 28, Palette.Ink);
            Text hint = Left("Hint", k, "", -236, 28, 472, 32, 20, Palette.Muted);
            Text offset = Left("Offset", k, "", -236, -12, 300, 36, 24, Palette.Ink);
            Text prompt = Left("Prompt", k, "", -236, -64, 290, 64, 20, Palette.SkyText);
            prompt.verticalOverflow = VerticalWrapMode.Overflow;
            Button calibrate = PixelMenuBuilder.PixelButton("CalibrateButton", k, frame, "开始校准", new Vector2(156, -56), new Vector2(176, 56), Palette.Orange, Palette.Ink, 22);

            PixelMenuBuilder.Assign(popup, "closeButton", close, "scanButton", scan, "calibrateButton", calibrate, "connectionDot", dot,
                "title", title, "connectionText", connection, "audioText", audio, "hitsTitle", hitsTitle, "calibrationTitle", calibrationTitle,
                "calibrationHint", hint, "offsetText", offset, "promptText", prompt);
            PixelMenuBuilder.AssignArray(popup, "faces", faces);
            PixelMenuBuilder.AssignArray(popup, "faceLabels", labels);
            PixelMenuBuilder.AssignArray(popup, "logSwatches", swatches);
            PixelMenuBuilder.AssignArray(popup, "logBars", bars);
            PixelMenuBuilder.AssignArray(popup, "logNames", names);
            PixelMenuBuilder.AssignArray(popup, "logValues", values);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log("PAD_TEST_READY");
        }

        public static int FaceRadius(int padRadius) { return Mathf.RoundToInt(padRadius * FaceRatio); }

        // Art pixel (x right, y down) to the Drum rect's local position.
        private static Vector2 ArtToLocal(int x, int y) { return new Vector2((x + .5f - ArtW / 2f) * ArtScale, (ArtH / 2f - y - .5f) * ArtScale); }

        private static Text Left(string name, Transform parent, string value, float left, float y, float w, float h, int size, Color color)
        {
            return PixelMenuBuilder.Label(name, parent, value, new Vector2(left + w / 2, y), new Vector2(w, h), size, color, TextAnchor.MiddleLeft);
        }

        // r*r + 0.8r instead of r*r: no single-pixel bumps at the top, bottom and sides of pixel circles.
        private static bool InCircle(int x, int y, int cx, int cy, float r) { return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r + r * .8f; }

        // Disc: 'k' ink outline around 'body' (filled by the caller's chooser of distance -> key).
        private static void Disc(char[][] g, int cx, int cy, int r, System.Func<float, char> fill)
        {
            for (int y = cy - r - 1; y <= cy + r + 1; y++)
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    if (!InCircle(x, y, cx, cy, r)) continue;
                    bool edge = !InCircle(x + 1, y, cx, cy, r) || !InCircle(x - 1, y, cx, cy, r) || !InCircle(x, y + 1, cx, cy, r) || !InCircle(x, y - 1, cx, cy, r);
                    g[y][x] = edge ? 'k' : fill(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)));
                }
        }

        private static Sprite DrawBody()
        {
            var g = new char[ArtH][];
            for (int y = 0; y < ArtH; y++) { g[y] = new char[ArtW]; for (int x = 0; x < ArtW; x++) g[y][x] = '.'; }
            // Client reply v6 §6-1: no drum body, only the eight pads (and the pedals, built as buttons)
            // at the product's proportions: white rim, ink outline, grey face.
            for (int i = 0; i < Pads.GetLength(0); i++)
            {
                int cx = Pads[i, 0], cy = Pads[i, 1], f = FaceRadius(Pads[i, 2]);
                Disc(g, cx, cy, Pads[i, 2], d => 'r');
                // Same disc as the face sprite laid over it, so no unlit pixels peek out around a lit face.
                for (int y = -f; y <= f; y++)
                    for (int x = -f; x <= f; x++)
                        if (InCircle(x + f, y + f, f, f, f)) g[cy + y][cx + x] = 'f';
            }
            // Soft drop shadow two pixels down-right.
            for (int y = ArtH - 3; y >= 0; y--)
                for (int x = ArtW - 3; x >= 0; x--)
                    if (g[y][x] != '.' && g[y][x] != 's' && g[y + 2][x + 2] == '.') g[y + 2][x + 2] = 's';
            var rows = new string[ArtH];
            for (int y = 0; y < ArtH; y++) rows[y] = new string(g[y]);
            return PixelArt.Save(ArtDir, "bx2_pads", rows, new Dictionary<char, Color32> {
                { 'b', new Color32(58, 54, 70, 255) }, { 'e', new Color32(34, 32, 44, 255) }, { 's', new Color32(34, 34, 34, 70) },
                { 'k', new Color32(34, 34, 34, 255) }, { 'r', new Color32(250, 249, 255, 255) }, { 'f', new Color32(84, 80, 98, 255) },
                { 'n', new Color32(40, 38, 50, 255) }, { 'm', new Color32(210, 206, 222, 255) }, { 'd', new Color32(24, 24, 30, 255) },
                { 'l', new Color32(232, 98, 98, 255) }, { 'p', new Color32(250, 249, 255, 255) } });
        }

        // White disc the size of a pad's face, tinted at run time (idle grey, lit pastel).
        private static Sprite DrawDisc(string name, int r)
        {
            int size = 2 * r + 1;
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var row = new char[size];
                for (int x = 0; x < size; x++) row[x] = InCircle(x, y, r, r, r) ? 'w' : '.';
                rows[y] = new string(row);
            }
            return PixelArt.Save(ArtDir, name, rows, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) } });
        }
    }
}

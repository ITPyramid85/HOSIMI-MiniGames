using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Client reply v5 §4: body text and buttons use a large, readable system font so children can read
    // them at once; the pixel font (fontOverride, MonoBit) stays only on texts marked PixelText — numbers,
    // scores and short game words. Small readable text is raised to MinReadableSize and shrinks back only
    // if it would not fit its box.
    public sealed class MenuTypography : MonoBehaviour
    {
        public const int MinReadableSize = 20;
        [SerializeField] private Font fontOverride;
        private static readonly Dictionary<GameLanguage, Font> readable = new Dictionary<GameLanguage, Font>();

        private void Awake() { Apply(); }

        public void Apply()
        {
            Font body = ReadableFont(Strings.CurrentLanguage);
            Font pixel = fontOverride != null ? fontOverride : body;
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                if (label.GetComponent<PixelText>() != null) { label.font = pixel; continue; }
                label.font = body;
                if (label.fontSize >= MinReadableSize || label.resizeTextForBestFit) continue;
                label.resizeTextMaxSize = MinReadableSize;
                label.resizeTextMinSize = Mathf.Min(label.fontSize, 12);
                label.fontSize = MinReadableSize;
                label.resizeTextForBestFit = true;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            PixelChrome.Apply(transform);
        }

        // Per language, so Japanese gets Japanese glyph shapes and Korean a Hangul face first. Names cover
        // Android (Noto CJK), iOS / macOS and Windows; the editor uses whichever exists.
        public static Font ReadableFont(GameLanguage language)
        {
            Font font;
            if (readable.TryGetValue(language, out font) && font != null) return font;
            string[] first =
                language == GameLanguage.Japanese ? new[] { "Noto Sans CJK JP", "NotoSansCJKjp-Regular", "Hiragino Sans", "Yu Gothic UI", "Meiryo" } :
                language == GameLanguage.Korean ? new[] { "Noto Sans CJK KR", "NotoSansCJKkr-Regular", "Apple SD Gothic Neo", "Malgun Gothic" } :
                new[] { "Noto Sans CJK SC", "NotoSansCJKsc-Regular", "PingFang SC", "Microsoft YaHei" };
            var names = new List<string>(first);
            names.AddRange(new[] { "NotoSansCJK-Regular", "Noto Sans CJK SC", "Microsoft YaHei", "Malgun Gothic", "Roboto", "Arial" });
            font = Font.CreateDynamicFontFromOSFont(names.ToArray(), 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            readable[language] = font;
            return font;
        }
    }
}

using UnityEngine;

namespace RhythmGame
{
    // The one place UI colours come from. Client reply v5 §8: four pastel main colours (purple, red,
    // blue, orange) with black and white; the client's example values are used for the pastels.
    // Light surfaces carry near-black text; pastel fills carry Ink text too.
    public static class Palette
    {
        // Main colours
        public static readonly Color Purple = new Color32(201, 182, 240, 255);   // #C9B6F0
        public static readonly Color Red = new Color32(244, 163, 163, 255);      // #F4A3A3
        public static readonly Color Blue = new Color32(167, 199, 242, 255);     // #A7C7F2
        public static readonly Color Orange = new Color32(248, 199, 154, 255);   // #F8C79A

        // Surfaces
        public static readonly Color Background = new Color32(251, 250, 255, 255);   // white with a breath of purple
        public static readonly Color GridLine = Purple;
        public static readonly Color Panel = new Color32(255, 255, 255, 255);
        public static readonly Color Well = new Color32(242, 240, 248, 255);          // inset / unlit pad
        public static readonly Color Dim = new Color32(34, 34, 34, 90);                // modal blocker

        // Text
        public static readonly Color Ink = new Color32(34, 34, 34, 255);               // #222222
        public static readonly Color Muted = new Color32(118, 114, 130, 255);
        public static readonly Color OnColor = new Color32(255, 255, 255, 255);        // text on dark fills

        // Roles (kept as names the screens already use)
        public static readonly Color Sun = Orange;     // primary action
        public static readonly Color Sky = Blue;
        public static readonly Color Mint = Purple;    // owned / success
        public static readonly Color Pink = Red;
        public static readonly Color Coral = new Color32(236, 128, 128, 255);   // lives, warnings: a deeper pastel red
        public static readonly Color Grape = Purple;

        // Coloured text that must read on white
        public static readonly Color SkyText = new Color32(60, 110, 186, 255);
        public static readonly Color MintText = new Color32(116, 84, 196, 255);
        public static readonly Color SunText = new Color32(204, 112, 40, 255);
        public static readonly Color DangerText = new Color32(204, 72, 80, 255);
        public static readonly Color WarnText = new Color32(204, 112, 40, 255);

        // Effects
        public static readonly Color TextShadow = Orange;                              // under big Ink text
        public static readonly Color DropShadow = new Color32(34, 34, 34, 56);         // under cards and buttons
        public static readonly Color Shutter = Purple;                                  // screen-transition bars

        // Beat bars and other repeating accents cycle through the four main colours.
        public static readonly Color[] Rainbow = { Purple, Red, Blue, Orange };

        public static string Hex(Color color) { return "#" + ColorUtility.ToHtmlStringRGB(color); }

        // Grey level of the frame sprite's fill texels. At 1 a tint shows exactly as given, which the
        // light palette needs (a darker fill could never reach white).
        public const float FrameFill = 1f;

        // Tint that lands on `color` once multiplied by the frame fill.
        public static Color Lift(Color color)
        {
            return new Color(Mathf.Min(1, color.r / FrameFill), Mathf.Min(1, color.g / FrameFill), Mathf.Min(1, color.b / FrameFill), color.a);
        }
    }
}

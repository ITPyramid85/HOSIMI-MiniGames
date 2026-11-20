using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Marks a Text that keeps the pixel font (numbers, scores, short game words); MenuTypography gives
    // every other Text the readable font.
    [RequireComponent(typeof(Text))]
    public sealed class PixelText : MonoBehaviour
    {
        public static Text Mark(Text text)
        {
            if (text.GetComponent<PixelText>() == null) text.gameObject.AddComponent<PixelText>();
            return text;
        }
    }
}

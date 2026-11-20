using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    public sealed class RhythmNoteView : MonoBehaviour
    {
        [SerializeField] private Image head;
        [SerializeField] private Image tail;
        [SerializeField] private Text label;
        private BeatmapNote note;
        private int first, last;
        private RectTransform rect;
        private Color color;

        public void Initialize(BeatmapNote value, int firstIndex, int lastIndex)
        {
            note = value; first = firstIndex; last = lastIndex;
            rect = (RectTransform)transform;
            color = note.kind == RhythmNoteKind.Hold ? Palette.Grape :
                note.kind == RhythmNoteKind.Swipe ? Palette.Coral : Palette.Sky;
            head.color = color;
            tail.color = new Color(color.r, color.g, color.b, .35f);
            NoteShapeGraphic shape = head as NoteShapeGraphic;
            if (shape != null) shape.SetKind(note.kind);
            label.gameObject.SetActive(false);
            tail.gameObject.SetActive(note.kind == RhythmNoteKind.Hold);
        }

        public void Render(double time, double travelSeconds, RhythmInputJudge judge)
        {
            bool visible = time >= note.time - travelSeconds - .15 && !judge.IsResolved(last);
            head.gameObject.SetActive(visible);
            tail.gameObject.SetActive(visible && note.kind == RhythmNoteKind.Hold);
            if (!visible) return;
            const float lineY = -173, distance = 413;
            float y = lineY + (float)((note.time - time) / travelSeconds * distance);
            // An unjudged hold head can still travel below the line during its late-hit window.
            if (note.kind == RhythmNoteKind.Hold && time >= note.time && judge.IsResolved(first)) y = lineY;
            rect.anchoredPosition = new Vector2(0, y);
            if (note.kind == RhythmNoteKind.Hold)
            {
                float endY = lineY + (float)((note.time + note.duration - time) / travelSeconds * distance);
                float remaining = Mathf.Max(0, endY - y);
                tail.rectTransform.sizeDelta = new Vector2(17, remaining);
                head.color = judge.IsHeld && time >= note.time ? Color.white : color;
            }
        }
    }
}

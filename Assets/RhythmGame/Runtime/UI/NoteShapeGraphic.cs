using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // UI geometry stays crisp at any canvas scale and needs no external sprite assignment.
    public sealed class NoteShapeGraphic : Image
    {
        [SerializeField] private RhythmNoteKind kind;

        public void SetKind(RhythmNoteKind value)
        {
            kind = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            Vector2 center = bounds.center;
            Vector2 size = bounds.size;
            Color glow = color; glow.a *= .14f;
            Color inner = Color.Lerp(color, Palette.Panel, .6f);
            inner.a = color.a;
            Shape(mesh, center, size, glow);
            Shape(mesh, center, size * .88f, color);
            Shape(mesh, center, size * .72f, inner);
            Color mark = Color.Lerp(color, Palette.Ink, .35f); mark.a = color.a;
            if (kind == RhythmNoteKind.Hold)
            {
                Box(mesh, center + Vector2.left * size.x * .10f, new Vector2(size.x * .06f, size.y * .32f), mark);
                Box(mesh, center + Vector2.right * size.x * .10f, new Vector2(size.x * .06f, size.y * .32f), mark);
            }
            else if (kind == RhythmNoteKind.Tap) Box(mesh, center, size * .15f, mark);
        }

        private void Shape(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            if (kind != RhythmNoteKind.Swipe) { Box(mesh, center, size, tint); return; }
            int start = mesh.currentVertCount;
            mesh.AddVert(center + new Vector2(-size.x / 2, -size.y / 2), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(0, size.y / 2), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(size.x / 2, -size.y / 2), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Box(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            Vector2 half = size * .5f;
            Quad(mesh, center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y),
                center + half, center + new Vector2(half.x, -half.y), tint);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}

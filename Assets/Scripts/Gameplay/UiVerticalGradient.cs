using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Vertical colour gradient for a UI Graphic (title lettering: light gold on top, deep amber below).
    /// Put it BEFORE Outline/Shadow components so their copies keep their own flat colour.
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Gradient")]
    public sealed class UiVerticalGradient : BaseMeshEffect
    {
        [SerializeField] private Color top = new Color(1f, 0.97f, 0.72f);
        [SerializeField] private Color middle = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color bottom = new Color(0.86f, 0.45f, 0.1f);
        [Tooltip("true: each glyph/quad gets the full gradient (metal look per letter); false: across the whole text.")]
        [SerializeField] private bool perQuad = true;

        private static readonly List<UIVertex> verts = new List<UIVertex>();

        public void Set(Color t, Color m, Color b, bool quad = true)
        {
            top = t; middle = m; bottom = b; perQuad = quad;
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            if (perQuad)
            {
                for (int i = 0; i + 5 < verts.Count; i += 6) Paint(i, 6);
            }
            else Paint(0, verts.Count);
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        private void Paint(int start, int count)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = start; i < start + count; i++) { min = Mathf.Min(min, verts[i].position.y); max = Mathf.Max(max, verts[i].position.y); }
            float h = Mathf.Max(0.0001f, max - min);
            for (int i = start; i < start + count; i++)
            {
                UIVertex v = verts[i];
                float k = (v.position.y - min) / h; // 0 bottom .. 1 top
                Color c = k > 0.5f ? Color.Lerp(middle, top, (k - 0.5f) * 2f) : Color.Lerp(bottom, middle, k * 2f);
                c.a *= v.color.a / 255f;
                v.color = c;
                verts[i] = v;
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Runtime counterpart of the editor layout kit (EntryLayoutKit) for UI that is built at runtime (fashion shop):
    /// measured 9-slice content areas, layout-group stacks/rows with fixed sizes, one-line labels that shrink to fit.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Left, bottom, right, top thickness of the border Unity draws for a sliced Image (local units).</summary>
        public static Vector4 DrawnBorder(Image img)
        {
            var vh = new VertexHelper();
            typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null)
                .Invoke(img, new object[] { vh });
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            vh.Dispose();
            var xs = verts.Select(v => Mathf.Round(v.position.x * 100f) / 100f).Distinct().OrderBy(x => x).ToList();
            var ys = verts.Select(v => Mathf.Round(v.position.y * 100f) / 100f).Distinct().OrderBy(y => y).ToList();
            if (xs.Count < 4 || ys.Count < 4) return Vector4.zero;
            Rect r = img.rectTransform.rect;
            return new Vector4(xs[1] - r.xMin, ys[1] - r.yMin, r.xMax - xs[xs.Count - 2], r.yMax - ys[ys.Count - 2]);
        }

        /// <summary>Child rect inside a sliced/framed image: past the drawn border, plus padding.</summary>
        public static RectTransform Content(Image framed, float pad)
        {
            Vector4 b = framed.type == Image.Type.Sliced && framed.sprite ? DrawnBorder(framed) : Vector4.zero;
            var go = new GameObject("Content", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(framed.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(b.x + pad, b.y + pad);
            r.offsetMax = new Vector2(-(b.z + pad), -(b.w + pad));
            return r;
        }

        public static VerticalLayoutGroup VStack(Transform t, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static RectTransform HRow(Transform parent, string name, float height, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            Size(go.transform, height);
            return (RectTransform)go.transform;
        }

        public static LayoutElement Size(Component c, float height, float width = -1f, float flexW = -1f, float minW = -1f, float flexH = 0f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            if (height > 0f) { le.preferredHeight = height; le.minHeight = height; }
            le.flexibleHeight = flexH;
            if (width > 0f) le.preferredWidth = width;
            if (flexW >= 0f) le.flexibleWidth = flexW;
            if (minW > 0f) le.minWidth = minW;
            return le;
        }

        /// <summary>Gem button skin (FrameArt sprite name) for a role colour: gold = primary, green/teal = confirm,
        /// red = danger, white = Google, saturated blue = Facebook, violet = premium, greys = neutral, clear = ghost.</summary>
        public static string SkinFor(Color bg)
        {
            if (bg.a < 0.05f) return "btn_ghost";
            Color.RGBToHSV(bg, out float h, out float s, out float v);
            if (s < 0.12f && v > 0.85f) return "btn_white";
            if (s < 0.3f) return "btn_slate";
            if (h < 0.04f || h > 0.94f) return "btn_crimson";
            if (h < 0.17f) return "btn_gold";
            if (h < 0.55f) return "btn_jade"; // greens and teals: confirm / continue
            if (h < 0.64f) return s > 0.55f && v > 0.6f ? "btn_azure" : "btn_slate";
            return "btn_royal";
        }

        /// <summary>Label colour that reads on a gem skin (dark ink on gold/white, cream elsewhere).</summary>
        public static Color InkFor(string skin) =>
            skin == "btn_gold" ? new Color(0.24f, 0.13f, 0.03f) : skin == "btn_white" ? new Color(0.15f, 0.17f, 0.2f) : new Color(1f, 0.97f, 0.9f);

        /// <summary>Insets inside the CURRENT anchors (keeps e.g. a 0..0.36 column anchor).</summary>
        public static void Inset(RectTransform r, float l, float b, float rr, float t)
        {
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        }

        public static void Stretch(RectTransform r, float l = 0f, float b = 0f, float rr = 0f, float t = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        }
    }
}

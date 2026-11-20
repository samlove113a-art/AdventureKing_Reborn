using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AdventureKing.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Shared layout rules for every builder-made screen (entry, HUD, dialogs). The root cause of the old
    /// "title on the ornament" bugs was hand-placed coordinates inside 9-sliced frames whose drawn border band
    /// was never measured. Here:
    ///  - <see cref="DrawnBorder"/> measures the band Unity really draws (from the generated 9-slice mesh);
    ///  - <see cref="SetBand"/> sizes a frame so its top band has a chosen thickness;
    ///  - <see cref="FramedContent"/> returns a child rect that starts INSIDE the ornament (+ padding);
    ///  - VStack / HRow / Size put content in layout groups, so text and buttons flow instead of overlapping;
    ///  - labels inside buttons/fields are inset from the gilded rim and best-fit (never spill).
    /// The UiAudit PlayMode test checks the result at PC / phone / tablet resolutions.
    /// </summary>
    public static partial class EntrySceneBuilder
    {
        /// <summary>
        /// Full-screen fantasy backdrop (LoginBackdropBaker layers: sky, hills, mid, near) under every entry screen.
        /// Each layer is envelope-fitted to the screen (cropped, never stretched or letterboxed) and slightly
        /// enlarged so the parallax drift never reveals an edge.
        /// </summary>
        internal static void BuildBackdrop(Transform canvas)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(canvas, false);
            go.transform.SetAsFirstSibling();
            Stretch((RectTransform)go.transform);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.98f, 0.84f, 0.74f); // dawn haze colour if a layer were ever missing
            bg.raycastTarget = false;
            string[] names = { "sky", "hills", "mid", "near" };
            float[] depth = { 0.12f, 0.3f, 0.62f, 1f };
            var layers = new System.Collections.Generic.List<RawImage>();
            foreach (string n in names)
            {
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{LoginBackdropBaker.Dir}/{n}.png");
                if (!tex) throw new System.IO.FileNotFoundException("Login backdrop layer missing — run LoginBackdropBaker.BakeFromCommandLine", n);
                var l = new GameObject("Layer_" + n, typeof(RectTransform));
                l.layer = 5;
                l.transform.SetParent(go.transform, false);
                var raw = l.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                var fit = l.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
                l.transform.localScale = Vector3.one * 1.07f;
                layers.Add(raw);
            }
            var lb = go.AddComponent<LoginBackdrop>();
            var so = new UnityEditor.SerializedObject(lb);
            var lp = so.FindProperty("layers");
            var dp = so.FindProperty("depth");
            lp.arraySize = layers.Count;
            dp.arraySize = layers.Count;
            for (int i = 0; i < layers.Count; i++)
            {
                lp.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
                dp.GetArrayElementAtIndex(i).floatValue = depth[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Left, bottom, right, top thickness of the border Unity draws for a sliced Image (local units).</summary>
        internal static Vector4 DrawnBorder(Image img)
        {
            Canvas.ForceUpdateCanvases();
            var vh = new VertexHelper();
            MethodInfo m = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
            m.Invoke(img, new object[] { vh });
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            vh.Dispose();
            var xs = verts.Select(v => Mathf.Round(v.position.x * 100f) / 100f).Distinct().OrderBy(x => x).ToList();
            var ys = verts.Select(v => Mathf.Round(v.position.y * 100f) / 100f).Distinct().OrderBy(y => y).ToList();
            if (xs.Count < 4 || ys.Count < 4) return Vector4.zero;
            Rect r = img.rectTransform.rect;
            return new Vector4(xs[1] - r.xMin, ys[1] - r.yMin, r.xMax - xs[xs.Count - 2], r.yMax - ys[ys.Count - 2]);
        }

        /// <summary>Adjusts the frame's pixelsPerUnitMultiplier so the drawn TOP band is <paramref name="topBand"/> units.</summary>
        internal static Vector4 SetBand(Image img, float topBand)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector4 b = DrawnBorder(img);
                if (b.w <= 0.01f) return b;
                float k = b.w / topBand;
                if (Mathf.Abs(k - 1f) < 0.01f) return b;
                img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, img.pixelsPerUnitMultiplier * k);
            }
            return DrawnBorder(img);
        }

        /// <summary>Content rect inside a framed panel: starts past the drawn ornament band, plus padding.</summary>
        internal static RectTransform FramedContent(GameObject framed, float pad, float topBand = 0f)
        {
            var img = framed.GetComponent<Image>();
            Vector4 b = topBand > 0f ? SetBand(img, topBand) : DrawnBorder(img);
            var go = new GameObject("Content", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(framed.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(b.x + pad, b.y + pad);
            r.offsetMax = new Vector2(-(b.z + pad), -(b.w + pad));
            Debug.Log($"[UI] {framed.name}: drawn border L{b.x:0} B{b.y:0} R{b.z:0} T{b.w:0}, content {r.rect.size}");
            return r;
        }

        internal static VerticalLayoutGroup VStack(Transform t, float spacing, TextAnchor align = TextAnchor.UpperCenter, RectOffset pad = null)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.padding = pad ?? new RectOffset(0, 0, 0, 0);
            return v;
        }

        internal static RectTransform HRow(Transform parent, string name, float height, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            Size(go.transform, height);
            return (RectTransform)go.transform;
        }

        /// <summary>Preferred/min size for a layout child (height; optional width; flexible width share).</summary>
        internal static LayoutElement Size(Component c, float height, float width = -1f, float flexW = -1f, float minW = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (!le) le = c.gameObject.AddComponent<LayoutElement>(); // (no ?? : Unity fake-null in the editor)
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f; // never absorb spare height (a lone button must not balloon)
            if (width > 0f) le.preferredWidth = width;
            if (flexW >= 0f) le.flexibleWidth = flexW;
            if (minW > 0f) le.minWidth = minW;
            return le;
        }

        /// <summary>A label that fills its parent (with insets) and shrinks to fit instead of spilling.</summary>
        internal static Text FillLabel(Transform parent, string name, string text, int size, Color c, FontStyle style, float insetX, float insetY, int minSize = 14)
        {
            Text t = Label(parent, name, text, size, c, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, style);
            var r = t.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(insetX, insetY);
            r.offsetMax = new Vector2(-insetX, -insetY);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(minSize, size);
            t.resizeTextMaxSize = size;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        /// <summary>Text block for layout groups (wraps, best-fit, never spills its preferred height).</summary>
        internal static Text BlockLabel(Transform parent, string name, string text, int size, Color c, float height, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Text t = Label(parent, name, text, size, c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, height), style);
            t.alignment = align;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(16, size);
            t.resizeTextMaxSize = size;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Size(t, height);
            return t;
        }
    }
}

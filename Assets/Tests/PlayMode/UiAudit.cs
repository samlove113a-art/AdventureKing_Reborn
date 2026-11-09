using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Automated UI layout audit, run on the REAL screens at a given resolution (canvases are laid out for that
    /// render size, exactly as CanvasScaler would on such a screen), then captured to PNG for visual review.
    /// It measures the glyphs actually generated (not just the RectTransforms) and reports:
    ///  - text truncated or spilling out of its box (best-fit/wrap included);
    ///  - text whose glyphs overlap another text's glyphs (chồng chữ);
    ///  - text drawn on a decorative frame's border band (chữ trên hoa văn);
    ///  - text/buttons outside the screen or the simulated safe area;
    ///  - buttons too small to tap, or covered by another raycast target at their centre.
    /// Every problem is listed with the object path, so a failure says exactly what to fix.
    /// </summary>
    public static class UiAudit
    {
        public sealed class Report
        {
            public readonly List<string> Problems = new List<string>();
            public int Texts, Buttons;
            public override string ToString() => Problems.Count == 0 ? $"OK ({Texts} texts, {Buttons} buttons)" : string.Join("\n", Problems);
        }

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool Visible(Graphic g)
        {
            if (!g || !g.isActiveAndEnabled || g.color.a < 0.05f) return false;
            for (Transform t = g.transform; t; t = t.parent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg && cg.alpha < 0.05f) return false;
            }
            var rect = g.rectTransform.rect;
            return rect.width > 0.5f && rect.height > 0.5f;
        }

        /// <summary>Rect in the root canvas's local space.</summary>
        private static Rect InCanvas(RectTransform r, Transform canvas)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            Vector2 a = canvas.InverseTransformPoint(c[0]), b = canvas.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Tight bounds of the glyphs the Text really draws, in root canvas space (null = draws nothing).</summary>
        public static Rect? GlyphBounds(Text t, Transform canvas, out int visible, out int full)
        {
            visible = full = 0;
            if (string.IsNullOrEmpty(t.text)) return null;
            Vector2 size = t.rectTransform.rect.size;
            TextGenerationSettings st = t.GetGenerationSettings(size);
            var gen = new TextGenerator();
            gen.PopulateWithErrors(t.text, st, t.gameObject);
            visible = gen.characterCountVisible;
            var stO = t.GetGenerationSettings(size);
            stO.verticalOverflow = VerticalWrapMode.Overflow;
            stO.horizontalOverflow = t.horizontalOverflow;
            var genO = new TextGenerator();
            genO.PopulateWithErrors(t.text, stO, t.gameObject);
            full = genO.characterCountVisible;
            IList<UIVertex> verts = gen.verts;
            if (verts.Count == 0 || visible == 0) return null;
            float unit = 1f / t.pixelsPerUnit;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Count; i += 4)
            {
                // skip whitespace quads (zero area)
                Vector3 p0 = verts[i].position, p2 = verts[i + 2].position;
                if (Mathf.Abs(p2.x - p0.x) < 0.01f || Mathf.Abs(p2.y - p0.y) < 0.01f) continue;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 w = t.rectTransform.TransformPoint(verts[i + k].position * unit);
                    Vector2 c = canvas.InverseTransformPoint(w);
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                }
            }
            if (minX == float.MaxValue) return null;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Painted ornament depth (sprite px from the edge; left, bottom, right, top) of the purpose-made panel skins
        /// (FrameArt). Text may not cross it. Titles in a skin's own title band are fine (popup_card: band = top 58 px).
        /// </summary>
        private static readonly Dictionary<string, Vector4> SkinOrnament = new Dictionary<string, Vector4>
        {
            { "auth_card", new Vector4(32, 32, 32, 32) }, { "hall_panel", new Vector4(20, 20, 20, 20) },
            { "popup_card", new Vector4(8, 8, 8, 6) }, { "settings_card", new Vector4(22, 22, 22, 22) },
            { "parchment", new Vector4(20, 20, 20, 20) }, { "shop_window", new Vector4(30, 30, 30, 30) },
            { "minimap_frame", new Vector4(12, 12, 12, 12) }, { "hud_plate", new Vector4(6, 6, 40, 7) },
        };

        private static bool IsFrame(Image img) =>
            img && img.sprite && img.type == Image.Type.Sliced && img.sprite.border.sqrMagnitude > 1f &&
            (img.sprite.name.StartsWith("frame_") || img.sprite.name.Contains("frame") || SkinOrnament.ContainsKey(img.sprite.name));

        /// <summary>Inner (content) rect of a 9-sliced frame: its rect minus the border band Unity really draws
        /// (measured from the generated 9-slice mesh, so it is exact for any multiplier / size).</summary>
        private static Rect FrameInner(Image img, Rect r, Canvas root)
        {
            var vh = new VertexHelper();
            typeof(Image).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null).Invoke(img, new object[] { vh });
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            vh.Dispose();
            var xs = verts.Select(v => Mathf.Round(v.position.x * 100f) / 100f).Distinct().OrderBy(x => x).ToList();
            var ys = verts.Select(v => Mathf.Round(v.position.y * 100f) / 100f).Distinct().OrderBy(y => y).ToList();
            if (xs.Count < 4 || ys.Count < 4) return r; // not sliced: no separate border band
            Rect lr = img.rectTransform.rect;
            float scale = r.width / Mathf.Max(0.0001f, lr.width); // canvas units per local unit
            float l = (xs[1] - lr.xMin) * scale, b = (ys[1] - lr.yMin) * scale, ri = (lr.xMax - xs[xs.Count - 2]) * scale, t = (lr.yMax - ys[ys.Count - 2]) * scale;
            if (SkinOrnament.TryGetValue(img.sprite.name, out Vector4 orn))
            {
                // drawn border band in canvas units / sprite border px = how big one sprite pixel is drawn
                Vector4 sb = img.sprite.border;
                float kx = sb.x > 0f ? l / sb.x : 0f, ky = sb.w > 0f ? t / sb.w : 0f;
                l = orn.x * kx; ri = orn.z * kx; b = orn.y * ky; t = orn.w * ky;
            }
            return Rect.MinMaxRect(r.xMin + l, r.yMin + b, r.xMax - ri, r.yMax - t);
        }
        private static int Order(Transform t, Dictionary<Transform, int> order) => order.TryGetValue(t, out int o) ? o : -1;

        /// <summary>
        /// Audits every active root canvas. <paramref name="safeInset"/> is the fraction of width reserved on each
        /// side for notches/rounded corners (mobile); buttons and text must stay inside it.
        /// </summary>
        public static Report Run(float safeInset = 0f, float minButtonPx = 40f, float pixelsPerCanvasUnit = 1f)
        {
            var rep = new Report();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace || !canvas.isActiveAndEnabled) continue;
                Transform ct = canvas.transform;
                Rect screen = ((RectTransform)ct).rect;
                Rect safe = Rect.MinMaxRect(screen.xMin + screen.width * safeInset, screen.yMin, screen.xMax - screen.width * safeInset, screen.yMax);
                float px = canvas.scaleFactor * pixelsPerCanvasUnit;

                // draw order = hierarchy depth-first order
                var order = new Dictionary<Transform, int>();
                int n = 0;
                foreach (Transform t in ct.GetComponentsInChildren<Transform>(false)) order[t] = n++;

                var frames = ct.GetComponentsInChildren<Image>(false).Where(i => Visible(i) && IsFrame(i))
                    .Select(i => (img: i, rect: InCanvas(i.rectTransform, ct))).ToList();
                var plates = ct.GetComponentsInChildren<Image>(false).Where(i => Visible(i) && !IsFrame(i) && i.color.a > 0.85f)
                    .Select(i => (img: i, rect: InCanvas(i.rectTransform, ct))).ToList();

                // occluders: opaque images (cards, frames, plates) and full-screen modal blockers (dim layers)
                bool SeeThrough(Image i) => i.sprite && (i.sprite.name.Contains("rim") || i.sprite.name.Contains("glow") || i.sprite.name.Contains("frame_bar"));
                var opaque = ct.GetComponentsInChildren<Image>(false).Where(i => Visible(i) && i.color.a > 0.85f && !SeeThrough(i))
                    .Select(i => (o: Order(i.transform, order), r: InCanvas(i.rectTransform, ct), t: i.transform)).ToList();
                float screenArea = screen.width * screen.height;
                var modal = ct.GetComponentsInChildren<Image>(false).Where(i => Visible(i) && i.raycastTarget && i.color.a > 0.3f)
                    .Select(i => (o: Order(i.transform, order), r: InCanvas(i.rectTransform, ct))).Where(m => m.r.width * m.r.height > screenArea * 0.9f).ToList();
                // something drawn above (order > o) hides this region: an opaque image covering it, or a modal blocker
                bool UnderModal(int o) => modal.Any(m => m.o > o); // a dialog / shop is open on top: background is inactive
                bool Hidden(Transform self, int o, Rect region, int below = int.MaxValue) =>
                    opaque.Any(x => x.o > o && x.o < below && !x.t.IsChildOf(self) && x.r.Contains(region.min) && x.r.Contains(region.max));

                var glyphs = new List<(Text t, Rect r)>();
                foreach (Text t in ct.GetComponentsInChildren<Text>(false))
                {
                    if (!Visible(t)) continue;
                    if (t.GetComponentInParent<InputField>() is InputField f && (f.textComponent == t || f.placeholder == t) && !t.enabled) continue;
                    Rect? g = GlyphBounds(t, ct, out int visible, out int full);
                    if (g == null)
                    {
                        // nothing drawn although there is text: the box cannot hold even one line
                        if (full > 0) rep.Problems.Add($"TRUNCATED: {PathOf(t.transform)} shows 0/{full} chars ('{Short(t.text)}')");
                        continue;
                    }
                    if (UnderModal(Order(t.transform, order))) continue; // background behind an open dialog/shop
                    if (t.GetComponentInParent<RectMask2D>() is RectMask2D clip && !InCanvas((RectTransform)clip.transform, ct).Overlaps(g.Value)) continue; // scrolled out of view
                    if (Hidden(t.transform, Order(t.transform, order), g.Value))
                    {
                        int to = Order(t.transform, order);
                        var by = opaque.FirstOrDefault(x => x.o > to && !x.t.IsChildOf(t.transform) && x.r.Contains(g.Value.min) && x.r.Contains(g.Value.max));
                        rep.Problems.Add($"HIDDEN TEXT: {PathOf(t.transform)} is covered by a panel drawn on top ({(by.t ? PathOf(by.t) : "?")})");
                        continue;
                    }
                    rep.Texts++;
                    string p = PathOf(t.transform);
                    if (visible < full) rep.Problems.Add($"TRUNCATED: {p} shows {visible}/{full} chars ('{Short(t.text)}')");
                    Rect box = InCanvas(t.rectTransform, ct);
                    Rect gr = g.Value;
                    const float slack = 3f;
                    if (gr.xMin < box.xMin - slack || gr.xMax > box.xMax + slack || gr.yMin < box.yMin - slack - t.fontSize * 0.25f || gr.yMax > box.yMax + slack + t.fontSize * 0.25f)
                        rep.Problems.Add($"SPILLS BOX: {p} glyphs {Fmt(gr)} outside its rect {Fmt(box)}");
                    if (gr.xMin < safe.xMin - 1f || gr.xMax > safe.xMax + 1f || gr.yMin < screen.yMin - 1f || gr.yMax > screen.yMax + 1f)
                        rep.Problems.Add($"OFF SCREEN/SAFE AREA: {p} glyphs {Fmt(gr)} vs safe {Fmt(safe)}");
                    // on a frame's ornamental border band, unless it sits on its own opaque plate drawn above the frame
                    foreach (var (img, fr) in frames)
                    {
                        if (!fr.Overlaps(gr)) continue;
                        Rect inner = FrameInner(img, fr, canvas);
                        bool inside = inner.Contains(gr.min) && inner.Contains(gr.max);
                        if (inside) continue;
                        if (Order(img.transform, order) > Order(t.transform, order)) continue; // frame drawn over the text hides it
                        // allowed only on its own opaque plate drawn above the ornament (e.g. a speaker-name banner)
                        bool onPlate = plates.Any(pl => Order(pl.img.transform, order) > Order(img.transform, order) &&
                                                        pl.rect.Contains(gr.min) && pl.rect.Contains(gr.max));
                        if (!onPlate)
                            rep.Problems.Add($"ON FRAME BORDER: {p} glyphs {Fmt(gr)} cross the ornament of {PathOf(img.transform)} (content area {Fmt(inner)})");
                    }
                    glyphs.Add((t, gr));
                }
                for (int i = 0; i < glyphs.Count; i++)
                    for (int j = i + 1; j < glyphs.Count; j++)
                    {
                        Rect a = glyphs[i].r, b = glyphs[j].r;
                        float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                        if (ox <= 2f || oy <= 2f) continue;
                        int oi = Order(glyphs[i].t.transform, order), oj = Order(glyphs[j].t.transform, order);
                        Rect overlap = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                        if (Hidden(oi < oj ? glyphs[i].t.transform : glyphs[j].t.transform, Mathf.Min(oi, oj), overlap, Mathf.Max(oi, oj))) continue; // the lower text is under a card the upper one sits on
                        rep.Problems.Add($"TEXT OVERLAP: '{Short(glyphs[i].t.text)}' ({PathOf(glyphs[i].t.transform)}) x '{Short(glyphs[j].t.text)}' ({PathOf(glyphs[j].t.transform)}) by {ox:0}x{oy:0}");
                    }

                // buttons: size, inside the safe area, not covered at their centre
                var targets = ct.GetComponentsInChildren<Graphic>(false).Where(g => Visible(g) && g.raycastTarget).ToList();
                foreach (Selectable s in ct.GetComponentsInChildren<Selectable>(false))
                {
                    if (!s.isActiveAndEnabled || !(s.targetGraphic is Graphic tg) || !Visible(tg)) continue;
                    Rect r = InCanvas((RectTransform)s.transform, ct);
                    if (UnderModal(Order(s.transform, order))) continue; // inactive behind an open dialog/shop (intended)
                    if (s.GetComponentInParent<RectMask2D>() is RectMask2D sclip && !InCanvas((RectTransform)sclip.transform, ct).Contains(r.center)) continue; // scrolled out of its view (clipped)
                    rep.Buttons++;
                    string p = PathOf(s.transform);
                    if (Mathf.Min(r.width, r.height) * px < minButtonPx && !(s is Scrollbar))
                        rep.Problems.Add($"TOO SMALL: {p} is {r.width * px:0}x{r.height * px:0} px (< {minButtonPx} px)");
                    if (r.xMin < safe.xMin - 1f || r.xMax > safe.xMax + 1f || r.yMin < screen.yMin - 1f || r.yMax > screen.yMax + 1f)
                        rep.Problems.Add($"OUTSIDE SAFE AREA: {p} {Fmt(r)} vs {Fmt(safe)}");
                    Vector2 centre = r.center;
                    Graphic top = null;
                    int topOrder = -1;
                    foreach (Graphic g in targets)
                    {
                        if (!InCanvas(g.rectTransform, ct).Contains(centre)) continue;
                        if (g.GetComponentInParent<RectMask2D>() is RectMask2D m && !InCanvas((RectTransform)m.transform, ct).Contains(centre)) continue;
                        int o = Order(g.transform, order);
                        if (o > topOrder) { topOrder = o; top = g; }
                    }
                    if (top && top.transform != s.transform && !top.transform.IsChildOf(s.transform))
                        rep.Problems.Add($"COVERED: {p} centre is under {PathOf(top.transform)}");
                }
            }
            return rep;
        }

        private static bool IsAncestor(Transform child, Transform ancestor) => child && ancestor && child.IsChildOf(ancestor) && child != ancestor;
        private static string Short(string s) { s = s.Replace("\n", " "); return s.Length > 40 ? s.Substring(0, 40) + "…" : s; }
        private static string Fmt(Rect r) => $"[{r.xMin:0},{r.yMin:0}..{r.xMax:0},{r.yMax:0}]";

        // ------------------------------------------------------------------ capture at a resolution + audit
        /// <summary>
        /// Lays every overlay canvas out at w x h (Screen Space Camera into a render texture of that size), runs the
        /// audit in that layout, saves the frame to <paramref name="path"/> and restores the canvases.
        /// </summary>
        public static Report CaptureAndAudit(Camera cam, string path, int w, int h, float safeInset = 0f)
        {
            var overlays = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) overlays.Add(c);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            float aspect = cam.aspect;
            int mask = cam.cullingMask;
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.cullingMask = mask | (1 << 5);
            foreach (Canvas c in overlays)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();
            AdventureKing.Gameplay.ResponsiveAnchor.ApplyAll(); // responsive panels follow the new canvas size
            Canvas.ForceUpdateCanvases();
            AdventureKing.Gameplay.FitOneLine.ApplyAll();      // one-line labels re-fit to their new width
            Canvas.ForceUpdateCanvases();
            // input fields show a scrolled window of their text sized to their rect: refresh it for the new size
            foreach (InputField f in Object.FindObjectsByType<InputField>(FindObjectsInactive.Exclude)) f.ForceLabelUpdate();
            Canvas.ForceUpdateCanvases();
            foreach (Canvas c in overlays) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
            Canvas.ForceUpdateCanvases();
            Report rep = Run(safeInset, 40f);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            foreach (Canvas c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            cam.cullingMask = mask;
            cam.targetTexture = null;
            cam.aspect = aspect;
            rt.Release();
            Object.Destroy(rt);
            return rep;
        }

        public static readonly (string name, int w, int h, float safe)[] Resolutions =
        {
            ("pc_1920x1080", 1920, 1080, 0f),
            ("pc_1366x768", 1366, 768, 0f),
            ("phone_19.5x9", 2340, 1080, 0.035f),  // notch / rounded corners on both sides in landscape
            ("phone_16x9", 1280, 720, 0.02f),
            ("tablet_4x3", 2048, 1536, 0.02f),
        };

        /// <summary>Captures one screen at every resolution; returns all problems (prefixed by resolution).</summary>
        public static IEnumerator Screen(string dir, string screen, List<string> problems, Camera cam = null)
        {
            yield return null;
            cam = cam ? cam : Camera.main;
            Assert.IsNotNull(cam, "a camera to render the UI");
            foreach (var (name, w, h, safe) in Resolutions)
            {
                Report r = CaptureAndAudit(cam, $"{dir}/{screen}_{name}.png", w, h, safe);
                foreach (string p in r.Problems) problems.Add($"[{screen} {name}] {p}");
                TestContext.WriteLine($"[{screen} {name}] {r}");
                yield return null;
            }
        }

        public static void WriteReport(string path, List<string> problems)
        {
            var sb = new StringBuilder();
            sb.AppendLine(problems.Count == 0 ? "UI audit: no problems." : $"UI audit: {problems.Count} problems");
            foreach (string p in problems) sb.AppendLine(p);
            File.WriteAllText(path, sb.ToString());
        }
    }
}

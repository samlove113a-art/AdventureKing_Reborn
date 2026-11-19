using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// The 100% 2D entry flow: pixel-art backdrop (parallax panorama + the game's own ground / props + pixel dust), the logo-assembly intro,
    /// the loading screen, the title screen with the server picker, and the 2D hero preview of the character hall.
    /// Everything is Unity UI (Image / RawImage / Text): no camera stage, render texture, light, mesh or model.
    /// </summary>
    public static partial class EntrySceneBuilder
    {
        private const string WorldArt = "Assets/Art/World";
        private const float PixelPpuUi = 101.05f;

        private static Sprite LoadSprite(string path, bool point = true, bool repeat = false)
        {
            if (!File.Exists(path)) return null;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                bool dirty = ti.textureType != TextureImporterType.Sprite || (point && ti.filterMode != FilterMode.Point) || ti.mipmapEnabled
                             || (repeat && ti.wrapMode != TextureWrapMode.Repeat);
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    if (point) ti.filterMode = FilterMode.Point;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    if (repeat) ti.wrapMode = TextureWrapMode.Repeat;
                    ti.textureCompression = point ? TextureImporterCompression.Uncompressed : ti.textureCompression;
                    var st = new TextureImporterSettings();
                    ti.ReadTextureSettings(st);
                    st.spriteMeshType = SpriteMeshType.FullRect;
                    ti.SetTextureSettings(st);
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image PxImg(Transform parent, string name, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size, Image.Type type = Image.Type.Simple)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.type = type;
            img.raycastTarget = false;
            img.preserveAspect = type == Image.Type.Simple;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y < 0.5f ? 0f : 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }

        /// <summary>
        /// Pixel-art backdrop under every entry screen: the map's panorama (cropped to the screen, slow drift), far props, a ground band built
        /// from the game's own ground tiles, near props and drifting pixel dust. A dark vignette keeps panels readable.
        /// </summary>
        internal static void BuildPixelBackdrop(Transform canvas, string mapNo = "01")
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(canvas, false);
            go.transform.SetAsFirstSibling();
            Stretch((RectTransform)go.transform);
            var clip = go.AddComponent<RectMask2D>();
            var bgc = go.AddComponent<Image>();
            bgc.color = new Color(0.05f, 0.04f, 0.09f);
            bgc.raycastTarget = false;

            var layers = new System.Collections.Generic.List<RectTransform>();
            var depth = new System.Collections.Generic.List<float>();

            Sprite pano = LoadSprite($"{WorldArt}/Generated/c1_m{mapNo}_pixel_panorama_v2.png", point: true);
            if (pano)
            {
                // square panorama, 1.5x the screen height so the sway never shows an edge; centre on the mountains / mushrooms (upper-middle)
                var p = PxImg(go.transform, "Panorama", pano, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(2300f, 2300f));
                p.preserveAspect = false;
                layers.Add(p.rectTransform); depth.Add(0.25f);
            }

            // far props on the horizon (drawn small, tinted toward the haze so they sit behind the ground)
            Sprite tree = LoadSprite($"{WorldArt}/PixelProps/ch1_tree.png"), house = LoadSprite($"{WorldArt}/PixelProps/ch1_house.png"),
                lamp = LoadSprite($"{WorldArt}/PixelProps/ch1_lamp.png"), stall = LoadSprite($"{WorldArt}/PixelProps/ch1_stall.png"),
                fountain = LoadSprite($"{WorldArt}/PixelProps/ch1_fountain.png");
            var far = new GameObject("FarProps", typeof(RectTransform));
            far.layer = 5;
            far.transform.SetParent(go.transform, false);
            Stretch((RectTransform)far.transform);
            if (tree) { var t = PxImg(far.transform, "Tree", tree, new Vector2(0.14f, 0f), new Vector2(0f, 150f), new Vector2(tree.rect.width * 0.62f, tree.rect.height * 0.62f)); t.color = new Color(0.78f, 0.74f, 0.9f); }
            if (house) { var h = PxImg(far.transform, "House", house, new Vector2(0.86f, 0f), new Vector2(0f, 150f), new Vector2(house.rect.width * 0.9f, house.rect.height * 0.9f)); h.color = new Color(0.85f, 0.8f, 0.92f); }
            layers.Add((RectTransform)far.transform); depth.Add(0.5f);

            // ground band: the game's own grass lip + earth face, tiled
            Sprite top = LoadSprite($"{WorldArt}/PixelGround/ground_top_m{mapNo}.png", repeat: true), earth = LoadSprite($"{WorldArt}/PixelGround/ground_earth_m{mapNo}.png", repeat: true);
            var ground = new GameObject("GroundBand", typeof(RectTransform));
            ground.layer = 5;
            ground.transform.SetParent(go.transform, false);
            var gr = (RectTransform)ground.transform;
            gr.anchorMin = new Vector2(-0.1f, 0f); gr.anchorMax = new Vector2(1.1f, 0f);
            gr.pivot = new Vector2(0.5f, 0f); gr.sizeDelta = new Vector2(0f, 220f); gr.anchoredPosition = Vector2.zero;
            if (earth)
            {
                var e = PxImg(ground.transform, "Earth", earth, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 150f), Image.Type.Tiled);
                e.rectTransform.anchorMin = new Vector2(0f, 0f); e.rectTransform.anchorMax = new Vector2(1f, 0f); e.rectTransform.sizeDelta = new Vector2(0f, 150f);
                e.pixelsPerUnitMultiplier = 1f;
            }
            if (top)
            {
                var g = PxImg(ground.transform, "GrassLip", top, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(0f, 64f), Image.Type.Tiled);
                g.rectTransform.anchorMin = new Vector2(0f, 0f); g.rectTransform.anchorMax = new Vector2(1f, 0f); g.rectTransform.sizeDelta = new Vector2(0f, 64f);
                g.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            layers.Add(gr); depth.Add(0.8f);

            // near props standing on the grass line
            var near = new GameObject("NearProps", typeof(RectTransform));
            near.layer = 5;
            near.transform.SetParent(go.transform, false);
            Stretch((RectTransform)near.transform);
            if (lamp) PxImg(near.transform, "LampL", lamp, new Vector2(0.06f, 0f), new Vector2(0f, 150f), new Vector2(lamp.rect.width * 1.5f, lamp.rect.height * 1.5f));
            if (lamp) PxImg(near.transform, "LampR", lamp, new Vector2(0.94f, 0f), new Vector2(0f, 150f), new Vector2(lamp.rect.width * 1.5f, lamp.rect.height * 1.5f));
            if (stall) PxImg(near.transform, "Stall", stall, new Vector2(0.78f, 0f), new Vector2(0f, 146f), new Vector2(stall.rect.width * 1.3f, stall.rect.height * 1.3f));
            if (fountain) PxImg(near.transform, "Fountain", fountain, new Vector2(0.24f, 0f), new Vector2(0f, 146f), new Vector2(fountain.rect.width * 1.1f, fountain.rect.height * 1.1f));
            layers.Add((RectTransform)near.transform); depth.Add(1f);

            // vignette: dark at the edges and bottom so cards and text stay readable on the busy scene
            var vig = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            vig.layer = 5;
            vig.transform.SetParent(go.transform, false);
            Stretch((RectTransform)vig.transform);
            var vimg = vig.GetComponent<Image>();
            vimg.sprite = VignetteSprite();
            vimg.type = Image.Type.Simple;
            vimg.raycastTarget = false;

            // pixel dust over everything
            var dust = new GameObject("PixelDust", typeof(RectTransform));
            dust.layer = 5;
            dust.transform.SetParent(go.transform, false);
            Stretch((RectTransform)dust.transform);
            dust.AddComponent<PixelDust>();

            var bd = go.AddComponent<PixelBackdropUI>();
            var so = new SerializedObject(bd);
            var lp = so.FindProperty("layers"); var dp = so.FindProperty("depth");
            lp.arraySize = layers.Count; dp.arraySize = layers.Count;
            for (int i = 0; i < layers.Count; i++) { lp.GetArrayElementAtIndex(i).objectReferenceValue = layers[i]; dp.GetArrayElementAtIndex(i).floatValue = depth[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite VignetteSprite()
        {
            string path = $"{UiDir}/entry_vignette.png";
            if (!File.Exists(path))
            {
                const int N = 128;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x / (N - 1f) - 0.5f) * 2f, dy = (y / (N - 1f) - 0.5f) * 2f;
                        float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.7f + dy * dy) - 0.35f) / 0.85f);
                        float a = Mathf.Pow(d, 1.6f) * 0.8f + (y < N * 0.25f ? (1f - y / (N * 0.25f)) * 0.25f : 0f);
                        t.SetPixel(x, y, new Color(0.03f, 0.02f, 0.07f, Mathf.Clamp01(a)));
                    }
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================================================================== 1. intro: logo assembly
        private static GameObject BuildIntro(Transform canvas, Sprite logo, out LogoAssemblyIntro intro)
        {
            GameObject root = Panel(canvas, "SplashRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.025f, 0.06f, 1f), null);
            var dust = new GameObject("IntroDust", typeof(RectTransform));
            dust.layer = 5;
            dust.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dust.transform);
            dust.AddComponent<PixelDust>();
            var vig = Img(root.transform, "Vignette", new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, VignetteSprite());
            Stretch(vig.rectTransform);
            vig.raycastTarget = false;

            var stage = new GameObject("LogoStage", typeof(RectTransform));
            stage.layer = 5;
            stage.transform.SetParent(root.transform, false);
            var srt = (RectTransform)stage.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.54f);
            srt.sizeDelta = new Vector2(1000f, 562f);                  // 1024:576 like the logo
            var fit = stage.AddComponent<ResponsiveLogoFit>();

            // light sweep over the finished logo
            var sweep = new GameObject("LightSweep", typeof(RectTransform), typeof(Image));
            sweep.layer = 5;
            sweep.transform.SetParent(stage.transform, false);
            var swi = sweep.GetComponent<Image>();
            swi.color = new Color(1f, 0.95f, 0.8f, 0.30f);
            swi.raycastTarget = false;
            var swrt = (RectTransform)sweep.transform;
            swrt.anchorMin = swrt.anchorMax = new Vector2(0.5f, 0.5f);
            swrt.sizeDelta = new Vector2(70f, 760f);
            swrt.localRotation = Quaternion.Euler(0f, 0f, 18f);

            Text hint = Label(root.transform, "SkipHint", "Chạm để bỏ qua", 22, new Color(0.7f, 0.68f, 0.8f, 0.7f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(520f, 40f));
            hint.raycastTarget = false;
            var btn = root.AddComponent<Button>();   // the whole screen is the skip button
            btn.transition = Selectable.Transition.None;
            root.GetComponent<Image>().raycastTarget = true;

            intro = root.AddComponent<LogoAssemblyIntro>();
            var iso = new SerializedObject(intro);
            iso.FindProperty("logo").objectReferenceValue = logo;
            iso.FindProperty("stage").objectReferenceValue = srt;
            iso.FindProperty("sweep").objectReferenceValue = swrt;
            iso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ================================================================== 2. loading
        private static GameObject BuildLoading(Transform canvas, out LoadingScreen2D loading)
        {
            GameObject root = Panel(canvas, "LoadingRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.025f, 0.06f, 1f), null);
            Sprite pano = LoadSprite($"{WorldArt}/Generated/c1_m05_pixel_panorama_v2.png");     // the cave: dark, glowing, different from the title
            if (pano)
            {
                var p = PxImg(root.transform, "LoadingPanorama", pano, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(2000f, 2000f));
                p.preserveAspect = false;
                p.color = new Color(0.8f, 0.8f, 0.9f);
            }
            var vig = Img(root.transform, "Vignette", new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, VignetteSprite());
            Stretch(vig.rectTransform);
            vig.raycastTarget = false;
            var dust = new GameObject("LoadingDust", typeof(RectTransform));
            dust.layer = 5;
            dust.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dust.transform);
            dust.AddComponent<PixelDust>();

            Sprite logo = LogoArt.Sprite();
            if (logo)
            {
                var l = PxImg(root.transform, "Logo", logo, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(620f, 349f));
                l.pivot();
            }

            // bar: pixel field frame as the track, a gold fill inside
            GameObject panel = Panel(root.transform, "BarPanel", new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(900f, 150f), InkSoft, panelSprite);
            if (!Skin(panel, "hall_panel")) Frame(panel, 3f);
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.layer = 5;
            track.transform.SetParent(panel.transform, false);
            var tr = (RectTransform)track.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(780f, 34f);
            tr.anchoredPosition = new Vector2(0f, -4f);
            var tri = track.GetComponent<Image>();
            tri.color = new Color(0.2f, 0.15f, 0.3f, 1f);
            tri.raycastTarget = false;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.layer = 5;
            fillGo.transform.SetParent(track.transform, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = new Vector2(4f, 4f); fr.offsetMax = new Vector2(0f, -4f);
            var fimg = fillGo.GetComponent<Image>();
            fimg.color = Gold;
            fimg.raycastTarget = false;
            // the fill's width follows anchorMax.x: stretch between the track's own edges (inset 4 px)
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f);
            Text status = Label(panel.transform, "Status", "Đang tải...", 28, Cream, new Vector2(0f, 0.5f), new Vector2(56f, 36f), new Vector2(520f, 40f), FontStyle.Bold);
            status.alignment = TextAnchor.MiddleLeft;
            status.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text percent = Label(panel.transform, "Percent", "0%", 28, Gold, new Vector2(1f, 0.5f), new Vector2(-56f, 36f), new Vector2(180f, 40f), FontStyle.Bold);
            percent.alignment = TextAnchor.MiddleRight;
            percent.rectTransform.pivot = new Vector2(1f, 0.5f);
            Text tip = Label(root.transform, "Tip", "", 22, new Color(0.78f, 0.74f, 0.9f, 0.85f), new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(1100f, 40f));
            tip.raycastTarget = false;

            loading = root.AddComponent<LoadingScreen2D>();
            var lso = new SerializedObject(loading);
            lso.FindProperty("fill").objectReferenceValue = fr;
            lso.FindProperty("percentLabel").objectReferenceValue = percent;
            lso.FindProperty("statusLabel").objectReferenceValue = status;
            lso.FindProperty("tipLabel").objectReferenceValue = tip;
            lso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ================================================================== 3. title + server
        private static GameObject BuildTitle(Transform canvas, AdventureEntryFlow flow, out EntryServerPicker picker)
        {
            GameObject root = Panel(canvas, "TitleRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), null);
            root.GetComponent<Image>().raycastTarget = false;
            Sprite logo = LogoArt.Sprite();
            if (logo)
            {
                var l = PxImg(root.transform, "TitleLogo", logo, new Vector2(0.5f, 0.69f), Vector2.zero, new Vector2(820f, 461f));
                l.pivot();
                var fit = l.gameObject.AddComponent<ResponsiveLogoFit>();
            }

            // server bar: dot + name + state + "Đổi" (opens the list)
            GameObject bar = Panel(root.transform, "ServerBar", new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.27f), Vector2.zero, new Vector2(700f, 96f), InkSoft, panelSprite);
            if (!Skin(bar, "hall_panel")) Frame(bar, 3f);
            var dot = Img(bar.transform, "StateDot", new Vector2(0f, 0.5f), new Vector2(22f, 22f), new Color(1f, 0.82f, 0.3f), null);
            dot.rectTransform.anchoredPosition = new Vector2(48f, 0f);
            dot.raycastTarget = false;
            Text sname = Label(bar.transform, "ServerName", "Server 1 - Vùng Đất Khởi Nguyên", 28, Cream, new Vector2(0f, 0.5f), new Vector2(86f, 14f), new Vector2(420f, 40f), FontStyle.Bold);
            sname.alignment = TextAnchor.MiddleLeft; sname.raycastTarget = false;
            sname.rectTransform.pivot = new Vector2(0f, 0.5f);
            sname.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text sstate = Label(bar.transform, "ServerState", "Đang kiểm tra...", 20, Muted, new Vector2(0f, 0.5f), new Vector2(86f, -20f), new Vector2(420f, 30f));
            sstate.alignment = TextAnchor.MiddleLeft; sstate.raycastTarget = false;
            sstate.rectTransform.pivot = new Vector2(0f, 0.5f);
            Button open = Btn(bar.transform, "ServerOpen", "Đổi ▾", new Vector2(-28f, 0f), new Vector2(150f, 64f), new Color(0.3f, 0.4f, 0.6f), Cream, new Vector2(1f, 0.5f));
            Style(open, "btn_slate", Cream);

            // list (above the bar), one row template
            GameObject list = Panel(root.transform, "ServerList", new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.27f), new Vector2(0f, 150f), new Vector2(700f, 150f), Ink, panelSprite);
            if (!Skin(list, "hall_panel")) Frame(list, 3f);
            RectTransform listC = FramedContent(list, 6f, topBand: 24f);
            VStack(listC, 8f, TextAnchor.UpperCenter);
            Button rowT = Btn(listC, "RowTemplate", "Server", Vector2.zero, new Vector2(620, 64), new Color(0.2f, 0.26f, 0.38f), Cream);
            Style(rowT, "btn_slate", Cream);
            Size(rowT, 64f);
            list.SetActive(false);

            // BẮT ĐẦU
            Button start = Btn(root.transform, "StartButton", "BẮT ĐẦU", new Vector2(0f, 0f), new Vector2(460f, 120f), Gold, new Color(0.15f, 0.1f, 0.05f), new Vector2(0.5f, 0.13f));
            Style(start, "btn_gold", new Color(0.15f, 0.1f, 0.05f));
            Text st = start.GetComponentInChildren<Text>();
            st.fontSize = 52; st.fontStyle = FontStyle.Bold;
            var fitOne = st.GetComponent<FitOneLine>();
            if (fitOne) fitOne.Configure(52, 24);
            start.gameObject.AddComponent<PulseScale>();
            UnityEventTools.AddPersistentListener(start.onClick, flow.StartGame);

            Text ver = Label(root.transform, "Version", "Adventure King: Reborn", 18, new Color(0.7f, 0.68f, 0.8f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(520f, 30f));
            ver.raycastTarget = false;

            picker = root.AddComponent<EntryServerPicker>();
            var pso = new SerializedObject(picker);
            pso.FindProperty("nameLabel").objectReferenceValue = sname;
            pso.FindProperty("stateLabel").objectReferenceValue = sstate;
            pso.FindProperty("stateDot").objectReferenceValue = dot;
            pso.FindProperty("listPanel").objectReferenceValue = list;
            pso.FindProperty("listContent").objectReferenceValue = listC;
            pso.FindProperty("rowTemplate").objectReferenceValue = rowT;
            pso.FindProperty("openButton").objectReferenceValue = open;
            pso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }

    internal static class PxImageExt
    {
        /// <summary>Pivot at the image centre (the PxImg helper pivots anchors in the lower half at their foot).</summary>
        public static void pivot(this Image img) => img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}

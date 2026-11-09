using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Builds Assets/Scenes/AK_Entry.unity: Splash -> Login/Register -> Hero Selection hall.
    /// The hall is a real 3D stage (layer 29 "HeroPreview") rendered full-screen behind the UI, with
    /// the four animated class prefabs, drag-to-rotate, Try Attack / Try Skill / Reset View.
    /// Authentication stays fail-closed: no backend is wired, so login cannot fake success.
    /// </summary>
    public static partial class EntrySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/AK_Entry.unity";
        public const int PreviewLayer = 29;
        private const string UiDir = "Assets/Art/UI";
        private static Font font;
        private static Sprite panelSprite, pillSprite;

        // Palette (original): night-teal hall with warm gold accents.
        private static readonly Color Ink = new Color(0.07f, 0.09f, 0.14f, 0.82f);
        private static readonly Color InkSoft = new Color(0.1f, 0.13f, 0.2f, 0.7f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
        private static readonly Color Muted = new Color(0.72f, 0.78f, 0.86f);

        [MenuItem("AdventureKing/Scenes/Rebuild Entry Scene")]
        public static void BuildMenu() => Build();

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        internal static Sprite PanelSprite => panelSprite;

        private static Font boldFont;

        internal static void InitUi()
        {
            // Be Vietnam Pro (SIL OFL 1.1, Assets/Art/Fonts): crisp Vietnamese diacritics; real Bold file for bold text.
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-SemiBold.ttf");
            boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!boldFont) boldFont = font;
            panelSprite = RoundedSprite("ui_panel", 64, 18);
            pillSprite = RoundedSprite("ui_pill", 64, 30);
            enamelSprite = GeneratedUiSprite("ui_enamel", 128, 30, Enamel);
            rimSprite = GeneratedUiSprite("ui_gilded_rim", 128, 30, GildedRim);
            framePanel = FantasySprite("Assets/Art/UI/Fantasy/frame_panel.png", new Vector4(112, 112, 112, 136));
            frameBar = FantasySprite("Assets/Art/UI/Fantasy/frame_bar.png", new Vector4(150, 52, 150, 52));
        }

        // ------------------------------------------------------------ fantasy UI kit
        private static Sprite enamelSprite, rimSprite, framePanel, frameBar;
        internal static Sprite FramePanelSprite => framePanel;

        /// <summary>Glossy enamel body: vertical light gradient so a tint reads as polished lacquer.</summary>
        private static Color Enamel(float d, float v, int radius)
        {
            float a = Mathf.Clamp01(radius - d + 0.5f);
            float shade = Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 1f, v));
            float gloss = v > 0.58f ? Mathf.Lerp(0f, 0.18f, Mathf.InverseLerp(0.58f, 0.95f, v)) : 0f;
            float c = Mathf.Min(1f, shade + gloss);
            return new Color(c, c, c, a);
        }

        /// <summary>Gilded rim: dark outer line, bright bevelled gold band, light inner line; transparent inside.</summary>
        private static Color GildedRim(float d, float v, int radius)
        {
            // d = distance outside the inner rounded rect (0 inside the corner circle centre); radius = corner radius
            float edge = radius - d;                 // distance from the outer edge inward (px)
            if (edge < -0.5f) return Color.clear;
            const float band = 7f;
            if (edge > band + 1.5f) return Color.clear;
            float aa = Mathf.Clamp01(edge + 0.5f) * Mathf.Clamp01(band + 1.5f - edge);
            Color dark = new Color(0.32f, 0.2f, 0.07f), mid = new Color(0.86f, 0.64f, 0.26f), light = new Color(1f, 0.92f, 0.62f);
            float t = Mathf.Clamp01(edge / band);
            Color c = t < 0.15f ? Color.Lerp(dark, mid, t / 0.15f) : t < 0.55f ? Color.Lerp(mid, light, (t - 0.15f) / 0.4f) : Color.Lerp(light, mid, (t - 0.55f) / 0.45f);
            c = Color.Lerp(c * 0.8f, c, v); // top catches more light
            c.a = aa;
            return c;
        }

        private static Sprite GeneratedUiSprite(string name, int size, int radius, System.Func<float, float, int, Color> shader)
        {
            Directory.CreateDirectory(UiDir);
            string path = $"{UiDir}/{name}.png";
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    // distance from the rounded-rect outline measured as "radius - inward depth"
                    float inward = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float d = dx > 0 && dy > 0 ? Mathf.Sqrt(dx * dx + dy * dy) : radius - inward;
                    t.SetPixel(x, y, shader(d, y / (float)(size - 1), radius));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteBorder = new Vector4(radius + 4, radius + 4, radius + 4, radius + 4);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite FantasySprite(string path, Vector4 border)
        {
            if (!File.Exists(path)) return null;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            // Single mode: in Multiple mode the sub-sprite (frame_panel_0) has NO border, so the frame was drawn as one
            // stretched picture (ornament scaling with the panel, text landing on the crest). 9-slice needs Single.
            if (ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder = border;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Turns a plain panel into the gold-filigree fantasy frame (Meshy art, 9-sliced).</summary>
        internal static void Frame(GameObject panel, float thickness = 3f)
        {
            if (!framePanel) return;
            var img = panel.GetComponent<Image>();
            img.sprite = framePanel;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.pixelsPerUnitMultiplier = thickness; // higher = thinner frame band
        }

        /// <summary>Applies one of the purpose-made 9-slice skins (FrameArt) to a panel/button image. Returns false if the skin is missing.</summary>
        internal static bool Skin(GameObject go, string skin, float scale = 1f)
        {
            Sprite s = FrameArt.Get(skin);
            if (!s) return false;
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.pixelsPerUnitMultiplier = 1f / scale; // scale > 1 = thicker frame band
            var rim = go.transform.Find("GildedRim");
            if (rim) Object.DestroyImmediate(rim.gameObject);
            return true;
        }

        /// <summary>Re-skins a button made by <see cref="Btn"/> and sets its label colour.</summary>
        internal static Button Style(Button b, string skin, Color fg, float scale = 1f)
        {
            Skin(b.gameObject, skin, scale);
            foreach (Text t in b.GetComponentsInChildren<Text>(true))
            {
                t.color = fg;
                var sh = t.GetComponent<Shadow>();
                // dark text on light gems reads best without a drop shadow
                if (sh) sh.effectColor = fg.grayscale < 0.4f ? new Color(1f, 1f, 1f, 0.25f) : new Color(0f, 0f, 0f, 0.55f);
            }
            return b;
        }

        /// <summary>Gilded rim overlay on top of an element (buttons, fields, small panels).</summary>
        internal static void Rim(Transform parent, float alpha = 1f)
        {
            if (!rimSprite) return;
            var go = new GameObject("GildedRim", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.AddComponent<Image>();
            img.sprite = rimSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        public const string LogoPath = "Assets/Art/Branding/SAM_Logo.png";

        /// <summary>The owner-provided studio logo (SAM), imported as a crisp UI sprite.</summary>
        internal static Sprite OwnerLogo()
        {
            if (!File.Exists(LogoPath)) return null;
            var ti = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
            if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || ti.maxTextureSize != 1024)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        }

        public static void Build()
        {
            EnsureLayer();
            InitUi();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------------- audio + input
            new GameObject("AudioDirector").AddComponent<AdventureAudioDirector>();
            AudioLibrary.AssignTo(Object.FindAnyObjectByType<AdventureAudioDirector>());
            // Main camera only clears the screen and hosts the AudioListener (UI is Screen Space Overlay,
            // the hero stage renders through its own camera into a RenderTexture).
            var mainCam = new GameObject("MainCamera") { tag = "MainCamera" };
            var mc = mainCam.AddComponent<Camera>();
            mc.clearFlags = CameraClearFlags.SolidColor;
            mc.backgroundColor = Color.black;
            mc.cullingMask = 0;
            mainCam.AddComponent<AudioListener>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            // ---------------------------------------------------------------- 3D preview stage
            var stage = new GameObject("HeroPreviewStage");
            stage.transform.position = new Vector3(0f, -200f, 0f);
            RenderTexture rt = PreviewTexture();
            Camera previewCam = BuildStage(stage.transform, rt, out Transform mount);

            // ---------------------------------------------------------------- UI
            var canvasGo = new GameObject("EntryCanvas", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var flow = canvasGo.AddComponent<AdventureEntryFlow>();

            BuildBackdrop(canvasGo.transform); // fantasy dawn village behind login + hero hall (splash covers it)

            // Studio splash: the owner-provided SAM logo (dark metal + black lettering) on a warm ivory field.
            GameObject splash = Panel(canvasGo.transform, "SplashRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.965f, 0.955f, 0.935f, 1f), null);
            Sprite ownerLogo = OwnerLogo();
            var logo = Img(splash.transform, "OfficialLogo", new Vector2(0.5f, 0.52f), new Vector2(640, 640), Color.white, null);
            logo.sprite = ownerLogo;
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            logo.enabled = ownerLogo; // never substituted: without the owner's file the splash stays plain
            logo.gameObject.AddComponent<CanvasGroup>();
            logo.gameObject.AddComponent<SplashLogoFade>();

            GameObject auth = BuildAuth(canvasGo.transform, out InputField acc, out InputField pwd, out InputField confirm, out Text authStatus, out GameObject registerFields, flow);
            GameObject selection = BuildSelection(canvasGo.transform, rt, previewCam, mount, flow, out HeroSelectionPreview3D preview,
                out Text heroName, out Text heroDesc, out Text heroStats, out InputField charName, out Text[] slots, out Text existing, out Text selStatus);

            // status text is shared: auth status for auth screen; selection uses its own footer
            var so = new SerializedObject(flow);
            void R(string p, Object o) => so.FindProperty(p).objectReferenceValue = o;
            R("splashRoot", splash); R("authRoot", auth); R("selectionRoot", selection);
            R("splashLogo", logo); R("officialAdventureKingLogo", ownerLogo);
            so.FindProperty("gameplaySceneName").stringValue = VillageSceneBuilder.SceneName;
            R("accountInput", acc); R("passwordInput", pwd); R("confirmPasswordInput", confirm);
            R("statusText", authStatus); R("selectionStatusText", selStatus); R("registerFields", registerFields);
            R("heroName", heroName); R("heroDescription", heroDesc); R("heroStats", heroStats);
            R("characterNameInput", charName); R("existingCharacterDetails", existing); R("heroPreview", preview);
            var sl = so.FindProperty("slotLabels");
            sl.arraySize = 4;
            for (int i = 0; i < 4; i++) sl.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("splashDurationSeconds").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Composition root: the real local server (Server/AdventureKing.Server). If it is not running,
            // every auth/profile call fails with a clear message — nothing is simulated.
            // Own root object: it is DontDestroyOnLoad so the session follows the player into the world.
            var connection = new GameObject("ServerConnection").AddComponent<AdventureServerConnection>();
            var cso = new SerializedObject(connection);
            cso.FindProperty("entryFlow").objectReferenceValue = flow;
            cso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            VillageSceneBuilder.RegisterBuildScenes();
            Debug.Log("[ENTRY] Scene built: " + ScenePath);
        }

        // ================================================================== stage
        private static RenderTexture PreviewTexture()
        {
            string path = "Assets/Art/UI/HeroPreview_RT.renderTexture";
            Directory.CreateDirectory(UiDir);
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (!rt)
            {
                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "HeroPreview_RT" };
                AssetDatabase.CreateAsset(rt, path);
            }
            return rt;
        }

        private static Camera BuildStage(Transform stage, RenderTexture rt, out Transform mount)
        {
            int L = PreviewLayer;
            Material Toon(string name, Color c, float outline = 0f)
            {
                string p = $"{UiDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(Shader.Find(HeroArtPipeline.ToonShader)); AssetDatabase.CreateAsset(m, p); }
                m.SetColor("_BaseColor", c);
                m.SetFloat("_OutlineWidth", outline);
                m.SetFloat("_RimStrength", 0.12f);
                EditorUtility.SetDirty(m);
                return m;
            }

            GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Transform parent)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.layer = L;
                return go;
            }

            // Tiered stone dais with a gold inlay ring and soft glyph glow.
            Prim(PrimitiveType.Cylinder, "DaisBase", new Vector3(0, -0.18f, 0), new Vector3(2.6f, 0.12f, 2.6f), Toon("stage_stone_dark", new Color(0.32f, 0.36f, 0.44f), 0.004f), stage);
            Prim(PrimitiveType.Cylinder, "DaisTop", new Vector3(0, -0.04f, 0), new Vector3(2.1f, 0.06f, 2.1f), Toon("stage_stone", new Color(0.4f, 0.42f, 0.5f), 0.004f), stage);
            Prim(PrimitiveType.Cylinder, "DaisInlay", new Vector3(0, 0.0f, 0), new Vector3(1.75f, 0.022f, 1.75f), Toon("stage_gold", new Color(0.95f, 0.76f, 0.38f)), stage);
            Prim(PrimitiveType.Cylinder, "DaisCenter", new Vector3(0, 0.008f, 0), new Vector3(1.6f, 0.022f, 1.6f), Toon("stage_stone_light", new Color(0.46f, 0.48f, 0.55f)), stage);
            var glyph = new GameObject("DaisGlyph");
            glyph.layer = L;
            glyph.transform.SetParent(stage, false);
            glyph.transform.localPosition = new Vector3(0, 0.03f, 0);
            glyph.transform.localRotation = Quaternion.Euler(90, 0, 0);
            glyph.transform.localScale = Vector3.one * 1.55f;
            glyph.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var gmr = glyph.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/rune_add.mat");
            gmr.shadowCastingMode = ShadowCastingMode.Off;
            var spin = glyph.AddComponent<UiSpinner3D>();
            var sso = new SerializedObject(spin);
            sso.FindProperty("degreesPerSecond").vector3Value = new Vector3(0, 0, 10f);
            sso.FindProperty("tint").colorValue = new Color(0.55f, 0.85f, 1f, 0.55f);
            sso.ApplyModifiedPropertiesWithoutUndo();

            // No wall / pillars / floor any more: the camera clears to transparent and the shared fantasy backdrop
            // (LoginBackdrop) is visible behind the dais and the hero.
            var motes = new GameObject("Motes");
            motes.layer = L;
            motes.transform.SetParent(stage, false);
            motes.transform.localPosition = new Vector3(0, 1.4f, 2.5f);
            var ps = motes.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 6f; main.startSpeed = 0.12f; main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f, 0.8f), new Color(0.6f, 0.85f, 1f, 0.7f));
            main.maxParticles = 120; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(9, 4, 4);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = motes.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/dot_add.mat");

            // Light: warm key with soft shadows; ambient set for the whole scene.
            var key = new GameObject("PreviewKeyLight");
            key.transform.SetParent(stage, false);
            key.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional;
            kl.intensity = 1.0f;
            kl.color = new Color(1f, 0.9f, 0.8f);
            kl.shadows = LightShadows.Soft;
            kl.shadowStrength = 0.65f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.58f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.22f);
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.78f, 0.78f); // warm dawn, matches the backdrop
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.66f, 0.66f);

            var mountGo = new GameObject("ModelMount");
            mountGo.layer = L;
            mountGo.transform.SetParent(stage, false);
            mountGo.transform.localScale = Vector3.one * 1.45f;
            mount = mountGo.transform;

            var camGo = new GameObject("HeroPreviewCamera");
            camGo.transform.SetParent(stage, false);
            camGo.transform.localPosition = new Vector3(0f, 1.05f, -5.2f);
            camGo.transform.localRotation = Quaternion.Euler(6.5f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 26f;
            cam.cullingMask = 1 << L;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent: backdrop shows through
            cam.targetTexture = rt;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false; // keeps the alpha channel
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            return cam;
        }

        private static Material GradientMaterial()
        {
            string tp = $"{UiDir}/hall_gradient.png";
            var t = new Texture2D(4, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
            {
                float v = y / 255f;
                Color c = Color.Lerp(new Color(0.09f, 0.11f, 0.2f), new Color(0.2f, 0.3f, 0.46f), Mathf.SmoothStep(0, 1, v));
                c = Color.Lerp(c, new Color(0.36f, 0.42f, 0.55f), Mathf.Exp(-Mathf.Pow((v - 0.42f) * 6f, 2)) * 0.5f);
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            File.WriteAllBytes(tp, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            string mp = $"{UiDir}/hall_gradient.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!m) { m = new Material(Shader.Find("AdventureKing/VFX")); AssetDatabase.CreateAsset(m, mp); }
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_Intensity", 1f);
            m.renderQueue = 1000;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================== auth screen
        private static GameObject BuildAuth(Transform canvas, out InputField acc, out InputField pwd, out InputField confirm,
            out Text status, out GameObject registerFields, AdventureEntryFlow flow)
        {
            // Transparent root: the shared fantasy backdrop (LoginBackdrop, behind every entry screen) shows through.
            GameObject root = Panel(canvas, "AuthRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), null);
            root.GetComponent<Image>().raycastTarget = false;
            // Card on the right third on wide screens (the scene stays visible), centred on narrow ones.
            GameObject card = Panel(root.transform, "AuthCard", new Vector2(0.71f, 0.5f), new Vector2(0.71f, 0.5f), Vector2.zero, new Vector2(720, 880), Ink, panelSprite);
            var anchor = card.AddComponent<ResponsiveAnchor>();
            var aso = new SerializedObject(anchor);
            aso.FindProperty("wideX").floatValue = 0.71f;
            aso.FindProperty("narrowX").floatValue = 0.5f;
            aso.FindProperty("wideAspect").floatValue = 1.55f;
            aso.ApplyModifiedPropertiesWithoutUndo();
            if (!Skin(card, "auth_card")) Frame(card, 2.6f);
            RectTransform content = FramedContent(card, 6f, topBand: 52f);
            VStack(content, 12f, TextAnchor.MiddleCenter);

            Text title = BlockLabel(content, "Title", "Đăng nhập", 44, Gold, 58f, FontStyle.Bold);
            var tOutline = title.gameObject.AddComponent<Outline>();
            tOutline.effectColor = new Color(0.25f, 0.13f, 0.04f, 0.85f);
            tOutline.effectDistance = new Vector2(1.5f, -1.5f);
            BlockLabel(content, "Subtitle", "Chào mừng đến Adventure King", 22, Muted, 30f);
            acc = Field(content, "AccountInput", "Tên tài khoản hoặc email", Vector2.zero, false, new Vector2(560, 76));
            Size(acc, 76f);
            pwd = Field(content, "PasswordInput", "Mật khẩu (tối thiểu 8 ký tự)", Vector2.zero, true, new Vector2(560, 76));
            Size(pwd, 76f);
            registerFields = new GameObject("RegisterFields", typeof(RectTransform));
            registerFields.layer = 5;
            registerFields.transform.SetParent(content, false);
            Size(registerFields.transform, 76f);
            confirm = Field(registerFields.transform, "ConfirmPasswordInput", "Nhập lại mật khẩu", Vector2.zero, true, new Vector2(560, 76));
            Stretch((RectTransform)confirm.transform);
            registerFields.SetActive(false);

            RectTransform row1 = HRow(content, "PrimaryRow", 72f, 16f);
            Button login = Btn(row1, "LoginButton", "Đăng nhập", Vector2.zero, new Vector2(270, 72), Gold, new Color(0.15f, 0.1f, 0.05f));
            Size(login, 72f, 270f, 1f);
            Button register = Btn(row1, "RegisterButton", "Đăng ký", Vector2.zero, new Vector2(270, 72), new Color(0.3f, 0.4f, 0.6f), Cream);
            Style(register, "btn_jade", Cream);
            Size(register, 72f, 270f, 1f);
            BlockLabel(content, "Or", "hoặc", 20, Muted, 26f);
            RectTransform row2 = HRow(content, "SocialRow", 64f, 16f);
            Button google = Btn(row2, "GoogleButton", "Google", Vector2.zero, new Vector2(270, 64), new Color(0.95f, 0.96f, 0.98f), new Color(0.15f, 0.17f, 0.2f));
            Size(google, 64f, 270f, 1f);
            Button facebook = Btn(row2, "FacebookButton", "Facebook", Vector2.zero, new Vector2(270, 64), new Color(0.23f, 0.38f, 0.7f), Color.white);
            Size(facebook, 64f, 270f, 1f);
            var toggleRow = Btn(content, "ToggleRegister", "Chưa có tài khoản? Tạo mới", Vector2.zero, new Vector2(560, 62), new Color(0, 0, 0, 0), new Color(0.85f, 0.9f, 1f));
            Size(toggleRow, 62f); // ghost button: still a full-size touch target (>= 40 px on a 720p phone)
            Text toggleLabel = toggleRow.GetComponentInChildren<Text>();
            toggleLabel.fontStyle = FontStyle.Normal;
            status = BlockLabel(content, "AuthStatus", "", 22, new Color(1f, 0.66f, 0.58f), 56f);

            UnityEventTools.AddPersistentListener(login.onClick, flow.LoginPassword);
            UnityEventTools.AddPersistentListener(register.onClick, flow.RegisterPassword);
            UnityEventTools.AddPersistentListener(google.onClick, flow.ContinueWithGoogle);
            UnityEventTools.AddPersistentListener(facebook.onClick, flow.ContinueWithFacebook);
            UnityEventTools.AddPersistentListener(toggleRow.onClick, flow.ToggleRegisterMode);
            var fso = new SerializedObject(flow);
            fso.FindProperty("authTitle").objectReferenceValue = title;
            fso.FindProperty("registerToggleLabel").objectReferenceValue = toggleLabel;
            fso.FindProperty("loginButton").objectReferenceValue = login;
            fso.FindProperty("registerButton").objectReferenceValue = register;
            fso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ================================================================== selection hall
        private static GameObject BuildSelection(Transform canvas, RenderTexture rt, Camera cam, Transform mount, AdventureEntryFlow flow,
            out HeroSelectionPreview3D preview, out Text heroName, out Text heroDesc, out Text heroStats, out InputField charName,
            out Text[] slots, out Text existing, out Text status)
        {
            GameObject root = new GameObject("SelectionRoot", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch((RectTransform)root.transform);

            var raw = new GameObject("HeroPreviewImage", typeof(RectTransform)).AddComponent<RawImage>();
            raw.transform.SetParent(root.transform, false);
            Stretch(raw.rectTransform);
            raw.texture = rt;
            raw.raycastTarget = true;
            preview = raw.gameObject.AddComponent<HeroSelectionPreview3D>();

            Text hallTitle = Label(root.transform, "HallTitle", "Chọn Anh Hùng", 52, Gold, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(760, 70), FontStyle.Bold);
            hallTitle.raycastTarget = false;
            var outline = hallTitle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.13f, 0.04f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Every panel keeps >= 100 units from the screen edges (phone notches / rounded corners) and is laid out
            // with measured frame content areas + layout groups (no hand-placed text inside ornaments).
            const float Edge = 100f;

            // ---- class cards (left)
            GameObject cards = Panel(root.transform, "ClassCards", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(Edge + 175, 95), new Vector2(350, 600), InkSoft, panelSprite);
            if (!Skin(cards, "hall_panel")) Frame(cards, 3.2f);
            RectTransform cardList = FramedContent(cards, 0f, topBand: 40f);
            VStack(cardList, 10f, TextAnchor.MiddleCenter);
            (string name, string role, HeroClass c, Color tint)[] classes =
            {
                ("Kiếm Sĩ", "Cận chiến • Bền bỉ", HeroClass.Swordsman, new Color(1f, 0.55f, 0.35f)),
                ("Pháp Sư", "Phép thuật • Tầm xa", HeroClass.Mage, new Color(0.7f, 0.5f, 1f)),
                ("Sát Thủ", "Song đao • Tốc độ", HeroClass.Assassin, new Color(1f, 0.32f, 0.42f)),
                ("Cung Thủ", "Cung tên • Cơ động", HeroClass.Archer, new Color(0.45f, 0.9f, 0.5f)),
            };
            var cardImgs = new Image[4];
            var slotImgs = new Image[4];
            for (int i = 0; i < classes.Length; i++)
            {
                var (n, role, c, tint) = classes[i];
                Button b = Btn(cardList, "Card_" + c, "", Vector2.zero, new Vector2(270, 112), new Color(0.13f, 0.17f, 0.26f, 0.95f), Cream);
                Skin(b.gameObject, "hall_card");
                cardImgs[(int)c] = b.GetComponent<Image>();
                Size(b, 112f);
                var bar = Img(b.transform, "Accent", new Vector2(0f, 0.5f), new Vector2(10, 84), tint, pillSprite);
                bar.rectTransform.anchoredPosition = new Vector2(26, 0);
                var nameBox = new GameObject("NameBox", typeof(RectTransform));
                nameBox.layer = 5;
                nameBox.transform.SetParent(b.transform, false);
                var nb = (RectTransform)nameBox.transform;
                nb.anchorMin = Vector2.zero; nb.anchorMax = Vector2.one;
                nb.offsetMin = new Vector2(46, 14); nb.offsetMax = new Vector2(-22, -12);
                VStack(nb, 2f, TextAnchor.MiddleLeft);
                BlockLabel(nb, "Name", n, 32, Cream, 44f, FontStyle.Bold, TextAnchor.MiddleLeft);
                BlockLabel(nb, "Role", role, 19, tint, 26f, FontStyle.Normal, TextAnchor.MiddleLeft);
                switch (c)
                {
                    case HeroClass.Swordsman: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectSwordsman); break;
                    case HeroClass.Mage: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectMage); break;
                    case HeroClass.Assassin: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectAssassin); break;
                    default: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectArcher); break;
                }
            }

            // ---- class details + preview actions (right)
            GameObject info = Panel(root.transform, "ClassInfo", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-(Edge + 225), 95), new Vector2(450, 600), InkSoft, panelSprite);
            if (!Skin(info, "hall_panel")) Frame(info, 3.0f);
            RectTransform infoC = FramedContent(info, 0f, topBand: 40f);
            VStack(infoC, 6f, TextAnchor.UpperCenter);
            heroName = BlockLabel(infoC, "HeroName", "", 40, Gold, 52f, FontStyle.Bold);
            heroDesc = BlockLabel(infoC, "HeroDescription", "", 22, Cream, 96f, FontStyle.Normal, TextAnchor.UpperCenter);
            heroStats = BlockLabel(infoC, "HeroStats", "", 19, Muted, 28f);
            var bars = info.AddComponent<HeroStatBars>();
            var bso = new SerializedObject(bars);
            var fills = bso.FindProperty("fills");
            string[] statNames = { "Máu", "Sát thương/giây", "Phòng thủ", "Tầm đánh" };
            fills.arraySize = statNames.Length;
            for (int i = 0; i < statNames.Length; i++)
            {
                RectTransform row = HRow(infoC, "StatRow" + i, 34f, 10f, TextAnchor.MiddleLeft);
                Text sl = BlockLabel(row, "StatLabel" + i, statNames[i], 19, Muted, 34f, FontStyle.Normal, TextAnchor.MiddleLeft);
                Size(sl, 34f, 150f, 0f, 150f);
                var track = new GameObject("StatTrack" + i, typeof(RectTransform));
                track.layer = 5;
                track.transform.SetParent(row, false);
                Size(track.transform, 34f, 200f, 1f, 160f);
                if (FrameArt.Get("bar_track"))
                {
                    var fr = Img(track.transform, "StatFrame" + i, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, FrameArt.Get("bar_track"));
                    fr.rectTransform.anchorMin = new Vector2(0f, 0.5f); fr.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    fr.rectTransform.offsetMin = new Vector2(10f, -11f); fr.rectTransform.offsetMax = new Vector2(-10f, 11f);
                }
                else if (frameBar)
                {
                    // gilded bar frame (Meshy art) around the groove; the fill grows inside it
                    var fr = Img(track.transform, "StatFrame" + i, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, frameBar);
                    fr.rectTransform.anchorMin = Vector2.zero; fr.rectTransform.anchorMax = Vector2.one;
                    fr.rectTransform.offsetMin = fr.rectTransform.offsetMax = Vector2.zero;
                    fr.pixelsPerUnitMultiplier = 4.5f;
                }
                var groove = Img(track.transform, "StatBack" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1, 1, 1, 0.06f), pillSprite);
                groove.rectTransform.anchorMin = new Vector2(0f, 0.5f); groove.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                groove.rectTransform.offsetMin = new Vector2(18f, -6f); groove.rectTransform.offsetMax = new Vector2(-18f, 6f);
                var fill = Img(groove.transform, "StatFill" + i, new Vector2(0f, 0.5f), new Vector2(150, 12), Gold, pillSprite);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.anchoredPosition = Vector2.zero;
                fills.GetArrayElementAtIndex(i).objectReferenceValue = fill.rectTransform;
            }
            bso.FindProperty("preview").objectReferenceValue = preview;
            bso.ApplyModifiedPropertiesWithoutUndo();
            BlockLabel(infoC, "DragHint", "Kéo nhân vật để xoay", 18, Muted, 26f).raycastTarget = false;
            RectTransform actions = HRow(infoC, "PreviewActions", 62f, 8f);
            Button atk = Btn(actions, "TryAttack", "Đòn đánh", Vector2.zero, new Vector2(120, 62), new Color(0.2f, 0.26f, 0.38f, 0.95f), Cream);
            Size(atk, 62f, 120f, 1f);
            Button skl = Btn(actions, "TrySkill", "Kỹ năng", Vector2.zero, new Vector2(120, 62), new Color(0.48f, 0.3f, 0.7f, 0.95f), Cream);
            Size(skl, 62f, 120f, 1f);
            Button rst = Btn(actions, "ResetView", "Góc nhìn", Vector2.zero, new Vector2(120, 62), new Color(0.2f, 0.26f, 0.38f, 0.95f), Muted);
            Size(rst, 62f, 120f, 1f);
            UnityEventTools.AddPersistentListener(atk.onClick, preview.PlayAttackPreview);
            UnityEventTools.AddPersistentListener(skl.onClick, preview.PlaySkillPreview);
            UnityEventTools.AddPersistentListener(rst.onClick, preview.ResetRotation);

            // ---- slots + naming (bottom bar, stretched between the safe margins)
            GameObject bottom = Panel(root.transform, "SlotBar", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero, Ink, panelSprite);
            var br = (RectTransform)bottom.transform;
            br.pivot = new Vector2(0.5f, 0f);
            br.offsetMin = new Vector2(Edge, 24f);
            br.offsetMax = new Vector2(-Edge, 24f + 232f);
            if (!Skin(bottom, "hall_panel")) Frame(bottom, 3.4f);
            RectTransform barC = FramedContent(bottom, 0f, topBand: 40f);
            VStack(barC, 8f, TextAnchor.MiddleCenter);
            existing = BlockLabel(barC, "ExistingDetails", "", 20, Muted, 28f);
            RectTransform slotRow = HRow(barC, "SlotRow", 84f, 12f);
            slots = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                Button s = Btn(slotRow, "Slot" + i, "", Vector2.zero, new Vector2(190, 84), new Color(0.13f, 0.17f, 0.26f, 1f), Cream);
                Skin(s.gameObject, "hall_card");
                slotImgs[i] = s.GetComponent<Image>();
                Size(s, 84f, 180f, 1f, 120f);
                slots[i] = FillLabel(s.transform, "Label", "Ô " + (i + 1), 20, Cream, FontStyle.Normal, 16f, 6f, 14);
                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot0); break;
                    case 1: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot1); break;
                    case 2: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot2); break;
                    default: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot3); break;
                }
            }
            charName = Field(slotRow, "CharacterNameInput", "Tên nhân vật (3–16 ký tự)", Vector2.zero, false, new Vector2(340, 72), new Vector2(0.5f, 0.5f));
            Size(charName, 72f, 400f, 1.6f, 320f);
            Button create = Btn(slotRow, "CreateAndEnter", "Tạo & vào game", Vector2.zero, new Vector2(220, 72), Gold, new Color(0.15f, 0.1f, 0.05f));
            Size(create, 72f, 220f, 0.4f, 170f);
            Button enter = Btn(slotRow, "EnterGame", "Vào game", Vector2.zero, new Vector2(170, 72), new Color(0.3f, 0.6f, 0.4f), Color.white);
            Size(enter, 72f, 170f, 0.3f, 130f);
            status = Label(root.transform, "SelectionStatus", "", 22, new Color(1f, 0.66f, 0.58f), new Vector2(0.5f, 0f), new Vector2(0, 24f + 232f + 26f), new Vector2(1100, 40));
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 16; status.resizeTextMaxSize = 22;
            status.verticalOverflow = VerticalWrapMode.Truncate;
            UnityEventTools.AddPersistentListener(create.onClick, flow.CreateCharacterAndEnter);
            UnityEventTools.AddPersistentListener(enter.onClick, flow.EnterSelectedCharacter);
            Button back = Btn(root.transform, "BackToLogin", "Đăng xuất", new Vector2(-(Edge + 100), -70), new Vector2(200, 64), new Color(0.2f, 0.26f, 0.38f, 0.9f), Muted, new Vector2(1f, 1f));
            UnityEventTools.AddPersistentListener(back.onClick, flow.BackToAuthentication);

            // Selection highlight (gold-rimmed "on" card for the chosen class and slot).
            var hso = new SerializedObject(flow);
            void Arr(string prop, Image[] imgs)
            {
                var a = hso.FindProperty(prop);
                a.arraySize = imgs.Length;
                for (int i = 0; i < imgs.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = imgs[i];
            }
            Arr("classCards", cardImgs);
            Arr("slotCards", slotImgs);
            hso.FindProperty("cardSprite").objectReferenceValue = FrameArt.Get("hall_card");
            hso.FindProperty("cardOnSprite").objectReferenceValue = FrameArt.Get("hall_card_on");
            hso.ApplyModifiedPropertiesWithoutUndo();

            // Wire the preview component.
            var pso = new SerializedObject(preview);
            pso.FindProperty("modelMount").objectReferenceValue = mount;
            pso.FindProperty("previewCamera").objectReferenceValue = cam;
            pso.FindProperty("previewTexture").objectReferenceValue = rt;
            pso.FindProperty("targetImage").objectReferenceValue = raw;
            pso.FindProperty("previewMessage").objectReferenceValue = status;
            pso.FindProperty("previewLayer").intValue = PreviewLayer;
            pso.FindProperty("defaultYaw").floatValue = 200f;
            var entries = pso.FindProperty("classes");
            entries.arraySize = 4;
            int k = 0;
            foreach (HeroClass c in System.Enum.GetValues(typeof(HeroClass)))
            {
                var e = entries.GetArrayElementAtIndex(k++);
                e.FindPropertyRelative("heroClass").enumValueIndex = (int)c;
                e.FindPropertyRelative("modelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(c));
                e.FindPropertyRelative("idleState").stringValue = "Idle";
                e.FindPropertyRelative("attackState").stringValue = "Attack";
                e.FindPropertyRelative("skillState").stringValue = "Skill";
            }
            pso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ================================================================== UI helpers
        private static void EnsureLayer()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var p = tm.FindProperty("layers").GetArrayElementAtIndex(PreviewLayer);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = "HeroPreview";
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Sprite RoundedSprite(string name, int size, int radius)
        {
            Directory.CreateDirectory(UiDir);
            string path = $"{UiDir}/{name}.png";
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (name == "ui_glow") a = Mathf.Clamp01(1f - d / radius) * Mathf.Clamp01(1f - d / radius);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteBorder = new Vector4(radius, radius, radius, radius);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        internal static GameObject Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c, Sprite s)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax;
            if (aMin == Vector2.zero && aMax == Vector2.one) Stretch(r);
            else { r.anchoredPosition = pos; r.sizeDelta = size; }
            var img = go.AddComponent<Image>();
            img.color = c;
            img.sprite = s;
            img.type = s ? Image.Type.Sliced : Image.Type.Simple;
            return go;
        }

        internal static Image Img(Transform parent, string name, Vector2 anchor, Vector2 size, Color c, Sprite s)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor;
            r.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.sprite = s;
            img.type = s ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        internal static Text Label(Transform parent, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            var t = go.AddComponent<Text>();
            bool bold = style == FontStyle.Bold && boldFont && boldFont != font;
            t.font = bold ? boldFont : font; t.text = text; t.fontSize = size; t.color = c;
            t.fontStyle = bold ? FontStyle.Normal : style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        internal static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, Color fg, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            if (parent.GetComponent<Image>() && anchor == null)
            {
                // children of a panel are laid out from its top centre (cards, forms)
                a = new Vector2(0.5f, parent.name == "AuthCard" ? 1f : 0.5f);
            }
            r.anchorMin = r.anchorMax = a;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = enamelSprite ? enamelSprite : pillSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = go.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            // purpose-made gem skin chosen from the role colour the caller passes (no shared gold rim on everything)
            if (!Skin(go, SkinFor(bg)) && bg.a > 0.05f) Rim(go.transform);
            if (!string.IsNullOrEmpty(label))
            {
                // inset from the gilded rim; shrinks to fit (Vietnamese diacritics included) instead of spilling
                int max = size.y > 60 ? 26 : 22;
                Text t = FillLabel(go.transform, "Text", label, max, fg, FontStyle.Bold, Mathf.Clamp(size.y * 0.24f, 10f, 20f), 4f, 15);
                t.raycastTarget = false;
                t.gameObject.AddComponent<FitOneLine>().Configure(max, 14); // one line, shrinks to fit
                var tsh = t.GetComponent<Shadow>();
                if (tsh && fg.grayscale < 0.4f) { tsh.effectColor = new Color(1f, 0.97f, 0.85f, 0.35f); tsh.effectDistance = new Vector2(0f, -1.2f); } // embossed dark text on light gems
            }
            return b;
        }

        /// <summary>Gem skin for a role colour: gold = primary, green = confirm, red = danger, white/blue = Google/Facebook...</summary>
        internal static string SkinFor(Color bg) => UiKit.SkinFor(bg);

        private static InputField Field(Transform parent, string name, string placeholder, Vector2 pos, bool password,
            Vector2? size = null, Vector2? anchor = null)
        {
            Vector2 sz = size ?? new Vector2(540, 64);
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor ?? new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.sprite = enamelSprite ? enamelSprite : pillSprite; img.type = Image.Type.Sliced; img.color = new Color(0.05f, 0.06f, 0.11f, 0.82f);
            if (!Skin(go, "field")) Rim(go.transform, 0.85f);
            // text sits clear of the rounded gilded rim (inset ~ half the height), large and upright for readability
            float inset = Mathf.Clamp(sz.y * 0.6f, 30f, 48f);
            int fontSize = Mathf.RoundToInt(Mathf.Clamp(sz.y * 0.38f, 22f, 30f));
            var text = FillLabel(go.transform, "Text", "", fontSize, Cream, FontStyle.Normal, inset, 6f, 18);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; // InputField scrolls long input itself
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Object.DestroyImmediate(text.GetComponent<Shadow>());
            var ph = FillLabel(go.transform, "Placeholder", placeholder, fontSize - 2, new Color(0.78f, 0.82f, 0.9f, 0.72f), FontStyle.Normal, inset, 6f, 16);
            ph.alignment = TextAnchor.MiddleLeft;
            Object.DestroyImmediate(ph.GetComponent<Shadow>());
            var f = go.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.characterLimit = password ? 64 : 48;
            if (password) { f.contentType = InputField.ContentType.Password; f.asteriskChar = '•'; }
            return f;
        }
    }
}

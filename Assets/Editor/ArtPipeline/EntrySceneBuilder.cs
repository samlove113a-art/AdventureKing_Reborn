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
    public static class EntrySceneBuilder
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

        public static void Build()
        {
            EnsureLayer();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelSprite = RoundedSprite("ui_panel", 64, 18);
            pillSprite = RoundedSprite("ui_pill", 64, 30);
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

            GameObject splash = Panel(canvasGo.transform, "SplashRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.05f, 0.08f, 1f), null);
            var logo = Img(splash.transform, "OfficialLogo", new Vector2(0.5f, 0.5f), new Vector2(900, 420), Color.white, null);
            logo.preserveAspect = true;
            logo.enabled = false; // the owner-approved logo asset must be assigned; nothing is substituted

            GameObject auth = BuildAuth(canvasGo.transform, out InputField acc, out InputField pwd, out InputField confirm, out Text authStatus, out GameObject registerFields, flow);
            GameObject selection = BuildSelection(canvasGo.transform, rt, previewCam, mount, flow, out HeroSelectionPreview3D preview,
                out Text heroName, out Text heroDesc, out Text heroStats, out InputField charName, out Text[] slots, out Text existing, out Text selStatus);

            // status text is shared: auth status for auth screen; selection uses its own footer
            var so = new SerializedObject(flow);
            void R(string p, Object o) => so.FindProperty(p).objectReferenceValue = o;
            R("splashRoot", splash); R("authRoot", auth); R("selectionRoot", selection);
            R("splashLogo", logo);
            R("accountInput", acc); R("passwordInput", pwd); R("confirmPasswordInput", confirm);
            R("statusText", authStatus); R("registerFields", registerFields);
            R("heroName", heroName); R("heroDescription", heroDesc); R("heroStats", heroStats);
            R("characterNameInput", charName); R("existingCharacterDetails", existing); R("heroPreview", preview);
            var sl = so.FindProperty("slotLabels");
            sl.arraySize = 4;
            for (int i = 0; i < 4; i++) sl.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("splashDurationSeconds").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
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
            Prim(PrimitiveType.Cylinder, "DaisTop", new Vector3(0, -0.04f, 0), new Vector3(2.1f, 0.06f, 2.1f), Toon("stage_stone", new Color(0.56f, 0.6f, 0.68f), 0.004f), stage);
            Prim(PrimitiveType.Cylinder, "DaisInlay", new Vector3(0, 0.0f, 0), new Vector3(1.75f, 0.022f, 1.75f), Toon("stage_gold", new Color(0.95f, 0.76f, 0.38f)), stage);
            Prim(PrimitiveType.Cylinder, "DaisCenter", new Vector3(0, 0.008f, 0), new Vector3(1.6f, 0.022f, 1.6f), Toon("stage_stone_light", new Color(0.66f, 0.7f, 0.78f)), stage);
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

            // Backdrop: deep gradient wall, distant pillars for depth, drifting light motes.
            var back = new GameObject("Backdrop");
            back.layer = L;
            back.transform.SetParent(stage, false);
            back.transform.localPosition = new Vector3(0, 2.2f, 7f);
            back.transform.localScale = new Vector3(28, 14, 1);
            back.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var bmr = back.AddComponent<MeshRenderer>();
            bmr.sharedMaterial = GradientMaterial();
            bmr.shadowCastingMode = ShadowCastingMode.Off;
            var pillar = Toon("stage_pillar", new Color(0.2f, 0.26f, 0.36f), 0.006f);
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * 2.6f;
                float z = 3.6f + (i % 2) * 1.2f;
                Prim(PrimitiveType.Cylinder, "Pillar" + i, new Vector3(x, 1.8f, z), new Vector3(0.45f, 2.6f, 0.45f), pillar, stage);
                Prim(PrimitiveType.Cube, "PillarCap" + i, new Vector3(x, 4.45f, z), new Vector3(0.75f, 0.18f, 0.75f), pillar, stage);
            }
            var floor = Prim(PrimitiveType.Plane, "HallFloor", new Vector3(0, -0.24f, 3f), new Vector3(3, 1, 2), Toon("stage_floor", new Color(0.18f, 0.22f, 0.3f)), stage);
            floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
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
            kl.intensity = 1.2f;
            kl.color = new Color(1f, 0.94f, 0.84f);
            kl.shadows = LightShadows.Soft;
            kl.shadowStrength = 0.65f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.58f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.1f, 0.13f, 0.22f);
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 22f;

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
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.17f, 1f);
            cam.targetTexture = rt;
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
            GameObject root = Panel(canvas, "AuthRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.12f, 1f), null);
            Img(root.transform, "Glow", new Vector2(0.5f, 0.55f), new Vector2(1400, 900), new Color(0.25f, 0.35f, 0.6f, 0.25f), RoundedSprite("ui_glow", 128, 60));
            GameObject card = Panel(root.transform, "AuthCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(640, 760), Ink, panelSprite);
            Label(card.transform, "Title", "Đăng nhập", 50, Gold, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(560, 70), FontStyle.Bold);
            Label(card.transform, "Subtitle", "Tài khoản Adventure King", 24, Muted, new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(560, 36));
            acc = Field(card.transform, "AccountInput", "Tên tài khoản hoặc email", new Vector2(0, -200), false);
            pwd = Field(card.transform, "PasswordInput", "Mật khẩu (tối thiểu 8 ký tự)", new Vector2(0, -282), true);
            registerFields = new GameObject("RegisterFields", typeof(RectTransform));
            registerFields.transform.SetParent(card.transform, false);
            Stretch((RectTransform)registerFields.transform);
            confirm = Field(registerFields.transform, "ConfirmPasswordInput", "Nhập lại mật khẩu", new Vector2(0, -364), true);
            registerFields.SetActive(false);

            Button login = Btn(card.transform, "LoginButton", "Đăng nhập", new Vector2(-140, -460), new Vector2(260, 70), Gold, new Color(0.15f, 0.1f, 0.05f));
            Button register = Btn(card.transform, "RegisterButton", "Đăng ký", new Vector2(140, -460), new Vector2(260, 70), new Color(0.3f, 0.4f, 0.6f), Cream);
            Button google = Btn(card.transform, "GoogleButton", "Tiếp tục với Google", new Vector2(0, -552), new Vector2(540, 62), new Color(0.95f, 0.96f, 0.98f), new Color(0.15f, 0.17f, 0.2f));
            Button facebook = Btn(card.transform, "FacebookButton", "Tiếp tục với Facebook", new Vector2(0, -626), new Vector2(540, 62), new Color(0.23f, 0.38f, 0.7f), Color.white);
            var toggleRow = Btn(card.transform, "ToggleRegister", "Chưa có tài khoản? Tạo mới", new Vector2(0, -700), new Vector2(540, 44), new Color(0, 0, 0, 0), Muted);
            status = Label(card.transform, "AuthStatus", "", 22, new Color(1f, 0.62f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0, -36), new Vector2(600, 60));

            UnityEventTools.AddPersistentListener(login.onClick, flow.LoginPassword);
            UnityEventTools.AddPersistentListener(register.onClick, flow.RegisterPassword);
            UnityEventTools.AddPersistentListener(google.onClick, flow.ContinueWithGoogle);
            UnityEventTools.AddPersistentListener(facebook.onClick, flow.ContinueWithFacebook);
            UnityEventTools.AddBoolPersistentListener(toggleRow.onClick, flow.SetRegisterMode, true);
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
            var vignette = Img(root.transform, "Vignette", new Vector2(0.5f, 0.5f), new Vector2(1920, 1080), new Color(0, 0, 0, 0.0f), null);
            vignette.raycastTarget = false;

            Label(root.transform, "HallTitle", "Chọn Anh Hùng", 46, Gold, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(800, 64), FontStyle.Bold).raycastTarget = false;

            // Class cards (left)
            GameObject cards = Panel(root.transform, "ClassCards", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(200, 40), new Vector2(330, 620), InkSoft, panelSprite);
            (string name, string role, HeroClass c, Color tint)[] classes =
            {
                ("Kiếm Sĩ", "Cận chiến • Bền bỉ", HeroClass.Swordsman, new Color(1f, 0.55f, 0.35f)),
                ("Pháp Sư", "Phép thuật • Tầm xa", HeroClass.Mage, new Color(0.7f, 0.5f, 1f)),
                ("Sát Thủ", "Song đao • Tốc độ", HeroClass.Assassin, new Color(1f, 0.32f, 0.42f)),
                ("Cung Thủ", "Cung tên • Cơ động", HeroClass.Archer, new Color(0.45f, 0.9f, 0.5f)),
            };
            for (int i = 0; i < classes.Length; i++)
            {
                var (n, role, c, tint) = classes[i];
                Button b = Btn(cards.transform, "Card_" + c, "", new Vector2(0, 225 - i * 150), new Vector2(290, 132), new Color(0.13f, 0.17f, 0.26f, 0.95f), Cream);
                var bar = Img(b.transform, "Accent", new Vector2(0f, 0.5f), new Vector2(10, 104), tint, pillSprite);
                bar.rectTransform.anchoredPosition = new Vector2(22, 0);
                Label(b.transform, "Name", n, 34, Cream, new Vector2(0.5f, 0.5f), new Vector2(12, 18), new Vector2(240, 46), FontStyle.Bold).alignment = TextAnchor.MiddleLeft;
                Label(b.transform, "Role", role, 20, tint, new Vector2(0.5f, 0.5f), new Vector2(12, -24), new Vector2(240, 30)).alignment = TextAnchor.MiddleLeft;
                switch (c)
                {
                    case HeroClass.Swordsman: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectSwordsman); break;
                    case HeroClass.Mage: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectMage); break;
                    case HeroClass.Assassin: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectAssassin); break;
                    default: UnityEventTools.AddPersistentListener(b.onClick, flow.SelectArcher); break;
                }
            }

            // Detail panel (right)
            GameObject info = Panel(root.transform, "ClassInfo", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-240, 70), new Vector2(420, 560), InkSoft, panelSprite);
            heroName = Label(info.transform, "HeroName", "", 48, Gold, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(380, 64), FontStyle.Bold);
            heroDesc = Label(info.transform, "HeroDescription", "", 24, Cream, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(370, 110));
            heroDesc.alignment = TextAnchor.UpperCenter;
            heroStats = Label(info.transform, "HeroStats", "", 22, Muted, new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(380, 40));
            var bars = info.AddComponent<HeroStatBars>();
            var bso = new SerializedObject(bars);
            var fills = bso.FindProperty("fills");
            string[] statNames = { "Máu", "Sát thương/giây", "Phòng thủ", "Tầm đánh" };
            fills.arraySize = statNames.Length;
            for (int i = 0; i < statNames.Length; i++)
            {
                float y = -300 - i * 54;
                Label(info.transform, "StatLabel" + i, statNames[i], 20, Muted, new Vector2(0.5f, 1f), new Vector2(-110, y), new Vector2(170, 30)).alignment = TextAnchor.MiddleLeft;
                Img(info.transform, "StatBack" + i, new Vector2(0.5f, 1f), new Vector2(200, 14), new Color(1, 1, 1, 0.12f), pillSprite).rectTransform.anchoredPosition = new Vector2(80, y);
                var fill = Img(info.transform, "StatFill" + i, new Vector2(0.5f, 1f), new Vector2(200, 14), Gold, pillSprite);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.anchoredPosition = new Vector2(-20, y);
                fills.GetArrayElementAtIndex(i).objectReferenceValue = fill.rectTransform;
            }
            bso.FindProperty("preview").objectReferenceValue = preview;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // Preview actions (bottom centre)
            Button atk = Btn(root.transform, "TryAttack", "Xem đòn đánh", new Vector2(-220, -330), new Vector2(240, 64), new Color(0.2f, 0.26f, 0.38f, 0.95f), Cream, new Vector2(0.5f, 0.5f));
            Button skl = Btn(root.transform, "TrySkill", "Xem kỹ năng", new Vector2(40, -330), new Vector2(240, 64), new Color(0.48f, 0.3f, 0.7f, 0.95f), Cream, new Vector2(0.5f, 0.5f));
            Button rst = Btn(root.transform, "ResetView", "Đặt lại góc nhìn", new Vector2(290, -330), new Vector2(220, 64), new Color(0.2f, 0.26f, 0.38f, 0.95f), Muted, new Vector2(0.5f, 0.5f));
            UnityEventTools.AddPersistentListener(atk.onClick, preview.PlayAttackPreview);
            UnityEventTools.AddPersistentListener(skl.onClick, preview.PlaySkillPreview);
            UnityEventTools.AddPersistentListener(rst.onClick, preview.ResetRotation);
            Label(root.transform, "DragHint", "Kéo để xoay nhân vật", 20, Muted, new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(400, 30)).raycastTarget = false;

            // Slots + naming (bottom bar)
            GameObject bottom = Panel(root.transform, "SlotBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1700, 150), Ink, panelSprite);
            slots = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                Button s = Btn(bottom.transform, "Slot" + i, "", new Vector2(-660 + i * 250, 0), new Vector2(232, 104), new Color(0.13f, 0.17f, 0.26f, 1f), Cream);
                slots[i] = Label(s.transform, "Label", "Ô " + (i + 1), 20, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(214, 96));
                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot0); break;
                    case 1: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot1); break;
                    case 2: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot2); break;
                    default: UnityEventTools.AddPersistentListener(s.onClick, flow.SelectSlot3); break;
                }
            }
            charName = Field(bottom.transform, "CharacterNameInput", "Tên nhân vật (3–16 ký tự)", new Vector2(420, 28), false, new Vector2(340, 56), new Vector2(0.5f, 0.5f));
            Button create = Btn(bottom.transform, "CreateAndEnter", "Tạo & vào game", new Vector2(330, -34), new Vector2(200, 56), Gold, new Color(0.15f, 0.1f, 0.05f));
            Button enter = Btn(bottom.transform, "EnterGame", "Vào game", new Vector2(560, -34), new Vector2(200, 56), new Color(0.3f, 0.6f, 0.4f), Color.white);
            existing = Label(bottom.transform, "ExistingDetails", "", 20, Muted, new Vector2(0.5f, 1f), new Vector2(0, 26), new Vector2(1200, 30));
            status = Label(root.transform, "SelectionStatus", "", 22, new Color(1f, 0.62f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0, 186), new Vector2(1200, 34));
            UnityEventTools.AddPersistentListener(create.onClick, flow.CreateCharacterAndEnter);
            UnityEventTools.AddPersistentListener(enter.onClick, flow.EnterSelectedCharacter);
            Button back = Btn(root.transform, "BackToLogin", "Đăng xuất", new Vector2(-120, -60), new Vector2(180, 52), new Color(0.2f, 0.26f, 0.38f, 0.9f), Muted, new Vector2(1f, 1f));
            UnityEventTools.AddPersistentListener(back.onClick, flow.BackToAuthentication);

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

        private static Sprite RoundedSprite(string name, int size, int radius)
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

        private static GameObject Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c, Sprite s)
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

        private static Image Img(Transform parent, string name, Vector2 anchor, Vector2 size, Color c, Sprite s)
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

        private static Text Label(Transform parent, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.color = c; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, Color fg, Vector2? anchor = null)
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
            img.sprite = pillSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = go.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            if (!string.IsNullOrEmpty(label))
                Label(go.transform, "Text", label, size.y > 60 ? 26 : 22, fg, new Vector2(0.5f, 0.5f), Vector2.zero, size, FontStyle.Bold).raycastTarget = false;
            return b;
        }

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
            img.sprite = pillSprite; img.type = Image.Type.Sliced; img.color = new Color(1, 1, 1, 0.1f);
            var text = Label(go.transform, "Text", "", 24, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, sz - new Vector2(40, 10));
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            Object.DestroyImmediate(text.GetComponent<Shadow>());
            var ph = Label(go.transform, "Placeholder", placeholder, 22, new Color(0.7f, 0.75f, 0.85f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, sz - new Vector2(40, 10), FontStyle.Italic);
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

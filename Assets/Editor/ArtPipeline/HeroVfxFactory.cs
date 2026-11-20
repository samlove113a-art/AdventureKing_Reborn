using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Generates the original skill VFX for the four classes: procedural textures (soft dot, ring,
    /// slash gradient, sparkle star, rune circle), a crescent slash mesh, an arrow mesh, additive
    /// materials and effect prefabs, then wires them into HeroCombatPresentation.
    /// Each class gets its own colour language and shape language:
    ///   Swordsman gold/crimson heavy arcs + sparks, Mage violet/cyan rune circle + orb + burst,
    ///   Assassin crimson/black crossed quick slashes + spin ring, Archer leaf-green arrows + wind.
    /// </summary>
    public static class HeroVfxFactory
    {
        private const string Root = "Assets/Art/VFX";
        private static Material dotAdd, ringAdd, slashAdd, starAdd, runeAdd, smokeAlpha;
        private static Mesh arcMesh, arcFullMesh, arrowMesh;

        [MenuItem("AdventureKing/Art/Rebuild Skill VFX")]
        public static void RebuildMenu()
        {
            Ensure(force: true);
            foreach (HeroClass c in System.Enum.GetValues(typeof(HeroClass))) BuildClass(c);
            AssetDatabase.SaveAssets();
        }

        public static void AssignClassEffects(HeroCombatPresentation pres, HeroClass c)
        {
            Ensure(force: false);
            BuildClass(c);
            var so = new SerializedObject(pres);
            void Set(string p, Object o) => so.FindProperty(p).objectReferenceValue = o;
            string d = $"{Root}/{c}";
            Set("attackEffect", Load($"{d}/{c}_AttackFX.prefab"));
            Set("skillCastEffect", Load($"{d}/{c}_SkillCastFX.prefab"));
            Set("skillEffect", Load($"{d}/{c}_SkillFX.prefab"));
            var atkProj = Load($"{d}/{c}_AttackProjectile.prefab");
            var skProj = Load($"{d}/{c}_SkillProjectile.prefab");
            Set("attackProjectile", atkProj ? atkProj.GetComponent<VfxProjectile>() : null);
            Set("skillProjectile", skProj ? skProj.GetComponent<VfxProjectile>() : null);
            so.FindProperty("skillProjectileCount").intValue = c == HeroClass.Archer ? 3 : 1;
            so.FindProperty("skillSpreadDegrees").floatValue = c == HeroClass.Archer ? 16f : 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Load(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p);

        // ------------------------------------------------------------------ shared assets
        private static void Ensure(bool force)
        {
            if (!force && dotAdd && arcMesh) return;
            Directory.CreateDirectory($"{Root}/Textures");
            Directory.CreateDirectory($"{Root}/Materials");
            Directory.CreateDirectory($"{Root}/Meshes");
            Texture2D dot = Tex("soft_dot", 128, (u, v) => { float r = Rad(u, v); float a = Mathf.Exp(-r * r * 9f); return new Color(1, 1, 1, a); });
            Texture2D ring = Tex("ring", 256, (u, v) => { float r = Rad(u, v); float a = Mathf.Exp(-Mathf.Pow((r - 0.82f) * 14f, 2)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.62f) * 22f, 2)); return new Color(1, 1, 1, Mathf.Clamp01(a)); });
            Texture2D slash = Tex("slash", 256, (u, v) =>
            {
                // u: tail(0) -> head(1); v: inner(0) -> outer edge(1)
                float edge = Mathf.Pow(Mathf.Clamp01(v), 2.2f) * Mathf.SmoothStep(1f, 0.0f, Mathf.Clamp01((v - 0.92f) / 0.08f));
                float core = Mathf.Exp(-Mathf.Pow((v - 0.8f) * 9f, 2));
                float along = Mathf.Pow(Mathf.Clamp01(u), 1.4f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / 0.06f));
                float a = Mathf.Clamp01((edge * 0.7f + core) * along);
                float white = Mathf.Clamp01(core * along * 1.4f);
                return new Color(Mathf.Lerp(1f, 1f, white), Mathf.Lerp(0.85f, 1f, white), Mathf.Lerp(0.7f, 1f, white), a);
            });
            Texture2D star = Tex("sparkle", 128, (u, v) =>
            {
                float x = Mathf.Abs(u * 2 - 1), y = Mathf.Abs(v * 2 - 1);
                float cross = Mathf.Exp(-x * 40f) * Mathf.Exp(-y * 3.2f) + Mathf.Exp(-y * 40f) * Mathf.Exp(-x * 3.2f);
                float core = Mathf.Exp(-(x * x + y * y) * 30f);
                return new Color(1, 1, 1, Mathf.Clamp01(cross + core));
            });
            Texture2D rune = Tex("rune_circle", 512, RuneCircle);
            Texture2D smoke = Tex("smoke", 128, (u, v) =>
            {
                float r = Rad(u, v);
                float n = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.6f + Mathf.PerlinNoise(u * 13f + 3f, v * 13f) * 0.4f;
                return new Color(1, 1, 1, Mathf.Clamp01((1f - r * 1.15f) * (0.55f + n * 0.7f)));
            });

            dotAdd = Mat("dot_add", dot, true);
            ringAdd = Mat("ring_add", ring, true);
            slashAdd = Mat("slash_add", slash, true);
            starAdd = Mat("sparkle_add", star, true);
            runeAdd = Mat("rune_add", rune, true);
            smokeAlpha = Mat("smoke_alpha", smoke, false);
            arcMesh = SaveMesh("slash_arc", BuildArc(150f, 0.62f, 0.2f));
            arcFullMesh = SaveMesh("spin_ring", BuildArc(340f, 0.75f, 0.16f));
            arrowMesh = SaveMesh("arrow", BuildArrow());
            AssetDatabase.SaveAssets();
        }

        private static float Rad(float u, float v) => Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;

        private static Color RuneCircle(float u, float v)
        {
            float r = Rad(u, v);
            float ang = Mathf.Atan2(v - 0.5f, u - 0.5f);
            float a = 0f;
            a += Mathf.Exp(-Mathf.Pow((r - 0.95f) * 60f, 2));
            a += Mathf.Exp(-Mathf.Pow((r - 0.86f) * 70f, 2));
            a += 0.8f * Mathf.Exp(-Mathf.Pow((r - 0.55f) * 55f, 2));
            // tick marks between the outer rings
            float ticks = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 24f)), 30f);
            if (r > 0.87f && r < 0.94f) a += ticks * 0.9f;
            // eight small glyph dots on the middle band
            float glyph = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 80f);
            a += glyph * Mathf.Exp(-Mathf.Pow((r - 0.7f) * 18f, 2)) * 1.2f;
            // inner six-point star (two triangles)
            float k = 0f;
            for (int i = 0; i < 6; i++)
            {
                float th = i * Mathf.PI / 3f + Mathf.PI / 6f;
                Vector2 n = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                float d = Vector2.Dot(new Vector2(u - 0.5f, v - 0.5f) * 2f, n);
                k += Mathf.Exp(-Mathf.Pow((d - 0.27f) * 70f, 2)) * (r < 0.56f ? 1f : 0f);
            }
            a += k * 0.9f;
            a *= r < 1f ? 1f : 0f;
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        }

        private static Texture2D Tex(string name, int size, System.Func<float, float, Color> f)
        {
            string path = $"{Root}/Textures/{name}.png";
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, f((x + 0.5f) / size, (y + 0.5f) / size));
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Mat(string name, Texture2D tex, bool additive)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("AdventureKing/VFX"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("AdventureKing/VFX");
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Intensity", additive ? 1.6f : 1f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = $"{Root}/Meshes/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Crescent in the local XY plane (swing plane), sweeping counter-clockwise; u = along arc.</summary>
        private static Mesh BuildArc(float sweepDeg, float radius, float width)
        {
            const int seg = 40;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float a = Mathf.Deg2Rad * (-sweepDeg * 0.5f + sweepDeg * t);
                float w = width * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.85f + 0.15f));
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = dir * (radius - w);
                v[i * 2 + 1] = dir * radius;
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
                if (i < seg)
                {
                    int k = i * 6, b = i * 2;
                    tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
                    tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
                }
            }
            var m = new Mesh { name = "arc", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Stylised arrow along +Z: shaft, leaf-shaped head and two fletch fins (vertex-coloured).</summary>
        private static Mesh BuildArrow()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            var norms = new System.Collections.Generic.List<Vector3>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                // Separate vertices per side: shared verts with both windings average their normals to zero (renders black).
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = verts.Count;
                verts.AddRange(new[] { a, b, c, d, a, b, c, d });
                cols.AddRange(new[] { col, col, col, col, col, col, col, col });
                norms.AddRange(new[] { n, n, n, n, -n, -n, -n, -n });
                // Unity's face normal for (a,b,c) is Cross(b-a, c-a): front side keeps n, back side is reversed with -n.
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i + 4, i + 6, i + 5, i + 4, i + 7, i + 6 });
            }
            Color shaft = new Color(0.78f, 0.6f, 0.36f), head = new Color(0.82f, 0.88f, 0.9f), fl = new Color(0.36f, 0.72f, 0.36f);
            float s = 0.012f;
            Quad(new Vector3(-s, 0, -0.42f), new Vector3(s, 0, -0.42f), new Vector3(s, 0, 0.32f), new Vector3(-s, 0, 0.32f), shaft);
            Quad(new Vector3(0, -s, -0.42f), new Vector3(0, s, -0.42f), new Vector3(0, s, 0.32f), new Vector3(0, -s, 0.32f), shaft);
            Quad(new Vector3(-0.05f, 0, 0.3f), new Vector3(0.05f, 0, 0.3f), new Vector3(0.012f, 0, 0.46f), new Vector3(-0.012f, 0, 0.46f), head);
            Quad(new Vector3(0, -0.05f, 0.3f), new Vector3(0, 0.05f, 0.3f), new Vector3(0, 0.012f, 0.46f), new Vector3(0, -0.012f, 0.46f), head);
            Quad(new Vector3(-0.055f, 0, -0.42f), new Vector3(0.055f, 0, -0.42f), new Vector3(0.01f, 0, -0.28f), new Vector3(-0.01f, 0, -0.28f), fl);
            Quad(new Vector3(0, -0.055f, -0.42f), new Vector3(0, 0.055f, -0.42f), new Vector3(0, 0.01f, -0.28f), new Vector3(0, -0.01f, -0.28f), fl);
            var m = new Mesh { name = "arrow" };
            for (int v = 0; v < verts.Count; v++) verts[v] *= 0.62f; // ~0.55 m arrow next to a 1.6 m chibi hero
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ building blocks
        private static GameObject ArcFx(Transform parent, Mesh mesh, Color tint, float scale, Vector3 euler, float life, Vector3 spin)
        {
            var go = new GameObject("Arc");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mesh == arcMesh || mesh == arcFullMesh ? slashAdd : dotAdd;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var fade = go.AddComponent<VfxAutoDestroy>();
            var so = new SerializedObject(fade);
            so.FindProperty("lifetime").floatValue = life;
            so.FindProperty("tint").colorValue = tint;
            so.FindProperty("spinDegreesPerSecond").vector3Value = spin;
            var rs = so.FindProperty("fadeRenderers");
            rs.arraySize = 1;
            rs.GetArrayElementAtIndex(0).objectReferenceValue = mr;
            so.FindProperty("alphaOverLife").animationCurveValue = new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f));
            so.FindProperty("scaleOverLife").animationCurveValue = new AnimationCurve(new Keyframe(0, 0.82f), new Keyframe(1f, 1.12f));
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static ParticleSystem Burst(Transform parent, string name, Material mat, Color a, Color b, int count,
            float speedMin, float speedMax, float sizeMin, float sizeMax, float life, float gravity = 0f,
            ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere, float radius = 0.05f, bool stretch = false, float rate = 0f, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, life);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(count, 64);
            main.playOnAwake = true;
            var em = ps.emission;
            em.rateOverTime = rate;
            if (count > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = shape;
            sh.radius = radius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.15f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.2f;
            }
            return ps;
        }

        private static GameObject FxRoot(string name, float life)
        {
            var go = new GameObject(name);
            var so = new SerializedObject(go.AddComponent<VfxAutoDestroy>());
            so.FindProperty("lifetime").floatValue = life;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static GameObject Ground(Transform parent, Material mat, Color tint, float size, float life, float spin)
        {
            var go = new GameObject("GroundGlyph");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localPosition = new Vector3(0, 0.02f, 0);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fade = go.AddComponent<VfxAutoDestroy>();
            var so = new SerializedObject(fade);
            so.FindProperty("lifetime").floatValue = life;
            so.FindProperty("tint").colorValue = tint;
            so.FindProperty("spinDegreesPerSecond").vector3Value = new Vector3(0, 0, spin);
            var rs = so.FindProperty("fadeRenderers");
            rs.arraySize = 1;
            rs.GetArrayElementAtIndex(0).objectReferenceValue = mr;
            so.FindProperty("alphaOverLife").animationCurveValue = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.18f, 1), new Keyframe(0.75f, 0.9f), new Keyframe(1, 0));
            so.FindProperty("scaleOverLife").animationCurveValue = new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1, 1.05f));
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void Save(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static GameObject Projectile(string name, float speed, float range, GameObject impact, float lifeLinger, out Transform body)
        {
            var go = new GameObject(name);
            var p = go.AddComponent<VfxProjectile>();
            body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            var so = new SerializedObject(p);
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("impactEffect").objectReferenceValue = impact;
            so.FindProperty("lingerSeconds").floatValue = lifeLinger;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void SetProjectileFx(GameObject go, ParticleSystem trail, GameObject hide)
        {
            var so = new SerializedObject(go.GetComponent<VfxProjectile>());
            var d = so.FindProperty("detachOnImpact");
            d.arraySize = trail ? 1 : 0;
            if (trail) d.GetArrayElementAtIndex(0).objectReferenceValue = trail;
            var h = so.FindProperty("hideOnImpact");
            h.arraySize = hide ? 1 : 0;
            if (hide) h.GetArrayElementAtIndex(0).objectReferenceValue = hide;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ per class
        private static void BuildClass(HeroClass c)
        {
            string d = $"{Root}/{c}";
            switch (c)
            {
                case HeroClass.Swordsman:
                {
                    Color gold = new Color(1f, 0.82f, 0.4f), white = new Color(1f, 0.97f, 0.9f), crimson = new Color(1f, 0.35f, 0.25f);
                    var atk = FxRoot("Swordsman_AttackFX", 0.45f);
                    ArcFx(atk.transform, arcMesh, gold * 1.6f, 1.15f, new Vector3(0, -90, 35), 0.22f, new Vector3(0, 0, -260));
                    Burst(atk.transform, "Sparks", starAdd, gold, white, 12, 2.5f, 5f, 0.06f, 0.14f, 0.32f, 0.8f, stretch: true).transform.localPosition = new Vector3(0, 0, 0.35f);
                    Burst(atk.transform, "Flash", dotAdd, white, gold, 1, 0f, 0f, 0.55f, 0.6f, 0.12f).transform.localPosition = new Vector3(0, 0, 0.35f);
                    Save(atk, $"{d}/Swordsman_AttackFX.prefab");

                    var sk = FxRoot("Swordsman_SkillFX", 0.55f);
                    ArcFx(sk.transform, arcMesh, crimson * 1.8f, 1.6f, new Vector3(0, -90, 20), 0.28f, new Vector3(0, 0, -320));
                    ArcFx(sk.transform, arcMesh, gold * 1.5f, 1.35f, new Vector3(0, -90, -30), 0.24f, new Vector3(0, 0, -300));
                    Burst(sk.transform, "Sparks", starAdd, gold, crimson, 22, 3f, 7f, 0.07f, 0.18f, 0.4f, 1f, stretch: true).transform.localPosition = new Vector3(0, 0, 0.5f);
                    var shock = Ground(sk.transform, ringAdd, crimson * 1.4f, 1.6f, 0.35f, 0f);
                    shock.transform.localPosition = new Vector3(0, -0.5f, 0.45f);
                    Save(sk, $"{d}/Swordsman_SkillFX.prefab");

                    var cast = FxRoot("Swordsman_SkillCastFX", 0.9f);
                    Ground(cast.transform, ringAdd, gold * 1.2f, 1.4f, 0.8f, 40f);
                    Burst(cast.transform, "Embers", dotAdd, gold, crimson, 18, 0.4f, 1.2f, 0.05f, 0.1f, 0.8f, -0.3f, ParticleSystemShapeType.Circle, 0.5f);
                    Save(cast, $"{d}/Swordsman_SkillCastFX.prefab");
                    break;
                }
                case HeroClass.Mage:
                {
                    Color violet = new Color(0.72f, 0.42f, 1f), cyan = new Color(0.45f, 0.85f, 1f), white = new Color(0.95f, 0.92f, 1f);
                    var hit = FxRoot("Mage_ImpactFX", 0.7f);
                    Burst(hit.transform, "Burst", dotAdd, violet, cyan, 26, 1.5f, 4.5f, 0.08f, 0.2f, 0.55f);
                    Burst(hit.transform, "Stars", starAdd, white, cyan, 8, 0.8f, 2f, 0.12f, 0.22f, 0.5f);
                    var ring = Ground(hit.transform, ringAdd, violet * 1.6f, 1.1f, 0.4f, 0f);
                    ring.transform.localRotation = Quaternion.identity;
                    Save(hit, $"{d}/Mage_ImpactFX.prefab");

                    var bigHit = FxRoot("Mage_BigImpactFX", 1.0f);
                    Burst(bigHit.transform, "Burst", dotAdd, violet, cyan, 60, 2f, 7f, 0.1f, 0.3f, 0.8f);
                    Burst(bigHit.transform, "Stars", starAdd, white, violet, 16, 1f, 3f, 0.16f, 0.32f, 0.7f);
                    var r1 = Ground(bigHit.transform, ringAdd, cyan * 1.8f, 2.4f, 0.5f, 0f); r1.transform.localRotation = Quaternion.identity;
                    var r2 = Ground(bigHit.transform, ringAdd, violet * 1.6f, 1.5f, 0.6f, 0f); r2.transform.localRotation = Quaternion.identity;
                    Save(bigHit, $"{d}/Mage_BigImpactFX.prefab");

                    foreach (bool big in new[] { false, true })
                    {
                        var proj = Projectile(big ? "Mage_SkillProjectile" : "Mage_AttackProjectile", big ? 8f : 11f, big ? 6.5f : 5.5f,
                            Load(big ? $"{d}/Mage_BigImpactFX.prefab" : $"{d}/Mage_ImpactFX.prefab"), 0.7f, out Transform body);
                        // LDR pipeline: a white additive core stacks to pure white; keep it violet-tinted and smaller.
                        Color coreA = new Color(0.85f, 0.7f, 1f) * 0.55f, coreB = violet * 0.5f;
                        var core = Burst(body, "Core", dotAdd, coreA, coreB, 0, 0f, 0f, big ? 0.42f : 0.3f, big ? 0.48f : 0.34f, 0.12f, rate: 24f, loop: true);
                        var main = core.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        var trail = Burst(proj.transform, "Trail", dotAdd, violet, cyan, 0, 0f, 0.2f, big ? 0.22f : 0.14f, big ? 0.3f : 0.2f, 0.4f, rate: 70f, loop: true);
                        Burst(body, "Motes", starAdd, white, cyan, 0, 0.2f, 0.6f, 0.06f, 0.1f, 0.35f, rate: 18f, loop: true);
                        SetProjectileFx(proj, trail, body.gameObject);
                        Save(proj, $"{d}/{proj.name}.prefab");
                    }

                    var atk = FxRoot("Mage_AttackFX", 0.4f);
                    Burst(atk.transform, "Flash", dotAdd, white, violet, 1, 0f, 0f, 0.45f, 0.5f, 0.15f);
                    Burst(atk.transform, "Spark", starAdd, cyan, violet, 6, 0.6f, 1.5f, 0.08f, 0.14f, 0.3f);
                    Save(atk, $"{d}/Mage_AttackFX.prefab");

                    var cast = FxRoot("Mage_SkillCastFX", 1.5f);
                    Ground(cast.transform, runeAdd, violet * 1.7f, 2.1f, 1.45f, 55f);
                    Ground(cast.transform, ringAdd, cyan * 1.3f, 1.3f, 1.4f, -80f);
                    Burst(cast.transform, "Rise", dotAdd, cyan, violet, 0, 0.6f, 1.4f, 0.05f, 0.1f, 0.9f, -0.2f, ParticleSystemShapeType.Circle, 0.8f, rate: 40f).transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    Save(cast, $"{d}/Mage_SkillCastFX.prefab");

                    var sk = FxRoot("Mage_SkillFX", 0.5f);
                    Burst(sk.transform, "Flash", dotAdd, white, cyan, 1, 0f, 0f, 0.8f, 0.9f, 0.18f);
                    Save(sk, $"{d}/Mage_SkillFX.prefab");
                    break;
                }
                case HeroClass.Assassin:
                {
                    Color crimson = new Color(1f, 0.22f, 0.32f), dark = new Color(0.35f, 0.12f, 0.4f), white = new Color(1f, 0.9f, 0.92f);
                    var atk = FxRoot("Assassin_AttackFX", 0.3f);
                    ArcFx(atk.transform, arcMesh, crimson * 1.8f, 0.85f, new Vector3(0, -90, 50), 0.14f, new Vector3(0, 0, -520));
                    ArcFx(atk.transform, arcMesh, white * 1.2f, 0.75f, new Vector3(0, -90, -40), 0.12f, new Vector3(0, 0, 520));
                    Burst(atk.transform, "Cuts", starAdd, crimson, white, 8, 3f, 6f, 0.04f, 0.09f, 0.18f, stretch: true).transform.localPosition = new Vector3(0, 0, 0.3f);
                    Save(atk, $"{d}/Assassin_AttackFX.prefab");

                    var sk = FxRoot("Assassin_SkillFX", 0.45f);
                    var ringArc = ArcFx(sk.transform, arcFullMesh, crimson * 1.9f, 1.15f, new Vector3(90, 0, 0), 0.26f, new Vector3(0, 0, -900));
                    ringArc.transform.localPosition = new Vector3(0, -0.05f, -0.3f);
                    Burst(sk.transform, "Shadow", smokeAlpha, dark, new Color(0.1f, 0.05f, 0.12f, 0.8f), 10, 0.5f, 1.4f, 0.25f, 0.45f, 0.4f, 0f, ParticleSystemShapeType.Circle, 0.4f);
                    Burst(sk.transform, "Cuts", starAdd, crimson, white, 14, 3f, 7f, 0.05f, 0.1f, 0.22f, stretch: true, shape: ParticleSystemShapeType.Circle, radius: 0.5f);
                    Save(sk, $"{d}/Assassin_SkillFX.prefab");

                    var cast = FxRoot("Assassin_SkillCastFX", 0.6f);
                    Burst(cast.transform, "Smoke", smokeAlpha, dark, new Color(0.15f, 0.05f, 0.2f, 0.85f), 16, 0.3f, 1f, 0.35f, 0.6f, 0.55f, -0.1f, ParticleSystemShapeType.Circle, 0.35f);
                    Ground(cast.transform, ringAdd, crimson * 1.2f, 1.2f, 0.4f, 0f);
                    Save(cast, $"{d}/Assassin_SkillCastFX.prefab");
                    break;
                }
                case HeroClass.Archer:
                {
                    Color leaf = new Color(0.5f, 1f, 0.45f), gold = new Color(1f, 0.92f, 0.55f), white = new Color(0.95f, 1f, 0.9f);
                    var hit = FxRoot("Archer_ImpactFX", 0.6f);
                    Burst(hit.transform, "Leaves", starAdd, leaf, gold, 14, 1.5f, 3.5f, 0.06f, 0.14f, 0.45f, 0.6f, stretch: true);
                    Burst(hit.transform, "Puff", dotAdd, white, leaf, 1, 0f, 0f, 0.4f, 0.45f, 0.14f);
                    Save(hit, $"{d}/Archer_ImpactFX.prefab");

                    foreach (bool big in new[] { false, true })
                    {
                        var proj = Projectile(big ? "Archer_SkillProjectile" : "Archer_AttackProjectile", big ? 17f : 15f, big ? 7f : 6.5f,
                            Load($"{d}/Archer_ImpactFX.prefab"), 0.5f, out Transform body);
                        var arrow = new GameObject("Arrow");
                        arrow.transform.SetParent(body, false);
                        arrow.transform.localScale = Vector3.one * (big ? 1.25f : 1f);
                        arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
                        var mr = arrow.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = ArrowMaterial();
                        var trail = Burst(proj.transform, "Wind", dotAdd, big ? gold : leaf, white, 0, 0f, 0.1f, 0.06f, 0.1f, 0.25f, rate: 90f, loop: true);
                        SetProjectileFx(proj, trail, body.gameObject);
                        Save(proj, $"{d}/{proj.name}.prefab");
                    }

                    var atk = FxRoot("Archer_AttackFX", 0.3f);
                    Burst(atk.transform, "Snap", starAdd, white, leaf, 4, 0.5f, 1.2f, 0.06f, 0.1f, 0.2f);
                    Save(atk, $"{d}/Archer_AttackFX.prefab");

                    var sk = FxRoot("Archer_SkillFX", 0.45f);
                    Burst(sk.transform, "Gust", dotAdd, gold, leaf, 12, 1f, 3f, 0.1f, 0.2f, 0.35f, 0f, ParticleSystemShapeType.Cone, 0.1f);
                    Save(sk, $"{d}/Archer_SkillFX.prefab");

                    var cast = FxRoot("Archer_SkillCastFX", 1.2f);
                    Ground(cast.transform, ringAdd, leaf * 1.4f, 1.5f, 1.1f, 90f);
                    Burst(cast.transform, "Breeze", starAdd, leaf, gold, 0, 0.5f, 1.2f, 0.05f, 0.1f, 0.7f, -0.25f, ParticleSystemShapeType.Circle, 0.6f, rate: 26f);
                    Save(cast, $"{d}/Archer_SkillCastFX.prefab");
                    break;
                }
            }
        }

        private static Material ArrowMaterial()
        {
            string path = "Assets/Art/VFX/Materials/arrow_toon.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find(HeroArtPipeline.ToonShader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_OutlineWidth", 0f); // shaft is 2.4 cm wide: any outline turns it black
            m.SetShaderPassEnabled("SRPDefaultUnlit", false); // and skip the hull pass entirely (no z-fighting backfaces)
            m.SetFloat("_UseVertexColor", 1f);
            m.EnableKeyword("_VERTEX_COLOR");
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}

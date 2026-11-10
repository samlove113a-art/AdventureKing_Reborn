using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Building blocks for the class skill VFX (SkillVfxDirector): animated layers on the AdventureKing/VFXFlow shader
    /// (shape mask x scrolling noise, core/edge HDR gradient, dissolve rim), code-configured particle bursts, ribbon
    /// trails and small procedural meshes. Textures/materials: Resources/SkillVfx (Editor: SkillVfxArt). Original art.
    /// Quality: High on PC, a lighter set on phones (fewer particles, no secondary layers).
    /// </summary>
    public static class SkillVfxKit
    {
        public static bool Mobile => Application.isMobilePlatform || ForceMobile;
        /// <summary>QA: preview the phone set on PC.</summary>
        public static bool ForceMobile;
        public static float Density => Mobile ? 0.45f : 1f;
        /// <summary>HDR core gain: keeps each skill's colour identity under bloom instead of burning to white.</summary>
        public const float CoreGain = 0.75f;
        private static Color G(Color c) => new Color(c.r * CoreGain, c.g * CoreGain, c.b * CoreGain, c.a);

        private static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        private static Material add, premul;
        private static readonly Dictionary<string, Material> particleMats = new Dictionary<string, Material>();
        private static Mesh quad;
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static Texture2D Tex(string name)
        {
            if (!tex.TryGetValue(name, out Texture2D t) || !t) tex[name] = t = Resources.Load<Texture2D>("SkillVfx/" + name);
            return t;
        }

        public static Material Mat(bool additive)
        {
            if (!add) add = Resources.Load<Material>("SkillVfx/vfx_add");
            if (!premul) premul = Resources.Load<Material>("SkillVfx/vfx_premul");
            return additive ? add : premul;
        }

        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "vfx_quad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.RecalculateBounds();
                return quad;
            }
        }

        // ------------------------------------------------------------------ layers
        /// <summary>A textured layer (quad by default) that animates itself and is destroyed at the end of its life.</summary>
        public static FxLayer Layer(string texture, Vector3 pos, float size, Color core, Color edge, float life, bool additive = true, Mesh mesh = null)
        {
            var go = new GameObject("fx_" + texture);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh ? mesh : Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(additive);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var l = go.AddComponent<FxLayer>();
            l.Init(r, Tex(texture), size, G(core), edge, life);
            return l;
        }

        /// <summary>A procedural mesh with the same layered shader (blades, shards, rocks, ribbons).</summary>
        public static FxLayer MeshLayer(Mesh mesh, string texture, Vector3 pos, float size, Color core, Color edge, float life, bool additive = true)
            => Layer(texture, pos, size, core, edge, life, additive, mesh);

        // ------------------------------------------------------------------ particles
        public struct BurstSpec
        {
            public string Texture; public int Count; public float SpeedMin, SpeedMax, SizeMin, SizeMax, LifeMin, LifeMax;
            public Color Core, Edge; public float Gravity, Drag; public bool Stretch, Additive; public Vector3 Direction; public float Spread;
            public float Radius; public float Delay; public bool Ground; public float SpinMax;
        }

        public static BurstSpec Spec(string texture, int count, Color core, Color edge) => new BurstSpec
        {
            Texture = texture, Count = count, SpeedMin = 2f, SpeedMax = 6f, SizeMin = 0.1f, SizeMax = 0.25f, LifeMin = 0.35f, LifeMax = 0.8f,
            Core = core, Edge = edge, Additive = true, Direction = Vector3.up, Spread = 180f,
        };

        public static ParticleSystem Burst(Vector3 pos, BurstSpec s)
        {
            var go = new GameObject("fx_burst_" + s.Texture);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(s.Direction.sqrMagnitude > 0 ? s.Direction.normalized : Vector3.up, Vector3.forward);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startDelay = s.Delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifeMin, s.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, s.SpinMax > 0 ? Mathf.PI * 2f : 0f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 1f, 1f, 0.75f));
            main.gravityModifier = s.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            int n = Mathf.Max(1, Mathf.RoundToInt(s.Count * Density));
            main.maxParticles = n;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)n) });
            var sh = ps.shape;
            sh.shapeType = s.Spread >= 179f ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            sh.angle = Mathf.Min(s.Spread, 89f);
            sh.radius = Mathf.Max(0.01f, s.Radius);
            if (s.Ground) { sh.shapeType = ParticleSystemShapeType.Circle; sh.rotation = new Vector3(-90f, 0f, 0f); }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
            if (s.Drag > 0f) { var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = s.Drag; lim.multiplyDragByParticleSize = false; }
            if (s.SpinMax > 0f) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-s.SpinMax, s.SpinMax); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(s.Additive);
            r.renderMode = s.Stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (s.Stretch) { r.velocityScale = 0.06f; r.lengthScale = 1.6f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", Tex(s.Texture));
            block.SetColor("_ColorCore", G(s.Core));
            block.SetColor("_ColorEdge", s.Edge);
            block.SetFloat("_NoiseAmount", 0f);
            block.SetFloat("_CoreSharp", 1.5f);
            r.SetPropertyBlock(block);
            ps.Play();
            Object.Destroy(go, s.Delay + s.LifeMax + 0.5f);
            return ps;
        }

        // ------------------------------------------------------------------ trails
        public static TrailRenderer Trail(GameObject on, string texture, float width, float time, Color core, Color edge, float uvScroll = -2f)
        {
            var tr = on.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Mat(true);
            tr.time = time;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
            tr.textureMode = LineTextureMode.Stretch;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.alignment = LineAlignment.View;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", Tex(texture));
            block.SetColor("_ColorCore", G(core));
            block.SetColor("_ColorEdge", edge);
            block.SetVector("_UVScroll", new Vector4(uvScroll, 0f, 0f, 0f));
            block.SetVector("_NoiseScale", new Vector4(2f, 1f, -3f, 0f));
            block.SetFloat("_NoiseAmount", 0.45f);
            tr.SetPropertyBlock(block);
            return tr;
        }

        // ------------------------------------------------------------------ meshes (procedural, cached)
        private static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!meshes.TryGetValue(key, out Mesh m) || !m) meshes[key] = m = make();
            return m;
        }

        private static Mesh Build(string name, List<Vector3> v, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            var c = new Color[v.Count]; for (int i = 0; i < c.Length; i++) c[i] = Color.white; m.colors = c;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Open cylinder band (tornado / pillar), u around, v up; height 1, radius 0.5.</summary>
        public static Mesh Band => Cached("band", () =>
        {
            const int seg = 40;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
                v.Add(d); v.Add(d * 1.12f + Vector3.up); uv.Add(new Vector2(i / (float)seg * 3f, 0f)); uv.Add(new Vector2(i / (float)seg * 3f, 1f));
                if (i < seg) { int k = i * 2; t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            return Build("vfx_band", v, uv, t);
        });

        /// <summary>Long double-edged luminous blade with a cross guard (Thiên Kiếm), length 1 along +Y, tip up.</summary>
        public static Mesh Blade => Cached("blade", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            void Diamond(float y0, float y1, float w, float d, float tipW)
            {
                int b = v.Count;
                Vector3[] ring0 = { new Vector3(-w, y0, 0), new Vector3(0, y0, -d), new Vector3(w, y0, 0), new Vector3(0, y0, d) };
                Vector3[] ring1 = { new Vector3(-tipW, y1, 0), new Vector3(0, y1, -d * tipW / Mathf.Max(w, 1e-4f)), new Vector3(tipW, y1, 0), new Vector3(0, y1, d * tipW / Mathf.Max(w, 1e-4f)) };
                for (int i = 0; i < 4; i++) { v.Add(ring0[i]); uv.Add(new Vector2(i / 4f, y0)); }
                for (int i = 0; i < 4; i++) { v.Add(ring1[i]); uv.Add(new Vector2(i / 4f, y1)); }
                for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; t.AddRange(new[] { b + i, b + 4 + i, b + j, b + j, b + 4 + i, b + 4 + j }); }
            }
            Diamond(0.18f, 0.9f, 0.075f, 0.02f, 0.05f);    // blade
            Diamond(0.9f, 1.0f, 0.05f, 0.02f, 0.0005f);    // tip
            Diamond(0.13f, 0.18f, 0.22f, 0.04f, 0.2f);     // guard
            Diamond(0f, 0.13f, 0.03f, 0.03f, 0.03f);       // hilt
            return Build("vfx_blade", v, uv, t);
        });

        /// <summary>Crystal shard: elongated hexagonal bipyramid, length 1 along +Y.</summary>
        public static Mesh Shard => Cached("shard", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            v.Add(new Vector3(0, 0, 0)); uv.Add(new Vector2(0.5f, 0));
            v.Add(new Vector3(0, 1, 0)); uv.Add(new Vector2(0.5f, 1));
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; v.Add(new Vector3(Mathf.Cos(a) * 0.14f, 0.3f, Mathf.Sin(a) * 0.14f)); uv.Add(new Vector2(i / 6f, 0.3f)); }
            for (int i = 0; i < 6; i++) { int a = 2 + i, b = 2 + (i + 1) % 6; t.AddRange(new[] { 0, a, b, 1, b, a }); }
            return Build("vfx_shard", v, uv, t);
        });

        /// <summary>Lumpy rock (meteor core), radius ~0.5.</summary>
        public static Mesh Rock => Cached("rock", () =>
        {
            const int la = 10, lo = 16;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var rng = new System.Random(42);
            for (int i = 0; i <= la; i++)
                for (int j = 0; j <= lo; j++)
                {
                    float th = i / (float)la * Mathf.PI, ph = j / (float)lo * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float r = 0.5f * (0.82f + 0.18f * Mathf.PerlinNoise(d.x * 2.1f + 3f, d.y * 2.1f + d.z * 1.7f + 5f) + (j == lo ? 0f : 0f));
                    v.Add(d * r); uv.Add(new Vector2(j / (float)lo, i / (float)la));
                    if (i < la && j < lo) { int a = i * (lo + 1) + j, b = a + lo + 1; t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
                }
            _ = rng;
            return Build("vfx_rock", v, uv, t);
        });

        /// <summary>Throwing dagger / arrow: blade + shaft along +Z, length 1 (head at +Z).</summary>
        public static Mesh Dart => Cached("dart", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            void Quad4(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int k = v.Count; v.AddRange(new[] { a, b, c, d }); uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }); t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
            // head (two crossed diamonds) + shaft (two crossed thin quads) — readable from the side view
            for (int s = 0; s < 2; s++)
            {
                Vector3 w = s == 0 ? Vector3.up : Vector3.right;
                Quad4(new Vector3(0, 0, 0.62f), w * 0.09f + new Vector3(0, 0, 0.74f), new Vector3(0, 0, 1f), -w * 0.09f + new Vector3(0, 0, 0.74f));
                Quad4(-w * 0.018f, w * 0.018f, w * 0.018f + new Vector3(0, 0, 0.64f), -w * 0.018f + new Vector3(0, 0, 0.64f));
                Quad4(new Vector3(0, 0, 0f), w * 0.07f + new Vector3(0, 0, -0.04f), w * 0.05f + new Vector3(0, 0, 0.16f), new Vector3(0, 0, 0.14f));
            }
            return Build("vfx_dart", v, uv, t);
        });

        // ------------------------------------------------------------------ camera & time feedback
        public static void Shake(float amplitude, float seconds) { CameraFollow2D.AddShake(amplitude * (Mobile ? 0.8f : 1f), seconds); }
        public static void ZoomPunch(float amount, float seconds) { CameraFollow2D.AddZoomPunch(amount, seconds); }

        private static float hitStopUntil;
        /// <summary>A few frames of near-freeze on a big hit (local presentation only; the server clock is unaffected).</summary>
        public static void HitStop(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + realSeconds);
            if (!hitStopRunner) hitStopRunner = new GameObject("fx_hitstop").AddComponent<HitStopRunner>();
            Time.timeScale = 0.06f;
        }
        private static HitStopRunner hitStopRunner;
        private sealed class HitStopRunner : MonoBehaviour
        {
            private void Update() { if (Time.unscaledTime >= hitStopUntil && Time.timeScale < 1f) { Time.timeScale = 1f; Destroy(gameObject); } }
            private void OnDestroy() { if (Time.timeScale < 1f) Time.timeScale = 1f; }
        }
    }

    /// <summary>One animated VFX layer: grows, fades, spins, drifts, dissolves; optional billboard / ground-flat / follow.</summary>
    public sealed class FxLayer : MonoBehaviour
    {
        private MeshRenderer r;
        private MaterialPropertyBlock block;
        private float life, t, delay, size;
        private Vector3 stretch = Vector3.one;
        private float scaleFrom = 1f, scaleTo = 1f, scalePow = 1f;
        private float fadeIn = 0.08f, fadeOutStart = 0.6f, alphaMul = 1f;
        private float dissolveFrom, dissolveTo, dissolveStart = 1f;
        private float spin; private Vector3 spinAxis = Vector3.forward;
        private Vector3 velocity, gravity;
        private bool billboard;
        private Transform follow; private Vector3 followOffset;
        private Color core, edge;

        public void Init(MeshRenderer renderer, Texture2D mask, float s, Color c, Color e, float lifeSeconds)
        {
            r = renderer; size = s; core = c; edge = e; life = Mathf.Max(0.02f, lifeSeconds);
            block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", mask ? mask : Texture2D.whiteTexture);
            block.SetColor("_ColorCore", c);
            block.SetColor("_ColorEdge", e);
            block.SetVector("_NoiseScale", new Vector4(1f, 1f, 0.4f, 0.2f));
            block.SetFloat("_NoiseAmount", 0.35f);
            block.SetFloat("_CoreSharp", 2f);
            Apply(0f);
        }

        public FxLayer Scale(float from, float to, float pow = 0.5f) { scaleFrom = from; scaleTo = to; scalePow = pow; return this; }
        public FxLayer Stretch(float x, float y, float z = 1f) { stretch = new Vector3(x, y, z); return this; }
        public FxLayer Fade(float inFraction, float outStart, float alpha = 1f) { fadeIn = inFraction; fadeOutStart = outStart; alphaMul = alpha; return this; }
        public FxLayer Dissolve(float from, float to, float start) { dissolveFrom = from; dissolveTo = to; dissolveStart = start; return this; }
        public FxLayer Spin(float degPerSec, Vector3? axis = null) { spin = degPerSec; spinAxis = axis ?? Vector3.forward; return this; }
        public FxLayer Move(Vector3 v, Vector3? g = null) { velocity = v; gravity = g ?? Vector3.zero; return this; }
        public FxLayer Billboard() { billboard = true; return this; }
        /// <summary>Lies on the ground (XZ plane), seen at the camera's shallow angle.</summary>
        public FxLayer Ground() { transform.rotation = Quaternion.Euler(90f, 0f, 0f); transform.position += Vector3.up * 0.2f; return this; } // above the grass tufts
        public FxLayer Rotate(Quaternion q) { transform.rotation = q; return this; }
        public FxLayer Follow(Transform target, Vector3 offset) { follow = target; followOffset = offset; return this; }
        public FxLayer Delay(float seconds) { delay = seconds; if (seconds > 0f) r.enabled = false; return this; }
        public FxLayer Noise(float amount, Vector2 scale, Vector2 scroll) { block.SetFloat("_NoiseAmount", amount); block.SetVector("_NoiseScale", new Vector4(scale.x, scale.y, scroll.x, scroll.y)); return this; }
        public FxLayer NoiseTex(string name) { block.SetTexture("_NoiseTex", SkillVfxKit.Tex(name)); return this; }
        public FxLayer UvScroll(Vector2 s) { block.SetVector("_UVScroll", new Vector4(s.x, s.y, 0, 0)); return this; }
        public FxLayer Sharp(float k) { block.SetFloat("_CoreSharp", k); return this; }
        public FxLayer Rim(Color c) { block.SetColor("_RimColor", c); return this; }
        public FxLayer Parent(Transform p) { transform.SetParent(p, true); return this; }
        public float Life => life;
        private Mesh owned;
        /// <summary>A runtime mesh (baked afterimage) destroyed with this layer.</summary>
        public FxLayer OwnMesh(Mesh m) { owned = m; return this; }
        private void OnDestroy() { if (owned) Destroy(owned); }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (delay > 0f) { delay -= dt; if (delay <= 0f) r.enabled = true; else return; }
            t += dt;
            if (follow) transform.position = follow.position + followOffset;
            velocity += gravity * dt;
            transform.position += velocity * dt;
            if (spin != 0f && !billboard) transform.Rotate(spinAxis, spin * dt, Space.Self);
            if (billboard && Camera.main) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up) * Quaternion.Euler(0f, 0f, spinAngle += spin * dt);
            Apply(Mathf.Clamp01(t / life));
            if (t >= life) Destroy(gameObject);
        }
        private float spinAngle;

        private void Apply(float k)
        {
            float s = size * Mathf.Lerp(scaleFrom, scaleTo, Mathf.Pow(k, scalePow));
            transform.localScale = Vector3.Scale(stretch, Vector3.one * s);
            float a = (fadeIn > 0f ? Mathf.Clamp01(k / fadeIn) : 1f) * (k > fadeOutStart ? 1f - Mathf.SmoothStep(0f, 1f, (k - fadeOutStart) / Mathf.Max(0.01f, 1f - fadeOutStart)) : 1f);
            block.SetFloat("_Alpha", a * alphaMul);
            float dk = k < dissolveStart ? 0f : (k - dissolveStart) / Mathf.Max(0.01f, 1f - dissolveStart);
            block.SetFloat("_Dissolve", Mathf.Lerp(dissolveFrom, dissolveTo, dk));
            r.SetPropertyBlock(block);
        }
    }
}

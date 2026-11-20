using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Falling peach-blossom petals for the Wedding Hall: one ParticleSystem with a generated petal texture, slow
    /// fluttering fall, soft pink/cream tints. Mobile-friendly: capped particle count (lower on the Low graphics
    /// setting), no lights, no collisions. <see cref="Burst"/> adds a celebration shower (ceremony moments).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PetalFall : MonoBehaviour
    {
        [SerializeField] private float width = 40f, height = 9f;
        private ParticleSystem ps;
        public int Bursts { get; private set; }

        private static Texture2D petalTex;
        private static Texture2D PetalTexture()
        {
            if (petalTex) return petalTex;
            const int n = 64;
            petalTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // teardrop petal with a notch at the wide end and a lighter centre vein
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float w = 0.55f * Mathf.Sqrt(Mathf.Clamp01(1f - v * v)) * (0.65f + 0.35f * (v + 1f) * 0.5f);
                    float notch = v > 0.7f ? Mathf.Clamp01(0.12f - Mathf.Abs(u)) * 8f : 0f;
                    float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 18f) * (1f - notch);
                    float shade = Mathf.Lerp(0.82f, 1f, 1f - Mathf.Abs(u) / Mathf.Max(0.01f, w));
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
            petalTex.SetPixels(px); petalTex.Apply();
            return petalTex;
        }

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool low = QualitySettings.GetQualityLevel() == 0;
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.8f), new Color(1f, 0.93f, 0.9f));
            main.gravityModifier = 0.03f;
            main.maxParticles = low ? 70 : 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = low ? 6f : 18f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(width, 0.1f, 7f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f); vel.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.35f); vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f; noise.scrollSpeed = 0.2f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            r.alignment = ParticleSystemRenderSpace.Local;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", PetalTexture());
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_Cull", 0f);    // both sides: petals flip as they fall
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            r.sharedMaterial = mat;
            transform.position = new Vector3(transform.position.x, height, transform.position.z);
            ps.Simulate(8f, true, true); // already falling when the hall loads
            ps.Play();
        }

        /// <summary>Celebration shower around a point (ring exchange, cake, toast).</summary>
        public void Burst(Vector3 at, int count = 90)
        {
            if (!ps) return;
            Bursts++;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = at + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(2.5f, 5f), Random.Range(-1f, 1.5f));
                p.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.4f, 0.4f), 0f);
                ps.Emit(p, 1);
            }
        }
    }
}

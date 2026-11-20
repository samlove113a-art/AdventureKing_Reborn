using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Original HUD icon set, painted procedurally (signed-distance shapes, 4x supersampled): gold-rimmed navy
    /// medallions with a clear symbol (coin, diamond, gear, event star, 7-day calendar, gift, welfare heart, mail,
    /// ticket, crown, trophy, more), plus the round avatar frame. No external art, no fonts, crisp at 256 px.
    /// Output: Assets/Art/UI/Hud/*.png as sprites.
    /// </summary>
    public static class HudArt
    {
        public const string Dir = "Assets/Art/UI/Hud";
        private const int S = 256;

        // ---------------------------------------------------------------- SDF helpers (coordinates: -1..1, y up)
        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Box(Vector2 p, Vector2 c, Vector2 h, float round = 0f)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }
        private static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - w;
        }
        private static float Poly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        private static Vector2[] Star(int n, float ro, float ri, float rot = 90f)
        {
            var v = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                float a = (rot + i * 180f / n) * Mathf.Deg2Rad, r = i % 2 == 0 ? ro : ri;
                v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }

        private static readonly Color GoldHi = new Color(1f, 0.93f, 0.62f), GoldLo = new Color(0.78f, 0.52f, 0.16f);
        private static readonly Color NavyHi = new Color(0.2f, 0.27f, 0.5f), NavyLo = new Color(0.07f, 0.09f, 0.2f);
        private static readonly Color White = new Color(1f, 0.98f, 0.94f);

        /// <summary>Paints layers back-to-front: each layer = (sdf, colour(p)). Anti-aliased by 4x4 supersampling.</summary>
        private static void Paint(string name, bool medallion, params (Func<Vector2, float> sdf, Func<Vector2, Color> col)[] layers)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + 0.5f) / 4f) / S * 2f - 1f, (y + (sy + 0.5f) / 4f) / S * 2f - 1f);
                            Color c = Color.clear;
                            if (medallion)
                            {
                                c = Medallion(p);
                                // symbols: drop shadow, dark ink outline, then the fill lit from the top-left (bevel)
                                foreach (var (sdf, col) in layers)
                                {
                                    float d = sdf(p);
                                    if (d > 0.06f && sdf(p - ShadowOffset) > 0f) continue;
                                    Color k = col(p);
                                    if (k.a < 0.95f) { if (d < 0f) c = Color.Lerp(c, new Color(k.r, k.g, k.b, 1f), k.a); continue; } // highlights/glints stay flat
                                    if (sdf(p - ShadowOffset) < 0f && d >= 0f) c = Color.Lerp(c, Ink, 0.35f);
                                    if (d < Outline) c = Color.Lerp(c, Ink, 0.92f);
                                    if (d < 0f) c = Lit(k, p, d, sdf);
                                }
                            }
                            else
                                foreach (var (sdf, col) in layers)
                                    if (sdf(p) < 0f) { Color k = col(p); c = Color.Lerp(c, new Color(k.r, k.g, k.b, 1f), k.a); }
                            acc += c;
                        }
                    px[y * S + x] = acc / 16f;
                }
            t.SetPixels(px);
            t.Apply();
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        private static Func<Vector2, Color> Grad(Color lo, Color hi) => p => Color.Lerp(lo, hi, (p.y + 1f) * 0.5f);

        // ---------------------------------------------------------------- medallion look (shared by every HUD shortcut)
        private static readonly Color Ink = new Color(0.08f, 0.05f, 0.1f);
        private static readonly Vector2 ShadowOffset = new Vector2(0.03f, -0.05f);
        private const float Outline = 0.035f;
        private static readonly Vector2 LightDir = new Vector2(-0.55f, 0.83f);

        /// <summary>Bevelled gold rim (lit top-left, ink edges) around a deep jewel-blue face with a soft glow behind the
        /// symbol and a glossy highlight across the top.</summary>
        private static Color Medallion(Vector2 p)
        {
            float r = p.magnitude;
            if (r > 0.985f) return Color.clear;
            if (r > 0.955f) return Ink;
            if (r > 0.85f)
            {
                // rim: radial normal; shade by how much it faces the light, plus a fine engraved groove
                Vector2 n = r > 0.0001f ? p / r : Vector2.up;
                float facing = Vector2.Dot(n, LightDir);
                float across = Mathf.InverseLerp(0.955f, 0.85f, r);           // 0 outer .. 1 inner
                float roll = Mathf.Sin(across * Mathf.PI);                     // rounded profile
                Color gold = Color.Lerp(GoldLo, GoldHi, 0.5f + 0.45f * facing * (1f - across * 0.6f));
                gold = Color.Lerp(gold, Color.white, Mathf.Clamp01(facing) * roll * 0.35f);
                if (Mathf.Abs(r - 0.902f) < 0.006f) gold = Color.Lerp(gold, new Color(0.45f, 0.26f, 0.06f), 0.7f);
                for (int i = 0; i < 8; i++) // eight tiny studs on the rim
                {
                    float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                    float dd = (p - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.902f).magnitude;
                    if (dd < 0.022f) gold = Color.Lerp(GoldHi, Color.white, 0.5f);
                }
                return gold;
            }
            if (r > 0.835f) return Ink;
            // face: radial jewel gradient + glow behind the symbol + inner shadow under the rim + glossy cap
            Color face = Color.Lerp(new Color(0.16f, 0.32f, 0.62f), NavyLo, Mathf.SmoothStep(0f, 1f, r / 0.835f));
            face = Color.Lerp(face, new Color(0.36f, 0.62f, 0.95f), Mathf.Pow(Mathf.Clamp01(1f - r / 0.6f), 2f) * 0.55f);
            float innerShadow = Mathf.Clamp01((r - 0.7f) / 0.135f) * Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(p.normalized, LightDir));
            face = Color.Lerp(face, Ink, innerShadow * 0.45f);
            float gloss = Mathf.Clamp01(1f - ((p - new Vector2(-0.05f, 0.42f)) / new Vector2(0.62f, 0.3f)).magnitude);
            face = Color.Lerp(face, Color.white, Mathf.SmoothStep(0f, 1f, gloss) * 0.16f);
            return face;
        }

        /// <summary>Fill colour with a bevel: edges facing the light brighten, the far edges darken.</summary>
        private static Color Lit(Color k, Vector2 p, float d, Func<Vector2, float> sdf)
        {
            const float e = 0.01f, bevel = 0.1f;
            Vector2 n = new Vector2(sdf(p + new Vector2(e, 0f)) - sdf(p - new Vector2(e, 0f)), sdf(p + new Vector2(0f, e)) - sdf(p - new Vector2(0f, e)));
            if (n.sqrMagnitude > 1e-8f) n.Normalize();
            float edge = Mathf.Clamp01(1f + d / bevel); // 1 at the edge -> 0 deeper inside
            float facing = Vector2.Dot(n, LightDir);
            Color c = new Color(k.r, k.g, k.b, 1f);
            c = facing > 0f ? Color.Lerp(c, Color.white, facing * edge * 0.45f) : Color.Lerp(c, Ink, -facing * edge * 0.35f);
            return c;
        }

        public static Sprite Get(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        /// <summary>Default avatars: a real bust render of each class hero (Idle pose, 3/4 view, transparent background).</summary>
        public static void RenderPortraits()
        {
            const int size = 256, layer = 31;
            var lightGo = new GameObject("PortraitLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.cullingMask = 1 << layer;
            lightGo.transform.rotation = Quaternion.Euler(28f, 160f, 0f);
            var camGo = new GameObject("PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << layer;
            cam.fieldOfView = 18f;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var ambient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.68f, 0.72f);
            try
            {
                foreach (AdventureKing.Gameplay.HeroClass c in new[] { AdventureKing.Gameplay.HeroClass.Swordsman, AdventureKing.Gameplay.HeroClass.Mage, AdventureKing.Gameplay.HeroClass.Assassin, AdventureKing.Gameplay.HeroClass.Archer })
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(c)));
                    foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                    inst.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                    Animator anim = inst.GetComponentInChildren<Animator>();
                    var idle = System.Array.Find(anim.runtimeAnimatorController.animationClips, x => x.name == "Idle");
                    anim.runtimeAnimatorController = null;
                    if (idle) idle.SampleAnimation(anim.gameObject, 0.2f);
                    Transform head = System.Array.Find(inst.GetComponentsInChildren<Transform>(), t => t.name == "Head");
                    Vector3 focus = head.position + Vector3.up * 0.16f;
                    camGo.transform.position = focus + new Vector3(0f, 0.04f, -2.3f);
                    camGo.transform.LookAt(focus);
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    string path = $"{Dir}/portrait_{c}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    UnityEngine.Object.DestroyImmediate(inst);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }
            finally
            {
                RenderSettings.ambientLight = ambient;
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
            }
        }

        public static void BuildAll()
        {
            var gold = Grad(GoldLo, GoldHi);
            // currency
            Paint("coin", false,
                (p => Circle(p, Vector2.zero, 0.92f), gold),
                (p => Circle(p, Vector2.zero, 0.72f), Grad(new Color(0.85f, 0.58f, 0.18f), new Color(1f, 0.86f, 0.4f))),
                (p => Poly(p, Star(5, 0.48f, 0.2f)), Grad(GoldHi, White)));
            Paint("diamond", false,
                (p => Poly(p, new[] { new Vector2(0f, -0.92f), new Vector2(0.86f, 0.18f), new Vector2(0.52f, 0.62f), new Vector2(-0.52f, 0.62f), new Vector2(-0.86f, 0.18f) }),
                    p => Color.Lerp(new Color(0.25f, 0.55f, 0.95f), new Color(0.75f, 0.95f, 1f), (p.y + 1f) * 0.5f)),
                (p => Poly(p, new[] { new Vector2(0f, -0.92f), new Vector2(0.3f, 0.18f), new Vector2(-0.3f, 0.18f) }), _ => new Color(0.9f, 0.98f, 1f, 0.55f)),
                (p => Box(p, new Vector2(0f, 0.18f), new Vector2(0.86f, 0.02f)), _ => new Color(1f, 1f, 1f, 0.5f)));
            // menu medallions
            Paint("icon_settings", true,
                (p => Mathf.Min(Circle(p, Vector2.zero, 0.42f), Poly(p, Star(8, 0.6f, 0.44f, 0f))), gold),
                (p => Circle(p, Vector2.zero, 0.2f), _ => NavyLo));
            Paint("icon_event", true,
                (p => Poly(p, Star(8, 0.6f, 0.3f)), gold),
                (p => Circle(p, Vector2.zero, 0.24f), _ => new Color(0.92f, 0.3f, 0.36f)),
                (p => Poly(p, Star(4, 0.2f, 0.07f, 45f)), _ => White),
                (p => Mathf.Min(Poly(p - new Vector2(-0.52f, 0.5f), Star(4, 0.13f, 0.04f)), Poly(p - new Vector2(0.55f, -0.42f), Star(4, 0.1f, 0.035f))), _ => new Color(1f, 1f, 0.9f, 0.9f)));
            Paint("icon_checkin", true,
                (p => Box(p, new Vector2(0f, -0.06f), new Vector2(0.5f, 0.46f), 0.08f), _ => White),
                (p => Box(p, new Vector2(0f, 0.32f), new Vector2(0.5f, 0.12f), 0.06f), _ => new Color(0.86f, 0.25f, 0.24f)),
                (p => Mathf.Min(Box(p, new Vector2(-0.24f, 0.46f), new Vector2(0.05f, 0.1f), 0.03f), Box(p, new Vector2(0.24f, 0.46f), new Vector2(0.05f, 0.1f), 0.03f)), gold),
                // "7"
                (p => Mathf.Min(Seg(p, new Vector2(-0.2f, 0.1f), new Vector2(0.2f, 0.1f), 0.06f), Seg(p, new Vector2(0.2f, 0.1f), new Vector2(-0.04f, -0.4f), 0.06f)), _ => NavyLo));
            Paint("icon_firsttopup", true,
                (p => Box(p, new Vector2(0f, -0.14f), new Vector2(0.5f, 0.34f), 0.06f), _ => new Color(0.86f, 0.28f, 0.3f)),
                (p => Box(p, new Vector2(0f, 0.24f), new Vector2(0.58f, 0.1f), 0.05f), _ => new Color(0.95f, 0.36f, 0.36f)),
                (p => Mathf.Min(Box(p, new Vector2(0f, -0.02f), new Vector2(0.08f, 0.5f)), Box(p, new Vector2(0f, 0.24f), new Vector2(0.58f, 0.04f))), gold),
                (p => Mathf.Min(Circle(p, new Vector2(-0.18f, 0.44f), 0.13f), Circle(p, new Vector2(0.18f, 0.44f), 0.13f)), gold));
            Paint("icon_welfare", true,
                (p => Mathf.Min(Mathf.Min(Circle(p, new Vector2(-0.22f, 0.12f), 0.3f), Circle(p, new Vector2(0.22f, 0.12f), 0.3f)),
                    Poly(p, new[] { new Vector2(-0.5f, 0.02f), new Vector2(0.5f, 0.02f), new Vector2(0f, -0.56f) })), _ => new Color(0.95f, 0.38f, 0.5f)),
                (p => Circle(p, new Vector2(-0.2f, 0.22f), 0.08f), _ => new Color(1f, 1f, 1f, 0.7f)));
            Paint("icon_mail", true,
                (p => Box(p, Vector2.zero, new Vector2(0.56f, 0.38f), 0.06f), _ => White),
                (p => Mathf.Min(Seg(p, new Vector2(-0.54f, 0.34f), new Vector2(0f, -0.06f), 0.05f), Seg(p, new Vector2(0.54f, 0.34f), new Vector2(0f, -0.06f), 0.05f)), gold),
                (p => Circle(p, new Vector2(0f, -0.08f), 0.14f), _ => new Color(0.82f, 0.18f, 0.22f)), // wax seal
                (p => Poly(p - new Vector2(0f, -0.08f), Star(5, 0.08f, 0.035f)), _ => new Color(1f, 0.8f, 0.4f, 0.9f)));
            Paint("icon_giftcode", true,
                (p => Mathf.Max(Box(p, Vector2.zero, new Vector2(0.6f, 0.32f), 0.05f), -Mathf.Min(Circle(p, new Vector2(-0.6f, 0f), 0.13f), Circle(p, new Vector2(0.6f, 0f), 0.13f))), gold),
                (p => Box(p, Vector2.zero, new Vector2(0.02f, 0.26f)), _ => NavyLo),
                (p => Poly(p - new Vector2(0.24f, 0f), Star(5, 0.16f, 0.07f)), _ => NavyLo));
            Paint("icon_vip", true,
                (p => Poly(p, new[] { new Vector2(-0.56f, -0.3f), new Vector2(0.56f, -0.3f), new Vector2(0.62f, 0.34f), new Vector2(0.28f, 0.06f), new Vector2(0f, 0.46f), new Vector2(-0.28f, 0.06f), new Vector2(-0.62f, 0.34f) }), gold),
                (p => Box(p, new Vector2(0f, -0.4f), new Vector2(0.56f, 0.08f), 0.03f), gold),
                (p => Circle(p, new Vector2(0f, -0.1f), 0.1f), _ => new Color(0.86f, 0.25f, 0.3f)));
            Paint("icon_ranking", true,
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.2f), new Vector2(0.36f, 0.3f), 0.18f), Mathf.Min(Box(p, new Vector2(0f, -0.2f), new Vector2(0.06f, 0.16f)), Box(p, new Vector2(0f, -0.44f), new Vector2(0.3f, 0.07f), 0.03f))), gold),
                (p => Mathf.Max(Circle(p, new Vector2(-0.4f, 0.28f), 0.18f), -Circle(p, new Vector2(-0.4f, 0.28f), 0.1f)), gold),
                (p => Mathf.Max(Circle(p, new Vector2(0.4f, 0.28f), 0.18f), -Circle(p, new Vector2(0.4f, 0.28f), 0.1f)), gold),
                (p => Poly(p - new Vector2(0f, 0.22f), Star(5, 0.16f, 0.07f)), _ => NavyLo));
            // social: two friends + a small heart
            Paint("icon_social", true,
                (p => Mathf.Min(Mathf.Min(Circle(p, new Vector2(-0.26f, 0.22f), 0.17f), Box(p, new Vector2(-0.26f, -0.22f), new Vector2(0.24f, 0.2f), 0.18f)),
                    Mathf.Min(Circle(p, new Vector2(0.26f, 0.22f), 0.17f), Box(p, new Vector2(0.26f, -0.22f), new Vector2(0.24f, 0.2f), 0.18f))), _ => White),
                (p => Mathf.Min(Mathf.Min(Circle(p, new Vector2(-0.07f, 0.5f), 0.09f), Circle(p, new Vector2(0.07f, 0.5f), 0.09f)),
                    Poly(p, new[] { new Vector2(-0.15f, 0.47f), new Vector2(0.15f, 0.47f), new Vector2(0f, 0.3f) })), _ => new Color(0.95f, 0.35f, 0.45f)));
            // guild: shield with a wind bell
            Paint("icon_guild", true,
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.18f), new Vector2(0.48f, 0.34f), 0.08f), Poly(p, new[] { new Vector2(-0.48f, 0.0f), new Vector2(0.48f, 0.0f), new Vector2(0f, -0.62f) })), gold),
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.16f), new Vector2(0.36f, 0.24f), 0.06f), Poly(p, new[] { new Vector2(-0.36f, -0.02f), new Vector2(0.36f, -0.02f), new Vector2(0f, -0.48f) })), _ => new Color(0.2f, 0.3f, 0.62f)),
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.1f), new Vector2(0.14f, 0.16f), 0.12f), Box(p, new Vector2(0f, -0.06f), new Vector2(0.2f, 0.04f), 0.03f)), gold),
                (p => Circle(p, new Vector2(0f, -0.15f), 0.05f), gold));
            Paint("icon_more", true,
                (p => Mathf.Min(Circle(p, new Vector2(-0.34f, 0f), 0.12f), Mathf.Min(Circle(p, Vector2.zero, 0.12f), Circle(p, new Vector2(0.34f, 0f), 0.12f))), gold));
            Paint("icon_shop", true,
                (p => Box(p, new Vector2(0f, -0.12f), new Vector2(0.46f, 0.34f), 0.08f), gold),
                (p => Mathf.Max(Circle(p, new Vector2(0f, 0.22f), 0.3f), -Circle(p, new Vector2(0f, 0.22f), 0.2f)), gold),
                (p => Box(p, new Vector2(0f, -0.12f), new Vector2(0.28f, 0.18f), 0.05f), _ => NavyLo));
            // fitting-room pose buttons: white figure silhouettes
            Func<Vector2, Color> wh = _ => White;
            Paint("icon_pose_idle", true,
                (p => Mathf.Min(Circle(p, new Vector2(0f, 0.38f), 0.15f), Mathf.Min(Seg(p, new Vector2(0f, 0.2f), new Vector2(0f, -0.12f), 0.12f),
                    Mathf.Min(Seg(p, new Vector2(-0.07f, -0.14f), new Vector2(-0.1f, -0.5f), 0.07f), Mathf.Min(Seg(p, new Vector2(0.07f, -0.14f), new Vector2(0.1f, -0.5f), 0.07f),
                    Mathf.Min(Seg(p, new Vector2(-0.12f, 0.16f), new Vector2(-0.22f, -0.1f), 0.06f), Seg(p, new Vector2(0.12f, 0.16f), new Vector2(0.22f, -0.1f), 0.06f)))))), wh));
            Paint("icon_pose_run", true,
                (p => Mathf.Min(Circle(p, new Vector2(0.12f, 0.38f), 0.14f), Mathf.Min(Seg(p, new Vector2(0.08f, 0.2f), new Vector2(-0.04f, -0.1f), 0.11f),
                    Mathf.Min(Seg(p, new Vector2(-0.04f, -0.12f), new Vector2(0.24f, -0.34f), 0.07f), Mathf.Min(Seg(p, new Vector2(-0.06f, -0.12f), new Vector2(-0.3f, -0.42f), 0.07f),
                    Mathf.Min(Seg(p, new Vector2(0.08f, 0.16f), new Vector2(0.32f, 0.04f), 0.06f), Seg(p, new Vector2(0.04f, 0.14f), new Vector2(-0.22f, 0.22f), 0.06f)))))), wh),
                (p => Mathf.Min(Seg(p, new Vector2(-0.62f, 0.2f), new Vector2(-0.4f, 0.2f), 0.03f), Mathf.Min(Seg(p, new Vector2(-0.66f, 0.02f), new Vector2(-0.42f, 0.02f), 0.03f), Seg(p, new Vector2(-0.6f, -0.16f), new Vector2(-0.44f, -0.16f), 0.03f))), gold));
            Paint("icon_pose_jump", true,
                (p => Mathf.Min(Circle(p, new Vector2(0f, 0.26f), 0.14f), Mathf.Min(Seg(p, new Vector2(0f, 0.1f), new Vector2(0f, -0.18f), 0.11f),
                    Mathf.Min(Seg(p, new Vector2(-0.06f, -0.2f), new Vector2(-0.22f, -0.36f), 0.07f), Mathf.Min(Seg(p, new Vector2(0.06f, -0.2f), new Vector2(0.22f, -0.36f), 0.07f),
                    Mathf.Min(Seg(p, new Vector2(-0.1f, 0.08f), new Vector2(-0.28f, 0.34f), 0.06f), Seg(p, new Vector2(0.1f, 0.08f), new Vector2(0.28f, 0.34f), 0.06f)))))), wh),
                (p => Poly(p, new[] { new Vector2(0f, 0.68f), new Vector2(-0.16f, 0.5f), new Vector2(0.16f, 0.5f) }), gold));
            Paint("icon_pose_attack", true,
                (p => Seg(p, new Vector2(-0.34f, -0.34f), new Vector2(0.42f, 0.42f), 0.09f), _ => new Color(0.85f, 0.92f, 1f)),
                (p => Seg(p, new Vector2(-0.3f, -0.1f), new Vector2(-0.1f, -0.3f), 0.06f), gold),
                (p => Seg(p, new Vector2(-0.36f, -0.36f), new Vector2(-0.5f, -0.5f), 0.07f), gold),
                (p => Poly(p - new Vector2(0.2f, 0.36f), Star(4, 0.14f, 0.05f, 45f)), _ => White));
            // round frame drawn over the avatar portrait (transparent centre)
            Paint("avatar_ring", false,
                (p => Mathf.Max(Circle(p, Vector2.zero, 0.98f), -Circle(p, Vector2.zero, 0.84f)), gold),
                (p => Mathf.Max(Circle(p, Vector2.zero, 0.9f), -Circle(p, Vector2.zero, 0.86f)), _ => new Color(0.55f, 0.33f, 0.08f, 0.8f)),
                (p => Poly(p - new Vector2(0f, 0.9f), Star(4, 0.16f, 0.06f, 45f)), _ => new Color(0.4f, 0.95f, 0.9f)));
            // circular mask for the avatar
            Paint("avatar_mask", false, (p => Circle(p, Vector2.zero, 0.86f), _ => Color.white));
            // minimap symbols (dark outline so they read on the map)
            Color ink = new Color(0.04f, 0.06f, 0.12f);
            Paint("mini_portal", false,
                (p => Circle(p, Vector2.zero, 0.92f), _ => ink),
                (p => Mathf.Max(Circle(p, Vector2.zero, 0.8f), -Circle(p, Vector2.zero, 0.42f)), Grad(new Color(0.2f, 0.6f, 1f), new Color(0.6f, 1f, 1f))),
                (p => Circle(p, new Vector2(0.12f, 0.1f), 0.26f), _ => new Color(0.85f, 1f, 1f)));
            Paint("mini_npc", false,
                (p => Mathf.Min(Circle(p, new Vector2(0f, 0.12f), 0.8f), Poly(p, new[] { new Vector2(-0.3f, -0.5f), new Vector2(0.1f, -0.5f), new Vector2(-0.35f, -0.95f) })), _ => ink),
                (p => Mathf.Min(Circle(p, new Vector2(0f, 0.12f), 0.68f), Poly(p, new[] { new Vector2(-0.24f, -0.4f), new Vector2(0.06f, -0.4f), new Vector2(-0.28f, -0.8f) })), gold),
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.28f), new Vector2(0.09f, 0.26f), 0.06f), Circle(p, new Vector2(0f, -0.18f), 0.1f)), _ => ink));
            Paint("mini_player", false,
                (p => Poly(p, new[] { new Vector2(0f, -0.95f), new Vector2(0.72f, 0.3f), new Vector2(0f, 0.95f), new Vector2(-0.72f, 0.3f) }), _ => White),
                (p => Poly(p, new[] { new Vector2(0f, -0.75f), new Vector2(0.55f, 0.28f), new Vector2(0f, 0.76f), new Vector2(-0.55f, 0.28f) }), Grad(new Color(0.85f, 0.12f, 0.15f), new Color(1f, 0.5f, 0.45f))));
            // check-in rewards: bronze chest, outfit choice (wrapped crown), done tick
            Paint("reward_chest", false,
                (p => Box(p, new Vector2(0f, -0.2f), new Vector2(0.72f, 0.42f), 0.08f), Grad(new Color(0.45f, 0.24f, 0.1f), new Color(0.7f, 0.42f, 0.2f))),
                (p => Box(p, new Vector2(0f, 0.32f), new Vector2(0.72f, 0.2f), 0.14f), Grad(new Color(0.55f, 0.3f, 0.12f), new Color(0.82f, 0.52f, 0.26f))),
                (p => Mathf.Min(Box(p, new Vector2(0f, 0.05f), new Vector2(0.74f, 0.06f)), Mathf.Min(Box(p, new Vector2(-0.48f, 0f), new Vector2(0.07f, 0.62f)), Box(p, new Vector2(0.48f, 0f), new Vector2(0.07f, 0.62f)))), gold),
                (p => Box(p, new Vector2(0f, 0.02f), new Vector2(0.13f, 0.16f), 0.04f), gold),
                (p => Circle(p, new Vector2(0f, -0.02f), 0.05f), _ => new Color(0.2f, 0.1f, 0.04f)));
            Paint("reward_skin", false,
                (p => Box(p, new Vector2(0f, -0.18f), new Vector2(0.66f, 0.5f), 0.1f), Grad(new Color(0.55f, 0.25f, 0.75f), new Color(0.82f, 0.5f, 1f))),
                (p => Mathf.Min(Box(p, new Vector2(0f, -0.18f), new Vector2(0.09f, 0.5f)), Box(p, new Vector2(0f, -0.08f), new Vector2(0.66f, 0.08f))), gold),
                (p => Poly(p, new[] { new Vector2(-0.46f, 0.38f), new Vector2(0.46f, 0.38f), new Vector2(0.52f, 0.86f), new Vector2(0.22f, 0.62f), new Vector2(0f, 0.92f), new Vector2(-0.22f, 0.62f), new Vector2(-0.52f, 0.86f) }), gold),
                (p => Circle(p, new Vector2(0f, 0.66f), 0.08f), _ => new Color(0.35f, 0.95f, 0.85f)));
            Paint("reward_done", false,
                (p => Circle(p, Vector2.zero, 0.92f), _ => new Color(0.1f, 0.55f, 0.35f)),
                (p => Mathf.Min(Seg(p, new Vector2(-0.45f, 0.02f), new Vector2(-0.12f, -0.32f), 0.12f), Seg(p, new Vector2(-0.12f, -0.32f), new Vector2(0.5f, 0.38f), 0.12f)), _ => White));
            // collapse / expand arrow (medallion with a right chevron; rotated 180° when folded)
            Paint("icon_arrow", true,
                (p => Mathf.Min(Seg(p, new Vector2(-0.18f, 0.36f), new Vector2(0.22f, 0f), 0.11f), Seg(p, new Vector2(0.22f, 0f), new Vector2(-0.18f, -0.36f), 0.11f)), gold));
            Paint("icon_sun", false,
                (p => Mathf.Min(Circle(p, Vector2.zero, 0.5f), Poly(p, Star(10, 0.95f, 0.5f))), Grad(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.92f, 0.45f))));
            Paint("icon_moon", false,
                (p => Mathf.Max(Circle(p, Vector2.zero, 0.85f), -Circle(p, new Vector2(0.42f, 0.3f), 0.7f)), Grad(new Color(0.75f, 0.8f, 1f), new Color(1f, 0.98f, 0.85f))));
        }
    }
}

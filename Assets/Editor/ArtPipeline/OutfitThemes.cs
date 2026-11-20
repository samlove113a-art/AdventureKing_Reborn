using System.Collections.Generic;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Themed, funny cosmetics built in-engine (no Meshy): every piece changes the SILHOUETTE, not just a colour.
    /// The class heads carry their own hats in the body mesh (Mage witch hat, Archer cap), so nothing here wraps the
    /// head: pieces sit on a headband fitted to the head ring, or perch on the head's top point, which works on all
    /// four classes. Gold singles: duck buddy, frog eyes, bunny / cat / panda ears, mushroom cap, chef toque.
    /// Diamond full sets: Little Dino, Baby Shark, Sweet Dream (head + top + tail / pajamas).
    /// </summary>
    public static partial class OutfitArtPipeline
    {
        // ------------------------------------------------------------------ head geometry helpers
        private sealed class HeadFrame { public Ring Ring; public Vector3 C, Top; public float R, Ht; public BoneWeight W; }

        private static HeadFrame Frame(Hero h, float margin = 0.004f)
        {
            Ring ring = HeadRing(h, margin, 0.02f);
            int head = h.B("Head");
            Vector3 top = Vector3.zero;
            float best = float.MinValue;
            for (int i = 0; i < h.V.Length; i++)
                if (h.W[i].boneIndex0 == head && h.W[i].weight0 > 0.6f && h.V[i].y > best) { best = h.V[i].y; top = h.V[i]; }
            float r = ring.Points.Average(p => new Vector2(p.x - ring.Centre.x, p.z - ring.Centre.z).magnitude);
            // dome height is capped near the skull: the top vertex can be a feather or a witch-hat tip (toppers still use Top)
            float ht = Mathf.Clamp(top.y - ring.Centre.y, 0.05f * h.H, r * 1.05f);
            return new HeadFrame { Ring = ring, C = ring.Centre, Top = top, R = r, Ht = ht, W = OutfitMesh.Rigid(head) };
        }

        /// <summary>Point on the head "dome": angle around (0 right, 90 front, 180 left, 270 back), elevation 0 ring .. 1 top.</summary>
        private static Vector3 Dome(HeadFrame f, float angleDeg, float elev, out Vector3 normal)
        {
            float a = angleDeg * Mathf.Deg2Rad, e = Mathf.Clamp01(elev) * Mathf.PI * 0.5f;
            Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            normal = (o * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e)).normalized;
            return f.C + o * (f.R * Mathf.Cos(e)) + Vector3.up * (f.Ht * Mathf.Sin(e));
        }

        private static void Band(OutfitMesh m, HeadFrame f, float radius, Color a, Color b)
        {
            var path = Smooth(f.Ring.Points, 3).ToList();
            int t0 = m.T.Count;
            m.Tube(path, Enumerable.Repeat(radius, path.Count).ToList(), true, 8, (k, ang) => Color.Lerp(a, b, Mathf.Sin(ang) * 0.5f + 0.5f), _ => f.W, Vector3.up, 0.45f);
            m.Orient(t0, p => p - ClosestOnPath(path, p));
        }

        private static void Ball(OutfitMesh m, Vector3 c, Quaternion q, Vector3 size, System.Func<Vector3, Color> col, BoneWeight w, int seg = 12, int rings = 8, float vMin = -1f)
        {
            int t = m.T.Count;
            m.Ellipsoid(Matrix4x4.TRS(c, q, size), seg, rings, col, w, vMin);
            m.Orient(t, p => p - c);
        }

        private static void Cone(OutfitMesh m, Vector3 baseP, Vector3 dir, float len, float rad, float flat, Color root, Color tip, BoneWeight w)
        {
            Quaternion q = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.right).sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.right, dir).normalized : Vector3.forward, dir.normalized);
            var prof = new List<Vector2> { new Vector2(rad, 0f), new Vector2(rad * 0.75f, len * 0.45f), new Vector2(rad * 0.35f, len * 0.8f), new Vector2(0.0005f, len) };
            int t = m.T.Count;
            m.Lathe(Matrix4x4.TRS(baseP, q, new Vector3(1f, 1f, flat)), prof, 12, i => Color.Lerp(root, tip, i / 3f), w);
            m.Orient(t, p => { Vector3 d = p - baseP; d -= Vector3.Project(d, dir); return d.sqrMagnitude > 1e-8f ? d : dir; });
        }

        private static readonly Color Ink = new Color(0.08f, 0.07f, 0.1f);

        // ------------------------------------------------------------------ Gold singles
        /// <summary>A little yellow duckling sitting on top of the head.</summary>
        private static OutfitPiece[] DuckBuddy(Hero h)
        {
            HeadFrame f = Frame(h);
            var m = new OutfitMesh();
            float s = h.H * 1.4f; // reads at gameplay distance
            Vector3 seat = f.Top + Vector3.up * 0.006f * s;
            Color y0 = new Color(1f, 0.78f, 0.15f), y1 = new Color(1f, 0.95f, 0.5f), orange = new Color(1f, 0.5f, 0.12f);
            System.Func<Vector3, Color> yellow = l => Color.Lerp(y0, y1, (l.y + 1f) * 0.5f);
            Ball(m, seat + Vector3.up * 0.032f * s, Quaternion.identity, new Vector3(0.05f, 0.036f, 0.06f) * s, yellow, f.W);                  // body
            Vector3 head = seat + new Vector3(0f, 0.078f, 0.03f) * s;
            Ball(m, head, Quaternion.identity, Vector3.one * 0.033f * s, yellow, f.W);                                                            // head
            Ball(m, head + new Vector3(0f, -0.006f, 0.034f) * s, Quaternion.identity, new Vector3(0.02f, 0.008f, 0.02f) * s, _ => orange, f.W);   // beak
            foreach (float sx in new[] { -1f, 1f })
            {
                Ball(m, head + new Vector3(0.014f * sx, 0.008f, 0.027f) * s, Quaternion.identity, Vector3.one * 0.006f * s, _ => Ink, f.W, 8, 5);
                Ball(m, seat + new Vector3(0.046f * sx, 0.036f, -0.004f) * s, Quaternion.Euler(0f, 0f, 20f * sx), new Vector3(0.012f, 0.026f, 0.036f) * s, yellow, f.W); // wings
            }
            Ball(m, seat + new Vector3(0f, 0.05f, -0.058f) * s, Quaternion.Euler(-35f, 0f, 0f), new Vector3(0.016f, 0.008f, 0.02f) * s, yellow, f.W); // tail
            Ball(m, head + new Vector3(0f, 0.034f, 0f) * s, Quaternion.Euler(20f, 0f, 0f), new Vector3(0.004f, 0.014f, 0.008f) * s, _ => y0, f.W);    // tuft
            return new[] { Piece(h, "head_duck_buddy", m, vcMat) };
        }

        /// <summary>Green headband with two big bulging frog eyes on top.</summary>
        private static OutfitPiece[] FrogEyes(Hero h)
        {
            HeadFrame f = Frame(h);
            var m = new OutfitMesh();
            float s = h.H;
            Color g0 = new Color(0.22f, 0.62f, 0.2f), g1 = new Color(0.5f, 0.85f, 0.32f);
            Band(m, f, 0.009f * s, g0, g1);
            foreach (float ang in new[] { 62f, 118f })
            {
                Vector3 p = Dome(f, ang, 0.7f, out Vector3 n) + n * 0.03f * s;
                Ball(m, p - Vector3.forward * 0.008f * s, Quaternion.identity, Vector3.one * 0.05f * s, l => Color.Lerp(g0, g1, (l.y + 1f) * 0.5f), f.W); // socket
                Ball(m, p + Vector3.forward * 0.011f * s, Quaternion.identity, Vector3.one * 0.042f * s, _ => new Color(0.98f, 0.98f, 0.95f), f.W);        // eyeball
                Ball(m, p + Vector3.forward * 0.048f * s, Quaternion.identity, new Vector3(0.018f, 0.022f, 0.01f) * s, _ => Ink, f.W, 10, 6);            // pupil
                Ball(m, p + new Vector3(0.011f, 0.014f, 0.054f) * s, Quaternion.identity, Vector3.one * 0.006f * s, _ => Color.white, f.W, 6, 4);           // glint
            }
            return new[] { Piece(h, "head_frog_eyes", m, vcMat) };
        }

        /// <summary>Headband with two tall bunny ears that bob on their own pivots.</summary>
        private static OutfitPiece[] BunnyEars(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var band = new OutfitMesh();
            Band(band, f, 0.008f * s, new Color(0.95f, 0.95f, 0.97f), Color.white);
            var pieces = new List<OutfitPiece> { Piece(h, "head_bunny_ears", band, vcMat) };
            foreach (var (ang, tag) in new[] { (55f, "R"), (125f, "L") })
            {
                Vector3 root = Dome(f, ang, 0.55f, out Vector3 n) - n * 0.01f * h.H; // rooted into the hat/hair, not above it
                var extras = new List<ExtraJoint> { new ExtraJoint { Name = "Outfit_BunnyEar" + tag, Parent = "Head", MeshPos = root } };
                var w = OutfitMesh.Rigid(h.Bones.Length);
                var m = new OutfitMesh();
                Vector3 dir = (Vector3.up * 0.9f + n * 0.25f + (tag == "R" ? Vector3.right : Vector3.left) * 0.12f).normalized;
                Quaternion q = Quaternion.LookRotation(Vector3.forward, dir);
                Ball(m, root + dir * 0.085f * s, q, new Vector3(0.03f, 0.095f, 0.014f) * s, l => Color.Lerp(new Color(0.92f, 0.92f, 0.95f), Color.white, (l.y + 1f) * 0.5f), w);
                Ball(m, root + dir * 0.088f * s + Vector3.forward * 0.008f * s, q, new Vector3(0.017f, 0.076f, 0.009f) * s, _ => new Color(1f, 0.66f, 0.74f), w);
                pieces.Add(Piece(h, "head_bunny_ears_" + tag, m, vcMat, extras, PieceMotion.Pendulum));
            }
            return pieces.ToArray();
        }

        /// <summary>Ginger cat ears (flattened cones, cream inside) on a band, with a tiny gold bell at the front.</summary>
        private static OutfitPiece[] CatEars(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var m = new OutfitMesh();
            Color fur0 = new Color(0.86f, 0.42f, 0.14f), fur1 = new Color(1f, 0.66f, 0.3f), cream = new Color(1f, 0.86f, 0.74f);
            Band(m, f, 0.008f * s, fur0, fur1);
            foreach (float ang in new[] { 48f, 132f })
            {
                Vector3 p = Dome(f, ang, 0.66f, out Vector3 n);
                Vector3 dir = (Vector3.up + n * 0.45f).normalized;
                Cone(m, p - n * 0.004f * s, dir, 0.095f * s, 0.048f * s, 0.42f, fur0, fur1, f.W);
                Cone(m, p + Vector3.forward * 0.009f * s, dir, 0.07f * s, 0.03f * s, 0.3f, cream, new Color(1f, 0.7f, 0.7f), f.W);
            }
            Vector3 front = RingAt(f.Ring, 90f, out Vector3 fo) + fo * 0.008f * s + Vector3.down * 0.006f * s;
            Ball(m, front, Quaternion.identity, Vector3.one * 0.013f * s, l => Color.Lerp(new Color(0.85f, 0.62f, 0.2f), new Color(1f, 0.9f, 0.5f), (l.y + 1f) * 0.5f), f.W);
            return new[] { Piece(h, "head_cat_ears", m, vcMat) };
        }

        /// <summary>Round black panda ears on a black-and-white band.</summary>
        private static OutfitPiece[] PandaEars(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var m = new OutfitMesh();
            Band(m, f, 0.009f * s, new Color(0.12f, 0.12f, 0.14f), new Color(0.3f, 0.3f, 0.33f));
            foreach (float ang in new[] { 40f, 140f })
            {
                Vector3 p = Dome(f, ang, 0.62f, out Vector3 n) + n * 0.014f * s;
                Ball(m, p, Quaternion.LookRotation(Vector3.forward, n), new Vector3(0.036f, 0.036f, 0.02f) * s, l => Color.Lerp(new Color(0.07f, 0.07f, 0.08f), new Color(0.25f, 0.25f, 0.28f), (l.y + 1f) * 0.5f), f.W);
                Ball(m, p + Vector3.forward * 0.011f * s, Quaternion.LookRotation(Vector3.forward, n), new Vector3(0.02f, 0.02f, 0.01f) * s, _ => new Color(0.95f, 0.75f, 0.8f), f.W);
            }
            // a little bamboo sprig tucked in the band
            Vector3 side = RingAt(f.Ring, 20f, out Vector3 so);
            var path = new List<Vector3> { side + so * 0.006f * s, side + so * 0.012f * s + Vector3.up * 0.05f * s, side + so * 0.02f * s + Vector3.up * 0.09f * s };
            int t0 = m.T.Count;
            m.Tube(path, new List<float> { 0.006f * s, 0.005f * s, 0.004f * s }, false, 6, (k, a) => Color.Lerp(new Color(0.35f, 0.65f, 0.25f), new Color(0.6f, 0.85f, 0.4f), k / 2f), _ => f.W, Vector3.forward);
            m.Orient(t0, p => p - ClosestOnPath(path, p));
            Ball(m, path[2] + so * 0.012f * s, Quaternion.LookRotation(so, Vector3.up), new Vector3(0.006f, 0.02f, 0.002f) * s, _ => new Color(0.4f, 0.75f, 0.3f), f.W, 8, 5);
            return new[] { Piece(h, "head_panda_ears", m, vcMat) };
        }

        /// <summary>A big red toadstool with white spots sitting on top of the head.</summary>
        private static OutfitPiece[] MushroomCap(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var m = new OutfitMesh();
            Vector3 b = f.Top + Vector3.down * 0.01f * s;
            Color stem = new Color(0.98f, 0.93f, 0.82f);
            int t = m.T.Count;
            m.Lathe(Matrix4x4.Translate(b), new List<Vector2> { new Vector2(0.03f, 0f), new Vector2(0.026f, 0.03f), new Vector2(0.028f, 0.05f) }.Select(p => p * s).ToList(), 14, _ => stem, f.W);
            m.Orient(t, p => { Vector3 d = p - b; d.y = 0; return d; });
            Vector3 capC = b + Vector3.up * 0.05f * s;
            Ball(m, capC, Quaternion.identity, new Vector3(0.1f, 0.06f, 0.1f) * s, l => Color.Lerp(new Color(0.78f, 0.1f, 0.12f), new Color(1f, 0.32f, 0.28f), (l.y + 1f) * 0.5f), f.W, 18, 10, -0.05f);
            t = m.T.Count;
            m.Lathe(Matrix4x4.Translate(capC + Vector3.down * 0.003f * s), new List<Vector2> { new Vector2(0.1f, 0f), new Vector2(0.03f, 0.008f), new Vector2(0.001f, 0.01f) }.Select(p => p * s).ToList(), 18, _ => new Color(0.95f, 0.86f, 0.72f), f.W);
            m.Orient(t, _ => Vector3.down);
            var rng = new System.Random(5);
            for (int k = 0; k < 9; k++)
            {
                float lon = k * 40f + (float)rng.NextDouble() * 15f, lat = 0.25f + (float)rng.NextDouble() * 0.55f;
                Vector3 n = new Vector3(Mathf.Cos(lon * Mathf.Deg2Rad) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Sin(lon * Mathf.Deg2Rad) * Mathf.Cos(lat));
                Vector3 p = capC + Vector3.Scale(n, new Vector3(0.1f, 0.06f, 0.1f) * s);
                float r = (0.012f + (float)rng.NextDouble() * 0.008f) * s;
                Ball(m, p, Quaternion.LookRotation(Vector3.Cross(n, Vector3.right).normalized, n), new Vector3(r, 0.003f * s, r), _ => Color.white, f.W, 10, 4);
            }
            return new[] { Piece(h, "head_mushroom_cap", m, vcMat) };
        }

        /// <summary>A tall puffy white chef's toque on a pleated band.</summary>
        private static OutfitPiece[] ChefToque(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var m = new OutfitMesh();
            Vector3 b = f.Top + Vector3.down * 0.02f * s;
            int t = m.T.Count;
            m.Lathe(Matrix4x4.Translate(b), new List<Vector2> { new Vector2(0.055f, 0f), new Vector2(0.058f, 0.012f), new Vector2(0.058f, 0.05f), new Vector2(0.062f, 0.06f) }.Select(p => p * s).ToList(), 18,
                i => i % 2 == 0 ? new Color(0.92f, 0.93f, 0.96f) : Color.white, f.W);
            m.Orient(t, p => { Vector3 d = p - b; d.y = 0; return d; });
            Vector3 top = b + Vector3.up * 0.085f * s;
            Ball(m, top, Quaternion.identity, new Vector3(0.07f, 0.045f, 0.07f) * s, l => Color.Lerp(new Color(0.88f, 0.9f, 0.95f), Color.white, (l.y + 1f) * 0.5f), f.W, 16, 8);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                Ball(m, top + new Vector3(Mathf.Cos(a) * 0.045f, 0.012f, Mathf.Sin(a) * 0.045f) * s, Quaternion.identity, Vector3.one * 0.034f * s,
                    l => Color.Lerp(new Color(0.9f, 0.91f, 0.95f), Color.white, (l.y + 1f) * 0.5f), f.W, 12, 7);
            }
            return new[] { Piece(h, "head_chef_toque", m, vcMat) };
        }

        // ------------------------------------------------------------------ Diamond full sets
        /// <summary>Tail from the small of the back: tapering tube that curls up at the tip, on a pendulum joint (wags).</summary>
        private static OutfitPiece Tail(Hero h, string id, Color lo, Color hi, Color belly, bool spikes, bool sharkFin)
        {
            float s = h.H;
            Vector3 hips = h.P("Hips");
            int hipsB = h.B("Hips");
            float minZ = hips.z;
            for (int i = 0; i < h.V.Length; i++)
                if (Mathf.Abs(h.V[i].y - hips.y) < 0.03f * s && Mathf.Abs(h.V[i].x - hips.x) < 0.06f * s && h.W[i].boneIndex0 == hipsB) minZ = Mathf.Min(minZ, h.V[i].z);
            Vector3 root = new Vector3(hips.x, hips.y, minZ + 0.012f * s);
            var extras = new List<ExtraJoint> { new ExtraJoint { Name = "Outfit_" + id + "_Root", Parent = "Hips", MeshPos = root } };
            var w = OutfitMesh.Rigid(h.Bones.Length);
            var m = new OutfitMesh();
            var path = new List<Vector3> { root, root + new Vector3(0f, -0.02f, -0.06f) * s, root + new Vector3(0f, -0.045f, -0.12f) * s, root + new Vector3(0f, -0.05f, -0.18f) * s, root + new Vector3(0f, -0.03f, -0.235f) * s };
            var radii = new List<float> { 0.05f * s, 0.042f * s, 0.032f * s, 0.02f * s, 0.007f * s };
            int t0 = m.T.Count;
            m.Tube(path, radii, false, 12, (k, a) => Mathf.Sin(a) < -0.35f ? belly : Color.Lerp(lo, hi, k / 4f), _ => w, Vector3.up);
            m.Orient(t0, p => p - ClosestOnPath(path, p));
            Ball(m, root, Quaternion.identity, Vector3.one * 0.05f * s, l => lo, w, 10, 6); // rounded base
            if (spikes)
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = Vector3.Lerp(path[k], path[k + 1], 0.5f) + Vector3.up * Mathf.Lerp(radii[k], radii[k + 1], 0.5f) * 0.8f;
                    Cone(m, p, (Vector3.up * 0.9f + Vector3.back * 0.35f).normalized, (0.045f - k * 0.008f) * s, (0.018f - k * 0.003f) * s, 0.45f, new Color(1f, 0.6f, 0.15f), new Color(1f, 0.85f, 0.35f), w);
                }
            if (sharkFin)
                foreach (float dy in new[] { 1f, -1f })
                    Cone(m, path[4], (Vector3.back * 0.6f + Vector3.up * 0.8f * dy).normalized, 0.06f * s, 0.022f * s, 0.25f, lo, hi, w);
            return Piece(h, id, m, vcMat, extras, PieceMotion.Pendulum);
        }

        /// <summary>Khủng Long Con: orange back-spikes over the head, scaly green top with a yellow belly, wagging spiked tail.</summary>
        private static OutfitPiece[] DinoSet(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var crest = new OutfitMesh();
            Band(crest, f, 0.008f * s, new Color(0.2f, 0.55f, 0.25f), new Color(0.45f, 0.8f, 0.35f));
            float[] elev = { 0.35f, 0.62f, 0.86f, 1f, 0.86f, 0.62f, 0.35f };
            for (int k = 0; k < elev.Length; k++)
            {
                float ang = k < 3 ? 90f : 270f;
                Vector3 p = Dome(f, ang, elev[k], out Vector3 n);
                float size = k == 3 ? 1f : 1f - Mathf.Abs(k - 3) * 0.18f;
                Cone(crest, p - n * 0.004f * s, n, 0.085f * size * s, 0.032f * size * s, 0.4f, new Color(1f, 0.55f, 0.12f), new Color(1f, 0.86f, 0.35f), f.W);
            }
            var pieces = new List<OutfitPiece> { Piece(h, "set_little_dino_crest", crest, vcMat) };
            pieces.AddRange(TopShell(h, "set_little_dino_top", "Top_Dino", "dino", new Color(0.18f, 0.45f, 0.2f), new Color(0.32f, 0.62f, 0.28f))); // quiet green trim (yellow read as jagged noise)
            pieces.Add(Tail(h, "set_little_dino_tail", new Color(0.2f, 0.55f, 0.25f), new Color(0.42f, 0.78f, 0.32f), new Color(0.98f, 0.88f, 0.45f), true, false));
            return pieces.ToArray();
        }

        /// <summary>Cá Mập Nhỏ: dorsal fin on the head, grey-blue top with a white belly, forked shark tail.</summary>
        private static OutfitPiece[] SharkSet(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var fin = new OutfitMesh();
            Color g0 = new Color(0.3f, 0.42f, 0.56f), g1 = new Color(0.55f, 0.68f, 0.82f);
            Band(fin, f, 0.008f * s, g0, g1);
            Vector3 top = Dome(f, 270f, 0.9f, out Vector3 n);
            fin.Shard(Matrix4x4.TRS(top - n * 0.004f * s, Quaternion.Euler(-28f, 0f, 0f), new Vector3(0.016f, 0.16f, 0.075f) * s), 4, 0.05f, g1, g0, f.W);
            // little teeth along the front of the band
            for (int k = -3; k <= 3; k++)
            {
                Vector3 p = RingAt(f.Ring, 90f + k * 9f, out Vector3 o) + o * 0.006f * s;
                Cone(fin, p, (Vector3.down + o * 0.3f).normalized, 0.016f * s, 0.006f * s, 0.6f, Color.white, new Color(0.95f, 0.95f, 1f), f.W);
            }
            var pieces = new List<OutfitPiece> { Piece(h, "set_baby_shark_fin", fin, vcMat) };
            pieces.AddRange(TopShell(h, "set_baby_shark_top", "Top_Shark", "shark", new Color(0.32f, 0.44f, 0.58f), new Color(0.5f, 0.62f, 0.76f)));
            pieces.Add(Tail(h, "set_baby_shark_tail", g0, g1, new Color(0.92f, 0.95f, 1f), false, true));
            return pieces.ToArray();
        }

        /// <summary>Giấc Mơ Ngọt Ngào: floppy striped night-cap with a fluffy cuff and a pom-pom, plus a starry flannel pajama top.</summary>
        private static OutfitPiece[] DreamSet(Hero h)
        {
            HeadFrame f = Frame(h);
            float s = h.H;
            var cap = new OutfitMesh();
            Vector3 b = f.Top + Vector3.down * 0.012f * s;
            const float k1 = 1.6f; // chibi heads are big: the cap must read at gameplay distance
            var path = new List<Vector3> { b, b + new Vector3(0f, 0.05f, -0.005f) * s * k1, b + new Vector3(0.025f, 0.09f, -0.01f) * s * k1, b + new Vector3(0.07f, 0.1f, -0.01f) * s * k1, b + new Vector3(0.11f, 0.07f, -0.005f) * s * k1, b + new Vector3(0.13f, 0.03f, 0f) * s * k1 };
            var radii = new List<float> { 0.05f * s * k1, 0.042f * s * k1, 0.032f * s * k1, 0.022f * s * k1, 0.013f * s * k1, 0.006f * s * k1 };
            Color n0 = new Color(0.22f, 0.25f, 0.6f), n1 = new Color(0.55f, 0.65f, 1f);
            int t0 = cap.T.Count;
            cap.Tube(path, radii, false, 12, (k, a) => k % 2 == 0 ? n0 : n1, _ => f.W, Vector3.forward);
            cap.Orient(t0, p => p - ClosestOnPath(path, p));
            var cuff = new List<Vector3>();
            for (int i = 0; i < 16; i++) { float a = i / 16f * Mathf.PI * 2f; cuff.Add(b + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 0.052f * s * k1); }
            t0 = cap.T.Count;
            cap.Tube(cuff, Enumerable.Repeat(0.014f * s * k1, cuff.Count).ToList(), true, 8, (k, a) => Color.Lerp(new Color(0.92f, 0.92f, 0.98f), Color.white, Mathf.Sin(a) * 0.5f + 0.5f), _ => f.W, Vector3.up);
            cap.Orient(t0, p => p - ClosestOnPath(cuff, p));
            Ball(cap, path[5] + new Vector3(0.008f, -0.01f, 0f) * s * k1, Quaternion.identity, Vector3.one * 0.022f * s * k1, l => Color.Lerp(new Color(0.95f, 0.88f, 0.4f), new Color(1f, 0.98f, 0.75f), (l.y + 1f) * 0.5f), f.W);
            var pieces = new List<OutfitPiece> { Piece(h, "set_sweet_dream_cap", cap, vcMat) };
            pieces.AddRange(TopShell(h, "set_sweet_dream_top", "Top_Pajama", "pajama", new Color(0.42f, 0.55f, 0.88f), new Color(0.6f, 0.72f, 1f)));
            return pieces.ToArray();
        }

        // ------------------------------------------------------------------ patterns
        private static bool ThemePattern(string kind, float u, float v, out Color c)
        {
            switch (kind)
            {
                case "pajama":
                {
                    // sky-blue flannel, soft white stripes, little yellow stars
                    c = Color.Lerp(new Color(0.55f, 0.68f, 0.95f), new Color(0.62f, 0.75f, 1f), Noise(u * 20f, v * 20f) * 0.5f);
                    if (Mathf.Repeat(u * 10f, 1f) < 0.16f) c = Color.Lerp(c, Color.white, 0.75f);
                    Vector2 cell = new Vector2(Mathf.Repeat(u * 5f + (Mathf.Floor(v * 5f) % 2) * 0.5f, 1f) - 0.5f, Mathf.Repeat(v * 5f, 1f) - 0.5f);
                    float a = Mathf.Atan2(cell.y, cell.x), r = cell.magnitude;
                    float star = 0.13f * (0.55f + 0.45f * Mathf.Cos(a * 5f));
                    if (r < star && Mathf.Abs(Mathf.Repeat(u * 10f, 1f) - 0.08f) > 0.1f) c = new Color(1f, 0.88f, 0.35f);
                    c.a = 1f;
                    return true;
                }
                case "dino":
                {
                    // green scales (offset circles), darker scale edges
                    float cx = Mathf.Repeat(u * 14f + (Mathf.Floor(v * 18f) % 2) * 0.5f, 1f) - 0.5f, cy = Mathf.Repeat(v * 18f, 1f) - 0.5f;
                    float d = new Vector2(cx, cy * 1.2f).magnitude;
                    c = Color.Lerp(new Color(0.45f, 0.78f, 0.34f), new Color(0.22f, 0.52f, 0.24f), Mathf.SmoothStep(0.28f, 0.5f, d));
                    if (Noise(u * 6f, v * 6f) > 0.72f) c = Color.Lerp(c, new Color(0.9f, 0.75f, 0.3f), 0.35f); // freckles
                    c.a = 1f;
                    return true;
                }
                case "shark":
                {
                    // grey-blue skin with a soft white underside band (the texture wraps 3x around the torso, so no
                    // front-only features: they turned into ragged vertical stripes)
                    c = Color.Lerp(new Color(0.4f, 0.53f, 0.68f), new Color(0.52f, 0.65f, 0.8f), Noise(u * 10f, v * 10f) * 0.5f);
                    float belly = Mathf.SmoothStep(0.42f, 0.3f, v + (Noise(u * 4f, 0.5f) - 0.5f) * 0.04f);
                    c = Color.Lerp(c, new Color(0.94f, 0.96f, 1f), belly);
                    c.a = 1f;
                    return true;
                }
            }
            c = default;
            return false;
        }
    }
}

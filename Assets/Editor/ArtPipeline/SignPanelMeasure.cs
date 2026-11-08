using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Finds the blank parchment writing panel on a Meshy signboard the way the player sees it: a grid of rays is
    /// cast from the camera side against a temporary MeshCollider, each hit samples the model's own texture at the
    /// hit UV, and the largest connected light-parchment region is the panel. Sign text is laid out inside that
    /// measured rectangle (on the visible surface), instead of at a guessed position.
    /// </summary>
    internal static class SignPanelMeasure
    {
        internal struct Panel
        {
            public Vector3 Center;      // world, just in front of the visible surface
            public Quaternion Rotation; // canvas rotation (forward = into the board, away from the camera)
            public Vector2 Size;        // world metres (width, height) of the parchment region
        }

        private const float Step = 0.02f;

        internal static bool TryMeasure(GameObject board, string texturePath, out Panel panel)
        {
            panel = default;
            if (!board || !File.Exists(texturePath)) return false;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(texturePath))) return false; // readable copy; import settings untouched

            var colliders = new List<MeshCollider>();
            var bounds = new Bounds(board.transform.position, Vector3.zero);
            foreach (var mf in board.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colliders.Add(mc);
                bounds.Encapsulate(mf.GetComponent<Renderer>().bounds);
            }
            Physics.SyncTransforms();

            int nx = Mathf.CeilToInt(bounds.size.x / Step), ny = Mathf.CeilToInt(bounds.size.y / Step);
            var cream = new bool[nx, ny];
            var hitPoint = new Vector3[nx, ny];
            var hitNormal = new Vector3[nx, ny];
            float startZ = bounds.min.z - 1f;
            var debug = new Texture2D(nx, ny, TextureFormat.RGB24, false);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    debug.SetPixel(i, j, Color.magenta);
                    var origin = new Vector3(bounds.min.x + (i + 0.5f) * Step, bounds.min.y + (j + 0.5f) * Step, startZ);
                    if (!RaycastBoard(origin, colliders, out RaycastHit hit)) continue;
                    Color c = tex.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                    debug.SetPixel(i, j, c);
                    Color.RGBToHSV(c, out _, out float s, out float v);
                    // Light tan parchment (QA/Logs/sign_panel_samples.png): brighter and paler than the brown wood frame.
                    cream[i, j] = v > 0.6f && s < 0.5f;
                    hitPoint[i, j] = hit.point;
                    hitNormal[i, j] = hit.normal;
                }
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            Object.DestroyImmediate(tex);
            Directory.CreateDirectory("QA/Logs");
            File.WriteAllBytes("QA/Logs/sign_panel_samples.png", debug.EncodeToPNG()); // what the rays saw (QA evidence)
            Object.DestroyImmediate(debug);

            // Largest 4-connected parchment component = the writing panel.
            var label = new int[nx, ny];
            int best = 0, bestCount = 0, next = 0;
            var stack = new Stack<Vector2Int>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (!cream[i, j] || label[i, j] != 0) continue;
                    int id = ++next, count = 0;
                    stack.Push(new Vector2Int(i, j));
                    label[i, j] = id;
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        count++;
                        foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                        {
                            var q = p + d;
                            if (q.x < 0 || q.y < 0 || q.x >= nx || q.y >= ny || !cream[q.x, q.y] || label[q.x, q.y] != 0) continue;
                            label[q.x, q.y] = id;
                            stack.Push(q);
                        }
                    }
                    if (count > bestCount) { bestCount = count; best = id; }
                }
            if (bestCount < 40)
            {
                int hits = 0, creamCells = 0;
                for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) { if (hitNormal[i, j] != Vector3.zero) hits++; if (cream[i, j]) creamCells++; }
                Debug.LogWarning($"[SIGN] no panel: grid {nx}x{ny}, colliders {colliders.Count}, hits {hits}, parchment {creamCells}, largest {bestCount}");
                return false;
            }

            // Rows/columns that are mostly parchment give the usable rectangle (robust to ragged painted edges).
            var colFill = new int[nx]; var rowFill = new int[ny];
            Vector3 normal = Vector3.zero, centroid = Vector3.zero;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    if (label[i, j] == best) { colFill[i]++; rowFill[j]++; normal += hitNormal[i, j]; centroid += hitPoint[i, j]; }
            normal.Normalize();
            centroid /= bestCount;
            int maxCol = 0, maxRow = 0;
            foreach (int c in colFill) maxCol = Mathf.Max(maxCol, c);
            foreach (int r in rowFill) maxRow = Mathf.Max(maxRow, r);
            int i0 = nx, i1 = -1, j0 = ny, j1 = -1;
            for (int i = 0; i < nx; i++) if (colFill[i] >= maxCol * 0.6f) { i0 = Mathf.Min(i0, i); i1 = Mathf.Max(i1, i); }
            for (int j = 0; j < ny; j++) if (rowFill[j] >= maxRow * 0.6f) { j0 = Mathf.Min(j0, j); j1 = Mathf.Max(j1, j); }
            if (i1 < i0 || j1 < j0) return false;

            float x0 = bounds.min.x + i0 * Step, x1 = bounds.min.x + (i1 + 1) * Step;
            float y0 = bounds.min.y + j0 * Step, y1 = bounds.min.y + (j1 + 1) * Step;
            // Depth: the visible surface under the rectangle centre (plane through the panel hits).
            var c2 = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            float depth = centroid.z + (Vector3.Dot(new Vector3(c2.x - centroid.x, c2.y - centroid.y, 0f), normal) / Mathf.Max(0.2f, -normal.z));
            Vector3 onSurface = new Vector3(c2.x, c2.y, depth);
            float tilt = Mathf.Max(0.5f, -normal.z);
            panel.Center = onSurface + normal * 0.015f;
            panel.Rotation = Quaternion.LookRotation(-normal, Vector3.up);
            panel.Size = new Vector2((x1 - x0) / tilt, y1 - y0);
            Debug.Log($"[SIGN] parchment cells {bestCount}, rect x {x0:0.00}..{x1:0.00} y {y0:0.00}..{y1:0.00}, surface z {depth:0.00}, normal {normal}");
            return panel.Size.x > 0.15f && panel.Size.y > 0.1f;
        }

        /// <summary>
        /// Same camera-side ray grid, for any painted feature (e.g. the red paper of a lantern): returns the centre
        /// (on the visible surface) and size of the largest connected region whose texels match <paramref name="match"/>.
        /// </summary>
        internal static bool TryFindRegion(GameObject prop, string texturePath, System.Func<Color, bool> match, out Vector3 centre, out Vector2 size)
        {
            centre = default; size = default;
            if (!prop || !File.Exists(texturePath)) return false;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(texturePath))) return false;
            var colliders = new List<MeshCollider>();
            var bounds = new Bounds(prop.transform.position, Vector3.zero);
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colliders.Add(mc);
                bounds.Encapsulate(mf.GetComponent<Renderer>().bounds);
            }
            Physics.SyncTransforms();
            const float step = 0.025f;
            int nx = Mathf.CeilToInt(bounds.size.x / step), ny = Mathf.CeilToInt(bounds.size.y / step);
            var on = new bool[nx, ny];
            var point = new Vector3[nx, ny];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    var origin = new Vector3(bounds.min.x + (i + 0.5f) * step, bounds.min.y + (j + 0.5f) * step, bounds.min.z - 1f);
                    if (!RaycastBoard(origin, colliders, out RaycastHit hit)) continue;
                    on[i, j] = match(tex.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y));
                    point[i, j] = hit.point;
                }
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            Object.DestroyImmediate(tex);
            var label = new int[nx, ny];
            var stack = new Stack<Vector2Int>();
            List<Vector2Int> best = null;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (!on[i, j] || label[i, j] != 0) continue;
                    var comp = new List<Vector2Int>();
                    label[i, j] = 1;
                    stack.Push(new Vector2Int(i, j));
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop(); comp.Add(p);
                        foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                        {
                            var q = p + d;
                            if (q.x < 0 || q.y < 0 || q.x >= nx || q.y >= ny || !on[q.x, q.y] || label[q.x, q.y] != 0) continue;
                            label[q.x, q.y] = 1; stack.Push(q);
                        }
                    }
                    if (best == null || comp.Count > best.Count) best = comp;
                }
            if (best == null || best.Count < 12) return false;
            Vector3 sum = Vector3.zero;
            int x0 = nx, x1 = 0, y0 = ny, y1 = 0;
            foreach (var p in best) { sum += point[p.x, p.y]; x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            centre = sum / best.Count;
            size = new Vector2((x1 - x0 + 1) * step, (y1 - y0 + 1) * step);
            return true;
        }

        private static bool RaycastBoard(Vector3 origin, List<MeshCollider> colliders, out RaycastHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            bool any = false;
            var ray = new Ray(origin, Vector3.forward);
            foreach (var mc in colliders)
                if (mc.Raycast(ray, out RaycastHit h, 50f) && h.distance < bestDist) { best = h; bestDist = h.distance; any = true; }
            return any;
        }
    }
}

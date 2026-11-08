using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// INTERNAL QA: objective motion quality of each hero clip as the game plays it (hero prefab, clip import settings):
    ///  - loop seam: largest joint rotation jump (deg) and hips jump between the last and first frame of looping clips;
    ///  - shake: high-frequency residual of head, hands and weapon tip (position minus its 5-sample moving average),
    ///    as RMS in cm at game scale; smooth motion has a tiny residual, trembling has a large one.
    /// Output: QA/AnimAudit/motion_quality.md
    /// </summary>
    public static class MotionQualityAudit
    {
        public static void RunFromCommandLine()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/QA/Motion Quality Audit")]
        public static void Run()
        {
            var md = new StringBuilder("# Motion quality audit\n\nGame scale (hero ×1.55). Seam = jump between last and first frame of a looping clip. " +
                                       "Shake = RMS of high-frequency residual (cm); < 0.3 cm reads as smooth.\n\n" +
                                       "| Hero | Clip | Loop | Seam max joint (°) | Seam hips (cm) | Head shake | R hand shake | L hand shake | Weapon tip shake |\n|---|---|---|---|---|---|---|---|---|\n");
            foreach (var h in HeroArtDefinitions.Heroes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(h.Class));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.localScale = Vector3.one * 1.55f;
                Animator anim = go.GetComponentInChildren<Animator>();
                GameObject model = anim.gameObject;
                var ctrl = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = null;
                Transform[] all = go.GetComponentsInChildren<Transform>();
                Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
                Transform head = Find("Head"), rh = Find("RightHand"), lh = Find("LeftHand"), hips = Find("Hips");
                SkinnedMeshRenderer weapon = go.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.name.StartsWith("Weapon_"));
                var bones = all.Where(t => t.GetComponent<SkinnedMeshRenderer>() == null && t != go.transform && t != model.transform).ToArray();
                var mi = (ModelImporter)AssetImporter.GetAtPath($"{h.Folder}/{h.Fbx}");
                foreach (var clip in ((UnityEditor.Animations.AnimatorController)ctrl).animationClips.Distinct())
                {
                    var settings = mi.clipAnimations.FirstOrDefault(c => c.name == clip.name);
                    bool loop = settings != null && settings.loopTime;
                    // seam
                    clip.SampleAnimation(model, clip.length);
                    var endRot = bones.Select(b => b.localRotation).ToArray();
                    Vector3 endHips = hips.position;
                    clip.SampleAnimation(model, 0f);
                    float seamDeg = 0f;
                    var worst = new List<(string, float)>();
                    for (int i = 0; i < bones.Length; i++)
                    {
                        float a = Quaternion.Angle(endRot[i], bones[i].localRotation);
                        seamDeg = Mathf.Max(seamDeg, a);
                        worst.Add((bones[i].name, a));
                    }
                    if (loop) Debug.Log($"[MOTION] {h.Class} {clip.name} seam worst: " + string.Join(", ", worst.OrderByDescending(w => w.Item2).Take(5).Select(w => $"{w.Item1} {w.Item2:0.0}")));
                    float seamHips = Vector3.Distance(endHips, hips.position) * 100f;
                    // shake
                    const int N = 90;
                    var track = new Dictionary<string, Vector3[]> { ["head"] = new Vector3[N], ["rh"] = new Vector3[N], ["lh"] = new Vector3[N], ["tip"] = new Vector3[N] };
                    int tipIndex = -1;
                    for (int i = 0; i < N; i++)
                    {
                        clip.SampleAnimation(model, clip.length * i / (N - 1));
                        track["head"][i] = head.position; track["rh"][i] = rh.position; track["lh"][i] = lh.position;
                        track["tip"][i] = weapon ? WeaponVertex(weapon, ref tipIndex) : rh.position; // same vertex every frame
                    }
                    string Shake(Vector3[] p)
                    {
                        double s = 0; int n = 0;
                        for (int i = 2; i < N - 2; i++)
                        {
                            Vector3 avg = (p[i - 2] + p[i - 1] + p[i] + p[i + 1] + p[i + 2]) / 5f;
                            s += (p[i] - avg).sqrMagnitude; n++;
                        }
                        return (System.Math.Sqrt(s / n) * 100.0).ToString("0.00");
                    }
                    md.AppendLine($"| {h.Class} | {clip.name} | {(loop ? "yes" : "")} | {(loop ? seamDeg.ToString("0.0") : "-")} | {(loop ? seamHips.ToString("0.0") : "-")} | " +
                                  $"{Shake(track["head"])} | {Shake(track["rh"])} | {Shake(track["lh"])} | {Shake(track["tip"])} |");
                }
                anim.runtimeAnimatorController = ctrl;
                Object.DestroyImmediate(go);
            }
            Directory.CreateDirectory("QA/AnimAudit");
            File.WriteAllText("QA/AnimAudit/motion_quality.md", md.ToString());
            Debug.Log("[MOTION]\n" + md);
        }

        public static void LoopSearchFromCommandLine()
        {
            try { LoopSearch(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// For each looping clip, samples the whole Meshy take frame by frame (temporary probe clips, importer restored
        /// afterwards) and finds the frame range whose last pose best matches its first (true cycle).
        /// </summary>
        public static void LoopSearch()
        {
            var md = new StringBuilder("# Loop search (frames @30 fps)\n\n| Hero | Clip | Take | Take frames | Current | Current seam (mean°/max°) | Best | Best seam (mean°/max°) |\n|---|---|---|---|---|---|---|---|\n");
            string[] major = { "Hips", "Spine", "Spine01", "Spine02", "neck", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
            foreach (var h in HeroArtDefinitions.Heroes)
            {
                string fbx = $"{h.Folder}/{h.Fbx}";
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                ModelImporterClipAnimation[] keep = mi.clipAnimations;
                var loops = h.Clips.Where(c => c.Loop).ToArray();
                try
                {
                    var probes = keep.ToList();
                    foreach (var c in loops)
                    {
                        TakeInfo t = mi.importedTakeInfos.First(x => x.name == c.Take || x.name.EndsWith("|" + c.Take));
                        probes.Add(new ModelImporterClipAnimation
                        {
                            name = "__probe_" + c.Name, takeName = t.name, firstFrame = 0,
                            lastFrame = Mathf.Round((t.stopTime - t.startTime) * t.sampleRate),
                            lockRootRotation = true, keepOriginalOrientation = true, lockRootPositionXZ = true, keepOriginalPositionXZ = true,
                            lockRootHeightY = c.BakeY, keepOriginalPositionY = true,
                        });
                    }
                    mi.clipAnimations = probes.ToArray();
                    mi.SaveAndReimport();
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    var go = (GameObject)Object.Instantiate(asset);
                    var all = go.GetComponentsInChildren<Transform>();
                    var bones = major.Select(n => all.FirstOrDefault(b => b.name == n)).Where(b => b).ToArray();
                    var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().ToDictionary(c => c.name);
                    foreach (var c in loops)
                    {
                        AnimationClip probe = clips["__probe_" + c.Name];
                        int frames = Mathf.RoundToInt(probe.length * 30f);
                        var poses = new Quaternion[frames + 1][];
                        for (int f = 0; f <= frames; f++)
                        {
                            probe.SampleAnimation(go, f / 30f);
                            poses[f] = bones.Select(b => b.localRotation).ToArray();
                        }
                        (float mean, float max) Seam(int a, int b)
                        {
                            float s = 0f, mx = 0f;
                            for (int i = 0; i < bones.Length; i++) { float d = Quaternion.Angle(poses[a][i], poses[b][i]); s += d; mx = Mathf.Max(mx, d); }
                            return (s / bones.Length, mx);
                        }
                        int cur0 = Mathf.RoundToInt(c.First), cur1 = Mathf.Min(frames, Mathf.RoundToInt(c.Last));
                        var cur = Seam(cur0, cur1);
                        // Search: a cycle at least 70% as long as the current clip (never a degenerate tiny loop).
                        int minLen = Mathf.Max(8, Mathf.RoundToInt((c.Last - c.First) * 0.7f));
                        (int a, int b, float mean, float max) best = (cur0, cur1, cur.mean, cur.max);
                        for (int a = 0; a <= frames; a++)
                            for (int b = a + minLen; b <= frames; b++)
                            {
                                var s = Seam(a, b);
                                // prefer low max, then long clips (more variety), then close to the current start
                                float score = s.max + s.mean * 2f - (b - a) * 0.02f;
                                float bestScore = best.max + best.mean * 2f - (best.b - best.a) * 0.02f;
                                if (score < bestScore) best = (a, b, s.mean, s.max);
                            }
                        md.AppendLine($"| {h.Class} | {c.Name} | {c.Take} | {frames} | {cur0}-{cur1} | {cur.mean:0.0}/{cur.max:0.0} | {best.a}-{best.b} | {best.mean:0.0}/{best.max:0.0} |");
                    }
                    Object.DestroyImmediate(go);
                }
                finally
                {
                    mi.clipAnimations = keep;
                    mi.SaveAndReimport();
                }
            }
            Directory.CreateDirectory("QA/AnimAudit");
            File.WriteAllText("QA/AnimAudit/loop_search.md", md.ToString());
            Debug.Log("[LOOP]\n" + md);
        }

        /// <summary>World position of one fixed weapon vertex (the one farthest from the hand on the first call).</summary>
        private static Vector3 WeaponVertex(SkinnedMeshRenderer weapon, ref int index)
        {
            var baked = new Mesh();
            weapon.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            Object.DestroyImmediate(baked);
            Matrix4x4 m = weapon.transform.localToWorldMatrix;
            if (index < 0)
            {
                Transform hand = weapon.rootBone ? weapon.rootBone : weapon.transform;
                float bestD = -1f;
                for (int i = 0; i < v.Length; i++)
                {
                    float d = (m.MultiplyPoint3x4(v[i]) - hand.position).sqrMagnitude;
                    if (d > bestD) { bestD = d; index = i; }
                }
            }
            return m.MultiplyPoint3x4(v[index]);
        }
    }
}

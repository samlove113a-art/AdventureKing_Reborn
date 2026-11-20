using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// INTERNAL QA: objective audit of each hero's animation set, hands, weapons and face.
    ///  - contact sheet per class: every gameplay clip at 4 times, from the in-game camera yaw (110°);
    ///  - close-ups of face and weapon hand (idle and attack contact frame);
    ///  - weapon-to-hand distance on every sampled frame (weapon is skinned to the hand: must stay ~0);
    ///  - foot sliding: during Run ground contact, foot speed backwards vs. the motor's run speed;
    ///  - ladder: hand vertical speed vs. the motor's climb speed;
    ///  - rig capabilities: finger bones, face bones, blendshapes (facial expressions).
    /// Output: QA/AnimAudit/*.png and QA/AnimAudit/audit.json / audit.md.
    /// </summary>
    public static class AnimationAudit
    {
        private const float HeroScale = VillageSceneBuilder.HeroScale, RunSpeed = VillageSceneBuilder.RunSpeed, ClimbSpeed = VillageSceneBuilder.ClimbSpeed;
        private static readonly string[] Classes = { "Swordsman", "Mage", "Assassin", "Archer" };
        private static readonly string[] Clips = { "Idle", "Run", "Jump", "Fall", "Climb", "Attack", "Skill", "Hit", "Death" };
        private const int Cell = 300;

        public static void RunFromCommandLine()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/QA/Animation Audit")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("QA/AnimAudit");
            var light = new GameObject("Key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.7f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.86f, 0.94f);
            cam.fieldOfView = 24f;
            var rt = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;

            var report = new StringBuilder("# Animation audit\n\n");
            var json = new List<string>();
            foreach (string cls in Classes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{cls}_Visual.prefab");
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                hero.transform.localScale = Vector3.one * HeroScale;
                hero.transform.rotation = Quaternion.Euler(0f, 110f, 0f);
                Animator anim = hero.GetComponentInChildren<Animator>();
                GameObject model = anim.gameObject;
                var ctrl = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = null; // SampleAnimation writes transforms directly in edit mode
                Transform[] bones = model.GetComponentsInChildren<Transform>();
                Transform B(string part) => bones.FirstOrDefault(t => t.name.EndsWith(part));
                Transform rHand = B("RightHand"), lHand = B("LeftHand"), rFoot = B("RightFoot"), lFoot = B("LeftFoot"), head = B("Head");
                SkinnedMeshRenderer[] smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                SkinnedMeshRenderer body = smrs.FirstOrDefault(s => !s.name.StartsWith("Weapon_")) ?? smrs[0];
                SkinnedMeshRenderer[] weapons = smrs.Where(s => s.name.StartsWith("Weapon_")).ToArray();

                // ---- rig capabilities
                int fingerBones = bones.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "Thumb|Index|Middle|Ring|Pinky|Finger", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                int faceBones = bones.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "Jaw|Eye|Brow|Lid|Mouth|Lip", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                int blendshapes = smrs.Sum(s => s.sharedMesh ? s.sharedMesh.blendShapeCount : 0);

                // ---- contact sheet
                var sheet = new Texture2D(Cell * 4, Cell * Clips.Length, TextureFormat.RGB24, false);
                float maxWeaponGap = 0f;
                var clipNotes = new List<string>();
                for (int ci = 0; ci < Clips.Length; ci++)
                {
                    AnimationClip clip = ctrl.animationClips.FirstOrDefault(c => c.name == Clips[ci]);
                    if (!clip) { clipNotes.Add($"{Clips[ci]}: MISSING"); continue; }
                    for (int k = 0; k < 4; k++)
                    {
                        float t = clip.length * (k + 0.5f) / 4f;
                        clip.SampleAnimation(model, t);
                        foreach (var w in weapons)
                            maxWeaponGap = Mathf.Max(maxWeaponGap, WeaponGap(w, rHand, lHand));
                        Frame(cam, PoseBounds(bones, hero.transform), 1.1f);
                        Blit(cam, rt, sheet, k * Cell, (Clips.Length - 1 - ci) * Cell);
                    }
                }
                File.WriteAllBytes($"QA/AnimAudit/{cls}_clips.png", sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);

                // ---- close-ups: face idle, hand idle, face attack-contact, hand attack-contact
                var close = new Texture2D(Cell * 4, Cell, TextureFormat.RGB24, false);
                AnimationClip idle = ctrl.animationClips.First(c => c.name == "Idle");
                AnimationClip attack = ctrl.animationClips.First(c => c.name == "Attack");
                float contact = attack.events.Length > 0 ? attack.events[0].time : attack.length * 0.4f;
                Transform weaponHand = cls == "Archer" ? lHand : rHand;
                (AnimationClip clip, float time, Transform focus, float size)[] shots =
                {
                    (idle, idle.length * 0.3f, head, 0.5f), (idle, idle.length * 0.3f, weaponHand, 0.32f),
                    (attack, contact, head, 0.5f), (attack, contact, weaponHand, 0.32f),
                };
                for (int i = 0; i < shots.Length; i++)
                {
                    shots[i].clip.SampleAnimation(model, shots[i].time);
                    Vector3 focus = shots[i].focus.position + (shots[i].focus == head ? Vector3.up * 0.12f * HeroScale : Vector3.zero);
                    Frame(cam, new Bounds(focus, Vector3.one * shots[i].size * HeroScale), 1f);
                    Blit(cam, rt, close, i * Cell, 0);
                }
                File.WriteAllBytes($"QA/AnimAudit/{cls}_closeups.png", close.EncodeToPNG());
                Object.DestroyImmediate(close);

                // ---- foot sliding on Run (root XZ is baked: feet must move back at the run speed while planted)
                float slideRatio = FootSpeedRatio(ctrl, model, rFoot, lFoot);
                float climbRatio = ClimbSpeedRatio(ctrl, model, rHand, lHand);
                // With playback matched to speed (CharacterVisual3D), the residual foot slip at full run:
                var profile = hero.GetComponent<AdventureKing.Gameplay.HeroMotionProfile>();
                float playback = profile ? Mathf.Clamp(RunSpeed / (profile.runStrideSpeed * HeroScale), 0.5f, 2.6f) : 1f;
                float residualSlip = slideRatio > 0 ? Mathf.Abs(1f - slideRatio * playback) : -1f;
                clipNotes.Add($"runPlayback={playback:0.00}x, residualFootSlip={residualSlip * 100f:0}%");

                string weaponList = string.Join("+", weapons.Select(w => w.name));
                string line = $"{cls}: weapons={weaponList}, fingers={fingerBones}, faceBones={faceBones}, blendshapes={blendshapes}, " +
                              $"maxWeaponGap={maxWeaponGap * 100f:0.0}cm, runFootSpeed/runSpeed={slideRatio:0.00}, climbHandSpeed/climbSpeed={climbRatio:0.00}" +
                              (clipNotes.Count > 0 ? ", " + string.Join("; ", clipNotes) : "");
                Debug.Log("[AUDIT] " + line);
                report.AppendLine("- " + line);
                json.Add($"{{\"class\":\"{cls}\",\"fingerBones\":{fingerBones},\"faceBones\":{faceBones},\"blendshapes\":{blendshapes}," +
                         $"\"maxWeaponGapCm\":{maxWeaponGap * 100f:0.00},\"runFootSpeedRatio\":{slideRatio:0.000},\"climbSpeedRatio\":{climbRatio:0.000}}}");
                anim.runtimeAnimatorController = ctrl;
                Object.DestroyImmediate(hero);
            }
            File.WriteAllText("QA/AnimAudit/audit.json", "[\n" + string.Join(",\n", json) + "\n]\n");
            File.WriteAllText("QA/AnimAudit/audit.md", report.ToString());
            rt.Release();
        }

        /// <summary>
        /// Average backward foot speed (world units/s, hero scale applied) while the foot is planted
        /// (lowest 20% of its height range), divided by the motor run speed. 1.0 = no sliding at 1x playback.
        /// </summary>
        private static float FootSpeedRatio(RuntimeAnimatorController ctrl, GameObject model, Transform r, Transform l)
        {
            AnimationClip run = ctrl.animationClips.FirstOrDefault(c => c.name == "Run");
            if (!run || !r || !l) return -1f;
            const int N = 120;
            var speeds = new List<float>();
            foreach (Transform foot in new[] { r, l })
            {
                var pos = new Vector3[N];
                for (int i = 0; i < N; i++) { run.SampleAnimation(model, run.length * i / N); pos[i] = foot.position; }
                float minY = pos.Min(p => p.y), maxY = pos.Max(p => p.y);
                Vector3 fwd = model.transform.root.forward; fwd.y = 0; fwd.Normalize();
                float dt = run.length / N;
                for (int i = 0; i < N; i++)
                {
                    int j = (i + 1) % N;
                    if (pos[i].y > minY + (maxY - minY) * 0.2f) continue;
                    speeds.Add(-Vector3.Dot(pos[j] - pos[i], fwd) / dt);
                }
            }
            return speeds.Count == 0 ? -1f : speeds.Average() / RunSpeed;
        }

        private static float ClimbSpeedRatio(RuntimeAnimatorController ctrl, GameObject model, Transform r, Transform l)
        {
            AnimationClip climb = ctrl.animationClips.FirstOrDefault(c => c.name == "Climb");
            if (!climb || !r) return -1f;
            const int N = 120;
            var pos = new Vector3[N];
            for (int i = 0; i < N; i++) { climb.SampleAnimation(model, climb.length * i / N); pos[i] = r.position; }
            // While the hand grips a rung it moves DOWN relative to the body at the climb speed.
            float dt = climb.length / N;
            var down = new List<float>();
            for (int i = 0; i < N; i++)
            {
                float v = (pos[(i + 1) % N].y - pos[i].y) / dt;
                if (v < 0f) down.Add(-v);
            }
            if (down.Count == 0) return -1f;
            down.Sort();
            return down[down.Count / 2] / ClimbSpeed; // median grip speed
        }

        /// <summary>Bounds of the posed skeleton (SMR.bounds is not refreshed by edit-mode sampling).</summary>
        private static Bounds PoseBounds(Transform[] bones, Transform root)
        {
            var b = new Bounds(root.position + Vector3.up * 0.8f, Vector3.one * 0.2f);
            foreach (Transform t in bones) b.Encapsulate(t.position);
            b.Expand(0.35f); // skin, hair and hat extend past the joints
            return b;
        }

        /// <summary>Distance from the weapon's grip (closest baked vertex) to its hand joint, in metres.</summary>
        private static float WeaponGap(SkinnedMeshRenderer w, Transform rHand, Transform lHand)
        {
            var baked = new Mesh();
            w.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            Object.DestroyImmediate(baked);
            // The hand each weapon is skinned to (bow and off-hand dagger: left; others: right).
            Transform hand = w.name.EndsWith("_L") || w.name.Contains("Bow") ? lHand : rHand;
            float best = float.MaxValue;
            Matrix4x4 m = w.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length; i += 3) best = Mathf.Min(best, (m.MultiplyPoint3x4(v[i]) - hand.position).magnitude);
            return best;
        }

        private static void Frame(Camera cam, Bounds b, float margin)
        {
            float radius = b.extents.magnitude * margin;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = b.center + new Vector3(0f, radius * 0.15f, -dist);
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 3f);
            cam.farClipPlane = dist + radius * 3f;
        }

        private static void Blit(Camera cam, RenderTexture rt, Texture2D dst, int x, int y)
        {
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            dst.ReadPixels(new Rect(0, 0, Cell, Cell), x, y);
            dst.Apply();
            RenderTexture.active = null;
        }
    }
}

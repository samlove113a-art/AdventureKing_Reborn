using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Plays the real hero Animator (prefab + controller, as in game) for several loop cycles of Idle, Run and Climb
    /// and checks there is no pop: the largest per-frame joint change must stay close to the clip's normal motion,
    /// and the hips must not jump vertically at loop seams.
    /// </summary>
    public sealed class HeroMotionTests
    {
        private static readonly string[] Heroes = { "Swordsman", "Mage", "Assassin", "Archer" };
        private static readonly string[] Major = { "Hips", "Spine", "Spine01", "Spine02", "Head", "LeftArm", "LeftForeArm", "LeftHand",
            "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };

        /// <summary>
        /// Every gameplay state of every hero, driven through the real Animator parameters, reached and photographed
        /// at the in-game side angle (QA/AnimAudit/play_states_*.png): Idle, Run, Jump, Fall, Climb, Attack, Skill, Hit.
        /// </summary>
        [UnityTest]
        public IEnumerator All_Gameplay_States_Reached_And_Captured()
        {
            TestRunnerGuard.Protect();
            var lightGo = new GameObject("Test_Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            var camGo = new GameObject("Test_Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.8f, 0.87f, 0.94f);
            cam.fieldOfView = 30f;
            const int cell = 256;
            string[] states = { "Idle", "Run", "Jump", "Fall", "Climb", "Attack", "Skill", "Hit" };
            try
            {
                foreach (string hero in Heroes)
                {
#if UNITY_EDITOR
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{hero}_Visual.prefab");
#else
                    GameObject prefab = null;
#endif
                    GameObject go = Object.Instantiate(prefab);
                    go.transform.localScale = Vector3.one * 1.55f;
                    go.transform.rotation = Quaternion.Euler(0f, 110f, 0f); // same yaw as CharacterVisual3D facing right
                    var anim = go.GetComponentInChildren<Animator>();
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    camGo.transform.position = new Vector3(0f, 1.25f, -6.2f);
                    camGo.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                    var sheet = new Texture2D(cell * states.Length, cell, TextureFormat.RGB24, false);
                    for (int s = 0; s < states.Length; s++)
                    {
                        anim.Rebind();
                        anim.Update(0f);
                        yield return null;
                        switch (states[s])
                        {
                            case "Run": anim.SetFloat("Speed", 4.6f); anim.SetFloat("RunPlayback", 2.2f); break;
                            case "Jump": anim.SetBool("Grounded", false); anim.SetFloat("VerticalSpeed", 6f); break;
                            case "Fall": anim.SetBool("Grounded", false); anim.SetFloat("VerticalSpeed", -6f); break;
                            case "Climb": anim.SetBool("Climbing", true); anim.SetFloat("ClimbPlayback", 1.5f); break;
                            case "Attack": anim.SetTrigger("Attack"); break;
                            case "Skill": anim.SetTrigger("Skill"); break;
                            case "Hit": anim.SetTrigger("Hit"); break;
                        }
                        bool reached = false;
                        float limit = Time.time + 1f; // time-based: transitions are 0.05-0.15 s, frames can be very short in batch
                        while (!reached && Time.time < limit)
                        {
                            yield return null;
                            reached = anim.GetCurrentAnimatorStateInfo(0).IsName(states[s]);
                        }
                        Assert.IsTrue(reached, $"{hero} reaches {states[s]}");
                        // photograph mid-action
                        float wait = states[s] == "Attack" || states[s] == "Skill" || states[s] == "Hit" ? 0.18f : 0.3f;
                        float end = Time.time + wait;
                        while (Time.time < end) yield return null;
                        var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 };
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, cell, cell), s * cell, 0);
                        RenderTexture.active = null;
                        cam.targetTexture = null;
                        rt.Release();
                    }
                    sheet.Apply();
                    System.IO.Directory.CreateDirectory("QA/AnimAudit");
                    System.IO.File.WriteAllBytes($"QA/AnimAudit/play_states_{hero}.png", sheet.EncodeToPNG());
                    Object.Destroy(sheet);
                    Object.Destroy(go);
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
            }
        }

        [UnityTest]
        public IEnumerator Loops_Play_Without_Pops_Or_Hip_Jumps()
        {
            TestRunnerGuard.Protect();
            Time.captureFramerate = 60; // deterministic 1/60 s steps
            var report = new List<string>();
            try
            {
                foreach (string hero in Heroes)
                {
#if UNITY_EDITOR
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{hero}_Visual.prefab");
#else
                    GameObject prefab = null;
#endif
                    GameObject go = Object.Instantiate(prefab);
                    go.transform.localScale = Vector3.one * 1.55f;
                    var anim = go.GetComponentInChildren<Animator>();
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // no camera in this test scene
                    var all = go.GetComponentsInChildren<Transform>();
                    var bones = Major.Select(n => all.FirstOrDefault(t => t.name == n)).Where(t => t).ToArray();
                    Transform hips = bones.First(b => b.name == "Hips");

                    foreach (var (state, setup) in new (string, System.Action)[]
                             {
                                 ("Idle", () => { }),
                                 ("Run", () => { anim.SetFloat("Speed", 4.6f); anim.SetFloat("RunPlayback", 2.2f); }),
                                 ("Climb", () => { anim.SetBool("Climbing", true); anim.SetFloat("ClimbPlayback", 1.6f); }),
                             })
                    {
                        anim.Rebind();
                        anim.Update(0f);
                        setup();
                        // settle into the state (transition finished)
                        for (int i = 0; i < 30; i++) yield return null;
                        Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName(state), $"{hero} in {state}");
                        var prev = bones.Select(b => b.localRotation).ToArray();
                        // World space: the rig's local units are FBX centimetres under scaled parents.
                        float prevHipY = hips.position.y;
                        var steps = new List<float>();
                        var hipSteps = new List<float>();
                        for (int f = 0; f < 240; f++) // 4 s = several cycles, so seams are crossed
                        {
                            yield return null;
                            float step = 0f;
                            for (int i = 0; i < bones.Length; i++)
                            {
                                step = Mathf.Max(step, Quaternion.Angle(prev[i], bones[i].localRotation));
                                prev[i] = bones[i].localRotation;
                            }
                            steps.Add(step);
                            hipSteps.Add(Mathf.Abs(hips.position.y - prevHipY));
                            prevHipY = hips.position.y;
                        }
                        var sorted = steps.OrderBy(s => s).ToList();
                        float p90 = sorted[(int)(sorted.Count * 0.9f)], max = sorted.Last();
                        float hipMax = hipSteps.Max() * 100f; // cm per frame
                        report.Add($"{hero} {state}: joint step p90 {p90:0.0}°, max {max:0.0}°; hips max {hipMax:0.00} cm/frame");
                        Assert.Greater(p90, 0.05f, $"{hero} {state}: the animation is not actually playing (test would be blind)");
                        // A seam pop is a single frame far above the clip's normal motion.
                        Assert.Less(max, Mathf.Max(6f, p90 * 2.2f), $"{hero} {state} pops: max joint step {max:0.0}° vs p90 {p90:0.0}°");
                        Assert.Less(hipMax, 3.0f, $"{hero} {state} hips jump {hipMax:0.00} cm in one frame");
                    }
                    Object.Destroy(go);
                    yield return null;
                }
            }
            finally
            {
                Time.captureFramerate = 0;
                foreach (string line in report) TestContext.WriteLine(line);
                System.IO.Directory.CreateDirectory("QA/AnimAudit");
                System.IO.File.WriteAllLines("QA/AnimAudit/playmode_motion.txt", report);
            }
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Finger rig in Play Mode on the real hero prefabs: the weapon hand closes on its grip, the free hand rests
    /// half-curled, both close on rungs while climbing. Close-ups before (fingers off) / after go to QA/Hands/.
    /// </summary>
    public sealed class HeroHandsTests
    {
        private static readonly string[] Heroes = { "Swordsman", "Mage", "Assassin", "Archer" };

        [UnityTest]
        public IEnumerator Fingers_Grip_Weapons_Rest_And_Close_On_Rungs()
        {
            TestRunnerGuard.Protect();
            Directory.CreateDirectory("QA/Hands");
            var lightGo = new GameObject("Test_Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var camGo = new GameObject("Test_Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.8f, 0.86f, 0.93f);
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.02f;
            const int cell = 256;
            var sheet = new Texture2D(cell * 6, cell * Heroes.Length, TextureFormat.RGB24, false);
            try
            {
                for (int hi = 0; hi < Heroes.Length; hi++)
                {
#if UNITY_EDITOR
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{Heroes[hi]}_Visual.prefab");
#else
                    GameObject prefab = null;
#endif
                    GameObject go = Object.Instantiate(prefab);
                    go.transform.localScale = Vector3.one * 1.55f;
                    go.transform.rotation = Quaternion.Euler(0f, 150f, 0f);
                    var anim = go.GetComponentInChildren<Animator>();
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var fingers = go.GetComponent<HeroFingers>();
                    Assert.IsNotNull(fingers, Heroes[hi] + " HeroFingers");
                    Assert.IsTrue(fingers.left.joints.All(j => j) && fingers.right.joints.All(j => j), Heroes[hi] + " finger joints on both hands");
                    var all = go.GetComponentsInChildren<Transform>();
                    Transform lh = all.First(t => t.name == "LeftHand"), rh = all.First(t => t.name == "RightHand");
                    HeroFingers.Hand weaponHand = fingers.right.weaponGrip >= 0f ? fingers.right : fingers.left;
                    Transform weaponBone = weaponHand == fingers.right ? rh : lh;
                    Transform freeBone = weaponBone == rh ? lh : rh;
                    HeroFingers.Hand freeHand = weaponHand == fingers.right ? fingers.left : fingers.right;
                    Assert.GreaterOrEqual(weaponHand.weaponGrip, 0.2f, Heroes[hi] + " weapon grip measured");

                    IEnumerator Shot(Transform target, int col)
                    {
                        // look across the curl plane (along the curl axis) so the bend reads clearly
                        HeroFingers.Hand data = target == rh ? fingers.right : fingers.left;
                        Vector3 axis = data.joints[0].TransformDirection(data.curlAxisLocal[0]).normalized;
                        Vector3 focus = (target.position + data.joints[1].position) * 0.5f;
                        camGo.transform.position = focus + axis * 0.5f;
                        camGo.transform.LookAt(focus, Vector3.up);
                        yield return null;
                        var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 };
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, cell, cell), col * cell, (Heroes.Length - 1 - hi) * cell);
                        RenderTexture.active = null;
                        cam.targetTexture = null;
                        rt.Release();
                    }

                    // BEFORE: fingers as imported (straight, the old look)
                    fingers.enabled = false;
                    HeroFingers.Pose(fingers.left, 0f); HeroFingers.Pose(fingers.right, 0f);
                    yield return new WaitForSeconds(0.3f);
                    HeroFingers.Pose(fingers.left, 0f); HeroFingers.Pose(fingers.right, 0f);
                    yield return Shot(weaponBone, 0);
                    yield return Shot(freeBone, 2);
                    // AFTER: rig on
                    fingers.enabled = true;
                    yield return new WaitForSeconds(0.6f);
                    Assert.AreEqual(weaponHand.weaponGrip, weaponHand.current, 0.05f, Heroes[hi] + " weapon hand closes on the grip");
                    Assert.Greater(freeHand.current, 0.2f, Heroes[hi] + " free hand rests curled");
                    yield return Shot(weaponBone, 1);
                    yield return Shot(freeBone, 3);
                    // CLIMB: both hands close on the rung
                    anim.SetBool("Climbing", true);
                    float until = Time.time + 1f;
                    while (!anim.GetCurrentAnimatorStateInfo(0).IsName("Climb") && Time.time < until) yield return null;
                    yield return new WaitForSeconds(0.6f);
                    Assert.Greater(fingers.left.current, 0.6f, Heroes[hi] + " left hand closes on the rung");
                    Assert.Greater(fingers.right.current, 0.6f, Heroes[hi] + " right hand closes on the rung");
                    yield return Shot(lh, 4);
                    yield return Shot(rh, 5);
                    Object.Destroy(go);
                    yield return null;
                }
                sheet.Apply();
                File.WriteAllBytes("QA/Hands/play_hands.png", sheet.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(sheet);
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
            }
        }
    }
}

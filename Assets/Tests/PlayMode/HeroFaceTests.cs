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
    /// Real hero prefabs in Play Mode (Idle animation running underneath): every expression and a real blink are
    /// rendered as close-ups for QA (QA/Faces/play_*), and the face weights are checked numerically.
    /// </summary>
    public sealed class HeroFaceTests
    {
        private static readonly string[] Heroes = { "Swordsman", "Mage", "Assassin", "Archer" };

        [UnityTest]
        public IEnumerator Four_Heroes_Blink_And_Show_Expressions_Over_Idle()
        {
            TestRunnerGuard.Protect();
            Directory.CreateDirectory("QA/Faces");
            var lightGo = new GameObject("Test_Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            var camGo = new GameObject("Test_FaceCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.86f, 0.94f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.05f;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.7f);

            var shots = new (string name, HeroFace.Expression? e, bool blink)[]
            {
                ("Idle", HeroFace.Expression.Neutral, false), ("Blink", null, true), ("Happy", HeroFace.Expression.Happy, false),
                ("Sad", HeroFace.Expression.Sad, false), ("Surprised", HeroFace.Expression.Surprised, false),
                ("Angry", HeroFace.Expression.Angry, false), ("Hit", HeroFace.Expression.Hurt, false),
            };
            try
            {
                foreach (string hero in Heroes)
                {
#if UNITY_EDITOR
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{hero}_Visual.prefab");
#else
                    GameObject prefab = null;
#endif
                    Assert.IsNotNull(prefab, hero + " prefab");
                    GameObject go = Object.Instantiate(prefab);
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f)); // face the camera (-Z)
                    var face = go.GetComponent<HeroFace>();
                    Assert.IsNotNull(face, hero + " has HeroFace");
                    var anim = go.GetComponentInChildren<Animator>();
                    Assert.IsNotNull(anim);
                    yield return null;
                    Transform head = go.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
                    float h = Vector3.Distance(head.position, go.GetComponentsInChildren<Transform>().First(t => t.name == "Hips").position);

                    foreach (var shot in shots)
                    {
                        if (shot.blink)
                        {
                            face.SetExpression(HeroFace.Expression.Neutral);
                            yield return new WaitForSeconds(0.4f);
                            face.Blink();
                            float until = Time.time + 1f;
                            while (face.BlinkAmount < 0.97f && Time.time < until) yield return null;
                            Assert.GreaterOrEqual(face.BlinkAmount, 0.97f, hero + " blink closes the eyes");
                        }
                        else
                        {
                            face.SetExpression(shot.e.Value);
                            yield return new WaitForSeconds(0.6f);
                            // never catch a random blink in an expression shot
                            float until = Time.time + 1f;
                            while (face.BlinkAmount > 0f && Time.time < until) yield return null;
                        }
                        // Frame the face straight on, following the animated head (combat idles tilt it).
                        Vector3 focus = head.position + face.FaceUp * h * 0.28f;
                        camGo.transform.position = focus + face.FaceForward * h * 4.2f;
                        camGo.transform.LookAt(focus, face.FaceUp);
                        Render(cam, $"QA/Faces/play_{hero}_{shot.name}.png");
                        if (shot.name == "Happy") Assert.Less(face.EyeOpen, 0.1f, hero + " happy eyes closed into arcs");
                        if (shot.name == "Idle") Assert.Greater(face.EyeOpen, 0.9f, hero + " neutral eyes open");
                        if (shot.name == "Sad") Assert.Less(face.MouthCurve, -0.4f, hero + " sad frown");
                    }
                    // Body animation keeps running while the face changes.
                    Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"), hero + " Idle still playing");
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

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(512, 512, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.Destroy(tex);
            rt.Release();
        }
    }
}

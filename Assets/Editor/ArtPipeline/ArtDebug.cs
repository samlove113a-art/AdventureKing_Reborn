using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Internal diagnostics: body facing (hip axis) per clip, measured in the visual root's space.</summary>
    public static class ArtDebug
    {
        public static float HipYaw(Transform root, Transform lUpLeg, Transform rUpLeg)
        {
            Vector3 right = root.InverseTransformDirection(rUpLeg.position - lUpLeg.position);
            right.y = 0f;
            Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        public static void DumpFromCommandLine()
        {
            foreach (HeroClass hc in System.Enum.GetValues(typeof(HeroClass)))
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(hc)));
                Animator a = inst.GetComponentInChildren<Animator>();
                var clips = a.runtimeAnimatorController.animationClips;
                Transform l = a.GetComponentsInChildren<Transform>().First(t => t.name == "LeftUpLeg");
                Transform r = a.GetComponentsInChildren<Transform>().First(t => t.name == "RightUpLeg");
                var parts = clips.Select(c =>
                {
                    float sum = 0f;
                    for (int i = 0; i < 5; i++)
                    {
                        c.SampleAnimation(a.gameObject, c.length * (i + 0.5f) / 5f);
                        sum += HipYaw(inst.transform, l, r);
                    }
                    return $"{c.name}={sum / 5f:0}";
                });
                Debug.Log($"[DBG] {hc} hip yaw: {string.Join(" ", parts)}");
                Object.DestroyImmediate(inst);
            }
            EditorApplication.Exit(0);
        }
    }
}

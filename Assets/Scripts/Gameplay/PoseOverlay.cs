using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Small procedural poses layered on top of whatever clip is playing (runs after the Animator, in LateUpdate):
    /// <b>Lean</b> bends spine, neck and head toward the facing direction (the wedding kiss: both partners lean in) and
    /// <b>Cut</b> raises the right arm forward and brings it down twice (cutting the cake; positive = forward for a hanging arm, measured in Game View). Eased in and out, so the body never
    /// snaps; uses the Meshy skeleton bone names (Spine01/Spine02/neck/Head, RightArm/RightForeArm).
    /// </summary>
    public sealed class PoseOverlay : MonoBehaviour
    {
        private Transform spine1, spine2, neck, head, arm, foreArm, model;
        private float leanTarget, lean, cutStart = -10f, cutSeconds;
        public float LeanNow => lean;
        public bool Cutting => Time.time - cutStart < cutSeconds;
        public bool HasArm => arm;
        public Transform Arm => arm;
        public int AppliedFrame { get; private set; }
        public Transform Model => model;

        public void Bind(Transform modelRoot)
        {
            model = modelRoot;
            // the bones the body mesh is actually skinned to (outfit layers can carry their own copies of the skeleton:
            // rotating a copy moves nothing on screen)
            SkinnedMeshRenderer body = null;
            foreach (var s in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh && (!body || s.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = s;
            Transform[] bones = body && body.bones != null && body.bones.Length > 0 ? body.bones : modelRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in bones)
                if (t) switch (t.name)
                {
                    case "Spine01": spine1 = t; break;
                    case "Spine02": spine2 = t; break;
                    case "neck": neck = t; break;
                    case "Head": head = t; break;
                    case "RightArm": arm = t; break;
                    case "RightForeArm": foreArm = t; break;
                }
        }

        /// <summary>Lean forward by this many degrees in total (0 = upright); eased.</summary>
        public void Lean(float degrees) => leanTarget = degrees;

        /// <summary>Two cutting strokes over the given time.</summary>
        public void Cut(float seconds) { cutStart = Time.time; cutSeconds = seconds; }

        private void LateUpdate()
        {
            if (!model) return;
            AppliedFrame = Time.frameCount;
            lean = Mathf.MoveTowards(lean, leanTarget, Time.deltaTime * 30f);
            // the model's own right axis (it is yawed to face left/right): bending about it pitches toward the facing
            Vector3 right = model.right;
            if (lean > 0.01f)
            {
                void Bend(Transform b, float share) { if (b) b.rotation = Quaternion.AngleAxis(lean * share, right) * b.rotation; }
                Bend(spine1, 0.3f); Bend(spine2, 0.3f); Bend(neck, 0.2f); Bend(head, 0.2f);
            }
            if (Cutting && arm)
            {
                float k = (Time.time - cutStart) / Mathf.Max(0.1f, cutSeconds);
                float raise = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);                     // up over the whole gesture
                float strokes = 0.5f + 0.5f * Mathf.Cos(Mathf.Clamp01(k) * Mathf.PI * 4f);   // two strokes down
                // swing in the screen plane toward the facing side (the heroes stand 3/4 to the camera: a pitch about
                // the model's own right axis moved the hand toward the lens and read as no motion at all)
                Vector3 swing = Vector3.forward * (model.forward.x < 0f ? -1f : 1f);
                arm.rotation = Quaternion.AngleAxis(raise * (95f + 25f * strokes), swing) * arm.rotation; // up over the cake
                if (foreArm) foreArm.rotation = Quaternion.AngleAxis(-45f * raise * (1f - strokes), swing) * foreArm.rotation;
            }
        }
    }
}

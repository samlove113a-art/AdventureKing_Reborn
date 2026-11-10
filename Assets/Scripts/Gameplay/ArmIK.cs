using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Two-bone arm IK + head look for the Meshy skeletons (generic rigs: no Unity humanoid IK). Runs after the
    /// Animator and after <see cref="PoseOverlay"/> (lean), so a hand really reaches a world point — a partner's hand,
    /// a cake knife, a goblet, the mouth — while the clip keeps animating the rest of the body. Targets are set every
    /// frame by the caller (cinematic timeline) with a weight 0..1; weight 0 leaves the clip untouched.
    /// Elbow solved in the plane of shoulder, target and a bend hint (default: down and outward, like a relaxed arm).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class ArmIK : MonoBehaviour
    {
        public sealed class Chain
        {
            public Transform upper, lower, hand;
            public Vector3 target;
            public Vector3 bendHint;   // world direction the elbow should point to (0 = automatic)
            public float weight;
            public Quaternion? handRotation; // optional world rotation of the hand at the target
            public float Length => upper && lower && hand ? Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position) : 0f;
        }

        public Chain Left { get; } = new Chain();
        public Chain Right { get; } = new Chain();
        private Transform model, neck, head, spine2, headFront;
        private Vector3 lookTarget;
        private float lookWeight, headRoll;
        private HeroFace face;

        public Transform Model => model;
        public Transform Head => head;
        public Transform Spine => spine2;
        /// <summary>Front of the face (hero rigs carry a "headfront" bone; otherwise estimated from the face axes).</summary>
        public Vector3 FaceFront => headFront ? headFront.position : head ? head.position + FaceForward * 0.12f * Mathf.Abs(model.lossyScale.y) : transform.position;
        public Vector3 FaceForward => face ? face.FaceForward : model ? model.forward : Vector3.forward;
        public Vector3 FaceUp => face ? face.FaceUp : Vector3.up;
        public float ArmLength => Mathf.Max(Left.Length, Right.Length);
        public Vector3 ShoulderMid => Left.upper && Right.upper ? (Left.upper.position + Right.upper.position) * 0.5f : transform.position;

        public void Bind(Transform modelRoot)
        {
            model = modelRoot;
            face = modelRoot.GetComponentInChildren<HeroFace>(true);
            // the bones the body mesh is skinned to (outfit pieces can carry their own copies)
            SkinnedMeshRenderer body = null;
            foreach (var s in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh && (!body || s.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = s;
            Transform[] bones = body && body.bones != null && body.bones.Length > 0 ? body.bones : modelRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in bones)
            {
                if (!t) continue;
                switch (t.name)
                {
                    case "LeftArm": Left.upper = t; break;
                    case "LeftForeArm": Left.lower = t; break;
                    case "LeftHand": Left.hand = t; break;
                    case "RightArm": Right.upper = t; break;
                    case "RightForeArm": Right.lower = t; break;
                    case "RightHand": Right.hand = t; break;
                    case "neck": neck = t; break;
                    case "Head": head = t; break;
                    case "Spine02": spine2 = t; break;
                }
            }
            foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "headfront") { headFront = t; break; }
        }

        public Chain Arm(bool left) => left ? Left : Right;

        /// <summary>Reach this frame (call every frame while active).</summary>
        public void Reach(bool left, Vector3 target, float weight, Vector3 bendHint = default, Quaternion? handRotation = null)
        {
            Chain c = Arm(left);
            c.target = target;
            c.weight = Mathf.Clamp01(weight);
            c.bendHint = bendHint;
            c.handRotation = handRotation;
        }

        public void Release(bool left) => Arm(left).weight = 0f;
        public void ReleaseAll() { Left.weight = Right.weight = 0f; lookWeight = 0f; headRoll = 0f; }

        /// <summary>Turns neck + head toward a point (weight 0..1); roll tilts the head sideways (kiss).</summary>
        public void Look(Vector3 target, float weight, float roll = 0f)
        {
            lookTarget = target;
            lookWeight = Mathf.Clamp01(weight);
            headRoll = roll;
        }

        private void LateUpdate()
        {
            if (!model) return;
            if (lookWeight > 0.001f && head) SolveLook();
            Solve(Left, true);
            Solve(Right, false);
        }

        private void SolveLook()
        {
            Vector3 fwd = FaceForward;
            Vector3 to = lookTarget - head.position;
            if (to.sqrMagnitude < 1e-6f) return;
            Quaternion full = Quaternion.FromToRotation(fwd, to.normalized);
            full.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            ang = Mathf.Clamp(ang, -55f, 55f) * lookWeight;
            if (neck) neck.rotation = Quaternion.AngleAxis(ang * 0.4f, axis) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(ang * 0.6f, axis) * head.rotation;
            if (Mathf.Abs(headRoll) > 0.01f) head.rotation = Quaternion.AngleAxis(headRoll * lookWeight, FaceForward) * head.rotation;
        }

        private void Solve(Chain c, bool left)
        {
            if (c.weight < 0.001f || !c.upper || !c.lower || !c.hand) return;
            Vector3 a = c.upper.position, b = c.lower.position, h = c.hand.position;
            float lab = Vector3.Distance(a, b), lbh = Vector3.Distance(b, h);
            Vector3 t = Vector3.Lerp(h, c.target, c.weight);
            Vector3 at = t - a;
            float d = Mathf.Clamp(at.magnitude, Mathf.Abs(lab - lbh) + 1e-3f, lab + lbh - 1e-3f);
            Vector3 dir = at.sqrMagnitude > 1e-8f ? at.normalized : model.forward;
            // bend hint: elbow down and out to the side of this arm, unless the caller asked for another direction
            Vector3 hint = c.bendHint.sqrMagnitude > 1e-6f ? c.bendHint.normalized
                : (-Vector3.up * 0.8f + (left ? -model.right : model.right) * 0.6f - model.forward * 0.2f).normalized;
            Vector3 perp = hint - Vector3.Dot(hint, dir) * dir;
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, model.right);
            perp.Normalize();
            float cosA = Mathf.Clamp((lab * lab + d * d - lbh * lbh) / (2f * lab * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = a + dir * (lab * cosA) + perp * (lab * sinA);
            c.upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * c.upper.rotation;
            b = c.lower.position; h = c.hand.position;
            c.lower.rotation = Quaternion.FromToRotation(h - b, a + dir * d - b) * c.lower.rotation;
            if (c.handRotation.HasValue)
                c.hand.rotation = Quaternion.Slerp(c.hand.rotation, c.handRotation.Value, c.weight);
        }
    }
}

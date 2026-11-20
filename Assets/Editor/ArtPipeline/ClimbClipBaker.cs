using System.Collections.Generic;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Bakes a ladder-climb clip whose hands and feet land on real rungs (VillageSceneBuilder ladders: rungs every
    /// 0.42 m, first 0.35 m above the foot, in the plane 0.4 m in front of the hero). Torso and head come from the
    /// Meshy climb take (in place); arms and legs are solved with two-bone IK each frame:
    ///  - each limb alternates a grip phase (locked to its rung while the body rises past it) and a reach phase
    ///    (hand/foot pulled back off the ladder, up two rungs, onto the next rung);
    ///  - left hand / right foot and right hand / left foot move together, half a cycle apart.
    /// One clip cycle = the body rising two rungs. The game drives the clip's normalized time from the hero's real
    /// height on the ladder (CharacterVisual3D, "ClimbPhase"), so gripping hands never slide at any climb speed.
    /// </summary>
    public static class ClimbClipBaker
    {
        // Rungs 0.30 m apart (chibi scale: the old 0.42 m needed more reach than the 0.26-0.33 m arms have).
        public const float RungSpacing = 0.3f, FirstRung = 0.3f, LadderZ = 0.4f, HeroScale = VillageSceneBuilder.HeroScale;
        const int Frames = 48;
        public const float Grip = 0.55f; // fraction of the cycle a limb holds its rung (hands overlap briefly)

        public sealed class Result
        {
            public AnimationClip Clip;
            public float GripTop;   // hand grip height at grip start, relative to the hero's feet, model scale
            public string Report;
        }

        /// <summary>Bakes the clip on <paramref name="model"/> (the prefab's model instance) from the in-place base clip.</summary>
        public static Result Bake(HeroArtDefinitions.Hero h, GameObject model, AnimationClip baseClip, string path)
        {
            // Work at game scale and facing (+Z = towards the ladder), at the origin, feet on y = 0.
            Transform root = model.transform;
            Transform oldParent = root.parent;
            Vector3 oldPos = root.position, oldScale = root.localScale; Quaternion oldRot = root.rotation;
            root.SetParent(null, true);
            root.position = Vector3.zero;
            root.localScale = Vector3.one * HeroScale;
            // keep the prefab's own yaw fix (model faces +Z)
            Animator anim = model.GetComponent<Animator>();
            RuntimeAnimatorController keep = anim ? anim.runtimeAnimatorController : null;
            if (anim) anim.runtimeAnimatorController = null;

            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            Transform B(string n) => all.First(t => t.name == n);
            Transform hips = B("Hips");
            var arms = new[] { (B("LeftArm"), B("LeftForeArm"), B("LeftHand"), -1f), (B("RightArm"), B("RightForeArm"), B("RightHand"), 1f) };
            var legs = new[] { (B("LeftUpLeg"), B("LeftLeg"), B("LeftFoot"), -1f), (B("RightUpLeg"), B("RightLeg"), B("RightFoot"), 1f) };
            var bones = all.Where(t => t != root && t.GetComponent<SkinnedMeshRenderer>() == null).ToArray();

            // Proportions from the first base frame (character's own skeleton).
            baseClip.SampleAnimation(model, 0f);
            float Side(Transform t) => Vector3.Dot(t.position, root.right);
            // left/right by actual position (rigs name sides from the character's point of view)
            if (Side(arms[0].Item1) > Side(arms[1].Item1)) { (arms[0].Item4, arms[1].Item4) = (1f, -1f); (legs[0].Item4, legs[1].Item4) = (1f, -1f); }
            float armLen = Vector3.Distance(arms[0].Item1.position, arms[0].Item2.position) + Vector3.Distance(arms[0].Item2.position, arms[0].Item3.position);
            float legLen = Vector3.Distance(legs[0].Item1.position, legs[0].Item2.position) + Vector3.Distance(legs[0].Item2.position, legs[0].Item3.position);
            float shoulderY = (arms[0].Item1.position.y + arms[1].Item1.position.y) * 0.5f;
            float shoulderZ = (arms[0].Item1.position.z + arms[1].Item1.position.z) * 0.5f;
            float hipJointY = (legs[0].Item1.position.y + legs[1].Item1.position.y) * 0.5f;
            // Bring the body close enough to the ladder that the arms work bent, not locked straight.
            float bodyShift = (LadderZ - 0.06f) - (shoulderZ + armLen * 0.5f);
            // Each grip is centred on the shoulders (within arm's reach); feet grip about knee height; all on rungs.
            float s = RungSpacing, travel = 2f * s * Grip;
            float top = shoulderY + travel * 0.5f;
            float footTop = top - Mathf.Round((top - (hipJointY - legLen * 0.45f)) / s) * s;

            var rot = new Dictionary<Transform, Quaternion[]>();
            foreach (var b in bones) rot[b] = new Quaternion[Frames + 1];
            var hipPos = new Vector3[Frames + 1];
            float maxGap = 0f;
            for (int f = 0; f <= Frames; f++)
            {
                float p = f / (float)Frames;
                baseClip.SampleAnimation(model, (p % 1f) * baseClip.length);
                // close the base cycle smoothly (torso/head): blend the last 20% towards the first frame
                if (p > 0.8f && f > 0)
                {
                    float k = Mathf.SmoothStep(0f, 1f, (p - 0.8f) / 0.2f);
                    foreach (var b in bones) b.localRotation = Quaternion.Slerp(b.localRotation, rot[b][0], k);
                }
                hips.position += new Vector3(0f, 0f, bodyShift);

                // limbs: (phase offset, grip height at grip start)
                Vector3 Target(float phase, float gripTop, float side, float reachBack, float z)
                {
                    float q = (p + phase) % 1f;
                    float x = side * 0.2f;
                    if (q < Grip) return new Vector3(x, gripTop - q / Grip * travel, z);           // locked on the rung
                    float r = (q - Grip) / (1f - Grip);                                             // reach up to the next rung
                    float e = Mathf.SmoothStep(0f, 1f, r);
                    return new Vector3(x, Mathf.Lerp(gripTop - travel, gripTop, e), z - reachBack * Mathf.Sin(r * Mathf.PI));
                }
                for (int i = 0; i < 2; i++)
                {
                    var (upper, lower, end, side) = arms[i];
                    float phase = side < 0 ? 0f : 0.5f;
                    Vector3 grip = Target(phase, top, side, 0.16f, LadderZ - 0.05f);
                    Vector3 wrist = grip + new Vector3(0f, -0.03f, -0.07f);
                    Vector3 pole = upper.position + new Vector3(side * 0.6f, -0.7f, -0.25f);
                    SolveTwoBone(upper, lower, end, wrist, pole);
                    if ((p + phase) % 1f < Grip) maxGap = Mathf.Max(maxGap, Vector3.Distance(end.position, wrist));
                }
                for (int i = 0; i < 2; i++)
                {
                    var (upper, lower, end, side) = legs[i];
                    float phase = side < 0 ? 0.5f : 0f; // diagonal pairing with the opposite hand
                    Vector3 step = Target(phase, footTop, side * 0.6f, 0.14f, LadderZ - 0.08f);
                    Vector3 ankle = step + new Vector3(0f, 0.06f, -0.06f);
                    Vector3 pole = upper.position + new Vector3(side * 0.25f, 0.1f, 0.8f);
                    SolveTwoBone(upper, lower, end, ankle, pole);
                }
                foreach (var b in bones) rot[b][f] = b.localRotation;
                hipPos[f] = hips.localPosition;
            }

            // ---- write the clip (rotations of every bone, hips position), quaternion sign continuity
            var clip = new AnimationClip { name = "Climb", frameRate = 30f };
            float dt = 1f / 30f;
            foreach (var b in bones)
            {
                string bp = AnimationUtility.CalculateTransformPath(b, root);
                var q = rot[b];
                for (int f = 1; f <= Frames; f++) if (Quaternion.Dot(q[f], q[f - 1]) < 0f) q[f] = new Quaternion(-q[f].x, -q[f].y, -q[f].z, -q[f].w);
                var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                for (int f = 0; f <= Frames; f++)
                {
                    cx.AddKey(f * dt, q[f].x); cy.AddKey(f * dt, q[f].y); cz.AddKey(f * dt, q[f].z); cw.AddKey(f * dt, q[f].w);
                }
                Set(clip, bp, "m_LocalRotation.x", cx); Set(clip, bp, "m_LocalRotation.y", cy);
                Set(clip, bp, "m_LocalRotation.z", cz); Set(clip, bp, "m_LocalRotation.w", cw);
            }
            string hp = AnimationUtility.CalculateTransformPath(hips, root);
            var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
            for (int f = 0; f <= Frames; f++) { px.AddKey(f * dt, hipPos[f].x); py.AddKey(f * dt, hipPos[f].y); pz.AddKey(f * dt, hipPos[f].z); }
            Set(clip, hp, "m_LocalPosition.x", px); Set(clip, hp, "m_LocalPosition.y", py); Set(clip, hp, "m_LocalPosition.z", pz);
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);

            if (anim) anim.runtimeAnimatorController = keep;
            baseClip.SampleAnimation(model, 0f);
            root.SetParent(oldParent, true);
            root.position = oldPos; root.rotation = oldRot; root.localScale = oldScale;

            string report = $"{h.Class}: arm {armLen:0.00} m, leg {legLen:0.00} m, body shift {bodyShift:0.00} m, grip top {top:0.00} m, foot top {footTop:0.00} m, " +
                            $"max hand-to-rung gap while gripping {maxGap * 100f:0.0} cm";
            Debug.Log("[CLIMB] " + report);
            return new Result { Clip = clip, GripTop = top / HeroScale, Report = report };
        }

        static void Set(AnimationClip clip, string path, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);

        /// <summary>Analytic two-bone IK: rotates upper and lower so that end reaches target, bending towards pole.</summary>
        static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position;
            float l1 = Vector3.Distance(a, lower.position), l2 = Vector3.Distance(lower.position, end.position);
            Vector3 toT = target - a;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
            Vector3 dir = toT.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            Vector3 elbow = a + (dir * cosA + bend * Mathf.Sqrt(1f - cosA * cosA)) * l1;
            upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
            Vector3 endTarget = a + dir * d;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, endTarget - lower.position) * lower.rotation;
        }
    }
}

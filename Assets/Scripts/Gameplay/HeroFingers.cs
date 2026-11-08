using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Curls the hero finger blocks (knuckle / middle / tip joints added at import by HeroHandRig) after the
    /// Animator has posed the body: the weapon hand closes around its grip, the free hand rests half-curled,
    /// both hands close on rungs while climbing and open briefly when hit. Pure visual, no gameplay effect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroFingers : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Hand
        {
            public Transform[] joints = new Transform[3];
            public Quaternion[] restLocal = new Quaternion[3];
            public Vector3[] curlAxisLocal = new Vector3[3];
            [Tooltip("Curl (0 open .. 1 fist) that closes this hand around its weapon grip; < 0 = no weapon.")]
            public float weaponGrip = -1f;
            [HideInInspector] public float current;
        }

        public Hand left = new Hand(), right = new Hand();
        [SerializeField] private Animator animator;
        [SerializeField] private float relaxed = 0.32f, climbGrip = 0.7f, hitOpen = 0.05f;
        // gentle chibi curl: deeper angles fold the fused mitten skin into creases
        private static readonly float[] MaxAngle = { 58f, 62f, 44f };
        private static readonly int ClimbState = Animator.StringToHash("Climb"), HitState = Animator.StringToHash("Hit");

        public void Configure(Animator anim, Hand l, Hand r)
        {
            animator = anim;
            left = l;
            right = r;
        }

        /// <summary>Poses one hand at curl c (edit-time measurement and runtime).</summary>
        public static void Pose(Hand h, float c)
        {
            for (int j = 0; j < h.joints.Length; j++)
                if (h.joints[j]) h.joints[j].localRotation = h.restLocal[j] * Quaternion.AngleAxis(c * MaxAngle[j], h.curlAxisLocal[j]);
        }

        public float CurlOf(bool leftHand) => (leftHand ? left : right).current;

        private float Target(Hand h)
        {
            if (!animator) return h.weaponGrip >= 0f ? h.weaponGrip : relaxed;
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
            if (s.shortNameHash == ClimbState) return climbGrip;
            if (h.weaponGrip >= 0f) return h.weaponGrip;      // the weapon never leaves the hand
            if (s.shortNameHash == HitState) return hitOpen;   // startled: fingers fly open
            return relaxed;
        }

        private void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            foreach (Hand h in new[] { left, right })
            {
                if (h == null || h.joints == null || h.joints.Length == 0 || !h.joints[0]) continue;
                h.current = Mathf.Lerp(h.current, Target(h), k);
                Pose(h, h.current);
            }
        }
    }
}

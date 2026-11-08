using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Optional visual bridge: a rigged 3D character follows existing 2D physics.
    /// Attach to the XY gameplay root; assign a child model and its Animator.
    /// Does not create animations or change network/gameplay authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisual3D : MonoBehaviour
    {
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerMelee2D melee;
        [SerializeField] private Health2D health;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float turnSpeed = 16f;
        [Tooltip("Yaw of the visual root while facing right. 90 = pure profile; >90 turns the face " +
                 "slightly toward the camera. Facing left mirrors it (-yaw) so the face stays visible.")]
        [SerializeField] private float rightFacingYaw = 110f;
        [Tooltip("Yaw while on a ladder (0 = back to the camera, facing into the scene).")]
        [SerializeField] private float climbingYaw = 0f;

        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Climbing = Animator.StringToHash("Climbing");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int Skill = Animator.StringToHash("Skill");

        private static readonly int RunPlayback = Animator.StringToHash("RunPlayback");
        private static readonly int ClimbPlayback = Animator.StringToHash("ClimbPlayback");
        [Tooltip("Upper limit for Run playback speed (beyond this a little foot slide is preferred to frantic legs).")]
        [SerializeField, Min(1f)] private float maxRunPlayback = 2.6f;
        private HeroMotionProfile profile;
        private bool hasRunPlayback, hasClimbPlayback;

        public Animator Animator => animator;

        /// <summary>Starts the class skill animation; contact frames raise CharacterAnimationEvents.</summary>
        public void PlaySkill()
        {
            if (animator && !(health && health.IsDead)) animator.SetTrigger(Skill);
        }

        /// <summary>Wires a visual prefab instantiated at runtime (e.g. from the selected class).</summary>
        public void Bind(Transform root, Animator anim)
        {
            visualRoot = root;
            animator = anim;
            CacheProfile();
        }

        private void CacheProfile()
        {
            profile = null;
            if (visualRoot)
            {
                profile = visualRoot.GetComponentInParent<HeroMotionProfile>();
                if (!profile) profile = visualRoot.GetComponentInChildren<HeroMotionProfile>();
            }
            hasRunPlayback = hasClimbPlayback = false;
            if (!animator) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == RunPlayback) hasRunPlayback = true;
                if (p.nameHash == ClimbPlayback) hasClimbPlayback = true;
            }
        }

        /// <summary>Run playback that makes the planted foot move exactly with the ground at this speed.</summary>
        public float RunPlaybackFor(float horizontalSpeed)
        {
            if (!profile || !visualRoot) return 1f;
            float stride = profile.runStrideSpeed * Mathf.Abs(visualRoot.lossyScale.y);
            return stride > 0.01f ? Mathf.Clamp(horizontalSpeed / stride, 0.5f, maxRunPlayback) : 1f;
        }

        /// <summary>Climb playback that advances two rungs per cycle at this vertical speed (0 = hold still).</summary>
        public float ClimbPlaybackFor(float verticalSpeed)
        {
            if (!profile) return 1f;
            float perSecond = profile.climbAdvancePerCycle / Mathf.Max(0.05f, profile.climbCycleSeconds);
            return Mathf.Clamp(Mathf.Abs(verticalSpeed) / perSecond, 0f, 3.5f);
        }

        private void Awake()
        {
            if (!motor) motor = GetComponent<PlayerMotor2D>();
            if (!melee) melee = GetComponent<PlayerMelee2D>();
            if (!health) health = GetComponent<Health2D>();
            if (!animator && visualRoot) animator = visualRoot.GetComponentInChildren<Animator>();
            CacheProfile();
        }

        private void OnEnable()
        {
            if (melee) melee.Attacked += HandleAttack;
            if (health)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (melee) melee.Attacked -= HandleAttack;
            if (health)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (!motor) return;

            if (visualRoot)
            {
                float yaw = motor.IsClimbing ? climbingYaw
                    : motor.Facing < 0 ? -rightFacingYaw : rightFacingYaw;
                Quaternion target = Quaternion.Euler(0f, yaw, 0f);
                float factor = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, target, factor);
            }

            if (!animator) return;
            Vector2 motion = motor.Velocity;
            animator.SetFloat(Speed, Mathf.Abs(motion.x));
            animator.SetFloat(VerticalSpeed, motion.y);
            animator.SetBool(Grounded, motor.IsGrounded);
            animator.SetBool(Climbing, motor.IsClimbing);
            if (hasRunPlayback) animator.SetFloat(RunPlayback, RunPlaybackFor(Mathf.Abs(motion.x)));
            if (hasClimbPlayback) animator.SetFloat(ClimbPlayback, ClimbPlaybackFor(motion.y));
            if (health) animator.SetBool(Dead, health.IsDead);
        }

        private void HandleAttack()
        {
            if (animator) animator.SetTrigger(Attack);
        }

        private void HandleDamaged(Health2D victim, int amount)
        {
            if (animator && amount > 0 && !victim.IsDead) animator.SetTrigger(Hit);
        }

        private void HandleDied(Health2D victim)
        {
            if (animator) animator.SetBool(Dead, true);
        }
    }
}

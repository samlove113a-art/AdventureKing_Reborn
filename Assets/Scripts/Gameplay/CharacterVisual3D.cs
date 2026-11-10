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
            if (animator && !(health && health.IsDead))
            {
                animator.SetTrigger(Skill);
                if (face) face.Pulse(HeroFace.Expression.Angry, 0.7f); // determined battle cry
            }
        }

        /// <summary>
        /// Emote gestures (wedding ceremony): Kneel (ring on one knee), Toast, Cheer, Celebrate, Bow — with a matching
        /// expression. Returns false when this hero's animator has no such state (older builds) or the hero is down.
        /// </summary>
        public bool PlayEmote(string state, HeroFace.Expression expression = HeroFace.Expression.Happy, float expressionSeconds = 2.5f)
        {
            if (!animator || (health && health.IsDead)) return false;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return false;
            animator.CrossFadeInFixedTime(hash, 0.2f, 0);
            if (face) face.Pulse(expression, expressionSeconds);
            LastEmote = state;
            return true;
        }

        public string LastEmote { get; private set; }

        /// <summary>Shows / hides the class weapon meshes (renderers "Weapon_*"): put away for ceremonies.</summary>
        public void SetWeaponsVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Weapon_")) r.enabled = visible;
            WeaponsVisible = visible;
        }

        public bool WeaponsVisible { get; private set; } = true;

        /// <summary>Puts a prop in the right hand (null removes it). The prop's origin is the grip, +Y up out of the fist.</summary>
        public void HoldInRightHand(GameObject prop) => HoldInHand(prop, false);

        /// <summary>Puts a prop in the left or right hand (null removes it).</summary>
        public void HoldInHand(GameObject prop, bool left)
        {
            if (held) Destroy(held);
            held = prop;
            if (!prop) return;
            Transform hand = null;
            string socket = left ? "Socket_L" : "Socket_R", bone = left ? "LeftHand" : "RightHand";
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == socket) { hand = t; break; }
            if (!hand) foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(bone)) { hand = t; break; }
            if (!hand) { Destroy(prop); held = null; return; }
            prop.transform.SetParent(hand, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            // world size stays the authored size whatever the bone scale
            Vector3 s = hand.lossyScale;
            prop.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
        }

        private GameObject held;
        public GameObject Held => held;

        private HeroFace face;
        private static readonly int ClimbPhaseId = Animator.StringToHash("ClimbPhase");
        private bool hasClimbPhase, hasBaseLocal;
        private Vector3 baseLocal;

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
            face = visualRoot ? visualRoot.GetComponentInChildren<HeroFace>() : null;
            if (visualRoot)
            {
                profile = visualRoot.GetComponentInParent<HeroMotionProfile>();
                if (!profile) profile = visualRoot.GetComponentInChildren<HeroMotionProfile>();
            }
            hasRunPlayback = hasClimbPlayback = hasClimbPhase = false;
            if (!animator) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == RunPlayback) hasRunPlayback = true;
                if (p.nameHash == ClimbPlayback) hasClimbPlayback = true;
                if (p.nameHash == ClimbPhaseId) hasClimbPhase = true;
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
            float scale = visualRoot ? Mathf.Abs(visualRoot.lossyScale.y) : 1f; // advance is measured at model scale 1
            float perSecond = profile.climbAdvancePerCycle * scale / Mathf.Max(0.05f, profile.climbCycleSeconds);
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

                // On a ladder the body eases onto the ladder's centre line so the hands meet the rungs
                // (the physics body may drift sideways inside the zone; this is visual only).
                if (!hasBaseLocal) { baseLocal = visualRoot.localPosition; hasBaseLocal = true; }
                LadderZone2D ladder = motor.IsClimbing ? motor.Ladder : null;
                float wantX = ladder ? Mathf.Clamp(ladder.transform.position.x - transform.position.x, -0.6f, 0.6f) : 0f;
                Vector3 lp = visualRoot.localPosition;
                lp.x = Mathf.Lerp(lp.x, baseLocal.x + wantX, 1f - Mathf.Exp(-14f * Time.deltaTime));
                visualRoot.localPosition = lp;
            }

            if (!animator) return;
            Vector2 motion = motor.Velocity;
            animator.SetFloat(Speed, Mathf.Abs(motion.x));
            animator.SetFloat(VerticalSpeed, motion.y);
            animator.SetBool(Grounded, motor.IsGrounded);
            animator.SetBool(Climbing, motor.IsClimbing);
            if (hasRunPlayback) animator.SetFloat(RunPlayback, RunPlaybackFor(Mathf.Abs(motion.x)));
            if (hasClimbPlayback) animator.SetFloat(ClimbPlayback, ClimbPlaybackFor(motion.y));
            if (hasClimbPhase && profile && profile.climbPhaseDriven && motor.Ladder && visualRoot)
                animator.SetFloat(ClimbPhaseId, profile.ClimbPhase(visualRoot.position.y, motor.Ladder.Bottom, Mathf.Abs(visualRoot.lossyScale.y)));
            if (health)
            {
                bool dead = health.IsDead;
                animator.SetBool(Dead, dead);
                if (wasDead && !dead) StandUp();
                wasDead = dead;
            }
        }

        private bool wasDead;
        private static readonly int IdleState = Animator.StringToHash("Idle");

        /// <summary>Revived (HP back above 0): leave the Death pose at once and stand in Idle.
        /// The Death state has no exit of its own, so without this only an Attack trigger pulled the body up.</summary>
        private void StandUp()
        {
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Hit);
            animator.ResetTrigger(Skill);
            animator.Play(IdleState, 0, 0f);
            animator.Update(0f);
            if (face) face.Pulse(HeroFace.Expression.Happy, 0.6f);
            Revived++;
        }

        /// <summary>How many times the body stood back up after death (tests).</summary>
        public int Revived { get; private set; }
        public bool InDeathPose => animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Death");

        private void HandleAttack()
        {
            if (animator) animator.SetTrigger(Attack);
            if (face && face.Current != HeroFace.Expression.Hurt) face.Pulse(HeroFace.Expression.Angry, 0.4f);
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

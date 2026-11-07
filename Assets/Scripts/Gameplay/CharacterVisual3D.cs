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
        [Tooltip("Yaw of the mesh while the character faces right. Author models consistently.")]
        [SerializeField] private float rightFacingYaw = 0f;

        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Climbing = Animator.StringToHash("Climbing");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Dead = Animator.StringToHash("Dead");

        private void Awake()
        {
            if (!motor) motor = GetComponent<PlayerMotor2D>();
            if (!melee) melee = GetComponent<PlayerMelee2D>();
            if (!health) health = GetComponent<Health2D>();
            if (!animator && visualRoot) animator = visualRoot.GetComponentInChildren<Animator>();
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
                float yaw = rightFacingYaw + (motor.Facing < 0 ? 180f : 0f);
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

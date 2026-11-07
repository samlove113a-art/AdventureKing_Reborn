using UnityEngine;

namespace AdventureKing.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private PlayerInput2D controls;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Transform groundProbe;
        [SerializeField] private LayerMask groundMask = 1 << 8;
        [SerializeField, Min(0.02f)] private float probeRadius = 0.12f;
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;
        [SerializeField, Min(0.1f)] private float groundAcceleration = 70f;
        [SerializeField, Min(0.1f)] private float airAcceleration = 30f;
        [SerializeField, Min(0.1f)] private float jumpVelocity = 10f;
        [SerializeField, Range(0.05f, 1f)] private float jumpCutMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float coyoteSeconds = 0.1f;
        [SerializeField, Min(0f)] private float jumpBufferSeconds = 0.12f;
        [SerializeField, Min(0.1f)] private float climbSpeed = 3f;

        private LadderZone2D ladder;
        private float normalGravity;
        private float lastGroundAt = float.NegativeInfinity;
        private float lastJumpAt = float.NegativeInfinity;
        private bool climbing, jumpRequested, cutJump;
        private int facing = 1;

        public int Facing => facing;
        public bool IsClimbing => climbing;
        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => body ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!controls) controls = GetComponent<PlayerInput2D>();
            normalGravity = body.gravityScale;
            body.freezeRotation = true;
        }

        private void Update()
        {
            if (controls.ConsumeJump())
            {
                lastJumpAt = Time.time;
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = groundProbe &&
                Physics2D.OverlapCircle(groundProbe.position, probeRadius, groundMask) != null;
            if (IsGrounded) lastGroundAt = Time.time;

            if (ladder && !climbing && Mathf.Abs(controls.Vertical) > 0.15f)
                SetClimbing(true);

            if (climbing)
            {
                if (!ladder) SetClimbing(false);
                else if (jumpRequested)
                {
                    SetClimbing(false);
                    body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
                    jumpRequested = false;
                    lastJumpAt = float.NegativeInfinity;
                    cutJump = false;
                }
                else
                {
                    body.linearVelocity = new Vector2(controls.Horizontal * runSpeed * 0.7f,
                        controls.Vertical * climbSpeed);
                    UpdateFacing();
                    return;
                }
            }

            float x = Mathf.MoveTowards(body.linearVelocity.x,
                controls.Horizontal * runSpeed,
                (IsGrounded ? groundAcceleration : airAcceleration) * Time.fixedDeltaTime);

            float y = body.linearVelocity.y;
            if (jumpRequested && Time.time - lastJumpAt <= jumpBufferSeconds &&
                Time.time - lastGroundAt <= coyoteSeconds)
            {
                y = jumpVelocity;
                jumpRequested = false;
                lastJumpAt = lastGroundAt = float.NegativeInfinity;
                cutJump = false;
            }
            else if (jumpRequested && Time.time - lastJumpAt > jumpBufferSeconds)
            {
                jumpRequested = false;
            }

            if (!controls.JumpHeld && !cutJump && y > 0.01f)
            {
                y *= jumpCutMultiplier;
                cutJump = true;
            }
            if (IsGrounded && y <= 0.01f) cutJump = false;

            body.linearVelocity = new Vector2(x, y);
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            if (controls.Horizontal > 0.1f) facing = 1;
            else if (controls.Horizontal < -0.1f) facing = -1;
            if (visual) visual.flipX = facing < 0;
        }

        private void SetClimbing(bool value)
        {
            if (climbing == value) return;
            climbing = value;
            body.gravityScale = value ? 0f : normalGravity;
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        }

        public void SetLadder(LadderZone2D zone, bool inside)
        {
            if (inside) ladder = zone;
            else if (ladder == zone) { ladder = null; SetClimbing(false); }
        }

        private void OnDisable()
        {
            climbing = jumpRequested = false;
            ladder = null;
            if (body) body.gravityScale = normalGravity;
        }

        private void OnDrawGizmosSelected()
        {
            if (!groundProbe) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundProbe.position, probeRadius);
        }
    }
}

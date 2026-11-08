using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Drives the hero face layer of the Toon shader (_FACE): natural blinking and blended expressions on top of
    /// any body animation. It only writes three shader vectors through a MaterialPropertyBlock on the body
    /// renderer, so Idle/Run/Jump/Attack/Skill keep playing untouched underneath.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroFace : MonoBehaviour
    {
        public enum Expression { Neutral, Happy, Sad, Surprised, Angry, Hurt }

        [SerializeField] private Renderer body;
        [Tooltip("Head bone and the face's forward / up axes in its local space (from the bind pose, set by the art pipeline).")]
        [SerializeField] private Transform headBone;
        [SerializeField] private Vector3 faceForwardLocal = Vector3.forward;
        [SerializeField] private Vector3 faceUpLocal = Vector3.up;

        /// <summary>World direction the face looks at right now (follows the animated head).</summary>
        public Vector3 FaceForward => headBone ? headBone.TransformDirection(faceForwardLocal).normalized : transform.forward;
        public Vector3 FaceUp => headBone ? headBone.TransformDirection(faceUpLocal).normalized : transform.up;
        public Transform HeadBone => headBone;

        public void BindHead(Transform head, Vector3 forwardLocal, Vector3 upLocal)
        {
            headBone = head;
            faceForwardLocal = forwardLocal;
            faceUpLocal = upLocal;
        }
        [Tooltip("Mouth curve of this hero's own painted mouth (keeps the neutral face recognisable).")]
        [SerializeField] private float neutralSmile = 0.35f;
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.2f, 5.8f);
        [SerializeField, Range(0f, 1f)] private float doubleBlinkChance = 0.14f;

        private static readonly int EyesId = Shader.PropertyToID("_FaceEyes");
        private static readonly int MouthId = Shader.PropertyToID("_FaceMouth");
        private static readonly int FxId = Shader.PropertyToID("_FaceFx");

        private struct Pose
        {
            public float open, bend, tilt, squint, curve, mouthOpen, wide, blush, hurt, tear;
            public static Pose Lerp(Pose a, Pose b, float t) => new Pose
            {
                open = Mathf.Lerp(a.open, b.open, t), bend = Mathf.Lerp(a.bend, b.bend, t),
                tilt = Mathf.Lerp(a.tilt, b.tilt, t), squint = Mathf.Lerp(a.squint, b.squint, t),
                curve = Mathf.Lerp(a.curve, b.curve, t), mouthOpen = Mathf.Lerp(a.mouthOpen, b.mouthOpen, t),
                wide = Mathf.Lerp(a.wide, b.wide, t), blush = Mathf.Lerp(a.blush, b.blush, t),
                hurt = Mathf.Lerp(a.hurt, b.hurt, t), tear = Mathf.Lerp(a.tear, b.tear, t),
            };
        }

        private MaterialPropertyBlock block;
        private Expression held = Expression.Neutral;
        private Expression pulse;
        private float pulseUntil = -1f;
        private Pose current;
        private float nextBlink, blinkStart = -10f;
        private bool doubleQueued;
        private Health2D health;

        public Expression Current => Time.time < pulseUntil ? pulse : held;
        /// <summary>0 = open, 1 = shut (blink only), for tests and QA.</summary>
        public float BlinkAmount { get; private set; }
        public float EyeOpen => current.open;
        public float MouthCurve => current.curve;

        /// <summary>Holds an expression until changed (dialogs, lobby, emotes).</summary>
        public void SetExpression(Expression e) => held = e;

        /// <summary>Shows an expression for a moment (hit, attack shout), then returns to the held one.</summary>
        public void Pulse(Expression e, float seconds)
        {
            pulse = e;
            pulseUntil = Time.time + seconds;
        }

        /// <summary>Forces a blink now (QA / cutscenes).</summary>
        public void Blink() => blinkStart = Time.time;

        public void Bind(Renderer bodyRenderer) => body = bodyRenderer;

        private Pose Target(Expression e)
        {
            switch (e)
            {
                case Expression.Happy: return new Pose { open = 0f, bend = 1f, squint = 1f, curve = 1f, mouthOpen = 0.45f, wide = 0.8f, blush = 0.7f };
                case Expression.Sad: return new Pose { open = 0.72f, tilt = -0.65f, squint = 0.1f, curve = -0.7f, wide = 0.45f, tear = 0.9f };
                case Expression.Surprised: return new Pose { open = 1f, curve = 0f, mouthOpen = 0.8f, wide = 0.35f };
                case Expression.Angry: return new Pose { open = 0.82f, tilt = 0.75f, squint = 0.18f, curve = -0.45f, mouthOpen = 0.15f, wide = 0.55f };
                case Expression.Hurt: return new Pose { open = 0f, hurt = 1f, curve = -0.35f, mouthOpen = 0.3f, wide = 0.6f };
                default: return new Pose { open = 1f, curve = neutralSmile, wide = 0.5f };
            }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (!body)
                foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.sharedMaterial && smr.sharedMaterial.IsKeywordEnabled("_FACE")) { body = smr; break; }
            current = Target(Expression.Neutral);
            ScheduleBlink();
        }

        private void OnEnable()
        {
            health = GetComponentInParent<Health2D>();
            if (health) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(Health2D victim, int amount)
        {
            if (amount > 0) Pulse(Expression.Hurt, victim.IsDead ? 999f : 0.55f); // dead heroes keep the pained face
        }

        private void ScheduleBlink() => nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);

        private void Update()
        {
            if (!body) return;
            float now = Time.time;
            // ---- blink: fast close (70 ms), short hold, slower open (120 ms); sometimes a second blink
            if (now >= nextBlink)
            {
                blinkStart = now;
                if (doubleQueued) { doubleQueued = false; ScheduleBlink(); }
                else if (Random.value < doubleBlinkChance) { doubleQueued = true; nextBlink = now + 0.32f; }
                else ScheduleBlink();
            }
            float bt = now - blinkStart;
            BlinkAmount = bt < 0.07f ? bt / 0.07f : bt < 0.11f ? 1f : bt < 0.23f ? 1f - (bt - 0.11f) / 0.12f : 0f;
            BlinkAmount = Mathf.SmoothStep(0f, 1f, BlinkAmount);

            // ---- expression: ease towards the target (quick for pain, softer otherwise)
            Expression e = Current;
            Pose target = Target(e);
            float rate = e == Expression.Hurt ? 30f : 12f;
            current = Pose.Lerp(current, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

            float open = current.open * (1f - BlinkAmount);
            float bend = Mathf.Lerp(current.bend, -0.35f, BlinkAmount * current.open); // a plain blink closes as a soft ∪ line
            body.GetPropertyBlock(block);
            block.SetVector(EyesId, new Vector4(open, bend, current.tilt * (1f - BlinkAmount), current.squint));
            block.SetVector(MouthId, new Vector4(current.curve, current.mouthOpen, current.wide, current.blush));
            block.SetVector(FxId, new Vector4(current.hurt, current.tear, 0f, 0f));
            body.SetPropertyBlock(block);
        }
    }
}

using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Per-class combat presentation driven by clip Animation Events (never by button presses):
    /// cast effects on wind-up, weapon trails / projectiles on the contact or release frame, and the
    /// impact sound only when a melee arc connects or a projectile actually arrives.
    /// Presentation only: damage stays in gameplay code (and later on the authoritative server).
    /// </summary>
    public sealed class HeroCombatPresentation : MonoBehaviour
    {
        [SerializeField] private HeroClass heroClass;
        [SerializeField] private CharacterAnimationEvents events;
        [Tooltip("Facing reference (the visual root). Effects spawn along its forward axis.")]
        [SerializeField] private Transform facing;
        [SerializeField] private Transform weaponSocket;
        [Tooltip("Optional fixed skill release point. If absent, use a stable chest-height point from the character collider (not the animated head/pivot).")]
        [SerializeField] private Transform skillMuzzle;

        [Header("Basic attack")]
        [SerializeField] private GameObject attackEffect;
        [SerializeField] private VfxProjectile attackProjectile;
        [SerializeField] private Vector3 attackOffset = new Vector3(0f, 0.55f, 0.55f);

        [Header("Skill")]
        [SerializeField] private GameObject skillCastEffect;
        [SerializeField] private GameObject skillEffect;
        [SerializeField] private VfxProjectile skillProjectile;
        [SerializeField, Min(1)] private int skillProjectileCount = 1;
        [SerializeField, Range(0f, 60f)] private float skillSpreadDegrees = 0f;
        [SerializeField] private Vector3 skillOffset = new Vector3(0f, 0.55f, 0.7f);

        public HeroClass HeroClass => heroClass;
        /// <summary>Set by the selection hall so projectiles end on the stage instead of flying into the camera.</summary>
        public float ProjectileRangeScale { get; set; } = 1f;

        private void Awake()
        {
            if (!events) events = GetComponentInChildren<CharacterAnimationEvents>(true);
            if (!facing) facing = transform;
        }

        private void OnEnable()
        {
            if (!events) return;
            events.AttackImpact += OnAttack;
            events.SkillCast += OnSkillCast;
            events.SkillImpact += OnSkill;
        }

        private void OnDisable()
        {
            if (!events) return;
            events.AttackImpact -= OnAttack;
            events.SkillCast -= OnSkillCast;
            events.SkillImpact -= OnSkill;
        }

        private Vector3 Point(Vector3 offset)
        {
            Vector3 fwd = Flat(facing.forward);
            return facing.position + Vector3.up * offset.y * Scale + fwd * offset.z * Scale;
        }

        /// <summary>Stable projectile spawn. Animated visual pivots may sit above the head.</summary>
        private Vector3 SkillPoint()
        {
            if (skillMuzzle && skillMuzzle.gameObject.activeInHierarchy)
                return skillMuzzle.position;

            Collider2D body = GetComponentInParent<Collider2D>();
            if (body && body.enabled && body.bounds.size.y > 0.1f)
            {
                Bounds bounds = body.bounds;
                float heightFraction = Mathf.Clamp(skillOffset.y, 0.35f, 0.70f);
                float forward = Mathf.Clamp(skillOffset.z * Mathf.Min(Scale, 1.5f), 0.1f, 1.2f);
                return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * heightFraction, facing.position.z)
                    + Flat(facing.forward) * forward;
            }

            // Previews can have no physics collider.
            return Point(skillOffset);
        }

        private float Scale => Mathf.Abs(facing.lossyScale.y);

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.right;
        }

        private Quaternion FacingRotation() => Quaternion.LookRotation(Flat(facing.forward), Vector3.up);

        private void OnAttack()
        {
            Vector3 at = Point(attackOffset);
            if (attackEffect) Spawn(attackEffect, at, FacingRotation());
            if (attackProjectile)
                Launch(attackProjectile, at, FacingRotation(), false);
            else
                Impact(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!facing) return;
            Vector3 at = SkillPoint();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(at, 0.12f);
            Gizmos.DrawRay(at, Flat(facing.forward) * 0.6f);
        }
#endif

        private void OnSkillCast()
        {
            if (skillCastEffect) Spawn(skillCastEffect, facing.position + Vector3.up * 0.02f, FacingRotation());
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayClassSkillCast(heroClass);
        }

        private void OnSkill()
        {
            Vector3 at = SkillPoint();
            Quaternion rot = FacingRotation();
            if (skillEffect) Spawn(skillEffect, at, rot);
            if (skillProjectile)
            {
                for (int i = 0; i < skillProjectileCount; i++)
                {
                    float t = skillProjectileCount == 1 ? 0f : i / (skillProjectileCount - 1f) - 0.5f;
                    Quaternion r = rot * Quaternion.Euler(-t * skillSpreadDegrees, 0f, 0f);
                    Launch(skillProjectile, at, r, true);
                }
            }
            else
            {
                Impact(true);
            }
        }

        private void Launch(VfxProjectile prefab, Vector3 at, Quaternion rot, bool skill)
        {
            VfxProjectile p = Instantiate(prefab, at, rot);
            p.transform.localScale = prefab.transform.localScale * Scale;
            SetLayerRecursive(p.transform, gameObject.layer);
            p.RangeScale = ProjectileRangeScale;
            p.Arrived += () => Impact(skill);
        }

        private void Impact(bool skill)
        {
            AdventureAudioDirector audio = AdventureAudioDirector.Instance;
            if (!audio) return;
            if (skill) audio.PlayClassSkillImpact(heroClass);
            else audio.PlayClassAttackImpact(heroClass);
        }

        private GameObject Spawn(GameObject prefab, Vector3 at, Quaternion rot)
        {
            GameObject go = Instantiate(prefab, at, rot);
            go.transform.localScale = prefab.transform.localScale * Scale;
            // Effects must render on the same layer as the hero (e.g. the lobby preview layer).
            SetLayerRecursive(go.transform, gameObject.layer);
            return go;
        }

        internal static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}

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

        /// <summary>
        /// Gameplay direction of the attack. The visual root is turned ±110° so the face shows to the camera, so its
        /// forward has a large component TOWARD the camera (measured: (0.94, 0, -0.34)) — projectiles used to fly
        /// diagonally into the screen. In play the hero faces ±X: use the pure side direction. Only when the visual
        /// faces the camera (selection hall) the turned forward is kept.
        /// </summary>
        private Vector3 Dir()
        {
            Vector3 f = facing.forward;
            return Mathf.Abs(f.x) > 0.2f ? new Vector3(Mathf.Sign(f.x), 0f, 0f) : Flat(f);
        }

        private Vector3 Point(Vector3 offset)
        {
            Vector3 p = facing.position + Vector3.up * offset.y * Scale + Dir() * offset.z * Scale;
            return Clamp(p);
        }

        /// <summary>Emitter of a projectile: the weapon socket (staff head / bow grip / blade) when there is one,
        /// else the offset point; both constrained to the hero's real body.</summary>
        private Vector3 Muzzle(Vector3 offset)
        {
            if (!weaponSocket) return Point(offset);
            Vector3 tip = heroClass == HeroClass.Archer ? weaponSocket.position : SkillVfxDirector.WeaponTipOf(weaponSocket);
            return Clamp(tip);
        }

        private Collider2D body;
        private bool bodyLooked;

        /// <summary>Keeps an emitter inside the hero's body height (not above the head, not in the ground), in front of
        /// the body and on the gameplay plane — using the Collider2D, never the mesh pivot.</summary>
        private Vector3 Clamp(Vector3 p)
        {
            if (!bodyLooked) { body = GetComponentInParent<Collider2D>(); bodyLooked = true; }
            Vector3 d = Dir();
            if (!body || Mathf.Abs(d.x) < 0.5f) return p; // selection hall: no gameplay body
            Bounds b = body.bounds;
            float h = Mathf.Max(0.5f, b.size.y);
            p.y = Mathf.Clamp(p.y, b.min.y + 0.3f * h, b.max.y - 0.18f * h);
            if ((p.x - b.center.x) * d.x < 0.15f * h) p.x = b.center.x + d.x * 0.15f * h;
            p.z = facing.position.z;
            return p;
        }

        private float Scale => Mathf.Abs(facing.lossyScale.y);

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.right;
        }

        private Quaternion FacingRotation() => Quaternion.LookRotation(Dir(), Vector3.up);

        /// <summary>A class skill choreography owns this hero: the clip events it borrows must not add the old effects.</summary>
        private bool SkillShowing => SkillVfxDirector.IsCasting(transform) || (transform.parent && SkillVfxDirector.IsCasting(transform.parent)) || SkillVfxDirector.IsCasting(transform.root);

        private void OnAttack()
        {
            if (SkillShowing) return;
            Vector3 at = Point(attackOffset);
            if (attackEffect) Spawn(attackEffect, at, FacingRotation());
            if (attackProjectile)
                Launch(attackProjectile, Muzzle(attackOffset), FacingRotation(), false);
            else
                Impact(false);
        }

        private void OnSkillCast()
        {
            if (SkillShowing) return;
            if (skillCastEffect) Spawn(skillCastEffect, facing.position + Vector3.up * 0.02f, FacingRotation());
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayClassSkillCast(heroClass);
        }

        private void OnSkill()
        {
            if (SkillShowing) return;
            Vector3 at = Point(skillOffset);
            Quaternion rot = FacingRotation();
            if (skillEffect) Spawn(skillEffect, at, rot);
            if (skillProjectile)
            {
                for (int i = 0; i < skillProjectileCount; i++)
                {
                    float t = skillProjectileCount == 1 ? 0f : i / (skillProjectileCount - 1f) - 0.5f;
                    Quaternion r = rot * Quaternion.Euler(-t * skillSpreadDegrees, 0f, 0f);
                    Launch(skillProjectile, Muzzle(skillOffset), r, true);
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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>One skill cast to present (the server already accepted it).</summary>
    public sealed class SkillCast
    {
        public HeroClass Class;
        public int Slot;                 // 1..3
        public Transform Hero;           // gameplay root (z = 0 plane)
        public CharacterVisual3D Visual;
        public int Facing = 1;
        public Vector3 Target;           // ground point the skill aims at (main target or landing centre)
        public float Radius = 2f, Range = 3f;
        public List<FarmMonsterView> Victims = new List<FarmMonsterView>();
        /// <summary>Another player's skill replayed from the sync: same effects and sounds, but no camera shake,
        /// hit-stop, zoom or framing for the viewer, and no numbers (the caster's client shows those).</summary>
        public bool Remote;
        /// <summary>Raised once, on the beat where the damage lands (popups, deaths, drops are shown then).</summary>
        public Action Landed;
        internal bool landedFired;
        internal void Land() { if (landedFired) return; landedFired = true; Landed?.Invoke(); }
    }

    /// <summary>
    /// The 12 class skills, each a layered choreography: charge (energy gathers) → release (the move) → impact (hit
    /// marks, monster reaction, camera feedback) → dissipate. Distinct colour, silhouette, rhythm and hit effect per
    /// skill; skill 3 is the biggest of each class but keeps the target readable. Presentation only: the server has
    /// already decided damage, MP and cooldown. The hero animation per skill is chosen here to match the effect timing.
    /// </summary>
    public sealed class SkillVfxDirector : MonoBehaviour
    {
        private static SkillVfxDirector host;
        private static readonly Dictionary<Transform, float> casting = new Dictionary<Transform, float>();
        public static int Played { get; private set; }
        public static int PlayedRemote { get; private set; }
        public static string LastSkill { get; private set; } = "";
        public static string LastRemoteSkill { get; private set; } = "";

        // camera / time feedback belongs to the caster's own screen only
        private static void Shake(SkillCast c, float amp, float s) { if (!c.Remote) SkillVfxKit.Shake(amp, s); }
        private static void Zoom(SkillCast c, float amount, float s) { if (!c.Remote) SkillVfxKit.ZoomPunch(amount, s); }
        private static void Stop(SkillCast c, float s) { if (!c.Remote) SkillVfxKit.HitStop(s); }

        /// <summary>True while a skill choreography owns this hero (the old per-clip class effects stay quiet).</summary>
        public static bool IsCasting(Transform hero) => hero && casting.TryGetValue(hero, out float until) && Time.time < until;

        public static void Play(SkillCast c)
        {
            if (c == null || !c.Hero) return;
            if (!host) { host = new GameObject("SkillVfxDirector").AddComponent<SkillVfxDirector>(); DontDestroyOnLoad(host.gameObject); }
            casting[c.Hero] = Time.time + 2.6f;
            // frame the move: the camera leans toward the midpoint of hero and target (clear of the HUD clusters)
            if (!c.Remote && Mathf.Abs(c.Target.x - Feet(c).x) > 0.5f) CameraFollow2D.Focus((Feet(c).x + c.Target.x) * 0.5f, 2.4f);
            if (c.Remote) { PlayedRemote++; LastRemoteSkill = c.Class + "_" + c.Slot; }
            else { Played++; LastSkill = c.Class + "_" + c.Slot; }
            host.StartCoroutine(host.Run(c));
        }

        private IEnumerator Run(SkillCast c)
        {
            IEnumerator routine = (c.Class, c.Slot) switch
            {
                (HeroClass.Swordsman, 1) => WindSlash(c), (HeroClass.Swordsman, 2) => BladeWhirl(c), (HeroClass.Swordsman, 3) => HeavenBlade(c),
                (HeroClass.Mage, 1) => Fireball(c), (HeroClass.Mage, 2) => IceStorm(c), (HeroClass.Mage, 3) => Meteor(c),
                (HeroClass.Assassin, 1) => DaggerFan(c), (HeroClass.Assassin, 2) => ShadowClones(c), (HeroClass.Assassin, 3) => ShadowCombo(c),
                (HeroClass.Archer, 1) => PiercingArrow(c), (HeroClass.Archer, 2) => ArrowRain(c), _ => PhoenixArrow(c),
            };
            yield return routine;
            c.Land(); // safety: the damage is always shown
        }

        // =================================================================== shared pieces
        private const float Front = -0.35f; // in front of the walking plane (z = 0)

        private static Vector3 P(Vector3 v, float dz = Front) => new Vector3(v.x, v.y, dz);
        /// <summary>Ground under the hero = bottom of its Collider2D. The gameplay root sits at the body CENTRE (measured
        /// y 0.80 with feet at 0.00), so "root + 1.05 m" used to be above the head (1.61) — the cause of skills
        /// appearing over the hero's head and of ground seals floating 0.8 m up.</summary>
        private static Vector3 Feet(SkillCast c) { var (bottom, _, cx) = Body(c); return new Vector3(cx, bottom, 0f); }

        private static float BodyHeight(SkillCast c) { var (bottom, top, _) = Body(c); return Mathf.Max(0.5f, top - bottom); }

        /// <summary>Hero body (bottom, top, centre x): its Collider2D; without one (a remote player's avatar) the shared
        /// convention — root at the body centre, feet 0.8 m below (RemoteAvatar mount), 1.6 m tall.</summary>
        public static (float bottom, float top, float cx) Body(SkillCast c)
        {
            var col = c.Hero.GetComponent<Collider2D>();
            if (col) return (col.bounds.min.y, col.bounds.max.y, col.bounds.center.x);
            float feet = c.Hero.position.y - BodyCentreAboveFeet;
            return (feet, feet + 1.61f, c.Hero.position.x);
        }
        public const float BodyCentreAboveFeet = 0.8f;

        /// <summary>Chest height (55 % of the body), in front of the walking plane.</summary>
        private static Vector3 Chest(SkillCast c) => P(Feet(c) + Vector3.up * 0.55f * BodyHeight(c));

        /// <summary>Ground point under the target as the player SEES it (the monster's view position).</summary>
        private static Vector3 TargetGround(SkillCast c) => P(c.Target, 0f);

        /// <summary>Body centre of the target as the player sees it.</summary>
        private static Vector3 TargetCenter(SkillCast c) => P(c.Target + Vector3.up * 0.62f, -0.45f);
        private static Vector3 Center(FarmMonsterView m) => P((Vector3)m.Position + Vector3.up * 0.65f, -0.45f);

        /// <summary>The weapon hand (right, or left for the bow) as the skills use it (QA probe).</summary>
        public static Transform HandOf(SkillCast c, bool left) => Socket(c, left ? "Socket_L" : "Socket_R");

        private static Transform Socket(SkillCast c, string name)
        {
            // the hero rigs have no "Socket_R/L" empties at runtime: the weapon hand IS the bone (HeroArtPipeline
            // weaponSocket = FindDeep(model, "RightHand"/"LeftHand")) — measured: the old lookup found nothing and
            // every skill silently used the fallback point
            Transform root = c.Visual ? c.Visual.transform : c.Hero;
            string bone = name == "Socket_L" ? "LeftHand" : "RightHand";
            Transform byBone = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name == name) return t;
                if (!byBone && t.name.EndsWith(bone)) byBone = t;
            }
            return byBone;
        }

        /// <summary>The far end of the weapon held on <paramref name="socket"/> (staff head, blade tip, arrow rest):
        /// exported weapons point along the socket's +Y (Tools/blender/export_character.py), so the tip is the weapon
        /// mesh's farthest extent along socket.up. No weapon → the socket itself.</summary>
        public static Vector3 WeaponTipOf(Transform hand)
        {
            if (!hand) return Vector3.zero;
            // the hero's weapons are skinned meshes bound to the hand bone (HeroArtPipeline), not children of it;
            // props put in a hand (HoldInHand) are children — take both
            var weapons = new List<Renderer>();
            foreach (Renderer r in hand.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer || r is TrailRenderer)) weapons.Add(r);
            // HeroArtPipeline names them "Weapon_<item>"; the one in THIS hand is the nearest to it
            SkinnedMeshRenderer held = null;
            float nearest = 0.35f * 0.35f;
            foreach (SkinnedMeshRenderer smr in hand.root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.name.StartsWith("Weapon_")) continue;
                float d = smr.bounds.SqrDistance(hand.position);
                if (d <= nearest) { nearest = d; held = smr; }
            }
            if (held) weapons.Add(held);
            Vector3 best = hand.position;
            float far = 0f;
            foreach (Renderer r in weapons)
            {
                Bounds b = r.bounds;
                Vector3 dir = b.center - hand.position;
                if (dir.sqrMagnitude < 1e-4f) dir = hand.up;
                dir.Normalize();
                float ext = 0f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    ext = Mathf.Max(ext, Vector3.Dot(corner - hand.position, dir));
                }
                if (ext > far) { far = ext; best = hand.position + dir * ext; }
            }
            return best;
        }

        /// <summary>
        /// Where a skill leaves the hero: the weapon socket (or the weapon's far end when <paramref name="tip"/>: staff
        /// head), constrained by the hero's real body (Collider2D): never above the head or below the knees, never
        /// behind the body, always on the gameplay plane in front of the hero. A missing socket falls back to the
        /// collider (chest height, a little in front), never to the mesh pivot.
        /// </summary>
        public static Vector3 Muzzle(SkillCast c, bool left = false, bool tip = false)
        {
            var (bottom, top, cx) = Body(c);
            float h = Mathf.Max(0.5f, top - bottom);
            Transform s = Socket(c, left ? "Socket_L" : "Socket_R");
            Vector3 p = s ? (tip ? WeaponTipOf(s) : s.position) : new Vector3(cx + c.Facing * 0.3f * h, bottom + 0.58f * h, 0f);
            p.y = Mathf.Clamp(p.y, bottom + 0.3f * h, top - 0.18f * h);
            if ((p.x - cx) * c.Facing < 0.15f * h) p.x = cx + c.Facing * 0.15f * h;
            return P(p);
        }

        private static Vector3 Hand(SkillCast c, bool left = false) => Muzzle(c, left);

        private static void Anim(SkillCast c, string state)
        {
            if (!c.Visual || !c.Visual.Animator) return;
            int h = Animator.StringToHash(state);
            if (c.Visual.Animator.HasState(0, h)) c.Visual.Animator.CrossFadeInFixedTime(h, 0.08f, 0);
            else c.Visual.PlaySkill();
        }

        private static void Sfx(string key, float gain = 1f)
        {
            var clip = Resources.Load<AudioClip>("SkillVfx/Audio/" + key);
            var a = AdventureAudioDirector.Instance;
            if (clip && a) a.PlayEffect(clip, gain);
        }

        private static float Rnd(float a, float b) => UnityEngine.Random.Range(a, b);

        /// <summary>Energy gathering: motes spiral into the point while a core swells and a small rune ring turns.</summary>
        private static void Charge(Vector3 at, Color core, Color edge, float seconds, float radius, int motes = 14, Transform follow = null)
        {
            int n = Mathf.RoundToInt(motes * SkillVfxKit.Density);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Rnd(0f, 0.4f), r = radius * Rnd(0.7f, 1.2f);
                Vector3 from = at + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                float life = seconds * Rnd(0.6f, 1f);
                SkillVfxKit.Layer("spark", from, Rnd(0.18f, 0.32f), core, edge, life).Billboard().Move((at - from) / life).Scale(1f, 0.3f, 1f).Fade(0.3f, 0.8f).Delay(Rnd(0f, seconds - life));
            }
            var glow = SkillVfxKit.Layer("glow", at, radius * 0.9f, core, edge, seconds + 0.12f).Billboard().Scale(0.2f, 1f, 1.6f).Fade(0.2f, 0.9f);
            if (follow) glow.Follow(follow, Vector3.zero + new Vector3(0, 0, Front - follow.position.z));
            SkillVfxKit.Layer("ring", at, radius * 1.4f, core, edge, seconds).Billboard().Scale(1.2f, 0.35f, 1f).Spin(220f).Fade(0.25f, 0.85f, 0.7f);
        }

        /// <summary>Impact light keeps the skill's hue: the core leans toward the edge colour (a near-white core under
        /// bloom burned into a white disc that hid the shape and the target).</summary>
        private static Color Hue(Color core, Color edge, float keep = 0.35f) => Color.Lerp(edge * 2.2f, core, keep);

        private static void Flash(Vector3 at, float size, Color core, Color edge, float life = 0.18f)
            => SkillVfxKit.Layer("glow", at, Mathf.Min(2.2f, size * 0.42f), Hue(core, edge), edge, life).Billboard().Scale(0.6f, 1.1f, 0.4f).Fade(0f, 0.25f, 0.6f);

        private static void Shock(Vector3 at, float radius, Color core, Color edge, float life = 0.4f, bool ground = true)
        {
            // thin bright ring that expands and thins out (not a filled disc)
            SkillVfxKit.Layer("ring", at, radius * 2f, Hue(core, edge, 0.5f), edge, life).Billboard().Scale(0.15f, 1f, 0.45f).Fade(0f, 0.25f, 0.85f).Sharp(4f).Noise(0.55f, new Vector2(3, 3), new Vector2(0.5f, 0.2f));
            // ground ring under the impact (impact points are body centres ≈ 0.62 m above the ground; it used to be
            // drawn at the impact height and float)
            if (ground) SkillVfxKit.Layer("ring", new Vector3(at.x, at.y - 0.62f, 0f), radius * 2.4f, Hue(core, edge, 0.5f), edge, life * 1.2f).Ground().Scale(0.1f, 1f, 0.4f).Fade(0f, 0.35f, 0.5f).Sharp(4f);
        }

        private static void Sparks(Vector3 at, int count, Color core, Color edge, Vector3 dir, float spread, float speed, float gravity = 1.2f, float size = 0.18f)
        {
            var s = SkillVfxKit.Spec("spark", count, core, edge);
            s.Direction = dir; s.Spread = spread; s.SpeedMin = speed * 0.5f; s.SpeedMax = speed; s.Stretch = true;
            s.SizeMin = size * 0.6f; s.SizeMax = size; s.Gravity = gravity; s.LifeMin = 0.25f; s.LifeMax = 0.55f; s.Drag = 2f;
            SkillVfxKit.Burst(at, s);
        }

        private static void Smoke(Vector3 at, int count, Color core, Color edge, float size = 0.9f, float rise = 0.6f, float life = 1f)
        {
            var s = SkillVfxKit.Spec("smoke", count, core, edge);
            s.Additive = false; s.Spread = 70f; s.Direction = Vector3.up; s.SpeedMin = rise * 0.4f; s.SpeedMax = rise; s.SizeMin = size * 0.6f; s.SizeMax = size;
            s.LifeMin = life * 0.6f; s.LifeMax = life; s.Radius = size * 0.4f; s.Drag = 1.5f; s.SpinMax = 1.5f;
            SkillVfxKit.Burst(at, s);
        }

        private static void Embers(Vector3 at, int count, Color core, Color edge, float radius = 0.8f, float life = 1.4f)
        {
            var s = SkillVfxKit.Spec("star", count, core, edge);
            s.Radius = radius; s.Direction = Vector3.up; s.Spread = 40f; s.SpeedMin = 0.4f; s.SpeedMax = 1.4f; s.Gravity = -0.15f;
            s.SizeMin = 0.08f; s.SizeMax = 0.2f; s.LifeMin = life * 0.5f; s.LifeMax = life;
            SkillVfxKit.Burst(at, s);
        }

        private static void React(SkillCast c, float strength, Color tint, Vector3? from = null)
        {
            foreach (FarmMonsterView m in c.Victims)
                if (m) m.React(tint, strength, m.Position.x - (from ?? Feet(c)).x);
        }

        private static void ReactNear(SkillCast c, Vector3 at, float radius, float strength, Color tint)
        {
            foreach (FarmMonsterView m in c.Victims)
                if (m && Mathf.Abs(m.Position.x - at.x) <= radius) m.React(tint, strength, m.Position.x - at.x);
        }

        private static FxLayer SlashArc(Vector3 at, float size, float angle, int facing, Color core, Color edge, float life = 0.26f)
        {
            return SkillVfxKit.Layer("slash", at, size, core, edge, life)
                .Rotate(Quaternion.Euler(0f, 0f, angle)).Stretch(facing, 1f).Scale(0.75f, 1.12f, 0.35f).Fade(0f, 0.45f)
                .Dissolve(0f, 0.9f, 0.45f).Noise(0.45f, new Vector2(2f, 1f), new Vector2(-3f * facing, 0f)).NoiseTex("noise_streak").Sharp(3f);
        }

        /// <summary>Explosion fire: long narrow tongues shooting outward along their own direction (upper half), stretching
        /// as they fly and burning out through the noise (round flame quads read as flat blobs).</summary>
        private static void FlameTongues(Vector3 at, int count, float length, float speed, Color core, Color edge, Color rim)
        {
            int n = Mathf.Max(4, Mathf.RoundToInt(count * (SkillVfxKit.Mobile ? 0.6f : 1f)));
            for (int i = 0; i < n; i++)
            {
                float a = -85f + i * (170f / (n - 1)) + Rnd(-7f, 7f);
                Vector3 d = Quaternion.Euler(0, 0, a) * Vector3.up;
                float len = length * Rnd(0.7f, 1.15f);
                SkillVfxKit.Layer("flame", at + d * 0.25f, len, Hue(core, edge, 0.55f), edge, Rnd(0.32f, 0.45f))
                    .Rotate(Quaternion.Euler(0, 0, a)).Stretch(0.38f, 1f).Move(d * speed * Rnd(0.8f, 1.2f)).Scale(0.35f, 1.25f, 0.5f)
                    .Noise(0.9f, new Vector2(1f, 1.5f), new Vector2(0, -4f)).Fade(0f, 0.3f).Dissolve(0f, 1f, 0.3f).Rim(rim);
            }
        }

        /// <summary>Sharp X cut on a victim (two crossing slim slashes).</summary>
        private static void XCut(Vector3 at, float size, Color core, Color edge)
        {
            foreach (float a in new[] { 40f, -40f })
                SkillVfxKit.Layer("spark", at, size, core, edge, 0.22f).Rotate(Quaternion.Euler(0f, 0f, a)).Stretch(1.6f, 0.22f).Scale(0.3f, 1.1f, 0.3f).Fade(0f, 0.4f);
            Flash(at, size * 0.8f, core, edge, 0.14f);
        }

        private static IEnumerator Wait(float s) { float end = Time.time + s; while (Time.time < end) yield return null; }

        /// <summary>Waits for the clip's own contact frame (Animation Event OnAttackImpact / OnSkillImpact) so the
        /// projectile leaves when the arm is really extended; <paramref name="fallback"/> seconds if the clip has none.
        /// (A fixed 0.45 s wait released the fireball while the hand was still drawn back behind the body.)</summary>
        private static IEnumerator WaitForContact(SkillCast c, bool skillEvent, float fallback)
        {
            var events = c.Visual ? c.Visual.GetComponentInChildren<CharacterAnimationEvents>() : null;
            if (!events) { yield return Wait(fallback); yield break; }
            bool hit = false;
            System.Action on = () => hit = true;
            if (skillEvent) events.SkillImpact += on; else events.AttackImpact += on;
            float end = Time.time + fallback + 0.35f;
            while (!hit && Time.time < end) yield return null;
            if (skillEvent) events.SkillImpact -= on; else events.AttackImpact -= on;
        }

        // =================================================================== Kiếm Sĩ — gió & thép (cyan / white / gold)
        private static readonly Color SwCore = new Color(2.2f, 2.6f, 2.5f), SwEdge = new Color(0.1f, 0.95f, 0.8f, 1f);

        /// <summary>1 · Chém Gió: a crescent wind blade leaves the sword and flies 2.6 m; X cuts and a gust on the target.</summary>
        private IEnumerator WindSlash(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Attack");
            Sfx("sw1_cast");
            Charge(Hand(c), SwCore, SwEdge, 0.24f, 0.6f, 10, Socket(c, "Socket_R"));
            yield return Wait(0.3f);
            Vector3 o = Chest(c) + Vector3.right * f * 0.7f;
            SlashArc(o, 2.6f, -25f * f, f, SwCore, SwEdge, 0.24f);
            SlashArc(o + Vector3.up * 0.1f, 2.0f, -10f * f, f, Color.white * 2.5f, SwEdge, 0.2f).Delay(0.03f);
            // the wind blade travels
            float range = Mathf.Max(1.6f, Mathf.Abs(c.Target.x - o.x) + 0.6f), speed = 10f, travel = range / speed;
            var blade = SlashArc(o, 1.9f, -80f * f, f, SwCore, SwEdge, travel + 0.12f).Move(Vector3.right * f * speed);
            blade.Dissolve(0f, 0.8f, 0.85f);
            SkillVfxKit.Trail(blade.gameObject, "spark", 0.55f, 0.16f, SwCore, SwEdge);
            for (int i = 0; i < 5; i++)
                SkillVfxKit.Layer("spark", o + new Vector3(0f, Rnd(-0.5f, 0.5f), 0f), Rnd(0.6f, 1.1f), SwCore * 0.7f, SwEdge, travel + 0.1f)
                    .Stretch(2.2f, 0.12f).Move(Vector3.right * f * speed * Rnd(0.85f, 1.1f)).Fade(0.1f, 0.6f).Delay(i * 0.02f);
            yield return Wait(travel);
            foreach (FarmMonsterView m in c.Victims) if (m) { XCut(Center(m), 1.6f, SwCore, SwEdge); Sparks(Center(m), 14, SwCore, SwEdge, Vector3.right * f, 50f, 7f); }
            React(c, 0.7f, SwEdge);
            Sfx("sw1_hit");
            Shake(c, 0.07f, 0.16f);
            Stop(c, 0.035f);
            c.Land();
            Vector3 end = P(new Vector3(o.x + f * range, o.y - 0.3f));
            Smoke(end, 5, new Color(1.1f, 1.2f, 1.2f), new Color(0.7f, 0.95f, 0.9f, 0.45f), 0.8f, 0.8f, 0.8f);
            Embers(end, 8, SwCore, SwEdge, 0.6f, 1f);
        }

        /// <summary>2 · Kiếm Khí Xoáy: a wind tornado wraps the hero; three spinning blade rings; the last one bursts out.</summary>
        private IEnumerator BladeWhirl(SkillCast c)
        {
            int f = c.Facing;
            float r = Mathf.Max(1.6f, c.Radius);
            Vector3 feet = P(Feet(c) + Vector3.up * 0.05f, 0f);
            Anim(c, "Skill");
            Sfx("sw2_cast");
            SkillVfxKit.Layer("circle", feet, r * 2.1f, SwCore * 0.8f, SwEdge, 1.9f).Ground().Spin(-120f).Scale(0.6f, 1f, 0.3f).Fade(0.12f, 0.8f, 0.8f);
            Charge(Chest(c), SwCore, SwEdge, 0.3f, 1.1f, 12);
            yield return Wait(0.25f);
            // tornado: two counter-rotating noise bands + a bright inner one
            Color green = new Color(0.4f, 1.8f, 1.2f);
            SkillVfxKit.MeshLayer(SkillVfxKit.Band, "beam", feet, 1f, SwCore, SwEdge, 1.5f).Stretch(r * 1.6f, 2.2f, r * 0.6f).Spin(-760f, Vector3.up)
                .Noise(0.85f, new Vector2(3f, 1f), new Vector2(-2.5f, 0.6f)).NoiseTex("noise_streak").Scale(0.4f, 1f, 0.3f).Fade(0.1f, 0.75f, 0.85f);
            SkillVfxKit.MeshLayer(SkillVfxKit.Band, "beam", feet, 1f, green, SwEdge, 1.4f).Stretch(r * 1.15f, 1.7f, r * 0.45f).Spin(900f, Vector3.up)
                .Noise(0.9f, new Vector2(4f, 1f), new Vector2(3f, 0.4f)).NoiseTex("noise_streak").Scale(0.4f, 1f, 0.3f).Fade(0.1f, 0.7f, 0.7f).Delay(0.05f);
            float[] beats = { 0.19f, 0.45f, 0.44f };
            for (int b = 0; b < 3; b++)
            {
                yield return Wait(beats[b]);
                bool last = b == 2;
                float size = r * (last ? 2.4f : 1.9f);
                // a full-turn blade sweep (crescent spinning around the hero) + ground ring
                SkillVfxKit.Layer("slash", Chest(c) + Vector3.down * 0.25f, size, SwCore, SwEdge, 0.3f).Spin(f * -1100f).Scale(0.8f, 1.1f, 0.4f).Fade(0f, 0.5f).Noise(0.35f, new Vector2(2, 1), new Vector2(-3, 0)).NoiseTex("noise_streak");
                SkillVfxKit.Layer("ring", feet + Vector3.up * 0.1f, size, SwCore, SwEdge, 0.32f).Ground().Scale(0.5f, 1.1f, 0.4f).Fade(0f, 0.4f);
                foreach (FarmMonsterView m in c.Victims) if (m) { Sparks(Center(m), last ? 16 : 8, SwCore, SwEdge, Vector3.up, 70f, last ? 7f : 5f); if (last) XCut(Center(m), 1.4f, SwCore, SwEdge); }
                React(c, last ? 1f : 0.45f, SwEdge);
                Sfx(last ? "sw2_hit" : "sw2_tick", last ? 1f : 0.7f);
                Shake(c, last ? 0.12f : 0.05f, last ? 0.25f : 0.12f);
                if (last) { Stop(c, 0.05f); Shock(Chest(c), r * 1.1f, SwCore, SwEdge, 0.45f); c.Land(); }
            }
            Sparks(feet + Vector3.up * 0.5f, 18, SwCore * 0.8f, SwEdge, Vector3.up, 30f, 6f, -0.4f, 0.22f);
            Smoke(feet + Vector3.up * 0.3f, 6, new Color(1.1f, 1.2f, 1.2f), new Color(0.7f, 0.95f, 0.9f, 0.4f), 1f, 1f, 0.9f);
        }

        private static readonly Color GoldCore = new Color(3.2f, 2.8f, 1.8f), GoldEdge = new Color(1f, 0.62f, 0.12f, 1f);

        /// <summary>3 · Thiên Kiếm: the hero raises the call, a rune seal opens over the target, a giant luminous sword
        /// falls from the sky and pins the ground — flash, double shockwave, light pillar, glowing cracks.</summary>
        private IEnumerator HeavenBlade(SkillCast c)
        {
            int f = c.Facing;
            Vector3 tg = TargetGround(c);
            float r = Mathf.Max(1.6f, c.Radius);
            Anim(c, "Cheer");
            Sfx("sw3_cast");
            // hero aura + gathering light in the raised hands
            SkillVfxKit.MeshLayer(SkillVfxKit.Band, "beam", P(Feet(c), 0f), 1f, GoldCore * 0.6f, GoldEdge, 1f).Stretch(1.3f, 2.4f, 0.5f).Spin(300f, Vector3.up).Noise(0.7f, new Vector2(3, 1), new Vector2(0, 1.2f)).NoiseTex("noise_streak").Fade(0.2f, 0.7f, 0.6f);
            Charge(Chest(c) + Vector3.up * 0.9f, GoldCore, GoldEdge, 0.75f, 1.2f, 18);
            // seal over the target + sky shaft
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.05f, r * 2.6f, Hue(GoldCore, GoldEdge, 0.5f), GoldEdge, 1.75f).Ground().Spin(90f).Scale(0.3f, 1f, 0.35f).Fade(0.12f, 0.85f).Sharp(1.2f);
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.06f, r * 1.7f, SwCore, SwEdge, 1.75f).Ground().Spin(-150f).Scale(0.2f, 1f, 0.5f).Fade(0.2f, 0.85f, 0.85f).Sharp(1.2f);
            SkillVfxKit.Layer("ring", tg + Vector3.up * 0.07f, r * 2.9f, Hue(GoldCore, GoldEdge, 0.4f), GoldEdge, 1.75f).Ground().Spin(40f).Scale(0.6f, 1f, 0.5f).Fade(0.2f, 0.85f, 0.9f);
            // the heavenly column: narrow, gold, clearly readable while the sword gathers
            SkillVfxKit.Layer("beam", P(tg + Vector3.up * 4.5f), 1f, Hue(GoldCore, GoldEdge, 0.45f), GoldEdge, 1.35f).Stretch(1.1f, 9.5f).Fade(0.3f, 0.85f, 0.9f).Noise(0.55f, new Vector2(1, 2), new Vector2(0, -1.8f));
            yield return Wait(0.4f);
            // the sword appears high above the target and descends slowly, gathering light (visible in the frame)
            Vector3 hover = P(tg + Vector3.up * 4.6f, -0.5f), low = P(tg + Vector3.up * 3.6f, -0.5f), hit = P(tg + Vector3.up * 0.9f, -0.5f);
            float descend = 0.48f, fall = 0.14f;
            var sword = SkillVfxKit.MeshLayer(SkillVfxKit.Blade, "beam", hover, 3.2f, GoldCore, new Color(1f, 0.66f, 0.2f, 1f), descend + fall + 0.55f)
                .Rotate(Quaternion.Euler(0f, 25f, 180f)).Move((low - hover) / descend).Fade(0.12f, 0.8f).Dissolve(0f, 1f, 0.72f).Rim(new Color(3.2f, 2f, 0.7f)).Noise(0.5f, new Vector2(2, 4), new Vector2(0, 2));
            var halo = SkillVfxKit.Layer("glow", hover, 2.2f, Hue(GoldCore, GoldEdge, 0.4f), GoldEdge, descend + fall).Billboard().Follow(sword.transform, new Vector3(0, -0.6f, 0)).Fade(0.2f, 0.9f, 0.55f);
            _ = halo;
            SkillVfxKit.Layer("star", hover, 2.4f, Hue(GoldCore, GoldEdge, 0.5f), GoldEdge, descend).Billboard().Spin(160f).Follow(sword.transform, new Vector3(0, 0.9f, -0.05f)).Fade(0.2f, 0.8f, 0.8f);
            Charge(hover + Vector3.down * 0.4f, GoldCore, GoldEdge, descend, 1.6f, 14);
            yield return Wait(descend - 0.12f);
            Anim(c, "Attack");
            yield return Wait(0.12f);
            sword.Move((hit - low) / fall);
            SkillVfxKit.Trail(sword.gameObject, "beam", 1.2f, 0.16f, GoldCore, GoldEdge);
            yield return Wait(fall);
            sword.Move(Vector3.zero);
            // IMPACT
            Vector3 c0 = P(tg + Vector3.up * 0.6f);
            Flash(c0, 5f, GoldCore, GoldEdge, 0.2f);
            Shock(c0, r * 1.5f, GoldCore, GoldEdge, 0.5f);
            Shock(c0, r * 2.1f, SwCore, SwEdge, 0.7f, false);
            SkillVfxKit.Layer("beam", P(tg + Vector3.up * 4f), 1f, Hue(GoldCore, GoldEdge, 0.5f), GoldEdge, 0.55f).Stretch(1.6f, 9f).Scale(1.3f, 0.25f, 1.5f).Fade(0f, 0.3f, 0.9f);
            SkillVfxKit.Layer("cracks", tg + Vector3.up * 0.04f, r * 2.6f, GoldCore, GoldEdge, 1.6f).Ground().Scale(0.6f, 1f, 0.2f).Fade(0f, 0.55f).Dissolve(0f, 1f, 0.5f);
            Sparks(c0, 40, GoldCore, GoldEdge, Vector3.up, 75f, 11f, 2f, 0.28f);
            Sparks(c0, 18, SwCore, SwEdge, Vector3.up, 160f, 7f, 1.5f, 0.2f);
            Smoke(P(tg + Vector3.up * 0.3f), 10, new Color(1.4f, 1.25f, 1f), new Color(0.75f, 0.62f, 0.45f, 0.55f), 1.4f, 1.2f, 1.1f);
            foreach (FarmMonsterView m in c.Victims) if (m) XCut(Center(m), 1.6f, GoldCore, GoldEdge);
            React(c, 1.5f, GoldEdge, tg);
            Sfx("sw3_hit");
            Shake(c, 0.3f, 0.5f);
            Zoom(c, 0.07f, 0.45f);
            Stop(c, 0.09f);
            c.Land();
            yield return Wait(0.3f);
            Embers(P(tg + Vector3.up * 0.5f), 26, GoldCore, GoldEdge, r, 1.8f);
        }

        // =================================================================== Pháp Sư — lửa / băng / thiên thạch
        private static readonly Color FireCore = new Color(3.2f, 2.4f, 1.1f), FireEdge = new Color(1f, 0.28f, 0.05f, 1f);
        private static readonly Color IceCore = new Color(2.2f, 2.8f, 3.2f), IceEdge = new Color(0.2f, 0.6f, 1f, 1f);
        private static readonly Color ArcCore = new Color(3f, 1.6f, 2.6f), ArcEdge = new Color(0.55f, 0.08f, 0.9f, 1f);

        private static FxLayer FireBall(Vector3 at, float size, float life)
        {
            var core = SkillVfxKit.Layer("glow", at, size * 1.1f, new Color(3.5f, 3f, 2f), FireEdge, life).Billboard().Fade(0.05f, 0.95f);
            SkillVfxKit.Layer("flame", at, size * 1.3f, FireCore, FireEdge, life).Billboard().Spin(-420f).Noise(0.8f, new Vector2(1, 1), new Vector2(0, -2.4f)).Fade(0.05f, 0.95f).Follow(core.transform, Vector3.zero);
            SkillVfxKit.Layer("flame", at, size * 1.0f, new Color(3f, 2.6f, 1.6f), FireEdge, life).Billboard().Spin(560f).Noise(0.8f, new Vector2(1.3f, 1.3f), new Vector2(0.4f, -3f)).Fade(0.05f, 0.95f).Follow(core.transform, Vector3.zero);
            return core;
        }

        /// <summary>1 · Cầu Lửa: flame swirls on the staff, a living fireball with an ember trail, a blast with smoke.</summary>
        private IEnumerator Fireball(SkillCast c)
        {
            Anim(c, "Attack");
            Sfx("mg1_cast");
            // the casting hand, constrained to the body (was: hand + 0.35 m world-up off a socket that was never found -> fallback above the head). The staff's far end is not used: in the cast pose it points down (measured y -0.50)
            Charge(Muzzle(c), FireCore, FireEdge, 0.42f, 0.7f, 12);
            yield return WaitForContact(c, false, 0.45f);
            Vector3 from = Muzzle(c), to = TargetCenter(c);
            float travel = Mathf.Clamp((to - from).magnitude / 13f, 0.12f, 0.55f);
            var ball = FireBall(from, 0.9f, travel + 0.05f).Move((to - from) / travel);
            SkillVfxProbe.Launched(c, from, ball.transform, (to - from) / travel);
            SkillVfxKit.Trail(ball.gameObject, "flame", 0.7f, 0.22f, FireCore, FireEdge, -3f);
            Flash(from, 1.2f, FireCore, FireEdge, 0.15f);
            // the fireball homes on the monster the player sees (its view hops / chases while the ball flies)
            FarmMonsterView aim = c.Victims.Count > 0 ? c.Victims[0] : null;
            float flown = 0f, ember = 0f;
            while (flown < travel && ball)
            {
                if (aim && aim.Alive) to = Center(aim);
                ball.Move((to - ball.transform.position) / Mathf.Max(0.02f, travel - flown));
                if ((ember -= Time.deltaTime) <= 0f)
                {
                    ember = 0.035f;
                    SkillVfxKit.Layer("star", ball.transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 0.2f, Rnd(0.08f, 0.16f), FireCore, FireEdge, 0.45f)
                        .Billboard().Move(new Vector3(Rnd(-0.4f, 0.4f), Rnd(0.2f, 0.8f), 0f), Vector3.down * 0.6f).Fade(0f, 0.4f);
                }
                flown += Time.deltaTime;
                yield return null;
            }
            // EXPLOSION
            Flash(to, 3f, FireCore, FireEdge, 0.18f);
            FlameTongues(to, 8, 1.4f, 2.8f, FireCore, FireEdge, new Color(3f, 1.2f, 0.3f));
            Shock(to, 1.2f, FireCore, FireEdge, 0.35f);
            Sparks(to, 20, FireCore, FireEdge, Vector3.up, 120f, 7f, 1.8f);
            Smoke(to + Vector3.up * 0.5f, 5, new Color(1f, 0.75f, 0.6f), new Color(0.45f, 0.3f, 0.28f, 0.4f), 0.9f, 1.2f, 1f);
            SkillVfxKit.Layer("cracks", TargetGround(c), 2f, new Color(1.6f, 0.6f, 0.1f), new Color(0.35f, 0.08f, 0.02f, 1f), 1.4f).Ground().Fade(0f, 0.5f).Dissolve(0f, 1f, 0.4f);
            // arrival: the loop ends one frame before the ball's last move — a homing projectile arrives ON the body
            if (aim && aim.Alive) to = Center(aim);
            if (ball) { ball.Move(Vector3.zero); ball.transform.position = to; }
            SkillVfxProbe.Impacted(ball ? ball.transform.position : to, aim ? Center(aim) : to);
            ReactNear(c, to, Mathf.Max(1.2f, c.Radius) + 0.4f, 0.8f, FireEdge);
            React(c, 0.2f, FireEdge, from);
            Sfx("mg1_hit");
            Shake(c, 0.1f, 0.2f);
            Stop(c, 0.04f);
            c.Land();
            Embers(to, 10, FireCore, FireEdge, 0.5f, 1.2f);
        }

        /// <summary>2 · Bão Băng: a frost seal on the ground, then crystal shards rain down and shatter; a frost nova to end.</summary>
        private IEnumerator IceStorm(SkillCast c)
        {
            Anim(c, "Cheer");
            Sfx("mg2_cast");
            Vector3 tg = TargetGround(c);
            float r = Mathf.Max(1.6f, c.Radius);
            Charge(Chest(c) + Vector3.up * 0.8f, IceCore, IceEdge, 0.45f, 1f, 12);
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.05f, r * 2.3f, IceCore, IceEdge, 1.7f).Ground().Spin(70f).Scale(0.3f, 1f, 0.3f).Fade(0.15f, 0.82f);
            var sky = SkillVfxKit.Spec("star", 26, IceCore, IceEdge);
            sky.Radius = r; sky.Direction = Vector3.down; sky.Spread = 25f; sky.SpeedMin = 1f; sky.SpeedMax = 2.4f; sky.LifeMin = 0.8f; sky.LifeMax = 1.4f; sky.SizeMin = 0.06f; sky.SizeMax = 0.14f;
            SkillVfxKit.Burst(P(tg + Vector3.up * 4.5f), sky);
            yield return Wait(0.45f);
            int n = SkillVfxKit.Mobile ? 7 : 11;
            for (int i = 0; i < n; i++)
            {
                float x = tg.x + Rnd(-r, r) * (i == n / 2 ? 0.1f : 1f);
                Vector3 land = new Vector3(x, tg.y + 0.15f, Rnd(-0.6f, -0.2f));
                Vector3 start = land + new Vector3(Rnd(-0.6f, 0.6f), Rnd(5f, 7f), 0f);
                float fall = 0.26f, size = Rnd(1f, 1.6f) * (i == n / 2 ? 1.5f : 1f);
                var shard = SkillVfxKit.MeshLayer(SkillVfxKit.Shard, "white", start, size, IceCore, IceEdge, fall + 0.55f)
                    .Rotate(Quaternion.LookRotation(Vector3.forward, land - start) * Quaternion.Euler(0, Rnd(0, 360), 0)).Move((land - start) / fall)
                    .Noise(0.4f, new Vector2(1, 3), new Vector2(0, 1)).Fade(0.05f, 0.75f).Dissolve(0f, 1f, 0.55f).Rim(new Color(2f, 3f, 4f));
                SkillVfxKit.Trail(shard.gameObject, "spark", 0.22f, 0.1f, IceCore, IceEdge);
                StartCoroutine(ShardLand(c, shard, land, fall, r));
                yield return Wait(0.07f);
            }
            yield return Wait(0.35f);
            Shock(P(tg + Vector3.up * 0.5f), r * 1.2f, IceCore, IceEdge, 0.55f);
            Smoke(P(tg + Vector3.up * 0.2f), 9, new Color(1.6f, 1.9f, 2.2f), new Color(0.6f, 0.8f, 1f, 0.5f), 1.3f, 0.5f, 1.4f);
            React(c, 0.9f, IceEdge, tg);
            Sfx("mg2_hit");
            Shake(c, 0.12f, 0.25f);
            c.Land();
        }

        private IEnumerator ShardLand(SkillCast c, FxLayer shard, Vector3 land, float fall, float r)
        {
            yield return Wait(fall);
            if (shard) shard.Move(Vector3.zero);
            Vector3 at = land + Vector3.up * 0.15f;
            Flash(at, 1f, IceCore, IceEdge, 0.12f);
            SkillVfxKit.Layer("star", at, 1.1f, IceCore, IceEdge, 0.25f).Billboard().Scale(0.3f, 1f, 0.4f).Fade(0f, 0.3f);
            Sparks(at, 8, IceCore, IceEdge, Vector3.up, 70f, 4.5f, 2.5f, 0.14f);
            SkillVfxKit.Layer("ring", new Vector3(land.x, land.y, 0f), 1.4f, IceCore, IceEdge, 0.4f).Ground().Scale(0.2f, 1f, 0.4f).Fade(0f, 0.4f);
            ReactNear(c, at, 1.1f, 0.35f, IceEdge);
            Sfx("mg2_tick", 0.5f);
            Shake(c, 0.035f, 0.08f);
        }

        /// <summary>3 · Thiên Thạch: a crimson-violet seal opens, a burning meteor tears down from the sky — blast, fire
        /// pillar, flying rocks, crater cracks, embers. The biggest Mage move, the target still visible through it.</summary>
        private IEnumerator Meteor(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Skill");
            Sfx("mg3_cast");
            Vector3 tg = TargetGround(c);
            float r = Mathf.Max(2f, c.Radius);
            Charge(Muzzle(c), ArcCore, ArcEdge, 1.2f, 1.1f, 20);
            SkillVfxKit.MeshLayer(SkillVfxKit.Band, "beam", P(Feet(c), 0f), 1f, ArcCore * 0.5f, ArcEdge, 1.3f).Stretch(1.4f, 2.2f, 0.5f).Spin(-260f, Vector3.up).Noise(0.8f, new Vector2(3, 1), new Vector2(0, 1.4f)).NoiseTex("noise_streak").Fade(0.25f, 0.75f, 0.35f);
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.05f, r * 2.5f, ArcCore, ArcEdge, 1.9f).Ground().Spin(80f).Scale(0.2f, 1f, 0.3f).Fade(0.15f, 0.85f);
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.06f, r * 1.5f, FireCore, FireEdge, 1.9f).Ground().Spin(-130f).Scale(0.2f, 1f, 0.5f).Fade(0.3f, 0.85f, 0.8f);
            yield return Wait(0.62f);
            // the meteor
            float fall = 0.62f;
            Vector3 hit = P(tg + Vector3.up * 0.7f, -0.5f), top = P(tg + new Vector3(-f * 6.5f, 10f), -0.5f);
            var rock = SkillVfxKit.MeshLayer(SkillVfxKit.Rock, "white", top, 1.5f, new Color(2.4f, 0.9f, 0.25f), new Color(0.16f, 0.07f, 0.06f, 1f), fall, false)
                .Spin(240f, new Vector3(0.3f, 1f, 0.2f)).Move((hit - top) / fall).Noise(0.95f, new Vector2(2, 2), new Vector2(0.2f, 0.4f)).Sharp(5f).Fade(0.05f, 1f);
            var halo = SkillVfxKit.Layer("glow", top, 3.2f, FireCore, FireEdge, fall).Billboard().Follow(rock.transform, Vector3.zero).Fade(0.1f, 1f, 0.9f);
            _ = halo;
            var tail = SkillVfxKit.Layer("flame", top, 3.4f, FireCore, ArcEdge, fall).Follow(rock.transform, (top - hit).normalized * 1.2f)
                .Rotate(Quaternion.LookRotation(Vector3.forward, top - hit)).Stretch(0.8f, 1.6f).Noise(0.85f, Vector2.one, new Vector2(0, -4f)).Fade(0.1f, 1f);
            _ = tail;
            SkillVfxKit.Trail(rock.gameObject, "flame", 1.6f, 0.35f, FireCore, ArcEdge, -4f);
            float t0 = Time.time;
            while (Time.time - t0 < fall && rock)
            {
                SkillVfxKit.Layer("smoke", rock.transform.position, Rnd(0.7f, 1.1f), new Color(1f, 0.7f, 0.6f), new Color(0.42f, 0.26f, 0.3f, 0.42f), 0.6f, false).Billboard().Spin(Rnd(-90, 90)).Scale(0.6f, 1.4f, 0.6f).Fade(0.1f, 0.3f, 0.7f);
                yield return Wait(0.04f);
            }
            // IMPACT
            Flash(hit, 6f, FireCore, FireEdge, 0.22f);
            Shock(hit, r * 1.5f, FireCore, FireEdge, 0.55f);
            Shock(hit, r * 2.2f, ArcCore, ArcEdge, 0.8f, false);
            SkillVfxKit.Layer("beam", P(tg + Vector3.up * 3.5f), 1f, Hue(FireCore, FireEdge, 0.5f), FireEdge, 0.6f).Stretch(2f, 8f).Scale(1.2f, 0.3f, 1.5f).Noise(0.8f, new Vector2(1, 2), new Vector2(0, -3f)).Fade(0f, 0.3f, 0.85f);
            FlameTongues(hit, 12, 2.4f, 4f, FireCore, FireEdge, new Color(2.4f, 0.9f, 1.6f));
            for (int i = 0; i < (SkillVfxKit.Mobile ? 4 : 8); i++)
                SkillVfxKit.MeshLayer(SkillVfxKit.Rock, "white", hit, Rnd(0.2f, 0.45f), new Color(1.6f, 0.6f, 0.2f), new Color(0.15f, 0.08f, 0.06f, 1f), 1.1f, false)
                    .Move(new Vector3(Rnd(-5f, 5f), Rnd(4f, 8f), Rnd(-1.2f, 0f)), Vector3.down * 16f).Spin(Rnd(200, 500), UnityEngine.Random.onUnitSphere).Noise(0.9f, Vector2.one * 2, Vector2.zero).Sharp(5f).Fade(0f, 0.8f);
            Sparks(hit, 46, FireCore, FireEdge, Vector3.up, 85f, 12f, 2.2f, 0.3f);
            Smoke(P(tg + Vector3.up * 0.4f), 10, new Color(1f, 0.72f, 0.62f), new Color(0.45f, 0.28f, 0.3f, 0.45f), 1.5f, 1.6f, 1.4f);
            SkillVfxKit.Layer("cracks", tg + Vector3.up * 0.04f, r * 2.4f, new Color(2.2f, 0.8f, 0.3f), new Color(0.3f, 0.05f, 0.1f, 1f), 2.2f).Ground().Scale(0.5f, 1f, 0.2f).Fade(0f, 0.6f).Dissolve(0f, 1f, 0.55f);
            React(c, 1.6f, FireEdge, tg);
            Sfx("mg3_hit");
            Shake(c, 0.36f, 0.6f);
            Zoom(c, 0.08f, 0.5f);
            Stop(c, 0.1f);
            c.Land();
            yield return Wait(0.35f);
            Embers(P(tg + Vector3.up * 0.4f), 30, FireCore, ArcEdge, r, 2f);
        }

        // =================================================================== Sát Thủ — bóng tối (violet / crimson)
        private static readonly Color ShCore = new Color(2.4f, 1.6f, 3.2f), ShEdge = new Color(0.45f, 0.1f, 1f, 1f);
        private static readonly Color CrCore = new Color(3.2f, 1.4f, 1.8f), CrEdge = new Color(0.9f, 0.05f, 0.25f, 1f);
        private static readonly Color Ink = new Color(0.22f, 0.12f, 0.32f), InkEdge = new Color(0.06f, 0.02f, 0.1f, 0.85f);

        /// <summary>Shadow afterimage: the hero's current pose baked into a translucent violet silhouette.</summary>
        private static void Afterimage(SkillCast c, Vector3 offset, Vector3 drift, float life, Color core, Color edge)
        {
            if (!c.Visual) return;
            foreach (SkinnedMeshRenderer smr in c.Visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var l = SkillVfxKit.MeshLayer(baked, "white", smr.transform.position + offset, 1f, core, edge, life, false)
                    .Rotate(smr.transform.rotation).Move(drift).Noise(0.7f, new Vector2(2, 4), new Vector2(0, 1.5f)).Fade(0.05f, 0.25f, 0.8f).Dissolve(0f, 0.9f, 0.3f).Rim(core);
                l.OwnMesh(baked);
            }
        }

        /// <summary>1 · Phi Dao: three spinning shadow daggers fan out; quick violet cuts on the target.</summary>
        private IEnumerator DaggerFan(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Attack");
            Sfx("as1_cast");
            Smoke(Hand(c), 3, Ink, InkEdge, 0.5f, 0.4f, 0.5f);
            yield return Wait(0.2f);
            Vector3 from = Hand(c);
            Vector3 to = TargetCenter(c);
            float dist = Mathf.Max(1f, Mathf.Abs(to.x - from.x)), speed = 17f, travel = dist / speed;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 dest = to + Vector3.up * (i * 0.32f);
                var d = SkillVfxKit.MeshLayer(SkillVfxKit.Dart, "white", from, 0.75f, ShCore, ShEdge, travel + 0.03f)
                    .Rotate(Quaternion.LookRotation((dest - from).normalized, Vector3.up)).Move((dest - from) / travel).Spin(1400f, Vector3.forward).Fade(0f, 0.95f).Delay(Mathf.Abs(i) * 0.03f);
                SkillVfxKit.Trail(d.gameObject, "spark", 0.16f, 0.12f, ShCore, ShEdge);
            }
            Flash(from, 0.8f, ShCore, ShEdge, 0.12f);
            yield return Wait(travel + 0.03f);
            foreach (FarmMonsterView m in c.Victims)
            {
                if (!m) continue;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = Center(m) + new Vector3(Rnd(-0.2f, 0.2f), (k - 1) * 0.3f, 0f);
                    XCut(at, 1f, ShCore, ShEdge);
                    Sparks(at, 6, ShCore, ShEdge, Vector3.right * f, 60f, 5f);
                    m.React(ShEdge, 0.35f, f);
                    Sfx("as1_hit", 0.7f);
                    yield return Wait(0.06f);
                }
                Smoke(Center(m), 4, Ink, InkEdge, 0.7f, 0.5f, 0.6f);
            }
            Shake(c, 0.06f, 0.14f);
            c.Land();
        }

        /// <summary>2 · Ảnh Phân Thân: shadow clones burst out to both sides slashing; violet crescents around the hero.</summary>
        private IEnumerator ShadowClones(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Skill");
            Sfx("as2_cast");
            Vector3 feet = P(Feet(c), 0f);
            Smoke(feet + Vector3.up * 0.4f, 8, Ink, InkEdge, 1.1f, 0.6f, 0.9f);
            SkillVfxKit.Layer("circle", feet + Vector3.up * 0.04f, c.Radius * 2.2f, ShCore, ShEdge, 1.5f).Ground().Spin(-160f).Scale(0.4f, 1f, 0.3f).Fade(0.1f, 0.8f, 0.7f);
            yield return Wait(0.12f);
            float[] beats = { 0.33f, 0.32f, 0.22f };
            for (int b = 0; b < 3; b++)
            {
                yield return Wait(beats[b]);
                bool last = b == 2;
                foreach (int side in new[] { -1, 1 })
                {
                    Afterimage(c, new Vector3(side * 0.8f, 0f, 0.2f), new Vector3(side * 3.2f, 0f, 0f), 0.42f, ShCore, ShEdge);
                    SlashArc(Chest(c) + Vector3.right * side * 1.2f, last ? 2.6f : 2f, side * (b * 35f - 20f), side, ShCore, ShEdge, 0.24f);
                }
                foreach (FarmMonsterView m in c.Victims) if (m) { XCut(Center(m), last ? 1.5f : 1.1f, last ? CrCore : ShCore, ShEdge); Sparks(Center(m), 8, ShCore, ShEdge, Vector3.up, 90f, 5f); }
                React(c, last ? 0.9f : 0.4f, ShEdge);
                Sfx(last ? "as2_hit" : "as2_tick", last ? 1f : 0.7f);
                Shake(c, last ? 0.1f : 0.045f, 0.14f);
                if (last) { Stop(c, 0.045f); c.Land(); }
            }
            Smoke(feet + Vector3.up * 0.5f, 8, Ink, InkEdge, 1f, 0.8f, 1f);
            Embers(feet + Vector3.up * 0.6f, 10, ShCore, ShEdge, 1f, 1f);
        }

        /// <summary>3 · Ám Ảnh Liên Trảm: the hero blurs into afterimages, a storm of seven criss-cross cuts closes on the
        /// target inside a shadow sphere, then a crimson X splits it open.</summary>
        private IEnumerator ShadowCombo(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Dodge");
            Sfx("as3_cast");
            Vector3 tg = TargetCenter(c);
            for (int i = 0; i < 4; i++)
            {
                Afterimage(c, new Vector3(-f * i * 0.3f, 0f, 0.15f), new Vector3(f * 1.5f, 0f, 0f), 0.5f, ShCore, ShEdge);
                yield return Wait(0.06f);
            }
            Anim(c, "Skill");
            SkillVfxKit.Layer("glow", tg, 2.2f, Ink * 1.2f, InkEdge, 1.3f, false).Billboard().Scale(0.4f, 1f, 0.5f).Fade(0.15f, 0.7f, 0.45f);
            SkillVfxKit.Layer("ring", tg, 3.4f, ShCore, ShEdge, 1.2f).Billboard().Spin(-300f).Scale(1.2f, 0.7f, 1f).Fade(0.15f, 0.8f, 0.6f);
            int cuts = 7;
            for (int i = 0; i < cuts; i++)
            {
                float a = i * 77f + Rnd(-12f, 12f);
                Vector3 at = tg + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.25f);
                SkillVfxKit.Layer("spark", at, 2.6f, i % 2 == 0 ? ShCore : CrCore, i % 2 == 0 ? ShEdge : CrEdge, 0.2f)
                    .Rotate(Quaternion.Euler(0, 0, a)).Stretch(1.5f, 0.16f).Scale(0.2f, 1.1f, 0.25f).Fade(0f, 0.35f);
                Sparks(at, 5, ShCore, ShEdge, Quaternion.Euler(0, 0, a) * Vector3.right, 30f, 6f, 0.5f, 0.14f);
                React(c, 0.3f, i % 2 == 0 ? ShEdge : CrEdge, tg + Vector3.right * (i % 2 == 0 ? -1 : 1));
                Sfx("as3_tick", 0.6f);
                Shake(c, 0.035f, 0.06f);
                yield return Wait(0.09f);
            }
            yield return Wait(0.12f);
            // the final crimson X
            foreach (float a in new[] { 42f, -42f })
                SkillVfxKit.Layer("slash", tg, 4f, CrCore, CrEdge, 0.4f).Rotate(Quaternion.Euler(0, 0, a + 90f)).Stretch(1f, 0.55f).Scale(0.5f, 1.15f, 0.3f).Fade(0f, 0.45f).Dissolve(0f, 0.9f, 0.45f).Rim(new Color(3f, 0.6f, 1.2f));
            Flash(tg, 3.2f, new Color(3.6f, 2f, 2.6f), CrEdge, 0.2f);
            Shock(tg, 1.8f, CrCore, CrEdge, 0.45f, false);
            Smoke(tg, 12, Ink, InkEdge, 1.3f, 1f, 1.2f);
            Sparks(tg, 30, CrCore, CrEdge, Vector3.up, 160f, 9f, 1.2f, 0.22f);
            React(c, 1.3f, CrEdge);
            Sfx("as3_hit");
            Shake(c, 0.25f, 0.4f);
            Zoom(c, 0.06f, 0.35f);
            Stop(c, 0.09f);
            c.Land();
            yield return Wait(0.2f);
            Embers(tg, 14, ShCore, CrEdge, 0.8f, 1.2f);
        }

        // =================================================================== Cung Thủ — gió rừng & phượng hoàng
        private static readonly Color LeafCore = new Color(2.2f, 3f, 1.6f), LeafEdge = new Color(0.25f, 0.85f, 0.2f, 1f);
        private static readonly Color PhCore = new Color(3.4f, 2.6f, 1.2f), PhEdge = new Color(1f, 0.38f, 0.04f, 1f);

        /// <summary>1 · Tên Xuyên: wind spirals on the bow; a glowing arrow wrapped in a spiral streaks through the line,
        /// bursting out the back of each target it pierces.</summary>
        private IEnumerator PiercingArrow(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Attack");
            Sfx("ar1_cast");
            Charge(Hand(c, true), LeafCore, LeafEdge, 0.38f, 0.6f, 10, Socket(c, "Socket_L"));
            yield return Wait(0.4f);
            Vector3 from = Hand(c, true);
            from.y = Mathf.Min(from.y, Chest(c).y);
            float range = Mathf.Max(3f, c.Range), speed = 24f, travel = range / speed;
            var arrow = SkillVfxKit.MeshLayer(SkillVfxKit.Dart, "white", from, 1.15f, LeafCore, LeafEdge, travel + 0.05f)
                .Rotate(Quaternion.LookRotation(Vector3.right * f, Vector3.up)).Move(Vector3.right * f * speed).Fade(0f, 0.9f);
            SkillVfxKit.Trail(arrow.gameObject, "spark", 0.45f, 0.18f, LeafCore, LeafEdge, -4f);
            Shock(from, 0.5f, LeafCore, LeafEdge, 0.25f, false);
            var hit = new HashSet<FarmMonsterView>();
            float t0 = Time.time; int ring = 0;
            while (Time.time - t0 < travel && arrow)
            {
                Vector3 p = arrow.transform.position;
                if (ring++ % 2 == 0)
                    SkillVfxKit.Layer("ring", p, 0.8f, LeafCore, LeafEdge, 0.3f).Rotate(Quaternion.Euler(0, 75f, 0)).Scale(0.4f, 1.1f, 0.4f).Fade(0f, 0.3f, 0.8f);
                foreach (FarmMonsterView m in c.Victims)
                    if (m && !hit.Contains(m) && (f > 0 ? p.x >= m.Position.x : p.x <= m.Position.x))
                    {
                        hit.Add(m);
                        Vector3 at = Center(m);
                        Flash(at, 1.4f, LeafCore, LeafEdge, 0.16f);
                        Sparks(at, 16, LeafCore, LeafEdge, Vector3.right * f, 25f, 10f, 0.5f, 0.22f);
                        SkillVfxKit.Layer("star", at, 1.4f, LeafCore, LeafEdge, 0.22f).Billboard().Scale(0.3f, 1f, 0.4f).Fade(0f, 0.3f);
                        m.React(LeafEdge, 0.7f, f);
                        Sfx("ar1_hit");
                        Shake(c, 0.07f, 0.12f);
                        if (hit.Count == 1) { Stop(c, 0.035f); c.Land(); }
                    }
                yield return null;
            }
            Flash(arrow ? arrow.transform.position : from, 0.9f, LeafCore, LeafEdge, 0.2f);
            c.Land();
        }

        /// <summary>2 · Mưa Tên: one glowing arrow shot into the sky splits; a volley rains over the marked circle, each
        /// arrow sticking in the ground with a dust puff before it fades.</summary>
        private IEnumerator ArrowRain(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Attack");
            Sfx("ar2_cast");
            Vector3 tg = TargetGround(c);
            float r = Mathf.Max(1.6f, c.Radius);
            SkillVfxKit.Layer("circle", tg + Vector3.up * 0.05f, r * 2.2f, LeafCore, LeafEdge, 1.9f).Ground().Spin(60f).Scale(0.3f, 1f, 0.3f).Fade(0.15f, 0.85f, 0.85f);
            yield return Wait(0.4f);
            Vector3 bow = Hand(c, true);
            Vector3 sky = P(new Vector3((bow.x + tg.x) * 0.5f, Feet(c).y + 7.5f));
            var up = SkillVfxKit.MeshLayer(SkillVfxKit.Dart, "white", bow, 1f, LeafCore, LeafEdge, 0.3f)
                .Rotate(Quaternion.LookRotation((sky - bow).normalized, Vector3.right)).Move((sky - bow) / 0.3f).Fade(0f, 0.9f);
            SkillVfxKit.Trail(up.gameObject, "spark", 0.35f, 0.2f, LeafCore, LeafEdge);
            yield return Wait(0.3f);
            Flash(sky, 2.4f, LeafCore, LeafEdge, 0.25f);
            SkillVfxKit.Layer("star", sky, 2.6f, LeafCore, LeafEdge, 0.35f).Billboard().Spin(200f).Scale(0.3f, 1f, 0.5f).Fade(0f, 0.4f);
            int n = SkillVfxKit.Mobile ? 9 : 15;
            for (int i = 0; i < n; i++)
            {
                Vector3 land = new Vector3(tg.x + Rnd(-r, r), tg.y + 0.1f, Rnd(-0.7f, 0.1f));
                Vector3 start = land + new Vector3(-f * 1.6f, 6.5f, 0f);
                float fall = 0.24f;
                var a = SkillVfxKit.MeshLayer(SkillVfxKit.Dart, "white", start, 0.9f, LeafCore, LeafEdge, fall + 0.55f)
                    .Rotate(Quaternion.LookRotation((land - start).normalized, Vector3.right)).Move((land - start) / fall).Fade(0f, 0.7f).Dissolve(0f, 1f, 0.6f);
                SkillVfxKit.Trail(a.gameObject, "spark", 0.2f, 0.1f, LeafCore, LeafEdge);
                StartCoroutine(ArrowLand(c, a, land, fall));
                yield return Wait(0.045f);
            }
            yield return Wait(0.3f);
            React(c, 0.6f, LeafEdge, tg);
            Sfx("ar2_hit");
            Shake(c, 0.1f, 0.2f);
            c.Land();
        }

        private IEnumerator ArrowLand(SkillCast c, FxLayer arrow, Vector3 land, float fall)
        {
            yield return Wait(fall);
            if (arrow) arrow.Move(Vector3.zero);
            Smoke(land + Vector3.up * 0.1f, 2, new Color(1.2f, 1.1f, 0.9f), new Color(0.6f, 0.5f, 0.35f, 0.5f), 0.5f, 0.4f, 0.6f);
            Sparks(land + Vector3.up * 0.1f, 4, LeafCore, LeafEdge, Vector3.up, 60f, 3f, 2f, 0.12f);
            ReactNear(c, land, 0.9f, 0.25f, LeafEdge);
            Sfx("ar2_tick", 0.45f);
        }

        /// <summary>Phoenix built from fire-feather layers: two fanned wings that flap, a flame body and a glowing head.</summary>
        private static Transform BuildPhoenix(Vector3 at, int f, float life, float scale)
        {
            var root = new GameObject("fx_phoenix").transform;
            root.position = at;
            root.localScale = new Vector3(f, 1f, 1f) * scale;
            var body = SkillVfxKit.Layer("flame", at, 1.1f, PhCore, PhEdge, life).Rotate(Quaternion.Euler(0, 0, -90f)).Stretch(0.7f, 1.6f).Noise(0.8f, Vector2.one, new Vector2(0, -3f)).Parent(root).Fade(0.1f, 0.85f);
            body.transform.localPosition = new Vector3(-0.3f, 0f, 0f);
            body.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            var head = SkillVfxKit.Layer("glow", at, 0.7f, Hue(PhCore, PhEdge, 0.6f), PhEdge, life).Parent(root).Fade(0.1f, 0.85f);
            head.transform.localPosition = new Vector3(0.45f, 0.1f, -0.05f);
            foreach (int side in new[] { 1, -1 })
            {
                var wing = new GameObject(side > 0 ? "wing_up" : "wing_down").transform;
                wing.SetParent(root, false);
                wing.localPosition = new Vector3(0f, 0.05f, side * 0.05f);
                wing.gameObject.AddComponent<WingFlap>().Init(side);
                for (int k = 0; k < 7; k++)
                {
                    float ang = 95f + k * 14f; // fan backwards-up
                    var fea = SkillVfxKit.Layer("feather", at, 1.1f + k * 0.08f, Hue(Color.Lerp(PhCore, new Color(3.2f, 1.4f, 0.4f), k / 7f), PhEdge, 0.45f), PhEdge, life)
                        .Noise(0.55f, new Vector2(1, 2), new Vector2(0, -2f)).Fade(0.1f, 0.85f, 0.65f).Parent(wing);
                    fea.transform.localRotation = Quaternion.Euler(0f, 0f, side > 0 ? ang - 90f : -(ang - 90f) - 180f);
                    fea.transform.localPosition = fea.transform.localRotation * Vector3.up * 0.55f;
                }
                wing.localScale = new Vector3(1f, side, 1f);
            }
            return root;
        }

        /// <summary>3 · Phượng Hoàng Tiễn: fire wings unfurl behind the archer during the long draw; the arrow becomes a
        /// phoenix that sweeps the line, scorching every target it crosses, and bursts into feathers.</summary>
        private IEnumerator PhoenixArrow(SkillCast c)
        {
            int f = c.Facing;
            Anim(c, "Skill");
            Sfx("ar3_cast");
            // wings behind the hero (presentation of the charge)
            Transform wings = BuildPhoenix(Chest(c) + new Vector3(-f * 0.2f, 0.25f, 0.35f), -f, 1.15f, 1.15f);
            foreach (Transform t in wings) if (t.name == "fx_flame" || t.name == "fx_glow") t.gameObject.SetActive(false);
            SkillVfxKit.MeshLayer(SkillVfxKit.Band, "beam", P(Feet(c), 0f), 1f, PhCore * 0.5f, PhEdge, 1.15f).Stretch(1.3f, 2.2f, 0.5f).Spin(320f, Vector3.up).Noise(0.8f, new Vector2(3, 1), new Vector2(0, 1.6f)).NoiseTex("noise_streak").Fade(0.25f, 0.8f, 0.6f);
            Charge(Hand(c, true), PhCore, PhEdge, 1.05f, 0.9f, 18, Socket(c, "Socket_L"));
            Embers(P(Feet(c) + Vector3.up * 0.5f), 16, PhCore, PhEdge, 0.9f, 1.4f);
            yield return Wait(1.12f);
            if (wings) Destroy(wings.gameObject);
            Vector3 from = Hand(c, true);
            from.y = Mathf.Min(from.y, Chest(c).y);
            float range = Mathf.Max(4f, c.Range), speed = 13f, travel = range / speed;
            Transform bird = BuildPhoenix(from, f, travel + 0.1f, 1.25f);
            var mover = bird.gameObject.AddComponent<Mover>();
            mover.velocity = Vector3.right * f * speed;
            var trailHost = new GameObject("trail"); trailHost.transform.SetParent(bird, false);
            SkillVfxKit.Trail(trailHost, "flame", 1.2f, 0.4f, PhCore, PhEdge, -4f);
            Flash(from, 2f, PhCore, PhEdge, 0.2f);
            Shake(c, 0.06f, 0.15f);
            var hit = new HashSet<FarmMonsterView>();
            float t0 = Time.time; int k = 0;
            while (Time.time - t0 < travel && bird)
            {
                Vector3 p = bird.position;
                if (k++ % 3 == 0)
                    SkillVfxKit.Layer("feather", p + new Vector3(-f * 0.6f, Rnd(-0.4f, 0.4f), 0f), Rnd(0.35f, 0.55f), PhCore, PhEdge, 0.9f)
                        .Spin(Rnd(-180, 180)).Move(new Vector3(-f * 0.6f, -0.3f, 0f), Vector3.down * 0.8f).Fade(0f, 0.4f).Dissolve(0f, 1f, 0.5f);
                foreach (FarmMonsterView m in c.Victims)
                    if (m && !hit.Contains(m) && (f > 0 ? p.x >= m.Position.x : p.x <= m.Position.x))
                    {
                        hit.Add(m);
                        Vector3 at = Center(m);
                        Flash(at, 2.2f, PhCore, PhEdge, 0.2f);
                        FlameTongues(at, 6, 1.4f, 3f, PhCore, PhEdge, new Color(3f, 1.4f, 0.3f));
                        Sparks(at, 18, PhCore, PhEdge, Vector3.right * f, 60f, 9f, 1.2f, 0.24f);
                        m.React(PhEdge, 1.1f, f);
                        Sfx("ar3_hit");
                        Shake(c, 0.13f, 0.18f);
                        if (hit.Count == 1) { Stop(c, 0.06f); c.Land(); }
                    }
                yield return null;
            }
            Vector3 end = bird ? bird.position : from + Vector3.right * f * range;
            Flash(end, 3.2f, PhCore, PhEdge, 0.25f);
            Shock(end, 1.4f, PhCore, PhEdge, 0.4f, false);
            for (int i = 0; i < (SkillVfxKit.Mobile ? 6 : 12); i++)
                SkillVfxKit.Layer("feather", end, Rnd(0.4f, 0.7f), PhCore, PhEdge, 1.1f).Spin(Rnd(-240, 240)).Move((Vector3)UnityEngine.Random.insideUnitCircle * 3f + Vector3.up, Vector3.down * 1.5f).Fade(0f, 0.45f).Dissolve(0f, 1f, 0.5f);
            Embers(end, 18, PhCore, PhEdge, 0.8f, 1.5f);
            Shake(c, 0.18f, 0.3f);
            Zoom(c, 0.05f, 0.35f);
            c.Land();
        }

        private sealed class Mover : MonoBehaviour
        {
            public Vector3 velocity;
            private void Update() => transform.position += velocity * Time.deltaTime;
        }

        private sealed class WingFlap : MonoBehaviour
        {
            private int side; private float seed;
            public void Init(int s) { side = s; seed = UnityEngine.Random.value * 3f; }
            private void Update()
            {
                float a = Mathf.Sin(Time.time * 14f + seed) * 22f;
                transform.localRotation = Quaternion.Euler(0f, 0f, side > 0 ? a : -a);
            }
        }
    }
}

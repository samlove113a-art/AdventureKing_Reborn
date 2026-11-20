using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// QA record of where the last skill projectile really started and landed, next to the reference points of the
    /// hero's body (hand / weapon socket, collider top = head, collider centre) — so emitter fixes are measured, not
    /// guessed. Presentation only; no gameplay reads it.
    /// </summary>
    public static class SkillVfxProbe
    {
        public static string Skill = "";
        public static int Facing;
        public static Vector3 Origin, Velocity, Impact, HandSocket, WeaponTip, BodyCenter;
        public static float HeadTop, BodyBottom;
        public static Transform Projectile;
        public static int Launches, Impacts;

        public static void Launched(SkillCast c, Vector3 origin, Transform projectile, Vector3 velocity)
        {
            Skill = c.Class + "_" + c.Slot;
            Facing = c.Facing;
            Origin = origin;
            Velocity = velocity;
            Projectile = projectile;
            Launches++;
            Transform s = SkillVfxDirector.HandOf(c, c.Class == HeroClass.Archer);
            HandSocket = s ? s.position : Vector3.zero;
            WeaponTip = s ? SkillVfxDirector.WeaponTipOf(s) : Vector3.zero;
            var col = c.Hero.GetComponent<Collider2D>();
            if (col) { HeadTop = col.bounds.max.y; BodyBottom = col.bounds.min.y; BodyCenter = col.bounds.center; }
            else { HeadTop = c.Hero.position.y + 1.6f; BodyBottom = c.Hero.position.y; BodyCenter = c.Hero.position + Vector3.up * 0.8f; }
        }

        /// <summary>Where the projectile burst and where the targeted monster's body was AT THAT MOMENT.</summary>
        public static void Impacted(Vector3 at, Vector3 targetAtImpact) { Impact = at; ImpactTarget = targetAtImpact; Impacts++; }
        public static Vector3 ImpactTarget;

        public static string Report() =>
            $"{Skill} facing={Facing} origin={Origin:F2} vel={Velocity:F2} hand={HandSocket:F2} tip={WeaponTip:F2} " +
            $"bodyCenter={BodyCenter:F2} head={HeadTop:F2} feet={BodyBottom:F2} impact={Impact:F2}";
    }
}

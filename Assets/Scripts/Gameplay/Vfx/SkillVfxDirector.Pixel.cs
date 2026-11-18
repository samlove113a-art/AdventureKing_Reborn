using System;
using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// 2D pixel skill effects for the sprite heroes (the default look): the same 12 skills, same timing contract as the 3D choreographies
    /// (animation contact beat → release → impact → c.Land()), drawn with the flip-book effects from Tools/chibi2d/make_fx.py
    /// (Resources/Chibi2D/Fx2). Effects start at the front of the body (never above the head), travel toward the monster the player sees,
    /// and land on it; damage / MP / cooldown were already decided by the server. Mage = purple magic / orb / ice / lightning / summon circle,
    /// Swordsman = sword aura / light slash / ground slam, Archer = pierce / arrow rain / leaf wind, Assassin = daggers / clones / dark slash / smoke.
    /// </summary>
    public sealed partial class SkillVfxDirector
    {
        private static readonly Color PuCore = new Color(0.85f, 0.55f, 1f), IceTint = new Color(0.7f, 0.95f, 1f), GoldTint = new Color(1f, 0.88f, 0.5f),
            LeafTint = new Color(0.8f, 1f, 0.6f), DarkTint = new Color(0.85f, 0.7f, 1f);

        /// <summary>One effect from Resources/Chibi2D/Fx2/{key} (or Chibi2D/Fx/{key} for the first three Mage ones).</summary>
        private static GameObject Fx(string key, Vector3 pos, float scale, float flipX = 1f, Color? tint = null, float rotZ = 0f, int order = 8, float flipY = 1f)
        {
            GameObject prefab = Resources.Load<GameObject>("Chibi2D/Fx2/" + key) ?? Resources.Load<GameObject>("Chibi2D/Fx/" + key);
            if (!prefab) return null;
            GameObject go = Instantiate(prefab, new Vector3(pos.x, pos.y, Front), Quaternion.Euler(0f, 0f, rotZ));
            scale *= FxScale;   // the strips are 2 m wide at scale 1: sized so a slash is about the width of a hero and a blast about a monster and a half
            go.transform.localScale = new Vector3(scale * flipX, scale * flipY, 1f);
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.color = tint ?? Color.white;
                sr.sortingOrder = order;
            }
            return go;
        }

        private const float FxScale = 0.6f;

        private static float AngleOf(Vector3 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        /// <summary>A looping sprite flown from→to in <paramref name="time"/> seconds (homing on a monster the player sees); gone on arrival.</summary>
        private static IEnumerator Fly(GameObject go, Vector3 from, Func<Vector3> to, float time, float spin = 0f)
        {
            float t = 0f;
            while (go && t < time)
            {
                t += Time.deltaTime;
                Vector3 target = to();
                Vector3 p = Vector3.Lerp(from, target, Mathf.Clamp01(t / time));
                go.transform.position = new Vector3(p.x, p.y, Front);
                if (spin != 0f) go.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                yield return null;
            }
            if (go) Destroy(go);
        }

        private static Vector3 AimPoint(SkillCast c) => c.Victims.Count > 0 && c.Victims[0] ? Center(c.Victims[0]) : TargetCenter(c);

        private IEnumerator PixelSkill(SkillCast c)
        {
            switch (c.Class, c.Slot)
            {
                case (HeroClass.Swordsman, 1): return PxSwordWave(c);
                case (HeroClass.Swordsman, 2): return PxSwordWhirl(c);
                case (HeroClass.Swordsman, _): return PxSwordSlam(c);
                case (HeroClass.Mage, 1): return PxMageOrb(c);
                case (HeroClass.Mage, 2): return PxMageIce(c);
                case (HeroClass.Mage, _): return PxMageMeteor(c);
                case (HeroClass.Archer, 1): return PxArcherPierce(c);
                case (HeroClass.Archer, 2): return PxArcherRain(c);
                case (HeroClass.Archer, _): return PxArcherMulti(c);
                case (HeroClass.Assassin, 1): return PxAssassinDaggers(c);
                case (HeroClass.Assassin, 2): return PxAssassinClones(c);
                default: return PxAssassinFinisher(c);
            }
        }

        // ---------------------------------------------------------------- Kiếm Sĩ
        private IEnumerator PxSwordWave(SkillCast c)
        {
            Anim(c, "Attack"); Sfx("sw1_cast");
            yield return WaitForContact(c, false, 0.3f);
            Vector3 from = Muzzle(c) + Vector3.right * c.Facing * 0.5f, to = AimPoint(c);
            Fx("slash_light", from, 1.5f, c.Facing, Color.white);
            float travel = Mathf.Clamp(Mathf.Abs(to.x - from.x) / 14f, 0.1f, 0.4f);
            var wave = Fx("slash_light", from, 2.0f, c.Facing, new Color(0.75f, 1f, 1f));
            if (wave) { var fb = wave.GetComponent<SpriteFlipbook>(); if (fb) { fb.Loop = true; fb.DestroyOnEnd = false; fb.Fps = 8f; } yield return Fly(wave, from, () => AimPoint(c), travel); }
            Fx("slash_light", to, 1.2f, c.Facing, Color.white, rotZ: 10f);
            Fx("hit_spark", to, 1.3f);
            React(c, 0.5f, new Color(0.4f, 1f, 0.9f), from); Sfx("sw1_hit"); Shake(c, 0.06f, 0.15f); Stop(c, 0.03f);
            c.Land();
        }

        private IEnumerator PxSwordWhirl(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("sw2_cast");
            Vector3 mid = Chest(c);
            Fx("whirl", mid, 2.4f, 1f, new Color(0.8f, 1f, 1f));
            yield return Wait(0.18f);
            Fx("slash_light", mid + Vector3.right * 0.9f, 1.5f, 1f, Color.white);
            Fx("slash_light", mid + Vector3.left * 0.9f, 1.5f, -1f, Color.white);
            yield return Wait(0.12f);
            foreach (FarmMonsterView m in c.Victims) if (m) { Fx("slash_light", Center(m), 1.1f, Mathf.Sign(m.Position.x - mid.x), Color.white, rotZ: UnityEngine.Random.Range(-20f, 20f)); Fx("hit_spark", Center(m), 1.1f); }
            ReactNear(c, mid, Mathf.Max(2f, c.Radius) + 0.4f, 0.6f, new Color(0.4f, 1f, 0.9f)); Shake(c, 0.08f, 0.18f); Sfx("sw1_hit");
            c.Land();
            yield return Wait(0.3f);
        }

        private IEnumerator PxSwordSlam(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("sw3_cast");
            Vector3 g = TargetGround(c);
            Fx("pillar", g + Vector3.up * 0.2f, 2.6f, 1f, GoldTint, order: 7);
            yield return Wait(0.35f);
            Fx("slam", g + Vector3.up * 0.05f, 2.8f, 1f, Color.white);
            Fx("slash_light", TargetCenter(c), 2.2f, c.Facing, Color.white);
            foreach (FarmMonsterView m in c.Victims) if (m) Fx("hit_spark", Center(m), 1.4f);
            React(c, 1f, new Color(1f, 0.85f, 0.4f), Feet(c)); Sfx("sw3_hit"); Shake(c, 0.16f, 0.3f); Stop(c, 0.07f); Zoom(c, 0.03f, 0.25f);
            c.Land();
            yield return Wait(0.4f);
        }

        // ---------------------------------------------------------------- Pháp Sư
        private IEnumerator PxMageOrb(SkillCast c)
        {
            Anim(c, "Attack"); Sfx("mg1_cast");
            Vector3 from = Muzzle(c);
            Fx("cast_circle", from, 0.9f, 1f, PuCore, order: 7);
            yield return WaitForContact(c, false, 0.4f);
            from = Muzzle(c) + Vector3.right * c.Facing * 0.3f;
            Vector3 to = AimPoint(c);
            var orb = Fx("orb", from, 0.9f, 1f, Color.white);
            float travel = Mathf.Clamp(Mathf.Abs(to.x - from.x) / 12f, 0.12f, 0.5f);
            if (orb) yield return Fly(orb, from, () => AimPoint(c), travel, 90f);
            to = AimPoint(c);
            Fx("explosion", to, 0.9f, 1f, Color.white);
            Fx("hit_spark", to, 1.3f, 1f, PuCore);
            ReactNear(c, to, Mathf.Max(1.2f, c.Radius) + 0.4f, 0.8f, PuCore); Sfx("mg1_hit"); Shake(c, 0.1f, 0.2f); Stop(c, 0.04f);
            c.Land();
        }

        private IEnumerator PxMageIce(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("mg2_cast");
            Vector3 g = TargetGround(c);
            float r = Mathf.Max(1.6f, c.Radius);
            Fx("cast_circle", g + Vector3.up * 0.1f, r * 1.4f, 1f, IceTint, order: 6);
            yield return Wait(0.4f);
            int n = SkillVfxKit.Mobile ? 6 : 9;
            for (int i = 0; i < n; i++)
            {
                float x = g.x + (i - (n - 1) / 2f) * (2f * r / n) + Rnd(-0.2f, 0.2f);
                Vector3 land = new Vector3(x, g.y + 0.5f, 0f), start = land + new Vector3(Rnd(-0.4f, 0.4f), 5.5f, 0f);
                var shard = Fx("ice_shard", start, 0.9f, 1f, Color.white, rotZ: 0f);
                if (shard) StartCoroutine(FlyThen(shard, start, land, 0.28f, () => { Fx("ice_burst", land, 0.85f, 1f, Color.white); Sfx("mg2_tick", 0.5f); }));
                yield return Wait(0.055f);
            }
            yield return Wait(0.2f);
            foreach (FarmMonsterView m in c.Victims) if (m) Fx("ice_burst", Center(m), 1.1f, 1f, IceTint);
            ReactNear(c, g, r + 0.4f, 0.7f, IceTint); Sfx("mg2_hit"); Shake(c, 0.08f, 0.2f);
            c.Land();
            yield return Wait(0.3f);
        }

        private IEnumerator FlyThen(GameObject go, Vector3 from, Vector3 to, float time, Action done)
        {
            yield return Fly(go, from, () => to, time);
            done?.Invoke();
        }

        private IEnumerator PxMageMeteor(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("mg3_cast");
            Vector3 g = TargetGround(c);
            float r = Mathf.Max(2f, c.Radius);
            Fx("cast_circle", g + Vector3.up * 0.1f, r * 1.8f, 1f, PuCore, order: 6);
            for (int i = 0; i < 3; i++) { Fx("lightning", new Vector3(g.x + Rnd(-r, r), g.y + 3.2f, 0f), 2.6f, 1f, Color.white, order: 7); yield return Wait(0.12f); }
            yield return Wait(0.2f);
            Vector3 start = g + new Vector3(-5f * c.Facing, 6.5f, 0f);
            var met = Fx("meteor", start, 1.6f, c.Facing, Color.white, rotZ: c.Facing > 0 ? 0f : 90f);
            if (met) yield return Fly(met, start, () => g + Vector3.up * 0.6f, 0.38f);
            Fx("explosion", g + Vector3.up * 1.1f, 2.4f, 1f, Color.white);
            Fx("slam", g + Vector3.up * 0.05f, 2.8f, 1f, PuCore);
            foreach (FarmMonsterView m in c.Victims) if (m) Fx("hit_spark", Center(m), 1.4f, 1f, PuCore);
            ReactNear(c, g, r + 0.5f, 1f, PuCore); Sfx("mg3_hit"); Shake(c, 0.2f, 0.35f); Stop(c, 0.08f); Zoom(c, 0.04f, 0.3f);
            c.Land();
            yield return Wait(0.4f);
        }

        // ---------------------------------------------------------------- Cung Thủ
        private IEnumerator PxArcherPierce(SkillCast c)
        {
            Anim(c, "Attack"); Sfx("ar1_cast");
            yield return WaitForContact(c, false, 0.35f);
            Vector3 from = Muzzle(c, true) + Vector3.right * c.Facing * 0.4f, to = AimPoint(c);
            float len = Mathf.Max(Mathf.Abs(to.x - from.x) + 3f, 6f);
            Vector3 end = new Vector3(from.x + c.Facing * len, to.y, 0f);
            var arrow = Fx("arrow", from, 1.6f, c.Facing, GoldTint);
            Fx("hit_spark", from, 0.8f, 1f, GoldTint);
            float travel = Mathf.Clamp(len / 22f, 0.15f, 0.45f);
            bool landed = false;
            float t = 0f;
            while (arrow && t < travel)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.Lerp(from, end, t / travel);
                arrow.transform.position = new Vector3(p.x, p.y, Front);
                foreach (FarmMonsterView m in c.Victims) if (m && Mathf.Abs(m.Position.x - p.x) < 0.5f && !landed) { Fx("hit_spark", Center(m), 1.2f, 1f, GoldTint); }
                yield return null;
            }
            if (arrow) Destroy(arrow);
            foreach (FarmMonsterView m in c.Victims) if (m) Fx("hit_spark", Center(m), 1.2f, 1f, GoldTint);
            React(c, 0.6f, GoldTint, from); Sfx("ar1_hit"); Shake(c, 0.07f, 0.15f); Stop(c, 0.03f);
            c.Land();
        }

        private IEnumerator PxArcherRain(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("ar2_cast");
            Vector3 g = TargetGround(c);
            float r = Mathf.Max(1.8f, c.Radius);
            Fx("cast_circle", g + Vector3.up * 0.1f, r * 1.4f, 1f, LeafTint, order: 6);
            Fx("leaf_wind", g + Vector3.up * 1.2f, r * 1.2f, 1f, Color.white, order: 9);
            yield return Wait(0.35f);
            int n = SkillVfxKit.Mobile ? 8 : 13;
            for (int i = 0; i < n; i++)
            {
                float x = g.x + Rnd(-r, r);
                Vector3 land = new Vector3(x, g.y + 0.2f, 0f), start = land + new Vector3(-1.2f, 6f, 0f);
                var ar = Fx("arrow", start, 0.9f, 1f, LeafTint, rotZ: AngleOf(land - start), flipY: 1f);
                if (ar) StartCoroutine(FlyThen(ar, start, land, 0.3f, () => Fx("hit_spark", land, 0.8f, 1f, LeafTint)));
                if (i == n / 2) { Sfx("ar2_tick", 0.5f); }
                yield return Wait(0.05f);
            }
            yield return Wait(0.2f);
            foreach (FarmMonsterView m in c.Victims) if (m) Fx("hit_spark", Center(m), 1.1f, 1f, LeafTint);
            ReactNear(c, g, r + 0.4f, 0.6f, LeafTint); Sfx("ar2_hit"); Shake(c, 0.07f, 0.18f);
            c.Land();
            yield return Wait(0.35f);
        }

        private IEnumerator PxArcherMulti(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("ar3_cast");
            Fx("leaf_wind", Chest(c), 1.5f, 1f, LeafTint, order: 9);
            yield return WaitForContact(c, true, 0.4f);
            Vector3 from = Muzzle(c, true) + Vector3.right * c.Facing * 0.4f, to = AimPoint(c);
            for (int i = 0; i < 5; i++)
            {
                Vector3 aim = to + new Vector3(0f, (i - 2) * 0.35f, 0f);
                Vector3 d = aim - from;
                var ar = Fx("arrow", from, 1.2f, 1f, i == 2 ? GoldTint : LeafTint, rotZ: AngleOf(d), flipY: d.x < 0 ? -1f : 1f);
                float travel = Mathf.Clamp(d.magnitude / 20f, 0.12f, 0.4f);
                Vector3 hit = aim;
                if (ar) StartCoroutine(FlyThen(ar, from, hit, travel, () => Fx("hit_spark", hit, 0.9f, 1f, GoldTint)));
                yield return Wait(0.04f);
            }
            yield return Wait(0.3f);
            Fx("explosion", to, 1.3f, 1f, new Color(1f, 0.95f, 0.6f));
            ReactNear(c, to, Mathf.Max(2f, c.Radius) + 0.4f, 1f, GoldTint); Sfx("ar3_hit"); Shake(c, 0.14f, 0.28f); Stop(c, 0.06f);
            c.Land();
            yield return Wait(0.3f);
        }

        // ---------------------------------------------------------------- Sát Thủ
        private IEnumerator PxAssassinDaggers(SkillCast c)
        {
            Anim(c, "Attack"); Sfx("as1_cast");
            yield return WaitForContact(c, false, 0.3f);
            Vector3 from = Muzzle(c), to = AimPoint(c);
            for (int i = 0; i < 5; i++)
            {
                Vector3 aim = to + new Vector3(0f, (i - 2) * 0.3f, 0f);
                var d = Fx("dagger", from, 0.9f, 1f, Color.white, rotZ: AngleOf(aim - from), flipY: aim.x < from.x ? -1f : 1f);
                Vector3 hit = aim;
                if (d) StartCoroutine(FlyThen(d, from, hit, 0.18f, () => Fx("hit_spark", hit, 0.8f, 1f, DarkTint)));
                yield return Wait(0.03f);
            }
            yield return Wait(0.2f);
            Fx("slash_dark", to, 1.6f, c.Facing, Color.white);
            React(c, 0.6f, DarkTint, from); Sfx("as1_hit"); Shake(c, 0.07f, 0.15f); Stop(c, 0.03f);
            c.Land();
        }

        private IEnumerator PxAssassinClones(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("as2_cast");
            Vector3 f = Feet(c);
            Fx("smoke", f + Vector3.up * 0.3f, 1.5f, 1f, Color.white, order: 9);
            var body = c.Visual ? c.Visual.GetComponentInChildren<SpriteRenderer>() : null;
            for (int k = -1; k <= 1; k += 2)
                if (body)   // dark echoes of the hero's current pose either side of it
                {
                    var go = new GameObject("ShadowClone");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = body.sprite; sr.flipX = body.flipX; sr.color = new Color(0.4f, 0.25f, 0.6f, 0.65f); sr.sortingOrder = 4;
                    go.transform.position = new Vector3(body.transform.position.x + k * 1.3f, body.transform.position.y, Front);
                    go.transform.localScale = body.transform.lossyScale;
                    Destroy(go, 0.8f);
                }
            yield return Wait(0.25f);
            foreach (FarmMonsterView m in c.Victims) if (m) { Fx("slash_dark", Center(m), 1.3f, Mathf.Sign(m.Position.x - f.x), Color.white); Fx("hit_spark", Center(m), 1f, 1f, DarkTint); }
            Fx("smoke", f + Vector3.up * 0.3f, 1.2f, 1f, Color.white, order: 9);
            ReactNear(c, TargetGround(c), Mathf.Max(2f, c.Radius) + 0.4f, 0.6f, DarkTint); Shake(c, 0.07f, 0.16f);
            c.Land();
            yield return Wait(0.4f);
        }

        private IEnumerator PxAssassinFinisher(SkillCast c)
        {
            c.Visual?.PlaySkill(); Sfx("as3_cast");
            Vector3 f = Feet(c), to = AimPoint(c);
            Fx("smoke", f + Vector3.up * 0.3f, 1.3f, 1f, Color.white, order: 9);
            yield return Wait(0.25f);
            for (int i = 0; i < 4; i++)
            {
                Fx("slash_dark", to, 1.5f + i * 0.15f, (i % 2 == 0 ? 1f : -1f), Color.white, rotZ: (i - 1.5f) * 25f);
                Fx("hit_spark", to, 1f, 1f, DarkTint);
                Sfx("as3_tick", 0.5f);
                yield return Wait(0.09f);
            }
            Fx("explosion", to, 1.5f, 1f, new Color(0.7f, 0.35f, 1f));
            ReactNear(c, to, Mathf.Max(1.8f, c.Radius) + 0.4f, 1f, DarkTint); Sfx("as3_hit"); Shake(c, 0.16f, 0.3f); Stop(c, 0.07f);
            c.Land();
            yield return Wait(0.35f);
        }
    }
}

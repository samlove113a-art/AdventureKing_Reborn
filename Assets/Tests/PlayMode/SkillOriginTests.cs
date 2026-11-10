using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Skill emitter position, measured (not guessed): Pháp Sư skill 1 (Cầu Lửa) facing right and facing left on the
    /// real server, plus the basic-attack projectile (old HeroCombatPresentation path). For each cast: where the
    /// projectile starts vs the staff tip / hand socket / head (collider top), its flight direction vs the facing
    /// (no flying backwards or into the camera), and where it lands vs the monster. Captures: QA/SkillOrigin/*.png,
    /// numbers: QA/SkillOrigin/report.txt. Run with a window.
    /// </summary>
    public sealed class SkillOriginTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/SkillOrigin";
        private readonly List<string> report = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5311);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            File.WriteAllLines($"{Dir}/report.txt", report);
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private IEnumerator WalkTo(float x, float timeout = 20f)
        {
            float end = Time.time + timeout;
            while (Mathf.Abs(Pos.x - x) > 0.2f)
            {
                bool right = Pos.x < x;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                Assert.Less(Time.time, end, $"timeout walking to x={x}, at {Pos}");
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            float settle = Time.time + 2f;
            while (Mathf.Abs(motor.Velocity.x) > 0.05f && Time.time < settle) yield return null;
        }

        private IEnumerator Tap(PlayerInput2D.TouchAction a) { input.SetTouchHeld(a, true); yield return null; input.SetTouchHeld(a, false); yield return null; }

        private static void Shot(string name)
        {
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        /// <summary>Faces the given way without moving away: a short step in that direction.</summary>
        private IEnumerator Face(int dir)
        {
            input.SetTouchHeld(dir > 0 ? PlayerInput2D.TouchAction.Right : PlayerInput2D.TouchAction.Left, true);
            yield return new WaitForSeconds(0.08f);
            input.SetTouchHeld(dir > 0 ? PlayerInput2D.TouchAction.Right : PlayerInput2D.TouchAction.Left, false);
            float settle = Time.time + 1f;
            while (Mathf.Abs(motor.Velocity.x) > 0.05f && Time.time < settle) yield return null;
            yield return new WaitForSeconds(0.35f); // visual yaw settles
            Assert.AreEqual(dir, motor.Facing, "facing");
        }

        private IEnumerator Fireball(string tag, FarmMonsterView m, int dir)
        {
            FarmField farm = FarmField.Current;
            var mana = motor.GetComponent<PlayerMana>();
            float until = Time.time + 30f;
            while ((Time.time < farm.CooldownEnds[0] || mana.Current < 20f || !m.Alive) && Time.time < until) yield return null;
            int launches = SkillVfxProbe.Launches, impacts = SkillVfxProbe.Impacts;
            Object.FindAnyObjectByType<HeroActionBar>().TryCast(1, false);
            until = Time.time + 3f;
            while (SkillVfxDirector.Played == 0 && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.12f);
            yield return new WaitForEndOfFrame();
            Shot($"{tag}_1_cast_start");
            until = Time.time + 3f;
            while (SkillVfxProbe.Launches == launches && Time.time < until) yield return null;
            Assert.Greater(SkillVfxProbe.Launches, launches, "fireball launched; " + farm.LastSkillError);
            Vector3 origin = SkillVfxProbe.Origin;
            yield return new WaitForSeconds(0.04f); // the layers fade in over their first frames
            yield return new WaitForEndOfFrame();
            Shot($"{tag}_2_leaves_staff");
            until = Time.time + 3f;
            while (SkillVfxProbe.Impacts == impacts && Time.time < until) yield return null;
            yield return new WaitForEndOfFrame();
            Shot($"{tag}_3_hits_monster");
            Vector3 monster = SkillVfxProbe.ImpactTarget; // the monster's body when the ball burst (it moves afterwards)
            float toTip = Vector3.Distance(Flat(origin), Flat(SkillVfxProbe.WeaponTip));
            float toHand = Vector3.Distance(Flat(origin), Flat(SkillVfxProbe.HandSocket));
            float aboveHead = origin.y - SkillVfxProbe.HeadTop;
            float ahead = (origin.x - SkillVfxProbe.BodyCenter.x) * dir;
            Vector3 v = SkillVfxProbe.Velocity;
            float impactErr = Vector2.Distance(new Vector2(SkillVfxProbe.Impact.x, SkillVfxProbe.Impact.y), new Vector2(monster.x, monster.y));
            report.Add($"[{tag}] {SkillVfxProbe.Report()}");
            report.Add($"[{tag}] origin→staffTip {toTip:F2} m, origin→hand {toHand:F2} m, origin above head {aboveHead:F2} m, ahead of body {ahead:F2} m, " +
                       $"vel x-sign {Mathf.Sign(v.x)} (facing {dir}), |vz|/|vx| {Mathf.Abs(v.z) / Mathf.Max(0.01f, Mathf.Abs(v.x)):F2}, impact→monster {impactErr:F2} m");
            Debug.Log(report[report.Count - 2] + "\n" + report[report.Count - 1]);
            Assert.Less(aboveHead, -0.1f, $"{tag}: the fireball must not start above the head ({aboveHead:F2} m)");
            Assert.Less(toHand, 0.4f, $"{tag}: the fireball must start at the casting hand ({toHand:F2} m away; pushed just in front of the body)");
            Assert.Greater(ahead, 0.1f, $"{tag}: it starts in front of the body");
            Assert.AreEqual(dir, (int)Mathf.Sign(v.x), $"{tag}: it flies the way the hero faces");
            Assert.Less(Mathf.Abs(v.z) / Mathf.Max(0.01f, Mathf.Abs(v.x)), 0.15f, $"{tag}: not toward the camera");
            Assert.Less(impactErr, 0.8f, $"{tag}: it lands on the monster");
        }

        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, p.y, 0f);

        /// <summary>Basic attack (old HeroCombatPresentation path): where its projectile is spawned and how it flies.</summary>
        private IEnumerator BasicAttack(string tag, int dir)
        {
            var seen = new HashSet<VfxProjectile>(Object.FindObjectsByType<VfxProjectile>(FindObjectsInactive.Exclude));
            yield return Tap(PlayerInput2D.TouchAction.Attack);
            float until = Time.time + 1.5f;
            VfxProjectile p = null;
            while (!p && Time.time < until)
            {
                p = Object.FindObjectsByType<VfxProjectile>(FindObjectsInactive.Exclude).FirstOrDefault(x => !seen.Contains(x));
                yield return null;
            }
            if (!p) { report.Add($"[{tag}] basic attack: no projectile seen"); yield break; }
            Vector3 start = p.transform.position;
            Vector3 fwd = p.transform.forward;
            var col = motor.GetComponent<Collider2D>();
            yield return new WaitForEndOfFrame();
            Shot($"{tag}_basic_attack");
            report.Add($"[{tag}] basic attack projectile start={start:F2} forward={fwd:F2} head={col.bounds.max.y:F2} bodyCenter={col.bounds.center:F2} " +
                       $"above head {start.y - col.bounds.max.y:F2} m, ahead {(start.x - col.bounds.center.x) * dir:F2} m, |fz|/|fx| {Mathf.Abs(fwd.z) / Mathf.Max(0.01f, Mathf.Abs(fwd.x)):F2}");
            Debug.Log(report[report.Count - 1]);
            Assert.Less(start.y - col.bounds.max.y, -0.1f, $"{tag}: basic attack projectile not above the head");
            Assert.AreEqual(dir, (int)Mathf.Sign(fwd.x), $"{tag}: basic attack flies the way the hero faces");
            Assert.Less(Mathf.Abs(fwd.z) / Mathf.Max(0.01f, Mathf.Abs(fwd.x)), 0.15f, $"{tag}: basic attack not toward the camera");
        }

        [UnityTest]
        public IEnumerator Mage_Skill1_Starts_At_The_Staff_Both_Facings()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("orig" + Random.Range(1000, 9999), "orig-pass-2026", HeroClass.Mage, "Pháp Sư Đo");
            motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            // hub → Đồng Cỏ Gió (slimes, short walk)
            var rb = motor.GetComponent<Rigidbody2D>();
            float portal = TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X;
            rb.position = new Vector2(portal - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(portal, 8f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            until = Time.time + 3f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>();
            var qp = Object.FindAnyObjectByType<QuestPanel>(); if (qp) qp.gameObject.SetActive(false);
            FarmField farm = FarmField.Current;
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;

            // ---- facing RIGHT: slime meadow_s1 (x -22) from its left
            FarmMonsterView s1 = farm.Monsters["meadow_s1"];
            yield return WalkTo(s1.Position.x - 4.5f);
            yield return Face(1);
            yield return BasicAttack("right", 1);
            yield return Fireball("right", s1, 1);

            // ---- facing LEFT: slime meadow_s2 (x -14) from its right
            FarmMonsterView s2 = farm.Monsters["meadow_s2"];
            yield return WalkTo(s2.Position.x + 4.5f);
            yield return Face(-1);
            yield return BasicAttack("left", -1);
            yield return Fireball("left", s2, -1);
        }
    }
}

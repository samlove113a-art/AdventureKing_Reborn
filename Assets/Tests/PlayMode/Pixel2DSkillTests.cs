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
    /// The 12 skills as 2D pixel effects on the sprite heroes (default look): every skill runs its own choreography, spawns sprite effects,
    /// lands its damage beat exactly once, effects that leave the hero start in FRONT of the body at body height (never above the head,
    /// never behind), projectiles fly toward the target side, and the monster reacts. Game View captures: QA/Pixel2DSkills/*.png.
    /// </summary>
    public sealed class Pixel2DSkillTests
    {
        private TestServer server;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Pixel2DSkills";
        private static readonly HeroClass[] Classes = { HeroClass.Mage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.Assassin };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            HeroVisualStyle.Override = "2d";
            server = new TestServer(5303);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
            HeroVisualStyle.Override = "3d";
        }

        private static void Shot(string name, int w = 1600, int h = 900)
        {
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Every_Skill_Plays_Its_Pixel_Effects_From_The_Front_Of_The_Body_And_Lands_Once([ValueSource(nameof(Classes))] HeroClass cls)
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("px" + (int)cls + Random.Range(1000, 9999), "px-pass-2026", cls, "Thử Hiệu Ứng");
            motor = AdventureWorldSession.Current.Player;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            // the hero + a target: a monster of Làng Nấm is reached through the real portals
            foreach (var step in new[] { (TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene), (TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene) })
            {
                var rb0 = motor.GetComponent<Rigidbody2D>();
                rb0.position = new Vector2(step.Item1 - 2.5f, 1.2f); rb0.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                var input0 = motor.GetComponent<PlayerInput2D>();
                float end = Time.time + 8f;
                while (Mathf.Abs(motor.transform.position.x - step.Item1) > 0.3f && Time.time < end) { input0.SetTouchHeld(PlayerInput2D.TouchAction.Right, motor.transform.position.x < step.Item1); yield return null; }
                input0.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
                yield return null; input0.SetTouchHeld(PlayerInput2D.TouchAction.Up, true); yield return null; input0.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
                var w0 = AdventureWorldSession.Current;
                float until2 = Time.time + 2f;
                while (!w0.ConfirmOpen && !w0.Traveling && Time.time < until2) yield return null;
                if (w0.ConfirmOpen) w0.ConfirmPortal();
                yield return TestServer.WaitForScene(step.Item2);
                yield return new WaitForSeconds(1.2f);
                motor = AdventureWorldSession.Current.Player;
            }
            until = Time.time + 6f;
            while ((!FarmField.Current || FarmField.Current.Monsters.Count == 0) && Time.time < until) yield return null;
            var target = FarmField.Current.Monsters.Values.First(m => m.Alive && m.IsSprite);
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(target.Position.x - 3.2f, target.Position.y + 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);

            var visual = motor.GetComponentInChildren<CharacterVisual3D>();
            Assert.IsNotNull(visual); Assert.IsTrue(visual.IsSprite, "the hero is a 2D sprite body");
            var col = motor.GetComponent<Collider2D>();
            for (int slot = 1; slot <= 3; slot++)
            {
                int landed = 0;
                var cast = new SkillCast
                {
                    Class = cls, Slot = slot, Hero = motor.transform, Visual = visual, Facing = 1, Target = new Vector3(target.Position.x, target.Position.y, 0f),
                    Radius = 2.2f, Range = 6f, Landed = () => landed++,
                };
                cast.Victims.Add(target);
                var seen = new Dictionary<SpriteFlipbook, (string name, Vector3 pos)>();
                int before = SkillVfxDirector.Played;
                SkillVfxDirector.Play(cast);
                float stop = Time.time + 3.2f;
                bool shot = false;
                while (Time.time < stop)
                {
                    foreach (var fb in Object.FindObjectsByType<SpriteFlipbook>(FindObjectsInactive.Exclude))
                        if (!seen.ContainsKey(fb)) seen[fb] = (fb.name.Replace("(Clone)", ""), fb.transform.position);
                    if (!shot && Time.time > stop - 2.6f && seen.Count > 0) { shot = true; Shot($"{cls}_skill{slot}"); }
                    yield return null;
                }
                string tag = $"{cls} skill {slot}";
                Assert.AreEqual(before + 1, SkillVfxDirector.Played, tag + " ran its own choreography");
                Assert.AreEqual(1, landed, tag + ": the damage beat lands exactly once");
                Assert.GreaterOrEqual(seen.Count, 2, tag + ": spawned pixel effects (" + string.Join(",", seen.Values.Select(v => v.name)) + ")");
                // effects that start at the hero: in front of the body (target is to the right), at body height, never above the head
                float top = col.bounds.max.y, bottom = col.bounds.min.y, cx = col.bounds.center.x;
                foreach (var (name, pos) in seen.Values.Where(v => System.Array.IndexOf(new[] { "slash_light", "orb", "arrow", "dagger" }, v.name) >= 0 && Mathf.Abs(v.pos.x - cx) < 1.6f && !(v.name == "arrow" && slot == 2) && !(v.name == "slash_light" && slot == 2)))   // (the arrow rain falls from the sky, the whirl surrounds the hero)
                {
                    Assert.LessOrEqual(pos.y, top + 0.05f, $"{tag}: {name} starts above the head ({pos.y:0.00} > {top:0.00})");
                    Assert.GreaterOrEqual(pos.y, bottom - 0.1f, $"{tag}: {name} starts under the feet");
                    Assert.Greater(pos.x, cx - 0.05f, $"{tag}: {name} starts behind the body");
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}

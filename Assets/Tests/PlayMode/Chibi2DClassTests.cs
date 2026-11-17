using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>The three 2D chibi classes cut from the owner's design sheets (Kiếm Sĩ, Cung Thủ, Sát Thủ) on the real server: same
    /// Animator contract, point-filtered 128 px frames, unlit pixel material, compact scale, every state plays, the gameplay collider and
    /// server data are untouched. Game View captures: QA/Chibi2D/{Class}_*.png</summary>
    public sealed class Chibi2DClassTests
    {
        private TestServer server;
        private PlayerInput2D input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            Directory.CreateDirectory("QA/Chibi2D");
            server = new TestServer(5297);
            yield return server.Start();
            HeroVisualStyle.Override = "2d";
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            HeroVisualStyle.Override = null;
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return new WaitForEndOfFrame();
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Chibi2D/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private static string Clip(Animator a) => a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault() ?? "";

        private IEnumerator Run(HeroClass cls)
        {
            string n = cls.ToString();
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("c2" + n.Substring(0, 2).ToLower() + Random.Range(1000, 9999), "chibi-pass-2026", cls, cls == HeroClass.Swordsman ? "Kiếm Sĩ Pixel" : cls == HeroClass.Archer ? "Cung Thủ Pixel" : "Sát Thủ Pixel");
            var world = AdventureWorldSession.Current;
            var motor = world.Player; input = motor.GetComponent<PlayerInput2D>();
            Assert.AreEqual(cls, world.Character.heroClass);
            Assert.IsNotNull(world.HeroModel.GetComponent<Chibi2DMarker>(), n + " uses its 2D body: " + world.HeroModel.name);
            Assert.AreEqual(0, world.HeroModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, "no 3D mesh");
            var pd = world.HeroModel.GetComponent<Paperdoll2D>();
            var sr = pd.Body;
            Assert.AreEqual("AdventureKing/PixelSprite", sr.sharedMaterial.shader.name);
            Assert.AreEqual(FilterMode.Point, sr.sprite.texture.filterMode);
            Assert.AreEqual(128, sr.sprite.texture.height);
            StringAssert.StartsWith(n + "_", sr.sprite.name, "sprite named per class");
            var col = motor.GetComponent<Collider2D>();
            Assert.IsTrue(col && col.enabled);
            Assert.That(sr.bounds.size.y, Is.InRange(1.8f, 3.2f), "compact size");
            Assert.LessOrEqual(sr.bounds.size.y * 0.5f, col.bounds.size.y * 1.15f, "drawn figure (about half of the frame) vs collider");

            var anim = world.HeroModel.GetComponentInChildren<Animator>();
            foreach (string state in new[] { "Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill" })
            {
                var clip = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == n + "_" + state);
                Assert.IsNotNull(clip, "sprite clip " + state);
                Assert.IsTrue(UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b => b.propertyName == "m_Sprite"), state);
            }
            yield return new WaitForSeconds(0.8f);
            StringAssert.EndsWith("Idle", Clip(anim));
            yield return Shot(n + "_1_idle");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, true);
            float untilRun = Time.time + 1.5f;         // a one-shot clip may still be finishing: wait for the Run state
            while (!Clip(anim).EndsWith("Run") && Time.time < untilRun) yield return null;
            StringAssert.EndsWith("Run", Clip(anim));
            Assert.Greater(world.HeroModel.transform.localScale.x, 0f);
            yield return Shot(n + "_2_run");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, true);
            yield return new WaitForSeconds(0.6f);
            Assert.Less(world.HeroModel.transform.localScale.x, 0f, "mirrored when facing left");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.5f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
            yield return new WaitForSeconds(0.25f);
            string air = Clip(anim);
            Assert.IsTrue(air.EndsWith("Jump") || air.EndsWith("Fall"), "airborne: " + air);
            yield return Shot(n + "_3_jump");
            yield return new WaitForSeconds(1.2f);
            int impacts = 0;
            var ev = world.HeroModel.GetComponent<CharacterAnimationEvents>();
            ev.AttackImpact += () => impacts++;
            ev.SkillImpact += () => impacts += 100;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
            float until = Time.time + 0.8f;            // short clips (Sát Thủ's fast combo) finish quickly: catch the state while it plays
            while (!Clip(anim).EndsWith("Attack") && Time.time < until) yield return null;
            StringAssert.EndsWith("Attack", Clip(anim));
            yield return Shot(n + "_4_attack");
            yield return new WaitForSeconds(1.0f);
            Assert.GreaterOrEqual(impacts, 1, "attack impact event");
            world.PlayerVisual.PlaySkill();
            yield return new WaitForSeconds(0.45f);
            StringAssert.EndsWith("Skill", Clip(anim));
            yield return Shot(n + "_5_skill");
            yield return new WaitForSeconds(1.3f);
            Assert.GreaterOrEqual(impacts, 100, "skill impact event");
            var health = motor.GetComponent<Health2D>();
            health.TakeDamage(5);
            yield return new WaitForSeconds(0.25f);
            StringAssert.EndsWith("Hit", Clip(anim));
            yield return new WaitForSeconds(0.8f);
            health.TakeDamage(health.MaxHealth * 3);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(health.IsDead);
            StringAssert.EndsWith("Death", Clip(anim));
            yield return Shot(n + "_6_death");
        }

        [UnityTest] public IEnumerator Swordsman_2D_Body() => Run(HeroClass.Swordsman);
        [UnityTest] public IEnumerator Archer_2D_Body() => Run(HeroClass.Archer);
        [UnityTest] public IEnumerator Assassin_2D_Body() => Run(HeroClass.Assassin);
    }
}
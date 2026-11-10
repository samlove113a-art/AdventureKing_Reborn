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
    /// QA showcase of the 12 class skills on the real server in Làng Nấm (rigged Nấm Non as targets): each skill is
    /// cast through the action bar, recorded at 30 fps (QA/SkillVfx/video/*.mp4 after encoding) and captured at four
    /// moments — charge, release, impact, dissipate (QA/SkillVfx/*.png). Skill 3 is cast again with the phone
    /// quality set. Must run WITH a window (batchmode does not show LateUpdate poses). Visual approval is the
    /// owner's — this test only proves the effects run, react and stay inside the frame.
    /// </summary>
    public sealed class SkillVfxShowcaseTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/SkillVfx";
        public static readonly List<string> FrameDirs = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5301);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SkillVfxKit.ForceMobile = false;
            Time.timeScale = 1f;
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
            File.WriteAllLines($"{Dir}/frame_dirs.txt", FrameDirs);
        }

        private Vector2 Pos => motor.transform.position;
        private void Bind() { motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>(); }

        private IEnumerator WalkTo(float x, float timeout = 25f)
        {
            float end = Time.time + timeout;
            while (Mathf.Abs(Pos.x - x) > 0.25f)
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

        private static void Shot(string name, int w = 1920, int h = 1080)
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

        private IEnumerator TakePortal(float x, string expectScene)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && !world.Traveling && Time.time < until) yield return null;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(expectScene);
            yield return new WaitForSeconds(1.5f);
            Bind();
        }

        private IEnumerator EnterMushroomVillage(HeroClass cls, string name)
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("vfx" + (int)cls + Random.Range(1000, 9999), "vfx-pass-2026", cls, name);
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            yield return TakePortal(TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene);
            // hide the quest panel for clean captures (the HUD stays)
            var qp = Object.FindAnyObjectByType<QuestPanel>();
            if (qp) qp.gameObject.SetActive(false);
            until = Time.time + 6f;
            while ((!FarmField.Current || FarmField.Current.Monsters.Count == 0) && Time.time < until) yield return null;
            Assert.IsNotNull(FarmField.Current, "Làng Nấm farm");
        }

        // capture moments (seconds after the director starts): charge, release, impact, dissipate
        private static readonly Dictionary<(HeroClass, int), float[]> Moments = new Dictionary<(HeroClass, int), float[]>
        {
            [(HeroClass.Swordsman, 1)] = new[] { 0.18f, 0.36f, 0.6f, 0.95f }, [(HeroClass.Swordsman, 2)] = new[] { 0.3f, 0.75f, 1.26f, 1.6f }, [(HeroClass.Swordsman, 3)] = new[] { 0.5f, 0.95f, 1.12f, 1.6f },
            [(HeroClass.Mage, 1)] = new[] { 0.3f, 0.58f, 0.78f, 1.2f }, [(HeroClass.Mage, 2)] = new[] { 0.35f, 0.85f, 1.3f, 1.9f }, [(HeroClass.Mage, 3)] = new[] { 0.6f, 1.05f, 1.28f, 1.8f },
            [(HeroClass.Assassin, 1)] = new[] { 0.12f, 0.27f, 0.42f, 0.8f }, [(HeroClass.Assassin, 2)] = new[] { 0.3f, 0.62f, 1.03f, 1.4f }, [(HeroClass.Assassin, 3)] = new[] { 0.2f, 0.7f, 1.33f, 1.7f },
            [(HeroClass.Archer, 1)] = new[] { 0.28f, 0.48f, 0.62f, 1.0f }, [(HeroClass.Archer, 2)] = new[] { 0.5f, 0.85f, 1.32f, 1.8f }, [(HeroClass.Archer, 3)] = new[] { 0.7f, 1.2f, 1.42f, 1.9f },
        };
        private static readonly string[] MomentNames = { "a_charge", "b_release", "c_impact", "d_dissipate" };

        private IEnumerator Cast(HeroClass cls, int slot, string monster, bool mobile)
        {
            FarmField farm = FarmField.Current;
            FarmMonsterView m = farm.Monsters[monster];
            float until = Time.time + 70f;
            while (!m.Alive && Time.time < until) yield return null; // respawn if needed
            SkillDefDto def = SkillBook.Skill(cls, slot);
            Assert.IsNotNull(def, "skill defs loaded");
            float stand = def.shape == "self" ? 0.9f : Mathf.Min(2.2f, def.range - 0.8f);
            // wait in the village square (safe zone: nothing can hit the hero) for full HP / MP and the slot's cooldown
            yield return WalkTo(TownLayout.Ch1Village.SafeZone.y - 3f);
            var mana = motor.GetComponent<PlayerMana>();
            var hp = motor.GetComponent<Health2D>();
            until = Time.time + 60f;
            while ((Time.time < farm.CooldownEnds[slot - 1] || mana.Current < mana.Max * 0.95f || hp.CurrentHealth < hp.MaxHealth * 0.9f) && Time.time < until) yield return null;
            int reactions = m.Reactions, played = SkillVfxDirector.Played;
            SkillVfxKit.ForceMobile = mobile;
            string tag = $"{(int)cls + 1}{cls}_{slot}{(mobile ? "_phone" : "")}";
            var bar = Object.FindAnyObjectByType<HeroActionBar>();
            GameplayRecorder rec = null;
            for (int attempt = 0; attempt < 4 && SkillVfxDirector.Played == played; attempt++)
            {
                yield return WalkTo(m.Position.x - stand); // the monster wanders around its spawn: aim at where it is now
                yield return new WaitForSeconds(0.25f);
                if (rec == null) rec = GameplayRecorder.Begin("skill_" + tag, 1280, 720);
                bar.TryCast(slot, false);
                until = Time.time + 1.5f;
                while (SkillVfxDirector.Played == played && Time.time < until) yield return null;
            }
            Assert.Greater(SkillVfxDirector.Played, played, $"{cls} skill {slot} accepted by the server; {farm.LastSkillError}");
            float t0 = Time.time, maxShake = 0f;
            float[] moments = Moments[(cls, slot)];
            int next = 0;
            while (Time.time - t0 < 2.7f)
            {
                maxShake = Mathf.Max(maxShake, CameraFollow2D.ShakeNow);
                if (next < moments.Length && Time.time - t0 >= moments[next] && (!mobile || next == 2))
                {
                    yield return new WaitForEndOfFrame();
                    Shot($"{tag}_{MomentNames[next]}", mobile ? 1600 : 1920, mobile ? 740 : 1080);
                    next++;
                }
                else if (next < moments.Length && mobile && next != 2 && Time.time - t0 >= moments[next]) next++;
                yield return null;
            }
            rec.End();
            FrameDirs.Add(rec.Directory + "|" + tag);
            SkillVfxKit.ForceMobile = false;
            Assert.AreEqual($"{cls}_{slot}", SkillVfxDirector.LastSkill);
            Assert.Greater(m.Reactions, reactions, $"{cls} skill {slot}: the target reacted to the hit");
            Assert.Greater(maxShake, 0.01f, $"{cls} skill {slot}: camera feedback");
            Assert.AreEqual(1f, Time.timeScale, "hit-stop released");
            foreach (string k in new[] { "_cast", "_hit" })
                Assert.IsNotNull(Resources.Load<AudioClip>("SkillVfx/Audio/" + Key(cls, slot) + k), "skill sound " + Key(cls, slot) + k);
        }

        private static string Key(HeroClass c, int slot) => c switch { HeroClass.Swordsman => "sw", HeroClass.Mage => "mg", HeroClass.Assassin => "as", _ => "ar" } + slot;

        private IEnumerator Showcase(HeroClass cls, string name)
        {
            yield return EnterMushroomVillage(cls, name);
            yield return Cast(cls, 1, "c1m1_n1", false);
            yield return Cast(cls, 2, "c1m1_n2", false);
            yield return Cast(cls, 3, "c1m1_n4", false);
            yield return Cast(cls, 3, "c1m1_n6", true);
        }

        [UnityTest] public IEnumerator Swordsman_Skills() => Showcase(HeroClass.Swordsman, "Kiếm Gió Thử");
        [UnityTest] public IEnumerator Mage_Skills() => Showcase(HeroClass.Mage, "Pháp Hỏa Thử");
        [UnityTest] public IEnumerator Assassin_Skills() => Showcase(HeroClass.Assassin, "Bóng Đêm Thử");
        [UnityTest] public IEnumerator Archer_Skills() => Showcase(HeroClass.Archer, "Tiễn Phong Thử");
    }
}

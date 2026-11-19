using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// 2D chibi Mage (baked from the 3D model) on the real server: same login / hero / world, the body is a SpriteRenderer driven by
    /// the same Animator parameters, flips left / right, plays Idle / Run / Jump / Attack / Skill / Hit / Death, and the 3D body
    /// is untouched. Game View captures: QA/Chibi2D/*.png
    /// </summary>
    public sealed class Chibi2DMageTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Chibi2D";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            Directory.CreateDirectory(Dir);
            server = new TestServer(5295);
            yield return server.Start();
            HeroVisualStyle.Override = "2d";
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            HeroVisualStyle.Override = "3d";
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();   // -batchmode never reaches end-of-frame
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

        private static string State(Animator a) => a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault() ?? "";

        [UnityTest]
        public IEnumerator Mage_Uses_The_2D_Chibi_Body_And_Every_State_Plays()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("c2d" + Random.Range(1000, 9999), "chibi-pass-2026", HeroClass.Mage, "Pháp Sư 2D");
            var world = AdventureWorldSession.Current;
            motor = world.Player; input = motor.GetComponent<PlayerInput2D>();
            Assert.IsNotNull(world.HeroModel.GetComponent<Chibi2DMarker>(), "the hero body is the 2D chibi prefab: " + world.HeroModel.name);
            Assert.AreEqual(0, world.HeroModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, "no 3D mesh on the 2D body");
            var sr = world.HeroModel.GetComponentInChildren<SpriteRenderer>();
            Assert.IsTrue(sr && sr.sprite, "sprite shown");
            Assert.AreEqual(FilterMode.Point, sr.sprite.texture.filterMode, "pixel art is drawn with point filtering (crisp, never smoothed)");
            Assert.AreEqual(192, sr.sprite.texture.height, "192 px pixel-art frames (new set)");
            foreach (string fx in new[] { "cast_circle", "arcane_orb", "hit_spark" })
            {
                var prefab = Resources.Load<GameObject>("Chibi2D/Fx/" + fx);
                Assert.IsNotNull(prefab, "skill effect asset " + fx);
                Assert.Greater(prefab.GetComponent<SpriteFlipbook>().Frames.Length, 3, fx + " frames");
            }
            var body = motor.GetComponent<Collider2D>();
            Assert.IsTrue(body && body.enabled, "gameplay collider untouched by the visual swap");
            Assert.That(body.bounds.size.y, Is.InRange(0.8f, 2.2f), "collider height unchanged");
            var anim = world.HeroModel.GetComponentInChildren<Animator>();
            var visual = world.PlayerVisual;
            Assert.IsTrue(visual.IsSprite);

            // every state clip is a sprite animation (no leftover bone clip drives the sprite)
            foreach (string state in new[] { "Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill" })
            {
                var clip = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Mage_" + state);
                Assert.IsNotNull(clip, "sprite clip for " + state);
                Assert.IsTrue(UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b => b.propertyName == "m_Sprite"), state + " animates the sprite");
            }
            yield return new WaitForSeconds(1.0f);
            StringAssert.EndsWith("Idle", State(anim));
            yield return Shot("01_idle");
            Sprite idle0 = sr.sprite;

            // run right then left: the sprite animates, and mirrors with the facing
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, true);
            yield return new WaitForSeconds(0.7f);
            StringAssert.EndsWith("Run", State(anim));
            Assert.Greater(world.HeroModel.transform.localScale.x, 0f, "facing right = unmirrored");
            yield return Shot("02_run_right");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, true);
            yield return new WaitForSeconds(0.7f);
            Assert.Less(world.HeroModel.transform.localScale.x, 0f, "facing left = mirrored");
            yield return Shot("03_run_left");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.6f);

            // jump
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
            yield return new WaitForSeconds(0.25f);
            string air = State(anim);
            Assert.IsTrue(air.EndsWith("Jump") || air.EndsWith("Fall"), "airborne clip: " + air);
            yield return Shot("04_jump");
            yield return new WaitForSeconds(1.2f);

            // attack + the animation event still fires (gameplay timing hooks)
            int impacts = 0;
            var ev = world.HeroModel.GetComponent<CharacterAnimationEvents>();
            ev.AttackImpact += () => impacts++;
            ev.SkillImpact += () => impacts += 100;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
            yield return new WaitForSeconds(0.3f);
            StringAssert.EndsWith("Attack", State(anim));
            yield return Shot("05_attack");
            yield return new WaitForSeconds(0.9f);
            Assert.GreaterOrEqual(impacts, 1, "AttackImpact event raised by the sprite clip");

            // skill
            visual.PlaySkill();
            yield return new WaitForSeconds(0.4f);
            StringAssert.EndsWith("Skill", State(anim));
            yield return Shot("06_skill");
            yield return new WaitForSeconds(1.2f);
            Assert.GreaterOrEqual(impacts, 100, "SkillImpact event raised by the sprite clip");

            // hit + death + revive through the same Health2D the server drives
            var health = motor.GetComponent<Health2D>();
            health.TakeDamage(5);
            yield return new WaitForSeconds(0.25f);
            StringAssert.EndsWith("Hit", State(anim));
            yield return Shot("07_hit");
            yield return new WaitForSeconds(0.8f);
            health.TakeDamage(health.MaxHealth * 3);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(health.IsDead);
            StringAssert.EndsWith("Death", State(anim));
            yield return Shot("08_death");
            Assert.AreEqual(HeroClass.Mage, world.Character.heroClass, "same character, same server data");
            Assert.AreNotEqual(idle0, null);
        }

        [UnityTest]
        public IEnumerator Pixel_Material_Is_Unlit_Scale_Is_Compact_And_Paperdoll_Swaps_Replace_Never_Stack()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("pd" + Random.Range(1000, 9999), "chibi-pass-2026", HeroClass.Mage, "Pháp Sư Đồ");
            var world = AdventureWorldSession.Current;
            motor = world.Player; input = motor.GetComponent<PlayerInput2D>();
            var pd = world.HeroModel.GetComponent<Paperdoll2D>();
            Assert.IsNotNull(pd, "paper doll on the 2D body");
            var sr = pd.Body;

            // 1) material: unlit pixel shader, no emission / HDR, brightness capped under the bloom threshold (no glow haze)
            Assert.AreEqual("AdventureKing/PixelSprite", sr.sharedMaterial.shader.name);
            Assert.IsFalse(sr.sharedMaterial.HasProperty("_EmissionColor"), "no emission");
            Assert.LessOrEqual(sr.sharedMaterial.GetFloat("_MaxLuma"), 0.86f, "under AK_WorldLook bloom threshold 0.86");
            Assert.AreEqual(Color.white, sr.color, "no tint");

            // 2) compact scale: the whole 2D body is about as tall as the gameplay collider (not a screen-filling giant)
            var col = motor.GetComponent<Collider2D>();
            yield return new WaitForSeconds(0.6f);
            Assert.That(sr.bounds.size.y, Is.InRange(1.8f, 3.2f), "sprite height in metres: " + sr.bounds.size.y);
            Assert.LessOrEqual(sr.bounds.size.y * 0.5f, col.bounds.size.y * 1.15f, "the drawn figure (about half of the frame) is not taller than the collider");
            Assert.AreEqual(1, pd.VisibleRenderers(), "only the base body at first");
            yield return Shot("10_scale_compact");

            // 3) outfit set from the paper-doll resources: replaces the body, follows the animation frame by frame
            Assert.IsTrue(pd.Equip(PaperSlot.Outfit, "fire_mage", true));
            yield return null;
            Assert.IsFalse(pd.Body.enabled, "base body hidden while a full outfit is worn");
            Assert.AreEqual(1, pd.VisibleRenderers(), "exactly one sprite on screen");
            Transform outfitLayer = world.HeroModel.transform.Find("Paper_Outfit");
            var outfitSr = outfitLayer.GetComponent<SpriteRenderer>();
            Texture fireTex = outfitSr.sprite.texture;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, true);
            yield return new WaitForSeconds(0.55f);
            Assert.AreEqual(pd.Body.sprite.name, outfitSr.sprite.name, "outfit layer shows the same clip + frame as the animated body");
            yield return Shot("11_outfit_fire_running");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            yield return new WaitForSeconds(0.4f);

            // 4) swap to another outfit: the old sprite is gone, still ONE layer and ONE visible sprite
            Assert.IsTrue(pd.Equip(PaperSlot.Outfit, "frost_mage", true));
            yield return null;
            Assert.AreEqual(1, world.HeroModel.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Paper_Outfit"), "one outfit layer, never two");
            Assert.AreEqual(1, pd.VisibleRenderers());
            Assert.AreNotEqual(fireTex, outfitSr.sprite.texture, "the new outfit replaced the old sprite");
            Assert.IsFalse(world.HeroModel.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sprite && r.sprite.texture == fireTex), "no trace of the old outfit left");
            yield return Shot("12_outfit_frost");

            // 5) the server outfit drives it: a known set equips, an unknown set clears
            pd.ApplyLoadout(new OutfitLoadout { set = "fire_mage" });
            Assert.AreEqual("fire_mage", pd.Equipped(PaperSlot.Outfit));
            pd.ApplyLoadout(new OutfitLoadout { set = "something_else" });
            yield return null;
            Assert.IsNull(pd.Equipped(PaperSlot.Outfit));
            Assert.IsTrue(pd.Body.enabled, "base body is back");
            Assert.AreEqual(1, pd.VisibleRenderers());

            // 6) part slots (hat / weapon): one renderer per slot, a new item replaces the previous one
            Sprite Dot(Color c)
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[64]; for (int i = 0; i < 64; i++) px[i] = c; tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0f), 110f);
            }
            Sprite red = Dot(Color.red), green = Dot(Color.green);
            var redSet = Paperdoll2D.Clips.ToDictionary(c => c, c => new[] { red });
            var greenSet = Paperdoll2D.Clips.ToDictionary(c => c, c => new[] { green });
            pd.EquipRuntime(PaperSlot.Hat, "hat_red", redSet);
            yield return null;
            pd.EquipRuntime(PaperSlot.Hat, "hat_green", greenSet);
            yield return null;
            Assert.AreEqual(1, world.HeroModel.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Paper_Hat"), "one hat layer");
            Assert.AreSame(green, world.HeroModel.transform.Find("Paper_Hat").GetComponent<SpriteRenderer>().sprite, "the new hat replaced the old one");
            Assert.AreEqual(2, pd.VisibleRenderers(), "body + one hat");
            pd.Clear(PaperSlot.Hat);
            Assert.AreEqual(1, pd.VisibleRenderers());
            Assert.IsFalse(pd.Equip(PaperSlot.Weapon, "no_such_item"), "missing art changes nothing");
            Assert.AreEqual(1, pd.VisibleRenderers());
        }
        [UnityTest]
        public IEnumerator Default_Style_Is_2D_And_The_3D_Models_Stay_Selectable()
        {
            HeroVisualStyle.Override = null;
            Assert.IsTrue(HeroVisualStyle.Chibi2D, "2D chibi is the default style");
            HeroVisualStyle.Override = "3d";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("c3d" + Random.Range(1000, 9999), "chibi-pass-2026", HeroClass.Mage, "Pháp Sư 3D");
            var world = AdventureWorldSession.Current;
            Assert.IsNull(world.HeroModel.GetComponent<Chibi2DMarker>());
            Assert.Greater(world.HeroModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 0, "3D body intact");
            HeroVisualStyle.Override = "2d";
            var archer3D = world.HeroPrefabs[(int)HeroClass.Archer];
            Assert.AreNotSame(archer3D, HeroVisualStyle.Resolve(HeroClass.Archer, archer3D), "2D style picks the 2D body");
            HeroVisualStyle.Override = "3d";
            Assert.AreSame(archer3D, HeroVisualStyle.Resolve(HeroClass.Archer, archer3D), "3D style keeps the original 3D prefab");
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests.PlayMode
{
    /// <summary>Pets move by style (baby dragons and animals walk, big dragons fly, the Eastern dragon glides in waves) and keep fixed relative sizes. Pictures come from the owner's database.</summary>
    public sealed class PetMotionTests
    {
        private static Pet2DBody Make(string id, float x)
        {
            GameObject go = Pet2DBody.Create(id);
            go.transform.position = new Vector3(x, 0f, 0f);
            var b = go.GetComponent<Pet2DBody>();
            b.Style = Pet2DBody.StyleOf(id);
            return b;
        }

        private static float Width(Pet2DBody b) => b.Renderer.sprite ? b.Renderer.sprite.bounds.size.x : 0f;
        private static float Height(Pet2DBody b) => b.Renderer.sprite ? b.Renderer.sprite.bounds.size.y : 0f;

        [UnityTest]
        public IEnumerator Pets_Walk_Fly_Or_Glide_And_Keep_Their_Relative_Sizes()
        {
            string[] ids = { "pet_chick", "pet_jade_dragon", "pet_fire_dragon", "pet_ember_tiger", "pet_night_horse", "pet_phoenix", "pet_war_dragon", "pet_ink_dragon" };
            var pets = ids.Select((id, i) => Make(id, 2.5f + i * 1.6f + (i >= 6 ? 0.9f : 0f) + (i == 7 ? 1.0f : 0f))).ToDictionary(b => b.gameObject.name.Replace("_Visual2D", ""), b => b);
            foreach (Pet2DBody b in pets.Values) Assert.IsNotNull(b.Renderer.sprite, b.name + " has its picture");

            Assert.AreEqual(Pet2DBody.MoveStyle.Walk, pets["pet_jade_dragon"].Style, "baby dragons walk");
            Assert.AreEqual(Pet2DBody.MoveStyle.Walk, pets["pet_ember_tiger"].Style);
            Assert.AreEqual(Pet2DBody.MoveStyle.Fly, pets["pet_war_dragon"].Style, "the big dragon flies");
            Assert.AreEqual(Pet2DBody.MoveStyle.Glide, pets["pet_ink_dragon"].Style, "the Eastern dragon glides");

            // heights above the ground over half a second of moving
            float walkMax = 0f, flyMin = 9f, glideMin = 9f, glideMax = 0f;
            for (int i = 0; i < 30; i++)
            {
                walkMax = Mathf.Max(walkMax, pets["pet_jade_dragon"].Animate(false, true, i * 0.4f, i * 0.1f, 1));
                float fly = pets["pet_war_dragon"].Animate(false, true, i * 0.4f, i * 0.1f, 1);
                flyMin = Mathf.Min(flyMin, fly);
                float g = pets["pet_ink_dragon"].Animate(false, true, i * 0.4f, i * 0.1f, 1);
                glideMin = Mathf.Min(glideMin, g); glideMax = Mathf.Max(glideMax, g);
                yield return null;
            }
            var wave = pets["pet_ink_dragon"].transform.Find("Wave");
            Assert.IsNotNull(wave, "the Eastern dragon is cut into slices for the slither");
            Assert.AreEqual(16, wave.childCount);
            float y0 = wave.GetChild(15).localPosition.y; yield return new WaitForSeconds(0.25f); pets["pet_ink_dragon"].Animate(false, true, 0f, 0f, 1);
            Assert.Greater(Mathf.Abs(y0 - wave.GetChild(15).localPosition.y), 0.0005f, "the wave travels along the body");
            Assert.AreEqual(0f, walkMax, 0.001f, "a walker stays on the ground");
            Assert.Greater(flyMin, 0.6f, "the flyer hovers well above the ground");
            Assert.Greater(glideMax - glideMin, 0.15f, "the Eastern dragon glides up and down in S waves");
            Assert.Greater(pets["pet_war_dragon"].Animate(true, false, 0f, 0f, 1), -1f);
            Assert.Less(pets["pet_war_dragon"].Animate(true, false, 0f, 0f, 1), 0.3f, "a hungry flyer sinks to the ground");

            // sizes: ordinary pets alike, tiger = horse, the war dragon bigger than the Eastern dragon
            float baseSide = Mathf.Max(Width(pets["pet_chick"]), Height(pets["pet_chick"]));
            foreach (string id in new[] { "pet_jade_dragon", "pet_fire_dragon", "pet_phoenix" })
                Assert.AreEqual(baseSide, Mathf.Max(Width(pets[id]), Height(pets[id])), 0.03f, id + " the same size as the other pets");
            Assert.AreEqual(Width(pets["pet_ember_tiger"]), Width(pets["pet_night_horse"]), 0.03f, "tiger and horse the same width");
            Assert.Greater(Width(pets["pet_war_dragon"]), Width(pets["pet_ink_dragon"]) * 1.3f, "the war dragon is bigger than the Eastern dragon");

            // the featured pets have three pictures: a four-step cycle A B C B
            foreach (string id in new[] { "pet_ember_tiger", "pet_night_horse", "pet_snow_wolf", "pet_war_dragon", "pet_phoenix" })
                Assert.AreEqual(3, pets.ContainsKey(id) ? pets[id].Frames.Length : Pet2DBody.Create(id).GetComponent<Pet2DBody>().Frames.Length, id + ": three animation frames (four-step cycle)");

            // contact picture for the owner: pets beside a hero
            Directory.CreateDirectory("QA/Pets");
            var camGo = new GameObject("PetCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 3.1f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.28f, 0.3f, 0.42f);
            camGo.transform.position = new Vector3(9.4f, 2.0f, -10f);
            GameObject heroGo = Object.Instantiate(Resources.Load<GameObject>("Chibi2D/Mage_Visual2D"));
            heroGo.transform.position = new Vector3(1.1f, 0f, 0f);
            foreach (Pet2DBody b in pets.Values) b.transform.position += new Vector3(1.0f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                foreach (var kv in pets) { var b = kv.Value; float h = b.Animate(false, true, Time.time * 8f, b.transform.position.x, 1); b.transform.position = new Vector3(b.transform.position.x, h, 0f); }
                yield return null;
            }
            var rt = new RenderTexture(1600, 560, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 560, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 560), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            File.WriteAllBytes("QA/Pets/size_and_motion.png", tex.EncodeToPNG());
            Object.Destroy(tex); Object.Destroy(camGo); Object.Destroy(heroGo);
            foreach (Pet2DBody b in pets.Values) Object.Destroy(b.gameObject);
        }
    }
}

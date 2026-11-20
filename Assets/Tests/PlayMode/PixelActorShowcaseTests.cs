using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>The pixel-art NPCs, monsters and bosses straight from Resources/Chibi2D/Actors (no server): every prefab loads, its Animator has
    /// the states the game plays (NpcActor: Idle/Walk/Wave/Talk/Bow, FarmMonsterView: Idle/Walk/Attack/Hit/Death, bosses also Skill*), each state
    /// really plays a sprite clip with a sprite on screen, and the in-game height is in the range of the old art. Captures: QA/PixelActors/*.png</summary>
    public sealed class PixelActorShowcaseTests
    {
        private static readonly string[] NpcStates = { "Idle", "Walk", "Wave", "Talk", "Bow" };
        private static readonly string[] MonsterStates = { "Idle", "Walk", "Attack", "Hit", "Death" };

        // name -> (expected states beyond Idle, old in-game height in metres)
        private static readonly (string name, string[] states, float height)[] Actors =
        {
            ("NpcElder", NpcStates, 1.2f), ("NpcSmith", NpcStates, 1.2f), ("NpcStorekeeper", NpcStates, 1.2f), ("NpcCaptain", NpcStates, 1.3f),
            ("NpcOfficiant", NpcStates, 1.2f), ("NpcMusician", NpcStates, 1.2f), ("NpcMushroomElder", NpcStates, 1.3f),
            ("MonsterNamNon", MonsterStates, 0.82f), ("PollenSlime", new[] { "Idle" }, 0.51f), ("PuffSlime", new[] { "Idle" }, 0.43f), ("MossSlime", new[] { "Idle" }, 0.51f),
            ("GlowMoss", MonsterStates, 0.84f), ("FlowerBee", MonsterStates, 0.81f), ("CrystalBat", MonsterStates, 0.7f), ("JadeTurtle", MonsterStates, 0.74f),
            ("ShadowLeaf", MonsterStates, 0.6f), ("ToxicSlime", MonsterStates, 0.71f), ("ToxicElite", MonsterStates, 1.47f), ("StoneGuard", MonsterStates, 1.25f),
            ("RoyalGuard", MonsterStates, 0.91f),
            ("DarkMushroom", MonsterStates.Concat(new[] { "Skill" }).ToArray(), 2.04f), ("MushroomChief", MonsterStates.Concat(new[] { "Skill" }).ToArray(), 3.4f),
            ("JadeGuardian", MonsterStates.Concat(new[] { "Skill" }).ToArray(), 3.4f), ("MushroomKing", MonsterStates.Concat(new[] { "Skill", "Skill2", "Skill3" }).ToArray(), 3.7f),
        };

        private Camera cam;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory("QA/PixelActors");
            var go = new GameObject("PixelActorCam");
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.34f, 0.40f);
            cam.nearClipPlane = -20f; cam.farClipPlane = 20f;
            spawned.Add(go);
        }

        [TearDown]
        public void TearDown() { foreach (var g in spawned) if (g) Object.Destroy(g); spawned.Clear(); }

        private IEnumerator Shot(string file, SpriteRenderer sr)
        {
            var b = sr.bounds;
            cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);
            cam.orthographicSize = Mathf.Max(2.0f, Mathf.Max(b.extents.x, b.extents.y) * 1.05f);   // never below 4 m of view: same px per metre for small actors
            if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();
            var rt = new RenderTexture(384, 384, 24);
            cam.targetTexture = rt; cam.aspect = 1f; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(384, 384, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); tex.Apply();
            var px = tex.GetPixels32();
            var bg = px[0];
            int minY = 384, maxY = -1;
            for (int y = 0; y < 384; y++)
                for (int x = 0; x < 384; x++)
                {
                    var c = px[y * 384 + x];
                    if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 24) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
                }
            measuredH = maxY < 0 ? 0f : (maxY - minY + 1) * (cam.orthographicSize * 2f / 384f);   // metres of visible content
            File.WriteAllBytes($"QA/PixelActors/{file}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(tex); rt.Release();
        }

        private float measuredH;   // visible content height (metres) of the last Shot, measured from the rendered pixels

        private static string Clip(Animator a) => a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault() ?? "";

        [UnityTest]
        public IEnumerator Every_Pixel_Actor_Loads_And_Plays_All_Its_States()
        {
            var report = new List<string>();
            foreach (var (name, states, expectHeight) in Actors)
            {
                var prefab = Resources.Load<GameObject>("Chibi2D/Actors/" + name + "_Visual2D");
                Assert.IsNotNull(prefab, name + " prefab in Resources/Chibi2D/Actors");
                var go = Object.Instantiate(prefab, new Vector3(0f, 0f, 0f), Quaternion.identity);
                spawned.Add(go);
                var anim = go.GetComponent<Animator>();
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                Assert.IsNotNull(anim, name + " Animator"); Assert.IsNotNull(sr, name + " SpriteRenderer");
                Assert.AreEqual(FilterMode.Point, sr.sprite.texture.filterMode, name + " point filtered");
                yield return null;

                float idleH = 0f;
                foreach (string state in states)
                {
                    Assert.IsTrue(anim.HasState(0, Animator.StringToHash(state)), name + " has state " + state);
                    anim.Play(state, 0, 0f);
                    yield return null; yield return null;
                    yield return new WaitForSeconds(0.12f);
                    Assert.IsNotNull(sr.sprite, name + "/" + state + " shows a sprite");
                    StringAssert.StartsWith(name + "_" + state, Clip(anim), name + " plays its own " + state + " clip");
                    if (state == "Idle" || state == states[states.Length - 1] || state.StartsWith("Skill")) yield return Shot(name + "_" + state, sr);
                    if (state == "Idle") idleH = measuredH;
                }
                Assert.Greater(idleH, 0.1f, name + " idle has a size");
                float ratio = idleH / expectHeight;
                report.Add($"{name,-18} idle {idleH:0.00} m (old {expectHeight:0.00}) x{ratio:0.00}  states {string.Join("/", states)}");
                Assert.That(ratio, Is.InRange(0.5f, 1.6f), $"{name} in-game height {idleH:0.00} m vs old {expectHeight:0.00} m");
                Object.Destroy(go);
                yield return null;
            }
            File.WriteAllLines("QA/PixelActors/report.txt", report);
            Debug.Log("[PIXELACTORS]\n" + string.Join("\n", report));
        }
    }
}


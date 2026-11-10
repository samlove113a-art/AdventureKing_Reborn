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
    /// The 180 s cinematic wedding, checked moment by moment without waiting three minutes: the player's hero (real
    /// login, real Wedding Hall) and a second hero body driven by the same timeline, with the clock jumping to each key
    /// moment and playing a few seconds continuously so IK, the kiss servo and the props settle exactly as in the full
    /// run. Asserts the interactions really happen (ring on the finger, faces touching, blade in the cake, rims
    /// touching, wine level dropping, hands staying on the props) for two class pairings.
    /// Screenshots: QA/Wedding180/*.png — run windowed for images that include the procedural poses.
    /// </summary>
    public sealed class WeddingCinematicTests
    {
        private TestServer server;
        private const string Dir = "QA/Wedding180";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5293, starterGold: 100000, devWelfare: true);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (WeddingCinematic.Current) WeddingCinematic.Current.Stop("test end");
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        [UnityTest] public IEnumerator Swordsman_And_Mage_Key_Moments() => Run(HeroClass.Swordsman, HeroClass.Mage, "sw_mage");
        [UnityTest] public IEnumerator Archer_And_Assassin_Key_Moments() => Run(HeroClass.Archer, HeroClass.Assassin, "ar_as");

        private static CeremonyState Ceremony(string aId, string aName, string bId, string bName)
        {
            var ids = new[] { ("entrance", 20000), ("opening", 25000), ("vows", 25000), ("rings", 30000), ("kiss", 20000), ("cake", 25000), ("toast", 20000), ("finale", 15000) };
            var steps = new List<CeremonyStepState>();
            long at = 0;
            foreach (var (id, ms) in ids) { steps.Add(new CeremonyStepState { id = id, startMs = at, ms = ms }); at += ms; }
            return new CeremonyState { id = "preview", state = "running", aId = aId, aName = aName, bId = bId, bName = bName, startMs = 0, endMs = at, steps = steps.ToArray() };
        }

        private static IEnumerator Shot(string name)
        {
            var gate = new GameObject("ShotGate").AddComponent<LateShot>();
            gate.Path = $"{Dir}/{name}.png";
            while (!gate.Done) yield return null;
            Object.Destroy(gate.gameObject);
        }

        [DefaultExecutionOrder(32000)]
        private sealed class LateShot : MonoBehaviour
        {
            public string Path; public bool Done;
            private void LateUpdate()
            {
                if (Done) return;
                var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
                GameplayRecorder.Capture(Camera.main, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                File.WriteAllBytes(Path, tex.EncodeToPNG());
                RenderTexture.active = null; Destroy(tex); rt.Release();
                Done = true;
            }
        }

        private IEnumerator Run(HeroClass ca, HeroClass cb, string tag)
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("wed" + tag + Random.Range(100, 999), "wed-pass-2026-" + tag, ca, "Chú Rể Long");
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            // walk through the town portal into the Wedding Hall (server-validated travel)
            var world = AdventureWorldSession.Current;
            var motor = world.Player;
            var rb = motor.GetComponent<Rigidbody2D>();
            var input = motor.GetComponent<PlayerInput2D>();
            float px = TownLayout.Hub.Portals.First(p => p.Id == "hub_wedding_hall").X;
            rb.position = new Vector2(px, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.WeddingHall.Scene);
            yield return new WaitForSeconds(1.0f);
            world = AdventureWorldSession.Current;

            // the partner: a second hero body of another class, driven by the same timeline
            var peer = new PeerState { id = "partner_" + tag, name = "Cô Dâu Mai", heroClass = (int)cb, x = -12f, y = 0.8f };
            RemoteAvatar r = RemoteAvatar.Create(peer, world.HeroPrefabs[(int)cb], world.HeroScale, null, null);
            yield return null;
            var host = Object.FindAnyObjectByType<WeddingCeremony>();
            CeremonyState cs = Ceremony(world.Character.characterId, "Chú Rể Long", peer.id, peer.name);
            WeddingCinematic.Setup setup = host.Setup(cs);
            float clock = -0.5f;
            setup.Clock = () => clock;
            setup.Preview = true;
            WeddingCinematic.Actor a = WeddingCinematic.Wrap(world.Character.characterId, "Chú Rể Long");
            WeddingCinematic.Actor b = WeddingCinematic.WrapRemote(r, peer.id, peer.name);
            Assert.IsNotNull(a, "player hero wrapped");
            WeddingCinematic cin = WeddingCinematic.Play(host.gameObject, setup, a, b);
            yield return new WaitForSeconds(0.8f);
            Debug.Log("[wedding] " + string.Join(" | ", cin.Events));

            // continuous slices around each key moment: (from, to, shot at, name)
            var slices = new (float from, float to, float shotAt, string name)[]
            {
                (8f, 12.2f, 12f, "01_entrance_walk"),
                (21f, 24.2f, 24f, "02_opening"),
                (45f, 48.2f, 48f, "03_vows_hand_in_hand"),
                (74f, 83.4f, 81.6f, "04_ring_sliding_on"),
                (83.4f, 83.6f, 83.5f, "04b_ring_on_finger"),
                (90f, 94.6f, 94.4f, "05_second_ring"),
                (103.5f, 110.2f, 109.2f, "06_kiss"),
                (114f, 116.2f, 116f, "07_after_kiss_heart"),
                (125f, 133.2f, 132.9f, "08_knife_in_cake"),
                (133.2f, 140.2f, 138.6f, "09_slice_served"),
                (145f, 151.6f, 151.5f, "10_glasses_clink"),
                (151.6f, 157.8f, 156.2f, "11_drinking"),
                (170f, 174.2f, 174f, "12_celebration"),
            };
            foreach (var (from, to, shotAt, name) in slices)
            {
                float start = Time.time;
                clock = from;
                bool shot = false;
                while (clock < to)
                {
                    clock = from + (Time.time - start);
                    if (!shot && clock >= shotAt)
                    {
                        clock = shotAt;
                        yield return Shot($"{tag}_{name}");
                        cin.Events.Add("shot " + name + ": " + cin.DebugState());
                        shot = true;
                        start = Time.time - (shotAt - from);
                    }
                    yield return null;
                }
            }
            string facts = $"arm/scale: {cin.Events.FirstOrDefault(e => e.StartsWith("measure"))}\n" +
                           $"ringOnB={cin.RingOnB} ringOnA={cin.RingOnA} ringToFinger={cin.RingToFingerB:0.000}\n" +
                           $"kissMin={cin.KissFaceDistanceMin:0.000} kissHeld={cin.KissHeldSeconds:0.0}s\n" +
                           $"knifeDepth={cin.KnifeDepthMax:0.000} cuts={cin.Cuts} sliceOnPlate={cin.SliceOnPlate}\n" +
                           $"clink={cin.Clinked} rimGap={cin.ClinkRimDistanceMin:0.000} wineA={cin.WineLevelA:0.00} wineB={cin.WineLevelB:0.00} mouthA={cin.GobletToMouthMinA:0.000} mouthB={cin.GobletToMouthMinB:0.000}\n" +
                           $"gripErrorMax={cin.GripErrorMax:0.000} ({cin.GripErrorWhere}) handsHeld={cin.HandsHeldSeconds:0.0}s";
            File.WriteAllText($"{Dir}/{tag}_facts.txt", facts + "\n" + string.Join("\n", cin.Events));
            Debug.Log("[wedding facts] " + facts);
            cin.Stop("done");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(cin.RingOnB && cin.RingOnA, "both rings on fingers\n" + facts);
            Assert.Less(cin.RingToFingerB, 0.03f, "ring sits on the finger\n" + facts);
            Assert.Greater(cin.KissHeldSeconds, 1.2f, "faces met for the kiss\n" + facts);
            Assert.AreEqual(2, cin.Cuts, "two cuts\n" + facts);
            Assert.Greater(cin.KnifeDepthMax, 0.05f, "blade went into the cake\n" + facts);
            Assert.IsTrue(cin.SliceOnPlate, "slice served\n" + facts);
            Assert.IsTrue(cin.Clinked && cin.ClinkRimDistanceMin < 0.015f && cin.ClinkRimDistanceMin > -0.035f, "goblet rims touched (no deep overlap)\n" + facts);
            Assert.Less(Mathf.Max(cin.WineLevelA, cin.WineLevelB), 0.6f, "both drank\n" + facts);
            Assert.Less(Mathf.Max(cin.GobletToMouthMinA, cin.GobletToMouthMinB), 0.08f, "goblets at the lips\n" + facts);
            Assert.Less(cin.GripErrorMax, 0.1f, "hands stay on what they hold\n" + facts);
            Assert.IsTrue(Camera.main.GetComponent<CameraFollow2D>().enabled, "camera handed back");
            Assert.IsTrue(input.enabled, "controls handed back");
            Assert.IsTrue(rb.simulated, "physics handed back");
        }
    }
}

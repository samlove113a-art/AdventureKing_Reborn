using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Chương 1 · Map 1 Làng Nấm through the real server: hub → Đồng Cỏ Gió → east "Cổng Pha Lê" → Làng Nấm; safe
    /// village square with Trưởng Làng Nấm (quest talk), rigged Nấm Non fought on the server, chapter quest progress
    /// read back fresh from the server, own music, minimap safe zone, no missing scripts. Game View captures:
    /// QA/Chapter1/*.png (run with a window: batchmode does not show LateUpdate poses).
    /// Fixture: the starter chain (main_01..main_10) is left out of the quest config so the chapter quests are open
    /// for a new hero (the chain order itself is covered by the server tests).
    /// </summary>
    public sealed class Chapter1Tests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Chapter1";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            string fixtureDir = Path.Combine(Path.GetTempPath(), "ak_ch1_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureDir);
            string quests = Path.Combine(fixtureDir, "quests.ch1.json");
            File.WriteAllLines(quests, ChapterQuestFixture(File.ReadAllLines(Path.Combine(TestServer.ServerConfigDir, "quests.json"))));
            server = new TestServer(5291, extraArgs: $"--AdventureKing:QuestConfigPath=\"{quests}\"");
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        /// <summary>quests.json without the starter main quests (one object per "{ "id": "main_..." block).</summary>
        private static IEnumerable<string> ChapterQuestFixture(string[] lines)
        {
            bool skipping = false;
            foreach (string l in lines)
            {
                string t = l.TrimStart();
                if (t.StartsWith("{ \"id\":")) skipping = t.StartsWith("{ \"id\": \"main_");
                else if (t.Length == 0 || t.StartsWith("]")) skipping = false;
                if (!skipping) yield return l;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private void Bind()
        {
            motor = AdventureWorldSession.Current.Player;
            input = motor.GetComponent<PlayerInput2D>();
        }

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

        private static IEnumerator Shot(string name, int w = 1920, int h = 1080)
        {
            yield return new WaitForEndOfFrame();
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

        private IEnumerator TakePortal(float x, string expectScene, string shotFirst = null)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            if (shotFirst != null) { yield return new WaitForSeconds(0.8f); yield return Shot(shotFirst); }
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && !world.Traveling && Time.time < until) yield return null;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(expectScene);
            yield return new WaitForSeconds(1.5f);
            Bind();
        }

        private static IEnumerator Fresh(System.Action<QuestLog> got)
        {
            bool done = false;
            AdventureServerConnection.Instance.GetQuests(AdventureWorldSession.Current.Character.characterId, (ok, log, err) => { done = true; got(ok ? log : null); });
            float end = Time.realtimeSinceStartup + 10f;
            while (!done && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Fight(FarmField farm, FarmMonsterView m, List<string> statesSeen, string shotName)
        {
            yield return WalkTo(m.Home.x - 1.4f);
            float until = Time.time + 25f;
            int swings = 0;
            while (m.Alive && Time.time < until)
            {
                yield return Tap(PlayerInput2D.TouchAction.Attack);
                swings++;
                if (swings == 3 && shotName != null) { yield return new WaitForSeconds(0.2f); yield return Shot(shotName); }
                float next = Time.time + 0.95f;
                while (Time.time < next) { statesSeen.Add(m.AnimState); yield return null; }
            }
            Assert.IsFalse(m.Alive, $"{m.SpawnId} killed by the server; last error: {farm.LastError}");
            float dieEnd = Time.time + 1.2f;
            while (Time.time < dieEnd) { statesSeen.Add(m.AnimState); yield return null; }
        }

        [UnityTest]
        public IEnumerator Hub_To_Meadow_To_Mushroom_Village_Fight_Talk_And_Quest_Saved()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ch1m" + Random.Range(1000, 9999), "ch1-pass-2026", HeroClass.Swordsman, "Lữ Khách Nấm");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            QuestBook.Refresh();
            until = Time.time + 5f;
            while ((QuestBook.CurrentMain == null || QuestBook.CurrentMain.id != "ch1_01") && Time.time < until) yield return null;
            Assert.AreEqual("ch1_01", QuestBook.CurrentMain?.id, "chapter quest open (fixture without the starter chain)");

            // ---- hub → Đồng Cỏ Gió → east gate → Làng Nấm (server portal graph)
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            var meadowGate = Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).FirstOrDefault(g => g.name == "Portal_meadow_east_ch1");
            Assert.IsNotNull(meadowGate, "Đồng Cỏ Gió has the chapter gate at its east end");
            yield return TakePortal(TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene, "00_meadow_east_gate");
            var world = AdventureWorldSession.Current;
            Assert.AreEqual(TownLayout.Ch1Village.MapId, world.MapId);
            Assert.AreEqual(TownLayout.Ch1Village.Spawn.x, Pos.x, 0.8f, "arrived at the west gate of Làng Nấm");
            var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));
            foreach (TownLayout.Deck d in TownLayout.Ch1Village.Decks)
            {
                RaycastHit2D hit = Physics2D.Raycast(new Vector2((d.XMin + d.XMax) * 0.5f, d.Top + 3f), Vector2.down, 4f);
                Assert.IsTrue(hit.collider && Mathf.Abs(hit.point.y - d.Top) < 0.3f, $"deck {d.Name} has a real collider at y={d.Top}");
            }
            Assert.AreEqual(TownLayout.Ch1Village.Climbs.Length, Object.FindObjectsByType<LadderZone2D>(FindObjectsInactive.Exclude).Length, "ladders / ropes");
            until = Time.realtimeSinceStartup + 4f;
            while (!(AdventureAudioDirector.Instance.CurrentMusic && AdventureAudioDirector.Instance.CurrentMusic.name.Contains("BGM_Ch1")) && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.Contains("BGM_Ch1_MushroomVillage", AdventureAudioDirector.Instance.CurrentMusic.name, "Làng Nấm plays its own theme");
            Assert.IsNotNull(GameObject.Find("SafeZone"), "minimap shows the safe zone");
            yield return new WaitForSeconds(1f);
            yield return Shot("01_arrival_west_gate");

            // the travel quest was recorded by the server on the portal trip
            QuestLog log = null;
            yield return Fresh(l => log = l);
            Assert.IsTrue(log.main[0].id == "ch1_01" && log.main[0].complete, "travel:ch1_mushroom_village recorded");
            bool claimed = false;
            QuestBook.Claim("ch1_01", (ok, r, err) => { claimed = ok; Assert.IsTrue(ok, err); });
            until = Time.time + 5f;
            while (!claimed && Time.time < until) yield return null;
            Assert.AreEqual("ch1_02", QuestBook.CurrentMain.id);

            // ---- village square: Trưởng Làng Nấm ("!" over the head), talk with E / ▲
            var elder = Object.FindObjectsByType<NpcTalkSpot>(FindObjectsInactive.Exclude).First(s => s.name == "NpcMushroomElder");
            var marker = elder.GetComponent<NpcQuestMarker>();
            yield return WalkTo(TownLayout.Ch1Village.ElderSpot.x - 1.6f);
            yield return null;
            Assert.AreEqual("!", marker.Shown, "quest giver mark");
            Assert.IsTrue(elder.PlayerNear, "talk prompt shown next to the elder");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("02_mushroom_elder_prompt");
            yield return Tap(PlayerInput2D.TouchAction.Interact);
            until = Time.time + 5f;
            while ((QuestBook.CurrentMain == null || !QuestBook.CurrentMain.complete) && Time.time < until) yield return null;
            Assert.AreEqual(1, elder.Talks);
            Assert.IsTrue(QuestBook.CurrentMain.complete, "talk:mushroom_elder counted by the server");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("03_mushroom_elder_talk");
            claimed = false;
            QuestBook.Claim("ch1_02", (ok, r, err) => { claimed = ok; Assert.IsTrue(ok, err); });
            until = Time.time + 5f;
            while (!claimed && Time.time < until) yield return null;
            Assert.AreEqual("ch1_03", QuestBook.CurrentMain.id);

            // ---- safe zone: no monster is placed so it can reach the square
            FarmField farm = FarmField.Current;
            Assert.IsNotNull(farm, "Làng Nấm has the farm (outskirts)");
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(6, farm.Monsters.Count, "server spawns shown");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.Home.x - 3.5f - 1.3f > TownLayout.Ch1Village.SafeZone.y), "monsters stay outside the safe zone");
            FarmMonsterView n1 = farm.Monsters["c1m1_n1"];
            Assert.AreEqual("Nấm Non", n1.NameLabel.text);
            Assert.IsNotNull(n1.GetComponentInChildren<Animator>(), "rigged Nấm Non (Meshy) with an Animator");

            // ---- fight two Nấm Non on the server; the rig plays Walk / Attack / Hit / Death
            var states = new List<string>();
            yield return WalkTo(n1.Home.x - 5f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("04_nam_non_field");
            yield return Fight(farm, n1, states, "05_fight_nam_non");
            yield return Shot("06_nam_non_death");
            yield return Fight(farm, farm.Monsters["c1m1_n2"], states, null);
            Assert.AreEqual(2, farm.Kills);
            foreach (string s in new[] { "Attack", "Hit", "Death" }) CollectionAssert.Contains(states, s, "animator state seen: " + s);
            Assert.Greater(n1.Strikes, 0, "Nấm Non fought back on the server's timer");

            // ---- the quest progress lives on the server (fresh read, not the client's copy)
            yield return new WaitForSeconds(0.5f);
            yield return Fresh(l => log = l);
            Assert.AreEqual("ch1_03", log.main[0].id);
            Assert.AreEqual(2, log.main[0].objectives[0].have, "kill:young_mushroom ×2 saved on the server");
            Assert.AreEqual(TownLayout.Ch1Village.MapId, world.Character.lastMapId);
            yield return Shot("07_after_two_kills");
        }

        /// <summary>Map 2 Đồng Cỏ Xanh: Làng Nấm east gate → Map 2 (server portal), terraces with real colliders, Slime Phấn
        /// Hoa (Meshy mesh, procedural hop) fought on the server, travel quest saved, own music, gate back to Làng Nấm.</summary>
        [UnityTest]
        public IEnumerator Mushroom_Village_East_Gate_To_Green_Meadow_Fight_Pollen_Slime_And_Back()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ch1n" + Random.Range(1000, 9999), "ch1-pass-2026", HeroClass.Mage, "Lữ Khách Cỏ");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            yield return TakePortal(TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene);
            Assert.IsNotNull(Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).FirstOrDefault(g => g.name == "Portal_ch1_village_east_m02"),
                "Làng Nấm has the Map 2 gate at its east end");
            yield return TakePortal(TownLayout.Ch1Village.Portals.First(p => p.Id == "ch1_village_east_m02").X, TownLayout.Ch1GreenMeadow.Scene, "m02_00_village_east_gate");

            var world = AdventureWorldSession.Current;
            Assert.AreEqual(TownLayout.Ch1GreenMeadow.MapId, world.MapId);
            Assert.AreEqual(TownLayout.Ch1GreenMeadow.MapId, world.Character.lastMapId, "server moved the hero to Map 2");
            Assert.AreEqual(TownLayout.Ch1GreenMeadow.Spawn.x, Pos.x, 0.8f);
            var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));
            foreach (TownLayout.Deck d in TownLayout.Ch1GreenMeadow.Decks)
            {
                RaycastHit2D hit = Physics2D.Raycast(new Vector2((d.XMin + d.XMax) * 0.5f, d.Top + 3f), Vector2.down, 4f);
                Assert.IsTrue(hit.collider && Mathf.Abs(hit.point.y - d.Top) < 0.3f, $"deck {d.Name} has a real collider at y={d.Top}");
            }
            Assert.AreEqual(TownLayout.Ch1GreenMeadow.Climbs.Length, Object.FindObjectsByType<LadderZone2D>(FindObjectsInactive.Exclude).Length, "ladders / ropes");
            foreach (string prop in new[] { "Prop_ch1_wood_fence", "Prop_ch1_flower_bush", "Prop_windmill" })
                Assert.IsNotNull(GameObject.Find(prop), "3D module placed: " + prop);
            until = Time.realtimeSinceStartup + 4f;
            while (!(AdventureAudioDirector.Instance.CurrentMusic && AdventureAudioDirector.Instance.CurrentMusic.name.Contains("GreenMeadow")) && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.Contains("BGM_Ch1_GreenMeadow", AdventureAudioDirector.Instance.CurrentMusic.name, "Đồng Cỏ Xanh plays its own theme");
            yield return new WaitForSeconds(1f);
            yield return Shot("m02_01_arrival_rest_stop");

            // ---- server spawns: pollen slimes (Meshy mesh, no stand-in), a few Nấm Non on the terraces
            FarmField farm = FarmField.Current;
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(10, farm.Monsters.Count, "server spawns shown");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.Home.x - 3.5f - 1.4f > TownLayout.Ch1GreenMeadow.SafeZone.y), "monsters stay outside the rest stop");
            FarmMonsterView p1 = farm.Monsters["c1m2_p1"];
            Assert.AreEqual("Slime Phấn Hoa", p1.NameLabel.text);
            Assert.IsNotNull(p1.transform.Find("Body/Prop_ch1_pollen_slime"), "Slime Phấn Hoa uses its own Meshy model");
            yield return WalkTo(p1.Home.x - 5f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("m02_02_pollen_slime_field");
            var states = new List<string>();
            yield return Fight(farm, p1, states, "m02_03_fight_pollen_slime");
            yield return Shot("m02_04_pollen_slime_poof");
            Assert.AreEqual(1, farm.Kills);

            // windmill hill view (quest chain ch1_04..06 is covered by the server tests)
            yield return WalkTo(40f, 30f);
            yield return new WaitForSeconds(1f);
            yield return Shot("m02_05_windmill_hill");

            // ---- back to Làng Nấm through the west gate
            yield return TakePortal(TownLayout.Ch1GreenMeadow.Portals[0].X, TownLayout.Ch1Village.Scene);
            Assert.AreEqual(TownLayout.Ch1Village.MapId, AdventureWorldSession.Current.Character.lastMapId);
            Assert.AreEqual(89f, Pos.x, 1f, "arrived at the east gate of Làng Nấm");
        }

        private static string BackdropTexture()
        {
            var mat = GameObject.Find("Backdrop_Painting").GetComponent<MeshRenderer>().sharedMaterial;
            foreach (string n in mat.GetTexturePropertyNames()) { Texture t = mat.GetTexture(n); if (t) return t.name; }
            return "";
        }

        /// <summary>Map 2 east gate → Map 3 (server portal, state kept, spawn point of Map 3, no Map 2 coordinate) and back;
        /// every map loads its own far backdrop (Map 1 no longer the town painting) and its own ParallaxLayer.</summary>
        [UnityTest]
        public IEnumerator Green_Meadow_East_Gate_To_Glow_Forest_Keeps_State_And_Each_Map_Has_Its_Own_Parallax()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ch1p" + Random.Range(1000, 9999), "ch1-pass-2026", HeroClass.Mage, "Lữ Khách Rừng");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            string hubTex = BackdropTexture();

            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            yield return TakePortal(TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene);
            string m1Tex = BackdropTexture();
            var m1Layer = GameObject.Find("Backdrop_Painting").GetComponent<ParallaxLayer>();
            Vector2 m1Follow = m1Layer.Follow;
            yield return new WaitForSeconds(1f);
            yield return Shot("m01_parallax_own_backdrop");
            yield return TakePortal(TownLayout.Ch1Village.Portals.First(p => p.Id == "ch1_village_east_m02").X, TownLayout.Ch1GreenMeadow.Scene);
            string m2Tex = BackdropTexture();

            var world = AdventureWorldSession.Current;
            long gold = world.Character.gold; int level = world.Character.level; int items = world.Character.inventory.Length;
            yield return TakePortal(TownLayout.Ch1GreenMeadow.Portals.First(p => p.Id == "ch1_m02_east_m03").X, TownLayout.Ch1GlowForest.Scene, "m03_00_m02_east_gate");
            world = AdventureWorldSession.Current;
            Assert.AreEqual(TownLayout.Ch1GlowForest.MapId, world.MapId);
            Assert.AreEqual(TownLayout.Ch1GlowForest.MapId, world.Character.lastMapId, "server moved the hero to Map 3");
            Assert.AreEqual(TownLayout.Ch1GlowForest.Spawn.x, Pos.x, 0.8f, "placed at the Map 3 spawn, not at Map 2's x≈100");
            Assert.AreEqual(gold, world.Character.gold); Assert.AreEqual(level, world.Character.level);
            Assert.AreEqual(items, world.Character.inventory.Length, "inventory kept");
            var rb = motor.GetComponent<Rigidbody2D>();
            Assert.Greater(Pos.y, -1f, "standing on the Map 3 ground, not falling out of the map");
            Assert.IsNotNull(Physics2D.Raycast(new Vector2(Pos.x, 3f), Vector2.down, 6f).collider, "Map 3 ground collider under the hero");
            var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));
            string m3Tex = BackdropTexture();
            yield return new WaitForSeconds(1f);
            yield return Shot("m03_01_arrival");

            // the portal cannot be used twice in a row / from the wrong side
            Assert.IsNull(Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).FirstOrDefault(g => g.name == "Portal_ch1_m02_east_m03"), "Map 2's gate is not on Map 3");

            yield return TakePortal(TownLayout.Ch1GlowForest.Portals[0].X, TownLayout.Ch1GreenMeadow.Scene);
            Assert.AreEqual(TownLayout.Ch1GreenMeadow.MapId, AdventureWorldSession.Current.Character.lastMapId);
            Assert.AreEqual(99f, Pos.x, 1f, "back at the east end of Map 2");

            // ---- parallax: each map has its own texture + layer rates, none is the town painting
            Debug.Log($"[PARALLAX] hub={hubTex} m1={m1Tex} m2={m2Tex} m3={m3Tex} m1Follow={m1Follow}");
            Assert.AreNotEqual("", m1Tex);
            StringAssert.Contains("c1_m01", m1Tex);
            StringAssert.Contains("c1_m02", m2Tex);
            StringAssert.Contains("c1_m03", m3Tex);
            Assert.AreNotEqual(m1Tex, m3Tex, "Map 3 has its own backdrop texture");
            Assert.AreNotEqual(hubTex, m1Tex, "Map 1 no longer uses the town backdrop");
            Assert.AreNotEqual(m1Tex, m2Tex);
        }

        /// <summary>Quest tracker on a phone: folded by default, manual open/close remembered separately from the PC choice,
        /// never overlapping the joystick / jump / interact / skill bar, no flicker; PC layout unchanged.</summary>
        [UnityTest]
        public IEnumerator Quest_Tracker_Folds_On_Phone_And_Keeps_Clear_Of_The_Touch_Controls()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ch1q" + Random.Range(1000, 9999), "ch1-pass-2026", HeroClass.Mage, "Lữ Khách Gọn");
            Bind();
            var existing = Object.FindAnyObjectByType<QuestPanel>();
            Assert.IsNotNull(existing);
            try { PlayerPrefs.DeleteKey("ak_quest_folded"); PlayerPrefs.DeleteKey("ak_quest_folded_mobile"); } catch { }

            // PC (this editor run): not folded by default
            QuestPanel pc = NewPanel(existing, "QuestPc");
            yield return null; yield return null;
            Assert.IsFalse(pc.Collapsed, "PC keeps the open tracker");
            Assert.AreEqual(1f, pc.transform.localScale.x, 0.001f);

            QuestPanel.ForceMobile = true;
            try
            {
                QuestPanel phone = NewPanel(existing, "QuestPhone");
                yield return null; yield return null;
                Assert.IsTrue(phone.Collapsed, "phone starts folded");
                for (int i = 0; i < 90; i++) { yield return null; Assert.IsTrue(phone.Collapsed, "no flicker / auto re-open"); }
                // folded bar is a thin strip: far above every touch control
                RectTransform[] controls = new[] { "TouchJoystick", "TouchJump", "TouchInteract" }
                    .Select(n => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.name == n)).ToArray();
                Assert.IsTrue(controls.All(c => c), "touch controls exist in the HUD");
                var bar = Object.FindObjectsByType<HeroActionBar>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                var barRects = bar.GetComponentsInChildren<RectTransform>(false).Where(r => r != bar.transform).ToList();
                RectTransform card = (RectTransform)phone.transform;
                AssertClear(card, controls, barRects, "folded");

                phone.SetCollapsed(false); // the player opens it by hand
                yield return null; yield return null;
                Assert.IsFalse(phone.Collapsed);
                AssertClear(card, controls, barRects, "expanded");
                yield return Shot("m_quest_phone_open");
                phone.SetCollapsed(true);
                yield return null;
                yield return Shot("m_quest_phone_folded");
                phone.SetCollapsed(false);
                Assert.AreEqual(0, PlayerPrefs.GetInt("ak_quest_folded_mobile", 1), "manual open remembered for the phone");
                Assert.AreEqual(0, PlayerPrefs.GetInt("ak_quest_folded", 0), "PC key untouched by phone choices");
                QuestPanel again = NewPanel(existing, "QuestPhone2");
                yield return null; yield return null;
                Assert.IsFalse(again.Collapsed, "a manual open is respected on the next start");

                // other phone screens: the expanded card always ends above the joystick ring (canvas = ref 1920x1080, match 0.6)
                foreach (var res in new[] { new Vector2(1280, 720), new Vector2(2400, 1080), new Vector2(2532, 1170), new Vector2(1024, 768), new Vector2(2960, 1440), new Vector2(1600, 720) })
                {
                    float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(res.x / 1920f, 2f), Mathf.Log(res.y / 1080f, 2f), UiStandard.Match));
                    float canvasH = res.y / scale; // (safe area bottom/top insets only add margin on the top, joystick moves with the bottom)
                    float bottomFromTop = 262f + 420f * QuestPanel.FitScale(canvasH);
                    Assert.LessOrEqual(bottomFromTop, canvasH - 395f, $"{res}: expanded card ends above the joystick (canvasH {canvasH:0})");
                }
            }
            finally { QuestPanel.ForceMobile = false; try { PlayerPrefs.DeleteKey("ak_quest_folded"); PlayerPrefs.DeleteKey("ak_quest_folded_mobile"); } catch { } }
        }

        private static QuestPanel NewPanel(QuestPanel like, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(like.transform.parent, false);
            like.gameObject.SetActive(false); // the real one stays out of the way
            return go.AddComponent<QuestPanel>();
        }

        private static Rect Bounds(RectTransform r)
        {
            var c = new Vector3[4]; r.GetWorldCorners(c);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y), Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
        }

        private static void AssertClear(RectTransform card, RectTransform[] controls, List<RectTransform> bar, string what)
        {
            Rect q = Bounds(card);
            foreach (RectTransform c in controls) Assert.IsFalse(q.Overlaps(Bounds(c)), $"{what}: quest card overlaps {c.name}");
            foreach (RectTransform c in bar) Assert.IsFalse(q.Overlaps(Bounds(c)), $"{what}: quest card overlaps the skill bar ({c.name})");
        }
    }
}

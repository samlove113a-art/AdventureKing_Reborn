using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace AdventureKing.Tests
{
    /// <summary>
    /// End-to-end, nothing mocked: the real AK_Entry UI and AK_Village map talk over HTTP to the real local
    /// server process (Server/AdventureKing.Server, built with `dotnet build`) using a throw-away SQLite file.
    ///   open game -> register -> name refused/accepted by server -> enter map -> walk -> "Lưu & Thoát"
    ///   -> server process restarted -> login -> enter -> same map, same position, same character.
    /// Ignored (not passed) when the server build or dotnet is missing.
    /// </summary>
    public sealed class ServerEndToEndTests
    {
        private const string EntryPath = "Assets/Scenes/AK_Entry.unity";
        private const int Port = 5199;
        private Process server;
        private string dbDir;

        private static string ServerDll => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Server/AdventureKing.Server/bin/Debug/net8.0/AdventureKing.Server.dll"));

        // ------------------------------------------------------------------ server process
        private IEnumerator StartServer()
        {
            if (!File.Exists(ServerDll)) Assert.Ignore("Server not built: run `dotnet build Server/AdventureKing.Server` first.");
            var psi = new ProcessStartInfo("dotnet",
                $"\"{ServerDll}\" --urls http://127.0.0.1:{Port} --AdventureKing:DatabasePath=\"{Path.Combine(dbDir, "e2e.db")}\"")
            {
                WorkingDirectory = Path.GetDirectoryName(ServerDll),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try { server = Process.Start(psi); }
            catch (System.ComponentModel.Win32Exception) { Assert.Ignore("dotnet runtime not on PATH."); }
            server.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log("[server] " + e.Data); };
            server.ErrorDataReceived += (_, e) => { if (e.Data != null) Debug.Log("[server!] " + e.Data); };
            server.BeginOutputReadLine();
            server.BeginErrorReadLine();

            float end = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < end)
            {
                using var req = UnityWebRequest.Get($"http://127.0.0.1:{Port}/api/v1/health");
                req.timeout = 2;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) yield break;
                Assert.IsFalse(server.HasExited, "server process exited early");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Assert.Fail("server did not become healthy");
        }

        private void StopServer()
        {
            if (server == null) return;
            try { if (!server.HasExited) { server.Kill(); server.WaitForExit(5000); } }
            catch (System.InvalidOperationException) { }
            server.Dispose();
            server = null;
        }

        // ------------------------------------------------------------------ scenes
        /// <summary>"Open the game": a fresh entry scene, connection pointed at the test server.</summary>
        private IEnumerator OpenGame()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(EntryPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
            AdventureServerConnection.Instance.SetBaseUrl($"http://127.0.0.1:{Port}/api/v1");
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }

        private static IEnumerator CloseGame() => TestRunnerGuard.CloseGame();

        private static IEnumerator WaitForScene(string name, float seconds = 20f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, "scene did not load");
            yield return null;
            yield return null;
        }

        private static AdventureEntryFlow Flow => Object.FindAnyObjectByType<AdventureEntryFlow>();
        private static T Ui<T>(string name) where T : Component => EntryLobbyTests.Find<T>(name);

        private static IEnumerator Login(string account, string password)
        {
            AdventureEntryFlow flow = Flow;
            flow.SkipSplash();
            yield return null;
            Ui<InputField>("AccountInput").text = account;
            Ui<InputField>("PasswordInput").text = password;
            flow.LoginPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, "status: " + flow.StatusMessage);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect(); // the game switches scenes in Single mode (logout, portals)
            dbDir = Path.Combine(Path.GetTempPath(), "ak_e2e_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dbDir);
            yield return StartServer();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return CloseGame();
            StopServer();
            try { Directory.Delete(dbDir, true); } catch (IOException) { }
        }

        [UnityTest]
        public IEnumerator Login_Select_Enter_Map_Exit_Relogin_Keeps_Progress()
        {
            const string account = "e2e.hero@test.vn", password = "e2e-pass-2026";

            // ---- 1. Open the game and register through the real auth UI.
            yield return OpenGame();
            AdventureEntryFlow flow = Flow;
            Assert.AreEqual(EntryScreen.Splash, flow.CurrentScreen);
            Assert.IsTrue(Ui<Image>("OfficialLogo").enabled && Ui<Image>("OfficialLogo").sprite, "owner logo on splash");
            flow.SkipSplash();
            yield return null;
            flow.SetRegisterMode(true);
            Ui<InputField>("AccountInput").text = account;
            Ui<InputField>("PasswordInput").text = password;
            Ui<InputField>("ConfirmPasswordInput").text = password;
            flow.RegisterPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, "status: " + flow.StatusMessage);
            Assert.AreEqual(0, flow.LoadedProfile.characters.Length);

            // ---- 2. Choose class + name; the server refuses impersonation, accepts a Vietnamese name.
            flow.SelectSlot(0);
            flow.SelectClass(HeroClass.Archer);
            var nameInput = Ui<InputField>("CharacterNameInput");
            nameInput.text = "GM Cung Thủ";
            flow.CreateCharacterAndEnter();
            yield return EntryLobbyTests.WaitIdle(flow);
            StringAssert.Contains("giả mạo", flow.StatusMessage);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen);

            nameInput.text = "Cung Thủ Gió";
            flow.CreateCharacterAndEnter();

            // ---- 3. Enter the map with the real 3D hero of the chosen class.
            yield return WaitForScene("AK_Village");
            var world = Object.FindAnyObjectByType<AdventureWorldSession>();
            Assert.IsNotNull(world);
            Assert.AreEqual("Cung Thủ Gió", world.Character.characterName);
            Assert.IsTrue(world.HeroModel, "hero model spawned");
            StringAssert.StartsWith("Archer_Visual", world.HeroModel.name);
            Assert.GreaterOrEqual(world.HeroModel.GetComponentsInChildren<SkinnedMeshRenderer>().Length, 2, "body + bow");
            yield return null; // HUD player frame fills in on its first update
            StringAssert.Contains("Cung Thủ Gió", Ui<Text>("HeroName").text, "player frame shows the server character name");
            var input = Object.FindAnyObjectByType<PlayerInput2D>();
            var motor = input.GetComponent<PlayerMotor2D>();
            Vector2 spawn = motor.transform.position;

            // ---- 4. Walk right for a while (same input path as the touch buttons).
            yield return new WaitForSeconds(0.8f); // land on the ground first
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, true);
            yield return new WaitForSeconds(1.4f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            float settle = Time.realtimeSinceStartup + 3f;
            while ((!motor.IsGrounded || Mathf.Abs(motor.Velocity.x) > 0.05f) && Time.realtimeSinceStartup < settle) yield return null;
            Vector2 walked = motor.transform.position;
            Assert.Greater(walked.x - spawn.x, 3f, "player did not move");
            var animator = world.HeroModel.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator && animator.runtimeAnimatorController, "hero animator wired");

            // ---- 5. "Lưu & Thoát": saved on the server, signed out, back at login.
            world.ExitToLogin();
            yield return WaitForScene("AK_Entry");
            flow = Flow;
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen);
            StringAssert.Contains("Đã lưu", Ui<Text>("AuthStatus").text);
            Assert.IsFalse(AdventureServerConnection.Instance.HasSession, "signed out");
            Assert.IsFalse(AdventurePlayerSession.IsActive);

            // ---- 6. Restart the server process: everything must come back from the database.
            yield return CloseGame();
            StopServer();
            yield return StartServer();
            yield return OpenGame();

            // ---- 7. Log in again and enter: same character, same map, same position.
            yield return Login(account, password);
            flow = Flow;
            Assert.AreEqual(1, flow.LoadedProfile.characters.Length);
            AdventureCharacterSnapshot saved = flow.LoadedProfile.characters[0];
            Assert.AreEqual("Cung Thủ Gió", saved.characterName);
            Assert.AreEqual(HeroClass.Archer, saved.heroClass);
            Assert.IsTrue(saved.hasPosition);
            Assert.AreEqual("village_wind_bell", saved.lastMapId);
            flow.EnterSelectedCharacter();
            yield return WaitForScene("AK_Village");
            yield return new WaitForSeconds(0.3f);
            motor = Object.FindAnyObjectByType<PlayerMotor2D>();
            Vector2 restored = motor.transform.position;
            Assert.AreEqual(walked.x, restored.x, 0.2f, "x restored from server");
            Assert.AreEqual(walked.y, restored.y, 0.3f, "y restored from server");
            Assert.IsTrue(Object.FindAnyObjectByType<AdventureWorldSession>().HeroModel.name.StartsWith("Archer_Visual"));

            // ---- 8. A wrong password is still refused after all of this.
            Object.FindAnyObjectByType<AdventureWorldSession>().ExitToLogin();
            yield return WaitForScene("AK_Entry");
            flow = Flow;
            Ui<InputField>("AccountInput").text = account;
            Ui<InputField>("PasswordInput").text = "not-the-password";
            flow.LoginPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen);
            StringAssert.Contains("Sai tên đăng nhập hoặc mật khẩu", Ui<Text>("AuthStatus").text);
        }

        [UnityTest]
        public IEnumerator Opening_The_Map_Without_Login_Never_Invents_A_Character()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/AK_Village.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
            var world = Object.FindAnyObjectByType<AdventureWorldSession>();
            Assert.IsNull(world.Character);
            Assert.IsFalse(world.HeroModel);
            StringAssert.Contains("Chưa đăng nhập", world.Status);
            yield return WaitForScene("AK_Entry", 5f);
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }
    }
}

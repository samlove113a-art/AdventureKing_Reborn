using System.Collections;
using System.Diagnostics;
using System.IO;
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
    /// End-to-end: the real AK_Entry UI talks over HTTP to the real local server process
    /// (Server/AdventureKing.Server, built with `dotnet build`) using a throw-away SQLite database.
    /// Register -> hero hall -> name validation by the server -> create -> enter -> server restart ->
    /// login again -> the character is still there. Nothing is mocked.
    /// Ignored (not passed) when the server build or dotnet is missing.
    /// </summary>
    public sealed class ServerEndToEndTests
    {
        private const string ScenePath = "Assets/Scenes/AK_Entry.unity";
        private const int Port = 5199;
        private Process server;
        private string dbDir;
        private Scene scene;

        private static string ServerDll => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Server/AdventureKing.Server/bin/Debug/net8.0/AdventureKing.Server.dll"));

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

        private IEnumerator LoadEntry()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            while (!op.isDone) yield return null;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            EntryLobbyTests.Find<AdventureServerConnection>().SetBaseUrl($"http://127.0.0.1:{Port}/api/v1");
            EntryLobbyTests.Find<AdventureEntryFlow>().SkipSplash();
            yield return null;
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }

        private IEnumerator UnloadEntry()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
            var director = Object.FindAnyObjectByType<AdventureAudioDirector>();
            if (director) Object.Destroy(director.gameObject);
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dbDir = Path.Combine(Path.GetTempPath(), "ak_e2e_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dbDir);
            yield return StartServer();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadEntry();
            StopServer();
            try { Directory.Delete(dbDir, true); } catch (IOException) { }
        }

        [UnityTest]
        public IEnumerator Register_Create_Enter_Then_Restart_And_Login_Keeps_Character()
        {
            yield return LoadEntry();
            var flow = EntryLobbyTests.Find<AdventureEntryFlow>();

            // Register through the real auth UI.
            flow.SetRegisterMode(true);
            EntryLobbyTests.Find<InputField>("AccountInput").text = "e2e.hero@test.vn";
            EntryLobbyTests.Find<InputField>("PasswordInput").text = "e2e-pass-2026";
            EntryLobbyTests.Find<InputField>("ConfirmPasswordInput").text = "e2e-pass-2026";
            flow.RegisterPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, "status: " + flow.StatusMessage);
            Assert.AreEqual(0, flow.LoadedProfile.characters.Length);

            // The server rejects impersonation names with a readable message on the hall.
            flow.SelectSlot(0);
            flow.SelectClass(HeroClass.Mage);
            var nameInput = EntryLobbyTests.Find<InputField>("CharacterNameInput");
            nameInput.text = "GM Pháp Sư";
            flow.CreateCharacterAndEnter();
            yield return EntryLobbyTests.WaitIdle(flow);
            StringAssert.Contains("giả mạo", flow.StatusMessage);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen);

            // Valid Vietnamese name: created and entered on the server.
            nameInput.text = "Pháp Sư Mây";
            flow.CreateCharacterAndEnter();
            yield return EntryLobbyTests.WaitIdle(flow);
            // No gameplay scene is in Build Settings yet, so entry stops right after the server confirmed it.
            StringAssert.Contains("Scene gameplay", flow.StatusMessage, "status: " + flow.StatusMessage);

            // Restart the server process: data must come from the database, not memory.
            yield return UnloadEntry();
            StopServer();
            yield return StartServer();
            yield return LoadEntry();
            flow = EntryLobbyTests.Find<AdventureEntryFlow>();
            EntryLobbyTests.Find<InputField>("AccountInput").text = "e2e.hero@test.vn";
            EntryLobbyTests.Find<InputField>("PasswordInput").text = "e2e-pass-2026";
            flow.LoginPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, "status: " + flow.StatusMessage);
            Assert.AreEqual(1, flow.LoadedProfile.characters.Length);
            AdventureCharacterSnapshot saved = flow.LoadedProfile.characters[0];
            Assert.AreEqual("Pháp Sư Mây", saved.characterName);
            Assert.AreEqual(HeroClass.Mage, saved.heroClass);
            Assert.AreEqual(0, saved.slotIndex);
            Assert.AreEqual(1, saved.level);
            StringAssert.Contains("Pháp Sư Mây", EntryLobbyTests.Find<Text>("ExistingDetails").text);

            // Wrong password after restart is still refused.
            flow.BackToAuthentication();
            EntryLobbyTests.Find<InputField>("PasswordInput").text = "not-the-password";
            flow.LoginPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen);
            StringAssert.Contains("Sai tên đăng nhập hoặc mật khẩu", EntryLobbyTests.Find<Text>("AuthStatus").text);
        }
    }
}

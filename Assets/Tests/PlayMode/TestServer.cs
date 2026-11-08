using System.Collections;
using System.Diagnostics;
using System.IO;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace AdventureKing.Tests
{
    /// <summary>Runs the real game server (built dll) on a throw-away SQLite file for a PlayMode test.</summary>
    public sealed class TestServer
    {
        private readonly int port;
        private readonly string dbDir;
        private Process process;

        public TestServer(int port)
        {
            this.port = port;
            dbDir = Path.Combine(Path.GetTempPath(), "ak_srv_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dbDir);
        }

        public string ApiUrl => $"http://127.0.0.1:{port}/api/v1";

        private static string Dll => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Server/AdventureKing.Server/bin/Debug/net8.0/AdventureKing.Server.dll"));

        public IEnumerator Start()
        {
            if (!File.Exists(Dll)) Assert.Ignore("Server not built: run `dotnet build Server/AdventureKing.Server` first.");
            var psi = new ProcessStartInfo("dotnet", $"\"{Dll}\" --urls http://127.0.0.1:{port} --AdventureKing:DatabasePath=\"{Path.Combine(dbDir, "test.db")}\"")
            {
                WorkingDirectory = Path.GetDirectoryName(Dll), UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            try { process = Process.Start(psi); }
            catch (System.ComponentModel.Win32Exception) { Assert.Ignore("dotnet runtime not on PATH."); }
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log("[server] " + e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Debug.Log("[server!] " + e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            float end = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < end)
            {
                using var req = UnityWebRequest.Get(ApiUrl + "/health");
                req.timeout = 2;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) yield break;
                Assert.IsFalse(process.HasExited, "server process exited early");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Assert.Fail("server did not become healthy");
        }

        public void Stop(bool deleteData = true)
        {
            if (process != null)
            {
                try { if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); } }
                catch (System.InvalidOperationException) { }
                process.Dispose();
                process = null;
            }
            if (deleteData) try { Directory.Delete(dbDir, true); } catch (IOException) { }
        }

        /// <summary>Opens the entry scene like a fresh game start and points the client at this server.</summary>
        public IEnumerator OpenGame()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/AK_Entry.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
            AdventureServerConnection.Instance.SetBaseUrl(ApiUrl);
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }

        /// <summary>Registers through the real UI, creates a character and waits until the world map is loaded.</summary>
        public static IEnumerator RegisterCreateAndEnter(string account, string password, HeroClass cls, string name)
        {
            var flow = Object.FindAnyObjectByType<AdventureEntryFlow>();
            flow.SkipSplash();
            yield return null;
            flow.SetRegisterMode(true);
            EntryLobbyTests.Find<InputField>("AccountInput").text = account;
            EntryLobbyTests.Find<InputField>("PasswordInput").text = password;
            EntryLobbyTests.Find<InputField>("ConfirmPasswordInput").text = password;
            flow.RegisterPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, "status: " + flow.StatusMessage);
            flow.SelectSlot(0);
            flow.SelectClass(cls);
            EntryLobbyTests.Find<InputField>("CharacterNameInput").text = name;
            flow.CreateCharacterAndEnter();
            yield return WaitForScene(TownLayout.Hub.Scene);
        }

        /// <summary>Waits for a scene by name only (e.g. back at AK_Entry after "Lưu & Thoát").</summary>
        public static IEnumerator WaitForSceneName(string name, float seconds = 20f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, "scene did not load");
            yield return null;
            yield return null;
        }

        public static IEnumerator WaitForScene(string name, float seconds = 25f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while ((SceneManager.GetActiveScene().name != name || !AdventureWorldSession.Current ||
                    AdventureWorldSession.Current.Character == null) && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, "scene did not load");
            yield return new WaitForSeconds(0.6f); // fade-in, hero spawned, physics settled
        }
    }
}

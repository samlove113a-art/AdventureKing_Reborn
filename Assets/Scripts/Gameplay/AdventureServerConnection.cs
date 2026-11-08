using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Real client for the Adventure King game server (Server/AdventureKing.Server, REST + JSON).
    /// Implements both the account and the profile contract used by <see cref="AdventureEntryFlow"/>.
    ///
    /// Security rules honoured here:
    ///  - the session token lives in memory only (never PlayerPrefs, never disk, never logs);
    ///  - the password is sent once over the request body and never stored;
    ///  - Google / Facebook go to the server, which refuses them until real OAuth is configured —
    ///    this class never pretends a social login succeeded;
    ///  - the client never sends currency, level or items: it only asks; the server decides.
    /// All callbacks run on Unity's main thread (coroutines).
    /// </summary>
    public sealed class AdventureServerConnection : MonoBehaviour, IAdventureAccountService, IAdventureProfileService
    {
        [Tooltip("Local test server. Change only to a server the owner operates (HTTPS required off-PC).")]
        [SerializeField] private string baseUrl = "http://127.0.0.1:5080/api/v1";
        [SerializeField, Min(1)] private int timeoutSeconds = 8;
        [SerializeField] private AdventureEntryFlow entryFlow;

        private string sessionToken;
        private AdventureAccountProfile lastProfile;

        /// <summary>Survives scene loads so the session follows the player from the hall into the world.</summary>
        public static AdventureServerConnection Instance { get; private set; }
        public bool HasSession => !string.IsNullOrEmpty(sessionToken);
        public string BaseUrl => baseUrl;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                // Returning to the entry scene: keep the live connection, hand it to the new flow.
                if (entryFlow) entryFlow.SetAccountService(Instance);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            if (entryFlow) entryFlow.SetAccountService(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>For tests / launcher configuration. Only loopback or HTTPS URLs are accepted.</summary>
        public void SetBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("url");
            bool loopback = url.StartsWith("http://127.0.0.1", StringComparison.Ordinal) ||
                            url.StartsWith("http://localhost", StringComparison.Ordinal);
            if (!loopback && !url.StartsWith("https://", StringComparison.Ordinal))
                throw new ArgumentException("Non-local servers must use HTTPS.");
            baseUrl = url.TrimEnd('/');
        }

        // ------------------------------------------------------------------ IAdventureAccountService
        public void SignIn(SignInMethod method, string account, string password, Action<bool, string> completed)
        {
            sessionToken = null;
            lastProfile = null;
            switch (method)
            {
                case SignInMethod.Google:
                    Send("POST", "/auth/google", "{}", false, (ok, _, err) => completed(false, err ?? "Đăng nhập Google chưa khả dụng."));
                    return;
                case SignInMethod.Facebook:
                    Send("POST", "/auth/facebook", "{}", false, (ok, _, err) => completed(false, err ?? "Đăng nhập Facebook chưa khả dụng."));
                    return;
            }
            string path = method == SignInMethod.PasswordRegister ? "/auth/register" : "/auth/login";
            string body = JsonUtility.ToJson(new CredentialsBody { account = account, password = password });
            Send("POST", path, body, false, (ok, json, err) =>
            {
                if (!ok) { completed(false, err); return; }
                SessionBody s = JsonUtility.FromJson<SessionBody>(json);
                if (s == null || string.IsNullOrEmpty(s.token)) { completed(false, "Máy chủ trả về phiên không hợp lệ."); return; }
                sessionToken = s.token;
                completed(true, "");
            });
        }

        /// <summary>Legacy contract: creates in the first free slot of the last loaded profile.</summary>
        public void CreateCharacter(string characterName, HeroClass heroClass, Action<bool, string> completed)
        {
            int slot = 0;
            if (lastProfile != null)
                while (slot < 4 && Array.Exists(lastProfile.characters, c => c.slotIndex == slot)) slot++;
            if (slot >= 4) { completed(false, "Tài khoản đã đủ 4 nhân vật."); return; }
            CreateCharacter(characterName, heroClass, slot, (ok, _, msg) => completed(ok, msg));
        }

        // ------------------------------------------------------------------ IAdventureProfileService
        public void LoadProfile(Action<bool, AdventureAccountProfile, string> completed)
        {
            Send("GET", "/profile", null, true, (ok, json, err) =>
            {
                if (!ok) { completed(false, null, err); return; }
                lastProfile = JsonUtility.FromJson<AdventureAccountProfile>(json);
                completed(lastProfile != null, lastProfile, lastProfile == null ? "Dữ liệu hồ sơ không hợp lệ." : "");
            });
        }

        public void CreateCharacter(string displayName, HeroClass heroClass, int slotIndex,
            Action<bool, AdventureCharacterSnapshot, string> completed)
        {
            string body = JsonUtility.ToJson(new CreateBody { characterName = displayName, heroClass = (int)heroClass, slotIndex = slotIndex });
            Send("POST", "/characters", body, true, (ok, json, err) =>
            {
                if (!ok) { completed(false, null, err); return; }
                completed(true, JsonUtility.FromJson<AdventureCharacterSnapshot>(json), "");
            });
        }

        public void EnterCharacter(string characterId, Action<bool, AdventureCharacterSnapshot, string> completed)
        {
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/enter", "{}", true, (ok, json, err) =>
            {
                if (!ok) { completed(false, null, err); return; }
                completed(true, JsonUtility.FromJson<AdventureCharacterSnapshot>(json), "");
            });
        }

        /// <summary>Asks the server to record a finished tutorial step (server enforces order).</summary>
        public void CompleteTutorialStep(string characterId, int step, Action<bool, AdventureCharacterSnapshot, string> completed)
        {
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/tutorial/" + step, "{}", true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<AdventureCharacterSnapshot>(json) : null, err));
        }

        /// <summary>Saves the character's map and position; the server validates map and bounds.</summary>
        public void SaveLocation(string characterId, string mapId, Vector2 position,
            Action<bool, AdventureCharacterSnapshot, string> completed)
        {
            string body = JsonUtility.ToJson(new LocationBody { mapId = mapId, x = position.x, y = position.y });
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/location", body, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<AdventureCharacterSnapshot>(json) : null, err));
        }

        public void SignOut()
        {
            if (HasSession) Send("POST", "/auth/logout", "{}", true, (_, __, ___) => { });
            sessionToken = null;
            lastProfile = null;
        }

        // ------------------------------------------------------------------ transport
        private void Send(string method, string path, string jsonBody, bool auth, Action<bool, string, string> done)
        {
            if (auth && !HasSession) { done(false, null, "Phiên đăng nhập đã hết. Hãy đăng nhập lại."); return; }
            StartCoroutine(SendRoutine(method, path, jsonBody, auth, done));
        }

        private IEnumerator SendRoutine(string method, string path, string jsonBody, bool auth, Action<bool, string, string> done)
        {
            using var req = new UnityWebRequest(baseUrl + path, method);
            if (jsonBody != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = timeoutSeconds;
            if (auth) req.SetRequestHeader("Authorization", "Bearer " + sessionToken);
            yield return req.SendWebRequest();

            string text = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                done(false, null, "Không kết nối được máy chủ trò chơi. Hãy bật server và thử lại.");
                yield break;
            }
            if (req.responseCode >= 200 && req.responseCode < 300)
            {
                done(true, text, null);
                yield break;
            }
            if (req.responseCode == 401 && auth) sessionToken = null;
            string message = null;
            try
            {
                ErrorBody e = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<ErrorBody>(text);
                message = e != null && !string.IsNullOrEmpty(e.message) ? e.message : null;
            }
            catch (ArgumentException) { }
            done(false, null, message ?? ("Máy chủ từ chối yêu cầu (" + req.responseCode + ")."));
        }

        [Serializable] private sealed class CredentialsBody { public string account; public string password; }
        [Serializable] private sealed class SessionBody { public string token; public string accountId; public long expiresMs; }
        [Serializable] private sealed class CreateBody { public string characterName; public int heroClass; public int slotIndex; }
        [Serializable] private sealed class LocationBody { public string mapId; public float x; public float y; }
        [Serializable] private sealed class ErrorBody { public string code; public string message; }
    }
}

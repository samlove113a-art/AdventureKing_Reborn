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
            ApplyEndpointOverride();
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            if (entryFlow) entryFlow.SetAccountService(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// For tests / launcher configuration. Accepted: loopback, HTTPS, or (development builds only) plain HTTP to a
        /// private LAN address (192.168.x.x, 10.x.x.x, 172.16–31.x.x) so a phone can reach the PC test server.
        /// </summary>
        public void SetBaseUrl(string url)
        {
            if (!IsAllowedUrl(url, Debug.isDebugBuild || Application.isEditor))
                throw new ArgumentException("Server URL must be HTTPS (or loopback / private LAN in development builds).");
            baseUrl = url.Trim().TrimEnd('/');
        }

        public static bool IsAllowedUrl(string url, bool developmentBuild)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            url = url.Trim();
            if (url.StartsWith("https://", StringComparison.Ordinal)) return true;
            if (!url.StartsWith("http://", StringComparison.Ordinal)) return false;
            string host = url.Substring(7).Split('/', ':')[0];
            if (host == "127.0.0.1" || host == "localhost") return true;
            if (!developmentBuild) return false;
            string[] p = host.Split('.');
            if (p.Length != 4 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b)) return false;
            return a == 10 || (a == 192 && b == 168) || (a == 172 && b >= 16 && b <= 31);
        }

        /// <summary>
        /// Optional override shipped in Resources/ServerEndpoint.txt (one line, e.g. http://192.168.1.46:5080/api/v1)
        /// so a phone build can reach the PC test server over the LAN. Ignored when invalid.
        /// </summary>
        private void ApplyEndpointOverride()
        {
            TextAsset t = Resources.Load<TextAsset>("ServerEndpoint");
            if (!t) return;
            string url = t.text.Trim();
            if (url.Length > 0 && IsAllowedUrl(url, Debug.isDebugBuild || Application.isEditor)) baseUrl = url.TrimEnd('/');
            else if (url.Length > 0) Debug.LogWarning("ServerEndpoint.txt ignored: URL not allowed in this build.");
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

        /// <summary>Asks the server to use a portal; the server checks map + reach and returns the new location.</summary>
        public void Travel(string characterId, string portalId, Vector2 position,
            Action<bool, AdventureCharacterSnapshot, string> completed)
        {
            string body = JsonUtility.ToJson(new LocationBody { mapId = "", x = position.x, y = position.y });
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/travel/" + UnityWebRequest.EscapeURL(portalId),
                body, true, (ok, json, err) => completed(ok, ok ? JsonUtility.FromJson<AdventureCharacterSnapshot>(json) : null, err));
        }

        // ------------------------------------------------------------------ wardrobe / fashion shop
        public void GetWardrobe(string characterId, Action<bool, WardrobeSnapshot, string> completed) =>
            Send("GET", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/wardrobe", null, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<WardrobeSnapshot>(json) : null, err));

        /// <summary>
        /// Buys one catalog item. <paramref name="requestId"/> identifies this purchase intent: reuse it when retrying
        /// after a lost connection, so the server can answer with the original result instead of charging again.
        /// The client sends no price and no balance: the server decides both.
        /// </summary>
        public void PurchaseCosmetic(string characterId, string itemId, string requestId, bool equip, Action<bool, WardrobeSnapshot, string, bool> completed)
        {
            string body = JsonUtility.ToJson(new PurchaseBody { itemId = itemId, requestId = requestId, equip = equip });
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/wardrobe/purchase", body, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<WardrobeSnapshot>(json) : null, err, !ok && lastWasConnectionError));
        }

        public void EquipOutfit(string characterId, OutfitLoadout outfit, Action<bool, WardrobeSnapshot, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/wardrobe/equip", JsonUtility.ToJson(outfit ?? new OutfitLoadout()), true,
                (ok, json, err) => completed(ok, ok ? JsonUtility.FromJson<WardrobeSnapshot>(json) : null, err));

        // ------------------------------------------------------------------ farm (the server owns HP, kills, Gold)
        public void GetFarm(string characterId, Action<bool, FarmState, string> completed) =>
            Send("GET", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm", null, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<FarmState>(json) : null, err));

        /// <summary>"I swing at this monster from here." No damage is sent: the server applies its class table.</summary>
        public void FarmAttack(string characterId, string spawnId, Vector2 position, Action<bool, FarmAttackResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm/attack",
                JsonUtility.ToJson(new AttackBody { spawnId = spawnId, x = position.x, y = position.y }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<FarmAttackResult>(json) : null, err));

        /// <summary>"I pick up this drop from here." The server checks owner + reach and credits the wallet once.</summary>
        public void FarmPickup(string characterId, string dropId, Vector2 position, Action<bool, FarmPickupResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm/pickup",
                JsonUtility.ToJson(new PickupBody { dropId = dropId, x = position.x, y = position.y }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<FarmPickupResult>(json) : null, err));

        /// <summary>Position heartbeat on a farm map; the answer is the hero's server-side HP (monster hits resolved).</summary>
        public void FarmPresence(string characterId, Vector2 position, Action<bool, HeroCombatState, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm/presence",
                JsonUtility.ToJson(new LocationBody { mapId = "", x = position.x, y = position.y }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<HeroCombatState>(json) : null, err));

        public void FarmRevive(string characterId, Action<bool, HeroCombatState, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm/revive", "{}", true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<HeroCombatState>(json) : null, err));

        /// <summary>"Cast skill N here, facing this way (at this monster)". Damage, MP, cooldown are the server's.</summary>
        public void FarmSkill(string characterId, int slot, string spawnId, Vector2 position, int facing, Action<bool, SkillCastResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/farm/skill",
                JsonUtility.ToJson(new SkillBody { slot = slot, spawnId = spawnId, x = position.x, y = position.y, facing = facing }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<SkillCastResult>(json) : null, err));

        public void GetSkills(Action<bool, SkillConfigDto, string> completed) =>
            Send("GET", "/skills", null, false, (ok, json, err) => completed(ok, ok ? JsonUtility.FromJson<SkillConfigDto>(json) : null, err));

        // ------------------------------------------------------------------ inventory (server-owned bag + gear)
        public void GetInventory(string characterId, Action<bool, InventoryState, string> completed) =>
            Send("GET", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/inventory", null, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<InventoryState>(json) : null, err));

        public void UseItem(string characterId, int slot, Action<bool, UseItemResult, string> completed, bool force = false) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/inventory/use", JsonUtility.ToJson(new UseBody { slot = slot, force = force }), true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<UseItemResult>(json) : null, err));

        public void EquipItem(string characterId, int slot, Action<bool, InventoryState, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/inventory/equip", JsonUtility.ToJson(new SlotBody { slot = slot }), true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<InventoryState>(json) : null, err));

        public void UnequipItem(string characterId, string equipSlot, Action<bool, InventoryState, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/inventory/unequip", JsonUtility.ToJson(new UnequipBody { equipSlot = equipSlot }), true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<InventoryState>(json) : null, err));

        // ------------------------------------------------------------------ NPC shop, tickets, quests
        public void GetShop(string shopId, Action<bool, NpcShop, string> completed) =>
            Send("GET", "/shops/" + UnityWebRequest.EscapeURL(shopId), null, false, (ok, json, err) => completed(ok, ok ? JsonUtility.FromJson<NpcShop>(json) : null, err));

        public void BuyFromShop(string characterId, string shopId, string itemId, int qty, string requestId, Action<bool, BuyResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/shop/buy",
                JsonUtility.ToJson(new BuyBody { shopId = shopId, itemId = itemId, qty = qty, requestId = requestId }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<BuyResult>(json) : null, err));

        public void UseTicket(string characterId, int slot, Action<bool, TicketResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/travel/ticket", JsonUtility.ToJson(new SlotBody { slot = slot }), true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<TicketResult>(json) : null, err));

        public void GetQuests(string characterId, Action<bool, QuestLog, string> completed) =>
            Send("GET", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/quests", null, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<QuestLog>(json) : null, err));

        public void ClaimQuest(string characterId, string questId, Action<bool, QuestClaimResult, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/quests/claim", JsonUtility.ToJson(new QuestBody { questId = questId }), true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<QuestClaimResult>(json) : null, err));

        public void QuestEvent(string characterId, string ev, Action<bool, QuestLog, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/quests/event", "{\"event\":\"" + ev.Replace("\"", "") + "\"}", true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<QuestLog>(json) : null, err));

        [Serializable] private sealed class BuyBody { public string shopId, itemId, requestId; public int qty; }
        [Serializable] private sealed class QuestBody { public string questId; }
        [Serializable] private sealed class SkillBody { public int slot; public string spawnId; public float x, y; public int facing; }
        [Serializable] private sealed class SlotBody { public int slot; }
        [Serializable] private sealed class UseBody { public int slot; public bool force; }
        [Serializable] private sealed class UnequipBody { public string equipSlot; }

        // ------------------------------------------------------------------ 7-day check-in (server decides everything)
        public void GetCheckin(string characterId, Action<bool, CheckinStatus, string> completed) =>
            Send("GET", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/checkin", null, true, (ok, json, err) =>
                completed(ok, ok ? JsonUtility.FromJson<CheckinStatus>(json) : null, err));

        public void ClaimCheckin(string characterId, string choiceItemId, Action<bool, CheckinStatus, string> completed) =>
            Send("POST", "/characters/" + UnityWebRequest.EscapeURL(characterId) + "/checkin/claim",
                JsonUtility.ToJson(new ClaimBody { choiceItemId = choiceItemId }), true, (ok, json, err) =>
                    completed(ok, ok ? JsonUtility.FromJson<CheckinStatus>(json) : null, err));

        /// <summary>VIP level from verified payments only (server decides; thresholds may be unconfigured).</summary>
        public void GetVip(Action<bool, int, bool, string> completed) =>
            Send("GET", "/vip", null, true, (ok, json, err) =>
            {
                VipBody v = ok ? JsonUtility.FromJson<VipBody>(json) : null;
                completed(ok && v != null, v?.level ?? 0, v?.configured ?? false, err);
            });

        private bool lastWasConnectionError;

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
            lastWasConnectionError = req.result == UnityWebRequest.Result.ConnectionError;
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
        [Serializable] private sealed class VipBody { public int level; public long paidVnd; public bool configured; }
        [Serializable] private sealed class PurchaseBody { public string itemId; public string requestId; public bool equip; }
        [Serializable] private sealed class ErrorBody { public string code; public string message; }
        [Serializable] private sealed class AttackBody { public string spawnId; public float x; public float y; }
        [Serializable] private sealed class PickupBody { public string dropId; public float x; public float y; }
        [Serializable] private sealed class ClaimBody { public string choiceItemId; }
    }
}

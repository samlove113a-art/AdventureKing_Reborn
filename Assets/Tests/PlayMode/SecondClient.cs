using System;
using System.Collections;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace AdventureKing.Tests
{
    /// <summary>
    /// A second, independent game client for multiplayer tests: its own account, session token and character on the
    /// same test server, talking to the exact endpoints the game uses (register, create, enter, travel, the 6 Hz sync,
    /// chat, friends, guild, marriage). It keeps its own heartbeat running, so the server sees it as present on its map.
    /// </summary>
    public sealed class SecondClient
    {
        private readonly string api;
        private string token;
        public string CharacterId { get; private set; }
        public string Name { get; private set; }
        public string MapId { get; private set; } = "village_wind_bell";
        public SyncState LastSync { get; private set; }
        public long ChatCursor { get; private set; }
        public int Syncs { get; private set; }
        public float X = 2f, Y = 0.9f;
        public int Facing = 1;
        public string Anim = "idle";
        public bool Heartbeat = true;
        public string LastError { get; private set; }

        public SecondClient(string apiUrl) { api = apiUrl; }

        [Serializable] private class Session { public string token; }
        [Serializable] private class Cred { public string account, password; }
        [Serializable] private class Create { public string characterName; public int heroClass, slotIndex; }
        [Serializable] private class Loc { public string mapId = ""; public float x, y; }
        [Serializable] private class Chat { public string channel, to, text; }
        [Serializable] private class Target { public string id, name, role; public bool accept; }
        [Serializable] private class Named { public string name; }

        public IEnumerator Call(string method, string path, object body, Action<string> ok, bool mustSucceed = true)
        {
            using var req = new UnityWebRequest(api + path, method);
            string json = body == null ? (method == "POST" ? "{}" : null) : JsonUtility.ToJson(body);
            if (json != null) req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (token != null) req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 10;
            yield return req.SendWebRequest();
            string text = req.downloadHandler.text;
            if (req.result != UnityWebRequest.Result.Success)
            {
                LastError = $"{method} {path}: {req.responseCode} {text}";
                if (mustSucceed) Assert.Fail("second client: " + LastError);
                yield break;
            }
            ok?.Invoke(text);
        }

        public IEnumerator Register(string account, string password, string name, HeroClass cls)
        {
            yield return Call("POST", "/auth/register", new Cred { account = account, password = password }, j => token = JsonUtility.FromJson<Session>(j).token);
            yield return Call("POST", "/characters", new Create { characterName = name, heroClass = (int)cls, slotIndex = 0 }, j => CharacterId = JsonUtility.FromJson<AdventureCharacterSnapshot>(j).characterId);
            Name = name;
            yield return Call("POST", $"/characters/{CharacterId}/enter", null, null);
        }

        /// <summary>A fresh login (as after quitting the game) for persistence checks.</summary>
        public IEnumerator Login(string account, string password) =>
            Call("POST", "/auth/login", new Cred { account = account, password = password }, j => token = JsonUtility.FromJson<Session>(j).token);

        public IEnumerator Travel(string portal, float atX)
        {
            X = atX;
            yield return Call("POST", $"/characters/{CharacterId}/travel/{portal}", new Loc { x = atX, y = 0f }, j =>
            {
                var c = JsonUtility.FromJson<AdventureCharacterSnapshot>(j);
                MapId = c.lastMapId; X = c.posX; Y = c.posY;
            });
        }

        public IEnumerator SyncOnce()
        {
            var body = new AdventureServerConnection.SyncBody { x = X, y = Y, facing = Facing, anim = Anim, chatAfter = ChatCursor, practiceSlot = PracticeSlot, practiceSeq = PracticeSeq };
            yield return Call("POST", $"/characters/{CharacterId}/sync", body, j =>
            {
                LastSync = JsonUtility.FromJson<SyncState>(j);
                ChatCursor = LastSync.chatCursor;
                Syncs++;
            }, mustSucceed: false);
        }

        /// <summary>Runs the 6 Hz heartbeat until <see cref="Heartbeat"/> is turned off (start with StartCoroutine).</summary>
        public IEnumerator HeartbeatLoop()
        {
            while (Heartbeat)
            {
                yield return SyncOnce();
                yield return new WaitForSecondsRealtime(0.16f);
            }
        }

        /// <summary>Practice cast in a safe zone (sent with the next heartbeats, like the game's action bar does).</summary>
        public int PracticeSlot, PracticeSeq;
        public void Practice(int slot) { PracticeSlot = slot; PracticeSeq++; }

        [Serializable] private class Presence { public float x, y; }
        [Serializable] private class SkillReq { public int slot, facing; public float x, y; public string spawnId; }

        /// <summary>Casts a skill on a farm map through the real endpoints (position heartbeat first, as the game does).</summary>
        public IEnumerator Skill(int slot, string spawnId)
        {
            yield return Call("POST", $"/characters/{CharacterId}/farm/presence", new Presence { x = X, y = Y }, null);
            yield return Call("POST", $"/characters/{CharacterId}/farm/skill", new SkillReq { slot = slot, facing = Facing, x = X, y = Y, spawnId = spawnId }, null);
        }

        public IEnumerator Say(string channel, string to, string text) => Call("POST", $"/characters/{CharacterId}/chat", new Chat { channel = channel, to = to, text = text }, null);
        public IEnumerator AcceptFriend(string fromId) => Call("POST", $"/characters/{CharacterId}/friends/accept", new Target { id = fromId }, null);
        public IEnumerator ApplyGuild(string guildId) => Call("POST", $"/characters/{CharacterId}/guild/apply", new Target { id = guildId }, null);
        public IEnumerator AcceptProposal(string fromId) => Call("POST", $"/characters/{CharacterId}/marriage/respond", new Target { id = fromId, accept = true }, null);
        public IEnumerator ConfirmCeremony() => Call("POST", $"/characters/{CharacterId}/marriage/ceremony/confirm", null, null);

        public IEnumerator Get<T>(string path, Action<T> got) => Call("GET", $"/characters/{CharacterId}{path}", null, j => got(JsonUtility.FromJson<T>(j)));
    }
}

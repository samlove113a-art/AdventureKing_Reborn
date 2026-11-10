using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The multiplayer heartbeat of a world map: ~6 times a second it sends where the local hero is (server-checked) and
    /// how it moves, and receives the other players on this map (spawned / updated / removed as RemoteAvatar), new chat,
    /// social badges and the wedding ceremony state. Works the same on PC and phone (plain HTTPS requests).
    /// Also offers "E · Tương tác" next to another player, which opens the social window for them.
    /// </summary>
    public sealed class WorldSync : MonoBehaviour
    {
        [SerializeField, Min(0.08f)] private float interval = 0.16f;
        [SerializeField] private Font font;

        public static WorldSync Current { get; private set; }
        public SyncState Last { get; private set; }
        public long ChatCursor { get; private set; }
        public int Received { get; private set; }
        public string LastError { get; private set; }
        public readonly Dictionary<string, RemoteAvatar> Avatars = new Dictionary<string, RemoteAvatar>();
        public event Action<ChatMessageState[]> ChatArrived;
        public event Action<SyncState> Synced;
        public RemoteAvatar Nearest { get; private set; }

        private bool busy;
        private float next, lastReceipt;
        private Transform avatarRoot;

        /// <summary>The server clock now (ms), extrapolated from the last answer.</summary>
        public long ServerNowMs => Last == null ? 0 : Last.serverMs + (long)((Time.realtimeSinceStartup - lastReceipt) * 1000f);

        private void Awake() => Current = this;
        private void OnDestroy() { if (Current == this) Current = null; }

        private void Update()
        {
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null || !world.Player || world.Traveling || world.Leaving) return;
            var net = AdventureServerConnection.Instance;
            if (!net || !net.HasSession) return;
            UpdateInteraction(world);
            if (busy || Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            busy = true;
            var body = world.Player.GetComponent<Rigidbody2D>();
            Vector2 pos = body ? body.position : (Vector2)world.Player.transform.position;
            CharacterVisual3D v = world.PlayerVisual;
            var req = new AdventureServerConnection.SyncBody
            {
                x = pos.x, y = pos.y, facing = world.Player.Facing, anim = v ? v.NetAnim() : "idle",
                emote = v ? v.LastEmote ?? "" : "", emoteSeq = v ? v.EmoteSeq : 0, chatAfter = ChatCursor,
            };
            net.Sync(world.Character.characterId, req, (ok, s, err) =>
            {
                busy = false;
                if (!ok || s == null) { LastError = err; return; }
                Apply(s);
            });
        }

        private void Apply(SyncState s)
        {
            Last = s;
            lastReceipt = Time.realtimeSinceStartup;
            Received++;
            var world = AdventureWorldSession.Current;
            if (!avatarRoot) avatarRoot = new GameObject("RemotePlayers").transform;
            var seen = new HashSet<string>();
            foreach (PeerState p in s.peers)
            {
                seen.Add(p.id);
                if (Avatars.TryGetValue(p.id, out RemoteAvatar a) && a) { a.Apply(p); continue; }
                GameObject prefab = world && p.heroClass >= 0 && p.heroClass < world.HeroPrefabs.Length ? world.HeroPrefabs[p.heroClass] : null;
                if (!prefab) continue;
                Avatars[p.id] = RemoteAvatar.Create(p, prefab, world.HeroScale, font, avatarRoot);
            }
            foreach (string gone in Avatars.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                if (Avatars[gone]) Destroy(Avatars[gone].gameObject);
                Avatars.Remove(gone);
            }
            if (s.chat != null && s.chat.Length > 0) ChatArrived?.Invoke(s.chat);
            ChatCursor = s.chatCursor;
            Synced?.Invoke(s);
        }

        public RemoteAvatar Avatar(string characterId) => characterId != null && Avatars.TryGetValue(characterId, out RemoteAvatar a) ? a : null;

        // ---------------------------------------------------------------- interact with another player
        private void UpdateInteraction(AdventureWorldSession world)
        {
            Vector2 me = world.Player.transform.position;
            RemoteAvatar best = null;
            float bestD = 1.8f;
            foreach (RemoteAvatar a in Avatars.Values)
            {
                if (!a || a.Overridden) continue;
                Vector2 d = (Vector2)a.transform.position - me;
                if (Mathf.Abs(d.y) < 1.5f && Mathf.Abs(d.x) < bestD) { bestD = Mathf.Abs(d.x); best = a; }
            }
            if (best != Nearest)
            {
                if (Nearest) Nearest.SetPrompt(false);
                Nearest = best;
                if (Nearest) Nearest.SetPrompt(true);
            }
            var input = world.Player.GetComponent<PlayerInput2D>();
            if (Nearest && input && input.enabled && SocialPanel.Instance && input.ConsumeInteract())
                SocialPanel.Instance.OpenFor(Nearest.State.name);
        }
    }
}

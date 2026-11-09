using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>The character's quest log as the server last reported it, plus the few client calls quests need.</summary>
    public static class QuestBook
    {
        public static QuestLog Log { get; private set; }
        public static event Action Changed;
        public static string LastError { get; private set; }
        private static readonly Dictionary<string, float> lastSent = new Dictionary<string, float>();
        private static bool busy;

        private static string CharId => AdventureWorldSession.Current && AdventureWorldSession.Current.Character != null ? AdventureWorldSession.Current.Character.characterId : null;

        public static void Set(QuestLog log)
        {
            if (log == null) return;
            Log = log;
            var world = AdventureWorldSession.Current;
            if (world && world.Character != null && log.level > 0) { world.Character.level = log.level; world.Character.experience = log.exp; }
            Changed?.Invoke();
        }

        public static void Refresh()
        {
            if (busy || CharId == null || !AdventureServerConnection.Instance) return;
            busy = true;
            AdventureServerConnection.Instance.GetQuests(CharId, (ok, log, err) => { busy = false; if (ok) Set(log); else LastError = err; });
        }

        public static QuestState CurrentMain => Log != null && Log.main != null && Log.main.Length > 0 ? Log.main[0] : null;

        /// <summary>Reports a client-side quest event (talking to an NPC, practising in town). Practice events are only sent
        /// while the tutorial quest that needs them is open; everything else the server records on its own.</summary>
        public static void Event(string ev)
        {
            if (CharId == null || !AdventureServerConnection.Instance) return;
            if (ev.StartsWith("practice:"))
            {
                string q = CurrentMain?.id;
                if (!(ev == "practice:attack" ? q == "main_02" : q == "main_03")) return;
            }
            if (lastSent.TryGetValue(ev, out float t) && Time.unscaledTime - t < 0.6f) return;
            lastSent[ev] = Time.unscaledTime;
            AdventureServerConnection.Instance.QuestEvent(CharId, ev, (ok, log, err) => { if (ok) Set(log); else LastError = err; });
        }

        public static void Claim(string questId, Action<bool, QuestClaimResult, string> done)
        {
            if (CharId == null || !AdventureServerConnection.Instance) return;
            AdventureServerConnection.Instance.ClaimQuest(CharId, questId, (ok, r, err) =>
            {
                if (ok && r != null)
                {
                    var world = AdventureWorldSession.Current;
                    if (world && world.Character != null) world.Character.gold += r.gold;
                    Set(r.log);
                }
                done?.Invoke(ok, r, err);
            });
        }

        public static void Clear() { Log = null; lastSent.Clear(); }
    }



}

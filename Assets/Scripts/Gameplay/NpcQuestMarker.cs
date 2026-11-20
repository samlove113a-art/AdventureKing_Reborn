using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>"!" (quest here) or "?" (finished quest to hand in) floating over an NPC, from the server quest log.</summary>
    public sealed class NpcQuestMarker : MonoBehaviour
    {
        [SerializeField] private string npcName;
        [SerializeField] private Text label;
        public string Shown => label && label.enabled ? label.text : "";

        public void Bind(string npc, Text l) { npcName = npc; label = l; }

        private void Update()
        {
            if (!label) return;
            QuestLog log = QuestBook.Log;
            string mark = "";
            if (log != null)
            {
                if (log.turnInNpc == npcName) mark = "?";
                else if (QuestBook.CurrentMain is { } m && m.npc == npcName && !m.complete) mark = "!";
            }
            label.enabled = mark != "";
            label.text = mark;
            label.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 6f, 0f);
        }
    }
}

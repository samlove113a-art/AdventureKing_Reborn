using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Talk to an NPC (E / ▲ / the prompt): faces the hero, gestures, a speech bubble, and the quest event.</summary>
    public sealed class NpcTalkSpot : MonoBehaviour
    {
        [SerializeField] private NpcActor actor;
        [SerializeField] private string eventKey = "talk:elder";
        [SerializeField] private string npcName = "Trưởng Làng";
        [SerializeField] private GameObject prompt;
        [SerializeField] private Text bubble;
        [SerializeField, TextArea] private string[] lines = new string[0];
        [SerializeField, Min(0.5f)] private float range = 2.4f;
        public bool PlayerNear { get; private set; }
        public int Talks { get; private set; }
        public string Said => bubble ? bubble.text : "";
        private float bubbleUntil;
        private int next;

        public void Bind(NpcActor a, string ev, string npc, GameObject p, Text b, string[] l) { actor = a; eventKey = ev; npcName = npc; prompt = p; bubble = b; lines = l; }

        private void Update()
        {
            var world = AdventureWorldSession.Current;
            if (!world || !world.Player) return;
            var greeter = FindAnyObjectByType<ElderGreeter>();
            bool busy = greeter && greeter.Greeting;
            Vector2 d = world.Player.transform.position - transform.position;
            bool near = !busy && Mathf.Abs(d.x) < range && Mathf.Abs(d.y) < 2.2f && !(world.Traveling || world.Leaving);
            if (near != PlayerNear)
            {
                PlayerNear = near;
                if (prompt) prompt.SetActive(near);
                if (actor) { if (near) actor.Face(world.Player.transform.position.x); else actor.FaceFront(); }
            }
            var input = world.Player.GetComponent<PlayerInput2D>();
            if (near && input && input.enabled && input.ConsumeInteract()) Talk();
            if (bubble && bubble.transform.parent.gameObject.activeSelf && Time.time > bubbleUntil) bubble.transform.parent.gameObject.SetActive(false);
        }

        /// <summary>Raised after each conversation (e.g. the Officiant starts the ceremony rehearsal).</summary>
        public event Action Talked;

        public void Talk()
        {
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null) return;
            Talks++;
            if (!string.IsNullOrEmpty(eventKey)) QuestBook.Event(eventKey); // hall NPCs have no quest event
            string text;
            QuestLog log = QuestBook.Log;
            if (log != null && log.turnInNpc == npcName) text = "Làm tốt lắm! Mở bảng Nhiệm vụ để nhận thưởng nhé.";
            else text = lines.Length > 0 ? lines[next++ % lines.Length].Replace("{name}", world.Character.characterName) : "Chào con!";
            if (bubble) { bubble.text = text; bubble.transform.parent.gameObject.SetActive(true); bubbleUntil = Time.time + 4.5f; }
            if (actor) StartCoroutine(actor.Gesture("Talk", 1.4f));
            Talked?.Invoke();
        }
    }
}

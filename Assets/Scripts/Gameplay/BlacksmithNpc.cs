using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Thợ Rèn in the town. Walk up: "Nói chuyện" (E / ▲ / touch button) appears; talking turns him to the hero, he waves, the quest event
    /// "talk:smith" is sent and the forge window opens (BlacksmithUI). Closing it, he bows. Same interaction pattern as the fashion merchant.
    /// </summary>
    public sealed class BlacksmithNpc : MonoBehaviour
    {
        [SerializeField] private NpcActor actor;
        [SerializeField] private BlacksmithUI forge;
        [SerializeField, Min(0.5f)] private float talkRange = 2.4f;

        public bool PlayerNear { get; private set; }
        public bool Busy { get; private set; }
        private PlayerInput2D input;

        private void Start()
        {
            if (!actor) actor = GetComponent<NpcActor>();
            if (!forge) forge = FindAnyObjectByType<BlacksmithUI>(FindObjectsInactive.Include);
            if (!forge) { enabled = false; return; }
            forge.TalkButton.onClick.AddListener(Talk);
            forge.Closed += () => { if (actor) StartCoroutine(actor.Gesture("Bow", 1.4f)); };
        }

        private void Update()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            PlayerMotor2D player = world ? world.Player : null;
            if (!player || !forge) return;
            if (!input) input = player.GetComponent<PlayerInput2D>();
            Vector2 d = player.transform.position - transform.position;
            bool near = Mathf.Abs(d.x) < talkRange && Mathf.Abs(d.y) < 2.2f && !forge.IsOpen && !(world.Traveling || world.Leaving);
            if (near != PlayerNear)
            {
                PlayerNear = near;
                forge.TalkButton.gameObject.SetActive(near);
                if (actor && !forge.IsOpen) { if (near) actor.Face(player.transform.position.x); else actor.FaceFront(); }
            }
            if (near && input && input.enabled && input.ConsumeInteract()) Talk();
        }

        public void Talk()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (Busy || !forge || forge.IsOpen || !world || world.Character == null) return;
            QuestBook.Event("talk:smith");
            StartCoroutine(Greet(world));
        }

        private IEnumerator Greet(AdventureWorldSession world)
        {
            Busy = true;
            forge.TalkButton.gameObject.SetActive(false);
            if (actor) { actor.Face(world.Player.transform.position.x); actor.Play("Wave", 0.12f); }
            forge.Open($"Chào <b>{world.Character.characterName}</b>! Đưa trang bị đây, lò rèn của ta nóng sẵn rồi.");
            yield return new WaitForSeconds(1.2f);
            if (actor && forge.IsOpen) actor.Play("Talk");
            Busy = false;
        }
    }
}

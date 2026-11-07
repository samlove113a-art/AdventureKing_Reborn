using UnityEngine;
using UnityEngine.Events;

namespace AdventureKing.Gameplay
{
    // Hook for actual NPC dialogue/quest UI.
    [RequireComponent(typeof(Collider2D))]
    public sealed class NpcInteraction2D : MonoBehaviour
    {
        [SerializeField] private UnityEvent onInteracted;
        private PlayerInput2D nearPlayer;

        private void Reset() => GetComponent<Collider2D>().isTrigger = true;

        private void Update()
        {
            if (nearPlayer && nearPlayer.ConsumeInteract()) onInteracted?.Invoke();
        }

        private void OnDisable() => nearPlayer = null;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var input = other.GetComponentInParent<PlayerInput2D>();
            if (input) nearPlayer = input;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerInput2D>() == nearPlayer) nearPlayer = null;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace AdventureKing.Gameplay
{
    // UGUI buttons: pointer up, exit or disable must never leave movement stuck.
    public sealed class TouchButton2D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private PlayerInput2D target;
        [SerializeField] private PlayerInput2D.TouchAction action;
        private bool pressed;

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            if (target) target.SetTouchHeld(action, true);
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();
        private void OnDisable() => Release();

        private void Release()
        {
            if (!pressed) return;
            pressed = false;
            if (target) target.SetTouchHeld(action, false);
        }
    }
}

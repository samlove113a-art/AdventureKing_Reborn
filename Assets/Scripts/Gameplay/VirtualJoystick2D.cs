using UnityEngine;
using UnityEngine.EventSystems;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Round on-screen joystick (bottom-left on phones). It follows one finger only (its own pointer id), so
    /// the other thumb can jump / attack at the same time. Release, cancel or disable always recentres it.
    /// </summary>
    public sealed class VirtualJoystick2D : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private PlayerInput2D target;
        [SerializeField] private RectTransform knob;
        [Tooltip("Knob travel radius in canvas units.")]
        [SerializeField] private float radius = 110f;
        [Range(0f, 0.6f)] [SerializeField] private float deadZone = 0.22f;

        private int pointerId = int.MinValue;
        private RectTransform rect;

        public Vector2 Value { get; private set; }
        public bool Held => pointerId != int.MinValue;

        private void Awake() => rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;          // a second finger on the pad is ignored
            pointerId = e.pointerId;
            Track(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointerId) Track(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        private void OnDisable() => Release();

        private void Track(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local - rect.rect.center, radius);
            if (knob) knob.anchoredPosition = offset;
            Vector2 v = offset / radius;
            float m = v.magnitude;
            // Rescale past the dead zone so small thumb jitter never walks the hero.
            Value = m < deadZone ? Vector2.zero : v.normalized * Mathf.InverseLerp(deadZone, 1f, m);
            if (target) target.SetStick(Value);
        }

        private void Release()
        {
            pointerId = int.MinValue;
            Value = Vector2.zero;
            if (knob) knob.anchoredPosition = Vector2.zero;
            if (target) target.SetStick(Vector2.zero);
        }
    }
}

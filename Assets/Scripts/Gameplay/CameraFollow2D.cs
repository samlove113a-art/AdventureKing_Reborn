using UnityEngine;

namespace AdventureKing.Gameplay
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.25f);
        [SerializeField, Min(0.01f)] private float smoothing = 0.16f;
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds;
        [SerializeField] private Vector2 maxBounds;
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (!target) return;
            Vector3 desired = new Vector3(target.position.x + offset.x,
                target.position.y + offset.y, transform.position.z);
            if (useBounds)
            {
                desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
                desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothing);
        }
    }
}

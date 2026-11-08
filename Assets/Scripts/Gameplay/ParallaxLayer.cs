using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Far scenery that drifts with the camera (factor 1 = glued to the camera, 0 = static world object),
    /// so a painted backdrop reads as very distant without needing an enormous quad.
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Range(0f, 1f)] private float followX = 0.9f;
        [SerializeField, Range(0f, 1f)] private float followY = 0.85f;
        private Vector3 origin;

        public void Bind(Transform cam, float fx, float fy)
        {
            cameraTransform = cam;
            followX = fx;
            followY = fy;
        }

        public Vector2 Follow => new Vector2(followX, followY);

        private void Awake() => origin = transform.position;

        private void LateUpdate()
        {
            if (!cameraTransform) return;
            Vector3 c = cameraTransform.position;
            transform.position = new Vector3(origin.x + c.x * followX, origin.y + c.y * followY, origin.z);
        }
    }
}

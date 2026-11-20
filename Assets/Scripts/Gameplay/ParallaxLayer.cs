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
        [SerializeField] private float driftSpeed;   // m/s of slow wind drift (clouds); 0 = none
        [SerializeField] private float wrapWidth = 120f;
        private Vector3 origin;
        private float drift;

        public void Bind(Transform cam, float fx, float fy)
        {
            cameraTransform = cam;
            followX = fx;
            followY = fy;
        }

        public void SetDrift(float speed, float wrap) { driftSpeed = speed; wrapWidth = wrap; }

        public Vector2 Follow => new Vector2(followX, followY);

        private void Awake() => origin = transform.position;

        private void LateUpdate()
        {
            if (!cameraTransform) return;
            Vector3 c = cameraTransform.position;
            if (driftSpeed != 0f) drift = Mathf.Repeat(drift + driftSpeed * Time.deltaTime + wrapWidth * 0.5f, wrapWidth) - wrapWidth * 0.5f;
            transform.position = new Vector3(origin.x + drift + c.x * followX, origin.y + c.y * followY, origin.z);
        }
    }
}

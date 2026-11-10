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

        // ---- skill feedback (presentation only): decaying shake + short FOV punch, applied on top of the follow
        private static float shakeAmp, shakeUntil, shakeLength;
        private static float punch, punchUntil, punchLength;
        private Vector3 shakeOffset;
        private Camera cam;
        private float baseFov = -1f;

        /// <summary>Shake strength right now (QA).</summary>
        public static float ShakeNow => Time.unscaledTime < shakeUntil ? shakeAmp * (shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, shakeLength) : 0f;

        public static void AddShake(float amplitude, float seconds)
        {
            if (amplitude >= ShakeNow) { shakeAmp = amplitude; shakeLength = seconds; shakeUntil = Time.unscaledTime + seconds; }
        }

        /// <summary>Zooms in by <paramref name="amount"/> (0.06 = 6 %) and eases back.</summary>
        public static void AddZoomPunch(float amount, float seconds) { punch = amount; punchLength = seconds; punchUntil = Time.unscaledTime + seconds; }

        private static float focusX, focusUntil;
        /// <summary>For <paramref name="seconds"/>, frame the midpoint between the hero and <paramref name="x"/>'s action.</summary>
        public static void Focus(float x, float seconds) { focusX = x; focusUntil = Time.time + seconds; }

        private void LateUpdate()
        {
            if (!target) return;
            transform.position -= shakeOffset;
            // during a skill the action (hero ↔ target midpoint) sits left of centre, clear of the right-hand HUD cluster
            // (the HUD cluster is on the RIGHT whichever way the hero faces: always shift the framing the same way —
            // a facing-based sign pushed a left-facing hero under the buttons)
            float fx = Time.time < focusUntil ? focusX + 1.1f : target.position.x;
            Vector3 desired = new Vector3(fx + offset.x,
                target.position.y + offset.y, transform.position.z);
            if (useBounds)
            {
                desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
                desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothing);
            float s = ShakeNow;
            shakeOffset = s > 0f ? new Vector3((Mathf.PerlinNoise(Time.unscaledTime * 38f, 1.3f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(4.1f, Time.unscaledTime * 41f) - 0.5f) * 2f * s, 0f) : Vector3.zero;
            transform.position += shakeOffset;
            if (!cam) cam = GetComponent<Camera>();
            // FOV is touched only while a punch runs (cinematics own the FOV otherwise)
            float k = Time.unscaledTime < punchUntil ? (punchUntil - Time.unscaledTime) / Mathf.Max(0.01f, punchLength) : 0f;
            if (cam && k > 0f)
            {
                if (baseFov < 0f) baseFov = cam.fieldOfView;
                cam.fieldOfView = baseFov * (1f - punch * Mathf.Sin(k * Mathf.PI * 0.5f));
            }
            else if (cam && baseFov > 0f) { cam.fieldOfView = baseFov; baseFov = -1f; }
        }
    }
}

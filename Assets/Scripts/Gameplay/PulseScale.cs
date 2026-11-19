using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>A slow, stepped scale pulse (1.00 -> 1.04, snapped to 0.01) that makes the main call-to-action breathe. Unscaled time.</summary>
    [DisallowMultipleComponent]
    public sealed class PulseScale : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amount = 0.04f, periodSeconds = 1.8f;

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.2832f / periodSeconds);
            float s = 1f + Mathf.Round(k * amount * 100f) / 100f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}

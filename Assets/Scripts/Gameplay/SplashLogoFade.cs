using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Fades and gently scales the studio logo in on the splash screen (unscaled time).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SplashLogoFade : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.8f;
        [SerializeField] private float startScale = 0.94f;
        private CanvasGroup group;
        private float startedAt;

        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            startedAt = Time.unscaledTime;
            group.alpha = 0f;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / fadeInSeconds);
            float e = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
            group.alpha = e;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, e);
        }
    }
}

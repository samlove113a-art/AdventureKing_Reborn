using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Gentle parallax for the pixel-art entry backdrop: every layer (panorama, far props, ground band, near props) drifts sideways on a
    /// slow sine at its own depth, snapped to a 2 px grid (pixel-art crisp, no shimmer). Pure UI Images, unscaled time. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelBackdropUI : MonoBehaviour
    {
        [SerializeField] private RectTransform[] layers = new RectTransform[0];
        [Tooltip("Share of the sway each layer follows (0 = still sky .. 1 = nearest).")]
        [SerializeField] private float[] depth = new float[0];
        [SerializeField, Min(0f)] private float swayPixels = 46f;
        [SerializeField, Min(1f)] private float periodSeconds = 26f;

        private Vector2[] home;

        private void Awake()
        {
            home = new Vector2[layers.Length];
            for (int i = 0; i < layers.Length; i++) if (layers[i]) home[i] = layers[i].anchoredPosition;
        }

        private void Update()
        {
            if (home == null) return;
            float s = Mathf.Sin(Time.unscaledTime * 6.2832f / periodSeconds);
            for (int i = 0; i < layers.Length; i++)
            {
                if (!layers[i]) continue;
                float d = i < depth.Length ? depth[i] : 0.5f;
                float x = home[i].x + Mathf.Round(s * swayPixels * d / 2f) * 2f;
                layers[i].anchoredPosition = new Vector2(x, home[i].y);
            }
        }
    }
}

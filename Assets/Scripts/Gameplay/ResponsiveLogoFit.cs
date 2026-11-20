using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Scales the logo block down on narrow / short screens (phones) so it never exceeds ~84% of the width or ~46% of the height of the Canvas.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ResponsiveLogoFit : MonoBehaviour
    {
        [SerializeField, Range(0.3f, 1.2f)] private float maxWidthShare = 0.84f, maxHeightShare = 0.46f;
        private RectTransform rt;
        private Vector2 baseSize;

        /// <summary>Fits a window instead of a logo: never larger than these shares of the Canvas (scale only goes down, never up).</summary>
        public ResponsiveLogoFit Configure(float widthShare, float heightShare) { maxWidthShare = widthShare; maxHeightShare = heightShare; return this; }

        private void OnEnable() { rt = (RectTransform)transform; baseSize = rt.sizeDelta; Apply(); }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (!rt) return;
            if (baseSize.x < 1f) baseSize = rt.sizeDelta;
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;
            Rect c = ((RectTransform)canvas.rootCanvas.transform).rect;
            if (c.width < 2f) return;
            float s = Mathf.Min(1f, c.width * maxWidthShare / baseSize.x, c.height * maxHeightShare / baseSize.y);
            rt.localScale = new Vector3(s, s, 1f);
        }
    }
}

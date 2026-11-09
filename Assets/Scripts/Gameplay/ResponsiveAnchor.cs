using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Places a panel at a different horizontal anchor on wide vs narrow screens (e.g. login card on the right third
    /// of a 16:9 / 19.5:9 screen so the scene stays visible, centred on a 4:3 tablet). Re-evaluates when the canvas
    /// size changes (rotation, window resize).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveAnchor : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float wideX = 0.7f;
        [SerializeField, Range(0f, 1f)] private float narrowX = 0.5f;
        [Tooltip("Width / height at or above which the wide layout is used.")]
        [SerializeField] private float wideAspect = 1.55f;
        [Tooltip("Hide on narrow screens (e.g. the title logo beside the login card has no room on a 4:3 tablet).")]
        [SerializeField] private bool hideWhenNarrow;
        private CanvasGroup group;

        private static readonly System.Collections.Generic.List<ResponsiveAnchor> live = new System.Collections.Generic.List<ResponsiveAnchor>();

        /// <summary>Re-applies every active anchor now (after a canvas was laid out at another size, e.g. QA captures).</summary>
        public static void ApplyAll() { foreach (ResponsiveAnchor a in live) if (a) a.Apply(); }

        private void OnEnable() { if (!live.Contains(this)) live.Add(this); Apply(); }
        private void OnDisable() => live.Remove(this);
        private void OnRectTransformDimensionsChange() => Apply();
        private void Update() => Apply();

        public void Apply()
        {
            var parent = transform.parent as RectTransform;
            if (!parent) return;
            Rect r = parent.rect;
            if (r.height <= 1f) return;
            bool wide = r.width / r.height >= wideAspect;
            float x = wide ? wideX : narrowX;
            if (hideWhenNarrow)
            {
                if (!group) group = GetComponent<CanvasGroup>();
                if (!group) group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = wide ? 1f : 0f;
                group.blocksRaycasts = wide;
            }
            var rt = (RectTransform)transform;
            if (Mathf.Approximately(rt.anchorMin.x, x) && Mathf.Approximately(rt.anchorMax.x, x)) return;
            rt.anchorMin = new Vector2(x, rt.anchorMin.y);
            rt.anchorMax = new Vector2(x, rt.anchorMax.y);
        }
    }
}

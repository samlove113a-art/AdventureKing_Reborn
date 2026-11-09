using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Shortcut icons next to the minimap. PC: every icon visible (two rows). Phones/tablets: only the key ones;
    /// "Thêm" shows / hides the secondary ones (on PC "Thêm" opens its info popup instead).
    /// </summary>
    public sealed class HudShortcutBar : MonoBehaviour
    {
        [SerializeField] private GameObject[] secondary = new GameObject[0];
        [SerializeField] private HudFeatureButton moreInfo;
        [Tooltip("QA: force the phone layout in the editor.")]
        [SerializeField] private bool forceCompact;
        [Tooltip("Arrow beside the icons: collapses / expands the whole icon block (PC and mobile).")]
        [SerializeField] private RectTransform arrowIcon;

        private const string CollapsedKey = "ak_hud_icons_collapsed";
        private CanvasGroup group;
        private float shown = 1f;

        public bool Compact { get; private set; }
        public bool Expanded { get; private set; }
        /// <summary>Whole icon block folded away (arrow). A local layout preference, not game data.</summary>
        public bool Collapsed { get; private set; }

        private void Start()
        {
            SetCompact(forceCompact || Application.isMobilePlatform);
            group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
            bool saved = false;
            try { saved = PlayerPrefs.GetInt(CollapsedKey, 0) == 1; } catch { }
            SetCollapsed(saved, true);
        }

        /// <summary>The arrow button.</summary>
        public void ToggleCollapsed() => SetCollapsed(!Collapsed, false);

        public void SetCollapsed(bool collapsed, bool instant)
        {
            Collapsed = collapsed;
            if (group) { group.interactable = !collapsed; group.blocksRaycasts = !collapsed; }
            if (instant) { shown = collapsed ? 0f : 1f; Apply(); }
            try { PlayerPrefs.SetInt(CollapsedKey, collapsed ? 1 : 0); } catch { }
        }

        private void Update()
        {
            float target = Collapsed ? 0f : 1f;
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 5f);
            Apply();
        }

        private void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, shown);
            if (group) group.alpha = e;
            transform.localScale = new Vector3(Mathf.Max(0.001f, e), 1f, 1f); // folds toward the right edge (pivot top-right)
            if (arrowIcon) arrowIcon.localScale = new Vector3(Collapsed ? -1f : 1f, 1f, 1f); // chevron points the way the block will move
        }

        public void SetCompact(bool compact)
        {
            Compact = compact;
            Expanded = !compact;
            foreach (GameObject g in secondary) if (g) g.SetActive(Expanded);
        }

        /// <summary>"Thêm" button.</summary>
        public void More()
        {
            if (!Compact) { if (moreInfo) moreInfo.Open(); return; }
            Expanded = !Expanded;
            foreach (GameObject g in secondary) if (g) g.SetActive(Expanded);
        }
    }
}

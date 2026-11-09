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

        public bool Compact { get; private set; }
        public bool Expanded { get; private set; }

        private void Start() => SetCompact(forceCompact || Application.isMobilePlatform);

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

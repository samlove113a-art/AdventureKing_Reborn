using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.QA
{
    /// <summary>
    /// INTERNAL QA ONLY: minimal UGUI dialogue handler wired to NpcInteraction2D.onInteracted,
    /// so the interact path is exercised through a real UI element. Final dialogue/quest UI is
    /// out of scope for this foundation.
    /// </summary>
    public sealed class QaNpcDialog : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text label;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 2.5f;
        private float hideAt;

        public int InteractCount { get; private set; }

        public void Show()
        {
            InteractCount++;
            if (label) label.text = $"[QA] NPC: Xin chào! (lần tương tác #{InteractCount})";
            if (panel) panel.SetActive(true);
            hideAt = Time.unscaledTime + visibleSeconds;
        }

        private void Update()
        {
            if (panel && panel.activeSelf && Time.unscaledTime >= hideAt) panel.SetActive(false);
        }
    }
}

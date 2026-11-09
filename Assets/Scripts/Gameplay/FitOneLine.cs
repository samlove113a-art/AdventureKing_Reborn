using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Keeps a short label (button, tab, chip) on ONE line: picks the largest font size between min and max whose
    /// single-line width fits the rect (uGUI best-fit would rather wrap "Tạo & vào game" onto two lines).
    /// Re-fits when the rect or the text changes; falls back to wrapping only below the minimum size.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Text))]
    public sealed class FitOneLine : MonoBehaviour
    {
        [SerializeField, Min(8)] private int maxSize = 26;
        [SerializeField, Min(8)] private int minSize = 15;

        private Text text;
        private string lastText;
        private float lastWidth = -1f;
        private static readonly List<FitOneLine> live = new List<FitOneLine>();

        public static void ApplyAll() { foreach (FitOneLine f in live) if (f) f.Fit(true); }

        public void Configure(int max, int min) { maxSize = max; minSize = min; Fit(true); }

        private void OnEnable() { if (!live.Contains(this)) live.Add(this); Fit(true); }
        private void OnDisable() => live.Remove(this);
        private void OnRectTransformDimensionsChange() => Fit(false);
        private void LateUpdate() => Fit(false);

        private void Fit(bool force)
        {
            if (!text) text = GetComponent<Text>();
            if (!text) return;
            float w = text.rectTransform.rect.width;
            if (!force && text.text == lastText && Mathf.Abs(w - lastWidth) < 0.5f) return;
            lastText = text.text;
            lastWidth = w;
            text.resizeTextForBestFit = false;
            int size = maxSize;
            for (; size > minSize; size--)
            {
                text.fontSize = size;
                if (text.preferredWidth <= w - 1f) break;
            }
            text.fontSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}

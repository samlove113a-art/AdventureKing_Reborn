using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Compact opt-in-style objective hint. Presentation only; never invents rewards or timers.</summary>
    public sealed class QuickGoalToast : MonoBehaviour
    {
        private static QuickGoalToast current;
        private CanvasGroup group;
        private Text label;
        private float endAt;
        private float lastAt;
        private string lastText;

        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!current)
            {
                var root = new GameObject("AdventureKing_QuickGoals", typeof(RectTransform));
                Object.DontDestroyOnLoad(root);
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 130;
                root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                current = root.AddComponent<QuickGoalToast>();
                current.Build();
            }
            if (current.lastText == message && Time.unscaledTime - current.lastAt < 9f) return;
            current.lastText = message;
            current.lastAt = Time.unscaledTime;
            current.endAt = Time.unscaledTime + 3.1f;
            current.label.text = message;
            current.group.alpha = 1f;
        }

        private void Build()
        {
            var bg = new GameObject("ObjectiveToast", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            var rt = (RectTransform)bg.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-26f, -110f);
            rt.sizeDelta = new Vector2(460f, 70f);
            var image = bg.GetComponent<Image>();
            image.color = new Color(0.1f, 0.13f, 0.25f, 0.94f);
            image.raycastTarget = false;
            var outline = bg.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.76f, 0.28f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            group = bg.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(bg.transform, false);
            var textRt = (RectTransform)go.transform;
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(18f, 5f); textRt.offsetMax = new Vector2(-18f, -5f);
            label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22; label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.94f, 0.75f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            group.alpha = 0f;
        }

        private void Update()
        {
            if (!group) return;
            float left = endAt - Time.unscaledTime;
            group.alpha = Mathf.Clamp01(left * 2f);
        }
    }
}

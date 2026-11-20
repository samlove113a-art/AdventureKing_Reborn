using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Overlay on the login screen: "Có bản cập nhật" + Cập nhật button + download progress bar. Shown only while a hot update is available / running.</summary>
    public sealed class HotUpdateBanner : MonoBehaviour
    {
        private GameObject box;
        private Text label, buttonText;
        private Image fill;
        private Button button;
        private float shownAt;

        public bool Visible => box && box.activeSelf;
        public string Text => label ? label.text : "";
        public Button UpdateButton => button;

        public static HotUpdateBanner Create(Font font, Sprite panel)
        {
            var host = new GameObject("HotUpdate", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var sc = host.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(host);
            var b = host.AddComponent<HotUpdateBanner>();
            b.Build(host.transform, font, panel);
            if (!HotUpdateManager.Instance) { var m = new GameObject("HotUpdateManager").AddComponent<HotUpdateManager>(); DontDestroyOnLoad(m.gameObject); }
            HotUpdateManager.Instance.Check();
            return b;
        }

        private Text Txt(Transform p, string n, int size, TextAnchor a, Font f)
        {
            var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
            var t = go.AddComponent<Text>(); t.font = f ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        private void Build(Transform root, Font font, Sprite panel)
        {
            box = new GameObject("Box", typeof(RectTransform), typeof(Image)); box.transform.SetParent(root, false);
            var rt = (RectTransform)box.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = new Vector2(0f, -20f); rt.sizeDelta = new Vector2(960f, 150f);
            var img = box.GetComponent<Image>(); img.sprite = panel; img.type = panel ? Image.Type.Sliced : Image.Type.Simple; img.color = panel ? Color.white : new Color(0.06f, 0.08f, 0.16f, 0.96f);
            label = Txt(box.transform, "Label", 24, TextAnchor.UpperLeft, font);
            var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(24f, 60f); lr.offsetMax = new Vector2(-250f, -14f);
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(box.transform, false);
            var br = (RectTransform)bar.transform; br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.offsetMin = new Vector2(24f, 18f); br.offsetMax = new Vector2(-250f, 38f);
            bar.GetComponent<Image>().color = new Color(0.07f, 0.06f, 0.12f, 1f);
            var f = new GameObject("Fill", typeof(RectTransform), typeof(Image)); f.transform.SetParent(bar.transform, false);
            fill = f.GetComponent<Image>(); fill.color = new Color(1f, 0.8f, 0.3f); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
            var fr = (RectTransform)f.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            var bgo = new GameObject("Update", typeof(RectTransform), typeof(Image), typeof(Button)); bgo.transform.SetParent(box.transform, false);
            var brt = (RectTransform)bgo.transform; brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f); brt.anchoredPosition = new Vector2(-20f, 0f); brt.sizeDelta = new Vector2(210f, 80f);
            bgo.GetComponent<Image>().color = new Color(0.82f, 0.6f, 0.18f);
            button = bgo.GetComponent<Button>(); button.onClick.AddListener(() => HotUpdateManager.Instance.StartUpdate());
            buttonText = Txt(bgo.transform, "T", 30, TextAnchor.MiddleCenter, font); buttonText.text = "Cập nhật";
            var tr = (RectTransform)buttonText.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            box.SetActive(false);
        }

        private void Update()
        {
            var m = HotUpdateManager.Instance;
            if (!m || !box) return;
            bool show = m.Status == HotUpdateManager.State.UpdateAvailable || m.Status == HotUpdateManager.State.Downloading || m.Status == HotUpdateManager.State.Failed
                || (m.Status == HotUpdateManager.State.Done && Time.unscaledTime < shownAt + 3f);
            if (m.Status == HotUpdateManager.State.Done && shownAt == 0f) shownAt = Time.unscaledTime;
            if (box.activeSelf != show) box.SetActive(show);
            if (!show) return;
            label.text = m.Message;
            fill.fillAmount = m.Progress;
            button.gameObject.SetActive(m.Status == HotUpdateManager.State.UpdateAvailable || m.Status == HotUpdateManager.State.Failed);
            buttonText.text = m.Status == HotUpdateManager.State.Failed ? "Thử lại" : "Cập nhật";
        }
    }
}

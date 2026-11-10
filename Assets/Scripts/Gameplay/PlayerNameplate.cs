using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Name plate above the hero's head: the character's REAL name from the server snapshot (never the album avatar).
    /// The VIP / Boss Hunter / special frame row sits above the name only when the server reports an owned AND equipped
    /// frame (/titles). Frames are granted only by server code from valid data — today nobody owns one (payments off,
    /// no world boss), so no frame is shown (no fake "VIP 5"). Frames never change Lực Chiến.
    /// </summary>
    public sealed class PlayerNameplate : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Sprite plateSprite;
        [SerializeField] private float aboveHead = 0.62f;
        [SerializeField] private Sprite[] frames = new Sprite[0]; // sprite name = server title id (VIP_01, BossHunter_03, Special_Event …)

        private Text label;
        private Transform head;
        private RectTransform plate;
        private Image frame;

        public string Shown => label ? label.text : "";
        public string EquippedTitle { get; private set; }
        public bool TitlesLoaded { get; private set; }
        public bool FrameShown => frame && frame.gameObject.activeSelf;
        public int FrameCount => frames.Length;

        /// <summary>Show the frame for a server-reported equipped title id (null/empty or unknown sprite = none).</summary>
        public void ShowTitle(string titleId)
        {
            EquippedTitle = string.IsNullOrEmpty(titleId) ? null : titleId;
            Sprite s = null;
            if (EquippedTitle != null) foreach (Sprite f in frames) if (f && f.name == EquippedTitle) { s = f; break; }
            if (!frame) return;
            frame.sprite = s;
            frame.gameObject.SetActive(s);
            if (s)
            {
                float h = 92f;
                frame.rectTransform.sizeDelta = new Vector2(h * s.rect.width / s.rect.height, h);
            }
        }

        private void LoadTitles()
        {
            AdventureWorldSession w = AdventureWorldSession.Current;
            var net = AdventureServerConnection.Instance;
            if (!w || w.Character == null || !net) return;
            net.GetTitles(w.Character.characterId, (ok, t, err) =>
            {
                TitlesLoaded = ok;
                ShowTitle(ok && t != null ? t.equipped : null); // offline / error: no frame
            });
        }

        private void Start()
        {
            var canvasGo = new GameObject("Nameplate", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 42;
            plate = (RectTransform)canvasGo.transform;
            plate.sizeDelta = new Vector2(300f, 54f);
            plate.localScale = Vector3.one * 0.0062f;
            var bg = new GameObject("Plate", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(plate, false);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            if (plateSprite) { bg.sprite = plateSprite; bg.type = Image.Type.Sliced; bg.color = new Color(1f, 1f, 1f, 0.92f); }
            else bg.color = new Color(0.06f, 0.08f, 0.16f, 0.75f);
            bg.raycastTarget = false;
            label = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(plate, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 2f); label.rectTransform.offsetMax = new Vector2(-16f, -2f);
            label.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(1f, 0.97f, 0.88f);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = 28;
            label.raycastTarget = false;
            var o = label.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.75f); o.effectDistance = new Vector2(1.5f, -1.5f);
            label.text = "";
            frame = new GameObject("TitleFrame", typeof(RectTransform)).AddComponent<Image>();
            frame.transform.SetParent(plate, false);
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0f);
            frame.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            frame.preserveAspect = true; frame.raycastTarget = false;
            frame.gameObject.SetActive(false);
            Invoke(nameof(LoadTitles), 1f);
        }

        private void LateUpdate()
        {
            AdventureWorldSession w = AdventureWorldSession.Current;
            if (label && w && w.Character != null && label.text != w.Character.characterName) label.text = w.Character.characterName;
            if (!head)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
                if (!head) return;
            }
            // follow the head (x) at a steady height above it, unaffected by the hero turning
            plate.position = new Vector3(head.position.x, head.position.y + aboveHead, transform.position.z - 0.3f);
            plate.rotation = Quaternion.identity;
        }
    }
}

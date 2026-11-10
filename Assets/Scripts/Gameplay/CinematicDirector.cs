using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Cinematic camera for scripted moments (the wedding ceremony): takes the main camera from CameraFollow2D,
    /// glides through a list of shots (position, look-at point, field of view, hold time) with eased moves, shows
    /// letterbox bars and a caption line, hides the HUD, pauses player input, and hands everything back at the end
    /// (also on Skip). Pure camera work: gameplay state is never changed by a cinematic.
    /// </summary>
    public sealed class CinematicDirector : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            public Vector3 position, lookAt;
            public float fov, move, hold;
            [TextArea] public string caption;
            public Shot(Vector3 position, Vector3 lookAt, float fov, float move, float hold, string caption = "")
            { this.position = position; this.lookAt = lookAt; this.fov = fov; this.move = move; this.hold = hold; this.caption = caption; }
        }

        public static CinematicDirector Current { get; private set; }
        public bool Playing { get; private set; }
        public int ShotIndex { get; private set; } = -1;
        public event Action<int> ShotStarted;

        private RectTransform barTop, barBottom;
        private Text captionText;
        private Button skip;
        private bool skipRequested;
        [SerializeField] private Font font;
        /// <summary>True while other canvases are hidden (tests).</summary>
        public bool HudHidden => Playing;

        private void Awake() => Current = this;
        private void OnDestroy() { if (Current == this) Current = null; }

        public void Configure(Font f) => font = f;

        private void BuildOverlay()
        {
            if (barTop) return;
            var go = new GameObject("CinematicOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            RectTransform Bar(string name, bool top)
            {
                var b = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
                b.transform.SetParent(go.transform, false);
                b.color = Color.black;
                var r = b.rectTransform;
                r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f);
                r.pivot = new Vector2(0.5f, top ? 1f : 0f);
                r.sizeDelta = new Vector2(0f, 0f);
                return r;
            }
            barTop = Bar("BarTop", true);
            barBottom = Bar("BarBottom", false);
            captionText = new GameObject("Caption", typeof(RectTransform)).AddComponent<Text>();
            captionText.transform.SetParent(barBottom, false);
            var cr = captionText.rectTransform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(80f, 10f); cr.offsetMax = new Vector2(-260f, -10f);
            captionText.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            captionText.fontSize = 32; captionText.alignment = TextAnchor.MiddleCenter; captionText.color = new Color(1f, 0.95f, 0.85f);
            captionText.resizeTextForBestFit = true; captionText.resizeTextMinSize = 20; captionText.resizeTextMaxSize = 32;
            var sk = new GameObject("Skip", typeof(RectTransform)).AddComponent<Image>();
            sk.transform.SetParent(barBottom, false);
            sk.color = new Color(1f, 1f, 1f, 0.12f);
            var sr = sk.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1f, 0.5f); sr.pivot = new Vector2(1f, 0.5f);
            sr.anchoredPosition = new Vector2(-40f, 0f); sr.sizeDelta = new Vector2(180f, 64f);
            skip = sk.gameObject.AddComponent<Button>();
            skip.targetGraphic = sk;
            skip.onClick.AddListener(Skip);
            var st = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            st.transform.SetParent(sk.transform, false);
            st.rectTransform.anchorMin = Vector2.zero; st.rectTransform.anchorMax = Vector2.one; st.rectTransform.offsetMin = st.rectTransform.offsetMax = Vector2.zero;
            st.font = captionText.font; st.fontSize = 26; st.alignment = TextAnchor.MiddleCenter; st.color = Color.white; st.text = "Bỏ qua ▸▸";
            go.SetActive(false);
        }

        public void Skip() => skipRequested = true;

        private int cue = -1;
        /// <summary>Jumps to this shot now (an outside clock, e.g. the server's ceremony steps, keeps the camera in step).</summary>
        public void Cue(int shotIndex) => cue = shotIndex;

        /// <summary>Plays the shots; <paramref name="done"/> runs after the camera is handed back.</summary>
        public void Play(IList<Shot> shots, Action done = null)
        {
            if (Playing || shots == null || shots.Count == 0) { done?.Invoke(); return; }
            StartCoroutine(Run(shots, done));
        }

        private static float Ease(float k) => k * k * (3f - 2f * k);

        private IEnumerator Run(IList<Shot> shots, Action done)
        {
            Playing = true;
            skipRequested = false;
            cue = -1;
            BuildOverlay();
            Camera cam = Camera.main;
            var follow = cam ? cam.GetComponent<CameraFollow2D>() : null;
            if (follow) follow.enabled = false;
            var world = AdventureWorldSession.Current;
            var input = world && world.Player ? world.Player.GetComponent<PlayerInput2D>() : null;
            if (input) input.enabled = false;
            // Hide every other screen overlay (HUD, joystick, popups) and every world-space label (name plates,
            // "E · Nói chuyện" prompts, speech bubbles): they would float huge in the close-ups.
            var overlays = new List<CanvasGroup>();
            var worldLabels = new List<Canvas>();
            Canvas mine = barTop.GetComponentInParent<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c == mine || !c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) { if (c.enabled) { c.enabled = false; worldLabels.Add(c); } continue; }
                var g = c.GetComponent<CanvasGroup>();
                if (!g) g = c.gameObject.AddComponent<CanvasGroup>();
                overlays.Add(g);
            }
            float[] alpha0 = overlays.ConvertAll(g => g.alpha).ToArray();
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            barTop.parent.gameObject.SetActive(true);
            // bars + HUD fade in
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.45f)
            {
                float e = Ease(k);
                barTop.sizeDelta = barBottom.sizeDelta = new Vector2(0f, 120f * e);
                for (int i = 0; i < overlays.Count; i++) if (overlays[i]) overlays[i].alpha = alpha0[i] * (1f - e);
                yield return null;
            }
            for (int i = 0; i < overlays.Count; i++) if (overlays[i]) { overlays[i].alpha = 0f; overlays[i].blocksRaycasts = false; }
            for (int i = 0; i < shots.Count && !skipRequested; i++)
            {
                if (cue > i) i = Mathf.Min(cue, shots.Count - 1);
                ShotIndex = i;
                ShotStarted?.Invoke(i);
                Shot s = shots[i];
                captionText.text = s.caption ?? "";
                Vector3 fromP = cam.transform.position; Quaternion fromR = cam.transform.rotation; float fromF = cam.fieldOfView;
                Quaternion toR = Quaternion.LookRotation(s.lookAt - s.position, Vector3.up);
                for (float k = 0f; k < 1f && !skipRequested && cue <= i; k += Time.deltaTime / Mathf.Max(0.01f, s.move))
                {
                    float e = Ease(Mathf.Clamp01(k));
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(fromP, s.position, e), Quaternion.Slerp(fromR, toR, e));
                    cam.fieldOfView = Mathf.Lerp(fromF, s.fov, e);
                    yield return null;
                }
                if (cue > i) { i--; continue; } // cued ahead mid-move: glide on to the cued shot from here, never snap
                cam.transform.SetPositionAndRotation(s.position, toR);
                cam.fieldOfView = s.fov;
                for (float t = 0f; t < s.hold && !skipRequested && cue <= i; t += Time.deltaTime) yield return null;
            }
            ShotIndex = -1;
            captionText.text = "";
            // hand back: glide to where the follow camera was, bars out, HUD back
            Vector3 endP = cam.transform.position; Quaternion endR = cam.transform.rotation; float endF = cam.fieldOfView;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.6f)
            {
                float e = Ease(k);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(endP, p0, e), Quaternion.Slerp(endR, r0, e));
                cam.fieldOfView = Mathf.Lerp(endF, f0, e);
                barTop.sizeDelta = barBottom.sizeDelta = new Vector2(0f, 120f * (1f - e));
                for (int i = 0; i < overlays.Count; i++) if (overlays[i]) overlays[i].alpha = alpha0[i] * e;
                yield return null;
            }
            cam.transform.SetPositionAndRotation(p0, r0);
            cam.fieldOfView = f0;
            for (int i = 0; i < overlays.Count; i++) if (overlays[i]) { overlays[i].alpha = alpha0[i]; overlays[i].blocksRaycasts = true; }
            foreach (Canvas c in worldLabels) if (c) c.enabled = true;
            barTop.parent.gameObject.SetActive(false);
            if (follow) follow.enabled = true;
            if (input) input.enabled = true;
            Playing = false;
            done?.Invoke();
        }
    }
}

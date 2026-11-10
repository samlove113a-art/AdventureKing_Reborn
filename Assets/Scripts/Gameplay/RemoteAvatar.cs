using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Another player on this map, drawn from the server heartbeat: the same hero model and outfit as on their own
    /// screen, a name plate (name · [guild] · ♥ partner), position interpolated ~150 ms behind the newest server
    /// sample (smooth at 6 Hz, no jitter), animation from the synced state (run / jump / climb / attack / skill / emotes).
    /// A cutscene can take over its placement (<see cref="Override"/>) so every client stages a ceremony identically.
    /// </summary>
    public sealed class RemoteAvatar : MonoBehaviour
    {
        public const float InterpDelay = 0.15f;
        public PeerState State { get; private set; }
        public CharacterVisual3D Visual { get; private set; }
        public PoseOverlay Pose { get; private set; }
        public GameObject Model { get; private set; }
        public float LastSeen { get; private set; }
        public string NameShown => nameText ? nameText.text : "";

        private readonly List<(float t, Vector2 p)> samples = new List<(float, Vector2)>();
        private Vector2 shown, lastShown;
        private string lastAnim = "";
        private int lastEmoteSeq;
        private Text nameText;
        private Vector3? overridePos;
        private int overrideFacing;

        public static RemoteAvatar Create(PeerState s, GameObject prefab, float heroScale, Font font, Transform parent)
        {
            var root = new GameObject("Remote_" + s.name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(s.x, s.y, 0f);
            var a = root.AddComponent<RemoteAvatar>();
            var mount = new GameObject("VisualMount").transform;
            mount.SetParent(root.transform, false);
            mount.localPosition = new Vector3(0f, -0.8f, 0f); // body centre -> feet (same as the local hero)
            a.Model = Instantiate(prefab, mount);
            a.Model.transform.localPosition = Vector3.zero;
            a.Model.transform.localScale = Vector3.one * heroScale;
            foreach (var fx in a.Model.GetComponentsInChildren<HeroCombatPresentation>(true)) Destroy(fx); // no skill VFX/sounds for others
            a.Visual = root.AddComponent<CharacterVisual3D>();
            a.Visual.Bind(a.Model.transform, a.Model.GetComponentInChildren<Animator>(true));
            HeroOutfit outfit = AdventureWorldSession.DressHero(a.Model, (HeroClass)s.heroClass);
            outfit.Apply(s.outfit ?? new OutfitLoadout());
            a.Pose = a.Model.AddComponent<PoseOverlay>();
            a.Pose.Bind(a.Model.transform);
            a.BuildPlate(font);
            a.shown = a.lastShown = new Vector2(s.x, s.y);
            a.Apply(s);
            return a;
        }

        private void BuildPlate(Font font)
        {
            var cg = new GameObject("Nameplate", typeof(RectTransform));
            cg.transform.SetParent(transform, false);
            cg.transform.localPosition = new Vector3(0f, 1.55f, -0.3f);
            var canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 41;
            var rt = (RectTransform)cg.transform;
            rt.sizeDelta = new Vector2(420f, 60f);
            rt.localScale = Vector3.one * 0.0062f;
            var bg = new GameObject("Plate", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            bg.rectTransform.anchorMin = new Vector2(0.1f, 0f); bg.rectTransform.anchorMax = new Vector2(0.9f, 1f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            bg.color = new Color(0.06f, 0.08f, 0.16f, 0.6f);
            bg.raycastTarget = false;
            nameText = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
            nameText.transform.SetParent(rt, false);
            nameText.rectTransform.anchorMin = Vector2.zero; nameText.rectTransform.anchorMax = Vector2.one;
            nameText.rectTransform.offsetMin = new Vector2(8f, 2f); nameText.rectTransform.offsetMax = new Vector2(-8f, -2f);
            nameText.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 26; nameText.alignment = TextAnchor.MiddleCenter; nameText.supportRichText = true;
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = 14; nameText.resizeTextMaxSize = 26;
            nameText.raycastTarget = false;
            nameText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
            var pr = new GameObject("Prompt", typeof(RectTransform)).AddComponent<Text>();
            pr.transform.SetParent(rt, false);
            pr.rectTransform.anchorMin = new Vector2(0f, 1f); pr.rectTransform.anchorMax = new Vector2(1f, 1f);
            pr.rectTransform.pivot = new Vector2(0.5f, 0f);
            pr.rectTransform.anchoredPosition = new Vector2(0f, 6f); pr.rectTransform.sizeDelta = new Vector2(0f, 44f);
            pr.font = nameText.font; pr.fontSize = 26; pr.alignment = TextAnchor.MiddleCenter; pr.color = new Color(0.75f, 0.95f, 1f);
            pr.text = "E · Tương tác"; pr.raycastTarget = false;
            pr.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            prompt = pr.gameObject;
            prompt.SetActive(false);
        }

        private GameObject prompt;
        public bool PromptShown => prompt && prompt.activeSelf;
        public void SetPrompt(bool on) { if (prompt) prompt.SetActive(on); }

        /// <summary>New server sample.</summary>
        public void Apply(PeerState s)
        {
            State = s;
            LastSeen = Time.time;
            samples.Add((Time.time, new Vector2(s.x, s.y)));
            if (samples.Count > 20) samples.RemoveAt(0);
            string line = (s.friend ? "<color=#9ff0a8>" : "<color=#ffffff>") + s.name + "</color>";
            if (!string.IsNullOrEmpty(s.guild)) line += " <color=#ffd27a>[" + s.guild + "]</color>";
            if (!string.IsNullOrEmpty(s.partner)) line += " <color=#ff9fb8>♥ " + s.partner + "</color>";
            if (nameText) nameText.text = line;
            if (s.anim != lastAnim)
            {
                if (s.anim is "attack" or "skill" or "hit") Visual.RemoteAction(s.anim);
                lastAnim = s.anim;
            }
            if (s.emoteSeq != lastEmoteSeq && !string.IsNullOrEmpty(s.emote) && overridePos == null) Visual.PlayEmote(s.emote);
            lastEmoteSeq = s.emoteSeq;
        }

        /// <summary>A cutscene places this avatar (null: back to the server samples).</summary>
        public void Override(Vector3? worldFeetPos, int facing = 1) { overridePos = worldFeetPos; overrideFacing = facing; }
        public bool Overridden => overridePos != null;

        /// <summary>A cinematic places the feet exactly this frame (no easing: hands meeting a partner's hands must
        /// line up to the centimetre). Null hands the avatar back to the server samples.</summary>
        public void Pin(Vector3? worldFeetPos)
        {
            overridePos = worldFeetPos;
            if (worldFeetPos == null) return;
            shown = lastShown = new Vector2(worldFeetPos.Value.x, worldFeetPos.Value.y + 0.8f);
            transform.position = new Vector3(shown.x, shown.y, worldFeetPos.Value.z);
        }

        /// <summary>Hides this player while someone else's ceremony plays (viewers never get in the way).</summary>
        public void SetHidden(bool hidden)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = !hidden;
            foreach (Canvas c in GetComponentsInChildren<Canvas>(true)) c.enabled = !hidden;
        }

        private void Update()
        {
            if (State == null) return;
            Vector2 target;
            if (overridePos != null) target = new Vector2(overridePos.Value.x, overridePos.Value.y + 0.8f);
            else
            {
                float t = Time.time - InterpDelay;
                target = samples[samples.Count - 1].p;
                for (int i = samples.Count - 1; i > 0; i--)
                    if (samples[i - 1].t <= t)
                    {
                        var (t0, p0) = samples[i - 1]; var (t1, p1) = samples[i];
                        target = Vector2.Lerp(p0, p1, t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 1f);
                        break;
                    }
            }
            lastShown = shown;
            // ease toward the target: never pops, even when a sample comes late (a cinematic pins it exactly)
            shown = Visual && Visual.Cinematic ? target : Vector2.Lerp(shown, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            transform.position = new Vector3(shown.x, shown.y, overridePos != null ? overridePos.Value.z : 0f);
            Vector2 vel = (shown - lastShown) / Mathf.Max(Time.deltaTime, 1e-4f);
            string anim = overridePos != null ? "idle" : State.anim;
            int facing = overridePos != null ? overrideFacing : (Mathf.Abs(vel.x) > 0.3f ? (vel.x > 0 ? 1 : -1) : State.facing);
            Visual.DriveRemote(vel, anim, facing);
        }
    }
}

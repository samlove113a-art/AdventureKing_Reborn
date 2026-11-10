using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Client view of one server monster (a slime): idle bounce + short hops around its spawn point, a small HP bar,
    /// a white hit flash, a squash "poof" on death and a pop-in when the server says it has respawned.
    /// It never decides HP or death itself — it only shows what the server answered.
    /// </summary>
    public sealed class FarmMonsterView : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        /// <summary>Rigged monster (Meshy): states Idle / Walk / Attack / Hit / Death. Null = procedural slime.</summary>
        [SerializeField] private Animator animator;
        [SerializeField] private float hpBarHeight = 1.25f;
        /// <summary>Animator state currently shown (tests / QA).</summary>
        public string AnimState { get; private set; } = "";
        private float oneShotUntil;

        public string SpawnId { get; private set; }
        public bool Alive { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public Vector2 Home { get; private set; }
        public Vector2 Position => transform.position;

        private Image hpFill;
        private GameObject hpRoot;
        private float flash, seed, hopT, hopFrom, hopTo, nextHop, deathT = -1f, popT = -1f;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_EmissionColor");

        public void Bind(FarmMonsterState s, Font font)
        {
            SpawnId = s.id;
            Home = new Vector2(s.x, s.y);
            transform.position = new Vector3(s.x, s.y, 0.35f);
            seed = Random.value * 10f;
            block = new MaterialPropertyBlock();
            BuildHpBar(s.name, font);
            Apply(s, true);
        }

        private void BuildHpBar(string label, Font font)
        {
            var canvasGo = new GameObject("HpBar", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, hpBarHeight, -0.2f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 35;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(160f, 46f);
            rt.localScale = Vector3.one * 0.0075f;
            hpRoot = canvasGo;
            Text name = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
            name.transform.SetParent(canvasGo.transform, false);
            var nr = name.rectTransform; nr.anchorMin = new Vector2(0f, 0.45f); nr.anchorMax = Vector2.one; nr.offsetMin = nr.offsetMax = Vector2.zero;
            name.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            name.text = label; name.fontSize = 22; name.alignment = TextAnchor.MiddleCenter; name.color = new Color(1f, 0.97f, 0.9f);
            // (the 25-unit box was shorter than the font's line height: Truncate drew nothing)
            name.verticalOverflow = VerticalWrapMode.Overflow; name.horizontalOverflow = HorizontalWrapMode.Overflow;
            NameLabel = name;
            name.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            Image track = new GameObject("Track", typeof(RectTransform)).AddComponent<Image>();
            track.transform.SetParent(canvasGo.transform, false);
            var tr = track.rectTransform; tr.anchorMin = new Vector2(0.1f, 0.08f); tr.anchorMax = new Vector2(0.9f, 0.38f); tr.offsetMin = tr.offsetMax = Vector2.zero;
            track.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            hpFill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
            hpFill.transform.SetParent(track.transform, false);
            var fr = hpFill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
            hpFill.color = new Color(0.9f, 0.25f, 0.25f);
            hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        /// <summary>Server state -> view. <paramref name="instant"/> skips the death / respawn animations.</summary>
        public void Apply(FarmMonsterState s, bool instant = false)
        {
            MaxHp = Mathf.Max(1, s.maxHp);
            Hp = s.hp;
            if (hpFill) hpFill.fillAmount = Hp / (float)MaxHp;
            if (hpRoot) hpRoot.SetActive(s.alive); // name + HP bar whenever it is alive
            if (!s.alive) Chase = null;
            if (s.alive && !Alive)
            {
                body.gameObject.SetActive(true);
                popT = instant ? -1f : 0f;
                body.localScale = Vector3.one;
                deathT = -1f;
                transform.position = new Vector3(Home.x, Home.y, transform.position.z);
            }
            else if (!s.alive && Alive) { deathT = instant ? 1f : 0f; if (instant) body.gameObject.SetActive(false); }
            else if (!s.alive && instant) body.gameObject.SetActive(false);
            Alive = s.alive;
        }

        public Text NameLabel { get; private set; }

        /// <summary>Monster name opacity (fades while the hero stands on top of it, so the hero's own nameplate
        /// stays readable; the HP bar always stays).</summary>
        public float NameAlpha => NameLabel ? NameLabel.color.a : 0f;

        private void LateUpdate()
        {
            if (!NameLabel) return;
            var w = AdventureWorldSession.Current;
            float want = 1f;
            if (w && w.Player)
            {
                Vector3 h = w.Player.transform.position, m = transform.position;
                if (Mathf.Abs(h.x - m.x) < 1.8f && Mathf.Abs(h.y - m.y) < 1.5f) want = 0f;
            }
            Color c = NameLabel.color;
            c.a = Mathf.MoveTowards(c.a, want, Time.deltaTime * 6f);
            NameLabel.color = c;
        }
        /// <summary>Hero this monster is fighting (set when the hero hits it); it chases within its leash.</summary>
        public Transform Chase { get; set; }
        public float Leash { get; set; } = 3.5f;
        public int Strikes { get; private set; }
        private float lungeT = -1f;

        /// <summary>Server confirmed a hit: flash + knock squash.</summary>
        public void ShowHit() { flash = 1f; if (animator && Alive) PlayOnce("Hit", 0.45f); }

        /// <summary>The server says this monster struck the hero: lunge at it.</summary>
        public void Strike() { lungeT = 0f; Strikes++; if (animator && Alive) PlayOnce("Attack", 0.7f); }

        private void Play(string state)
        {
            if (AnimState == state || !animator) return;
            AnimState = state;
            animator.CrossFadeInFixedTime(state, 0.12f);
        }

        private void PlayOnce(string state, float seconds)
        {
            AnimState = "";
            Play(state);
            oneShotUntil = Time.time + seconds;
        }

        /// <summary>Rigged monster: walks (no hop) around home or after the hero, faces its way in 3/4 view,
        /// Attack / Hit one-shots from the server's answers, Death clip then gone. Same server rules as the slimes.</summary>
        private void AnimatedUpdate(float dt)
        {
            if (deathT >= 0f && deathT < 1f)
            {
                if (deathT == 0f) PlayOnce("Death", 1.4f);
                deathT += dt / 1.4f;
                if (deathT >= 1f) body.gameObject.SetActive(false);
                UpdateFlash(dt);
                return;
            }
            if (!Alive) return;
            if (popT >= 0f)
            {
                popT += dt * 2.5f;
                body.localScale = Vector3.one * Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(popT));
                if (popT >= 1f) { popT = -1f; body.localScale = Vector3.one; }
            }
            float t = Time.time, x = transform.position.x, goal;
            if (Chase) goal = Mathf.Clamp(Chase.position.x + (Chase.position.x > x ? -0.9f : 0.9f), Home.x - Leash, Home.x + Leash);
            else
            {
                if (t >= nextHop) { hopTo = Home.x + Random.Range(-1.6f, 1.6f); nextHop = t + Random.Range(2.5f, 5f); }
                goal = hopTo == 0f ? Home.x : hopTo;
            }
            float speed = Chase ? 2.2f : 1.1f;
            float nx = Mathf.MoveTowards(x, goal, dt * speed);
            bool moving = Mathf.Abs(nx - x) > 0.0005f;
            transform.position = new Vector3(nx, Home.y, transform.position.z);
            float faceDir = Chase ? Mathf.Sign(Chase.position.x - nx) : moving ? Mathf.Sign(goal - x) : 0f;
            float yaw = faceDir > 0f ? 125f : faceDir < 0f ? 235f : 180f; // 3/4 view, never the back
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(0f, yaw, 0f), dt * 8f);
            if (t >= oneShotUntil) Play(moving ? "Walk" : "Idle");
            UpdateFlash(dt);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (animator) { AnimatedUpdate(dt); return; }
            if (deathT >= 0f && deathT < 1f)
            {
                deathT += dt * 3f;
                float k = Mathf.Clamp01(deathT);
                body.localScale = new Vector3(1f + k * 0.6f, Mathf.Max(0.05f, 1f - k), 1f + k * 0.6f);
                if (deathT >= 1f) body.gameObject.SetActive(false);
                return;
            }
            if (!Alive) return;
            if (popT >= 0f)
            {
                popT += dt * 2.5f;
                float k = Mathf.Clamp01(popT);
                body.localScale = Vector3.one * (k < 0.7f ? k / 0.7f * 1.15f : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f));
                if (popT >= 1f) popT = -1f;
                return;
            }
            float t = Time.time;
            if (Chase)
            {
                // fighting: close in on the hero, never past the leash the server uses
                float goal = Mathf.Clamp(Chase.position.x + (Chase.position.x > transform.position.x ? -0.9f : 0.9f), Home.x - Leash, Home.x + Leash);
                float x = Mathf.MoveTowards(transform.position.x, goal, dt * 2.4f);
                float lunge = 0f;
                if (lungeT >= 0f)
                {
                    lungeT += dt * 4f;
                    lunge = Mathf.Sin(Mathf.Clamp01(lungeT) * Mathf.PI) * 0.55f * Mathf.Sign(Chase.position.x - x);
                    if (lungeT >= 1f) lungeT = -1f;
                }
                transform.position = new Vector3(x + lunge, Home.y, transform.position.z);
                float bob = Mathf.Abs(Mathf.Sin(t * 7f)) * 0.22f;
                body.localPosition = new Vector3(0f, bob + Mathf.Abs(lunge) * 0.4f, 0f);
                body.localRotation = Quaternion.Euler(0f, Chase.position.x > x ? 25f : -25f, 0f);
                float sq = Mathf.Abs(Mathf.Sin(t * 7f)) * 0.1f + flash * 0.25f;
                body.localScale = new Vector3(1f + sq, 1f - sq, 1f + sq);
                UpdateFlash(dt);
                return;
            }
            // hop around home (visual only; the server keeps the spawn point)
            if (t >= nextHop && hopT >= 1f || hopT == 0f && nextHop == 0f)
            {
                hopFrom = transform.position.x;
                hopTo = Home.x + Random.Range(-1.2f, 1.2f);
                hopT = 0f;
                nextHop = t + Random.Range(1.5f, 3.5f);
            }
            float y = 0f;
            if (hopT < 1f)
            {
                hopT = Mathf.Min(1f, hopT + dt * 1.6f);
                float x = Mathf.Lerp(hopFrom, hopTo, Mathf.SmoothStep(0f, 1f, hopT));
                y = Mathf.Sin(hopT * Mathf.PI) * 0.45f;
                transform.position = new Vector3(x, Home.y, transform.position.z);
                body.localRotation = Quaternion.Euler(0f, hopTo > hopFrom ? 25f : -25f, 0f); // face the hop a little
            }
            float breathe = Mathf.Sin(t * 3.1f + seed) * 0.05f;
            float squash = flash * 0.25f;
            body.localPosition = new Vector3(0f, y, 0f);
            body.localScale = new Vector3(1f + breathe + squash, 1f - breathe - squash, 1f + breathe + squash);
            UpdateFlash(dt);
        }

        private void UpdateFlash(float dt)
        {
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 5f);
                foreach (Renderer r in renderers)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, Color.white * flash * 0.8f);
                    r.SetPropertyBlock(block);
                }
            }
        }

        public void Bind(Transform bodyRoot, Renderer[] rs) { body = bodyRoot; renderers = rs; }
        public void BindAnimated(Transform bodyRoot, Renderer[] rs, Animator a, float barHeight) { Bind(bodyRoot, rs); animator = a; hpBarHeight = barHeight; }
    }
}

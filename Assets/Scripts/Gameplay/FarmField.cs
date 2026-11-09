using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Farm map client: shows the server's monsters and the hero's Gold drops, turns the hero's swings into
    /// "attack spawn X from here" requests (no damage or Gold ever comes from the client), auto-picks up the hero's
    /// own drops when standing on them, and updates the HUD Gold from the server's confirmed balance.
    /// Polls the server every 1.5 s so respawns (server timer, 60 s) and expiries appear on their own.
    /// </summary>
    public sealed class FarmField : MonoBehaviour
    {
        [SerializeField] private FarmMonsterView puffTemplate, mossTemplate;
        [SerializeField] private GoldDropView coinTemplate;
        [SerializeField] private Font font;
        [SerializeField] private float pollSeconds = 1.5f;
        [SerializeField] private float pickupRadius = 1.0f;

        private readonly Dictionary<string, FarmMonsterView> monsters = new Dictionary<string, FarmMonsterView>();
        private readonly Dictionary<string, GoldDropView> drops = new Dictionary<string, GoldDropView>();
        private AdventureWorldSession world;
        private float nextSwing, nextPoll;
        private bool loaded, pollBusy;

        public static FarmField Current { get; private set; }
        public IReadOnlyDictionary<string, FarmMonsterView> Monsters => monsters;
        public IReadOnlyDictionary<string, GoldDropView> Drops => drops;
        public int Kills { get; private set; }
        public long GoldPickedUp { get; private set; }
        public string LastError { get; private set; }

        private void Awake() => Current = this;
        private void OnDestroy() { if (Current == this) Current = null; }

        private IEnumerator Start()
        {
            foreach (var t in new Component[] { puffTemplate, mossTemplate, coinTemplate }) if (t) t.gameObject.SetActive(false);
            float until = Time.time + 10f;
            while ((!(world = AdventureWorldSession.Current) || world.Character == null || !world.Player) && Time.time < until) yield return null;
            if (!world || world.Character == null) yield break;
            var melee = world.Player.GetComponent<PlayerMelee2D>();
            if (melee) melee.Attacked += OnSwing;
            Poll();
        }

        private AdventureServerConnection Net => AdventureServerConnection.Instance;
        private string CharId => world.Character.characterId;
        private Vector2 HeroPos => world.Player.transform.position;

        private void Poll()
        {
            if (pollBusy || !Net || world == null || world.Character == null) return;
            pollBusy = true;
            Net.GetFarm(CharId, (ok, state, err) =>
            {
                pollBusy = false;
                nextPoll = Time.time + pollSeconds;
                if (!ok || state == null) { LastError = err; return; }
                foreach (FarmMonsterState s in state.monsters)
                {
                    if (!monsters.TryGetValue(s.id, out FarmMonsterView v))
                    {
                        FarmMonsterView tpl = s.type == "moss_slime" && mossTemplate ? mossTemplate : puffTemplate;
                        v = Instantiate(tpl, transform);
                        v.name = "Monster_" + s.id;
                        v.gameObject.SetActive(true);
                        v.Bind(s, font);
                        monsters[s.id] = v;
                    }
                    else v.Apply(s);
                }
                var listed = new HashSet<string>(state.drops.Select(d => d.id));
                foreach (FarmDropState d in state.drops) if (!drops.ContainsKey(d.id)) SpawnDrop(d, new Vector2(d.x, d.y));
                foreach (var gone in drops.Where(kv => !listed.Contains(kv.Key) && !kv.Value.Pending).Select(kv => kv.Key).ToList())
                {
                    if (drops[gone]) Destroy(drops[gone].gameObject);
                    drops.Remove(gone);
                }
                loaded = true;
            });
        }

        private void SpawnDrop(FarmDropState d, Vector2 from)
        {
            if (!coinTemplate) return;
            GoldDropView g = Instantiate(coinTemplate, transform);
            g.name = "Gold_" + d.id;
            g.gameObject.SetActive(true);
            g.Bind(d, from);
            drops[d.id] = g;
        }

        /// <summary>Nearest living monster in front of the hero within the class reach (the server re-checks).</summary>
        public FarmMonsterView TargetFor(Vector2 hero, int facing, float reach) =>
            monsters.Values.Where(m => m && m.Alive && Mathf.Abs(m.Home.y - hero.y) < 2.2f)
                .Where(m => { float dx = m.Position.x - hero.x; return Mathf.Abs(dx) <= reach + 0.6f && (dx * facing >= -0.6f); })
                .OrderBy(m => Mathf.Abs(m.Position.x - hero.x)).FirstOrDefault();

        private void OnSwing()
        {
            if (!loaded || Time.time < nextSwing) return;
            HeroClassStats stats = HeroClassCatalog.Get(world.Character.heroClass);
            var motor = world.Player.GetComponent<PlayerMotor2D>();
            FarmMonsterView target = TargetFor(HeroPos, motor ? motor.Facing : 1, stats.attackRange);
            if (!target) return;
            nextSwing = Time.time + stats.attackIntervalSeconds; // honest pace: never trips the server's anti-spam check
            Net.FarmAttack(CharId, target.SpawnId, HeroPos, (ok, r, err) =>
            {
                if (!ok || r == null) { LastError = err; return; }
                target.ShowHit();
                target.Apply(r.monster);
                Popup(target.Position + Vector2.up * 1.4f, "-" + r.damage, new Color(1f, 0.92f, 0.6f), 30);
                if (r.HasDrop)
                {
                    Kills++;
                    SpawnDrop(r.drop, target.Position);
                }
            });
        }

        private void Update()
        {
            if (world == null || world.Character == null || !world.Player) return;
            if (Time.time >= nextPoll) Poll();
            Vector2 hero = HeroPos;
            foreach (GoldDropView g in drops.Values.ToList())
            {
                if (!g || g.Pending) continue;
                if (g.Expired) continue;
                Vector2 rest = g.Ground + new Vector2(0.6f, 0f);
                if (Mathf.Abs(hero.x - rest.x) > pickupRadius || Mathf.Abs(hero.y - rest.y) > 2f) continue;
                g.Pending = true;
                GoldDropView drop = g;
                Net.FarmPickup(CharId, drop.DropId, hero, (ok, r, err) =>
                {
                    if (!ok || r == null)
                    {
                        LastError = err;
                        drop.Pending = false;
                        return;
                    }
                    drops.Remove(drop.DropId);
                    world.Character.gold = r.gold; // HUD shows the server-confirmed balance
                    GoldPickedUp += r.amount;
                    if (drop) drop.Collect(world.Player.transform);
                    Popup(Vector2.zero, "+" + r.amount + " Vàng", new Color(1f, 0.84f, 0.3f), 32, world.Player.transform); // rides above the hero
                });
            }
        }

        /// <summary>Floating text (damage / Gold) that rises and fades.</summary>
        private void Popup(Vector2 at, string text, Color color, int size, Transform follow = null)
        {
            var go = new GameObject("Popup", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, -0.5f);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 45;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(320f, 60f);
            rt.localScale = Vector3.one * 0.01f;
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(go.transform, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.15f, 0.08f, 0.02f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            StartCoroutine(Rise(go, t, follow));
        }

        private static IEnumerator Rise(GameObject go, Text t, Transform follow)
        {
            Vector3 start = go.transform.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.9f)
            {
                if (!go) yield break;
                Vector3 basePos = follow ? follow.position + new Vector3(0f, 2.1f, -0.5f) : start; // Gold text rides above the hero
                go.transform.position = basePos + Vector3.up * k * 0.9f;
                Color c = t.color; c.a = 1f - Mathf.Max(0f, k - 0.5f) * 2f; t.color = c;
                yield return null;
            }
            if (go) Destroy(go);
        }
    }
}

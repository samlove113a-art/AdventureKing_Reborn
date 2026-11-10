using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Farm map client: shows the server's monsters and the hero's Gold drops, turns the hero's swings into
    /// "attack spawn X from here" and "cast skill N" requests (no damage, MP or Gold ever comes from the client),
    /// picks up the hero's own drops only when the player presses ↓/S or "Nhặt" (no automatic pickup by the hero;
    /// automatic collection is the Pet's job later), and updates the HUD from the server's confirmed values.
    /// Polls the server every 1.5 s so respawns (server timer, 60 s) and expiries appear on their own.
    /// </summary>
    public sealed class FarmField : MonoBehaviour
    {
        [SerializeField] private FarmMonsterView puffTemplate, mossTemplate;
        /// <summary>One view template per server monster type (Chương 1+). Unknown types fall back to the puff slime.</summary>
        [System.Serializable] public struct TypedTemplate { public string type; public FarmMonsterView view; }
        [SerializeField] private TypedTemplate[] typedTemplates = new TypedTemplate[0];
        [SerializeField] private GoldDropView coinTemplate;
        [SerializeField] private Font font;
        [SerializeField] private float pollSeconds = 1.5f;
        [SerializeField] private float pickupRadius = 1.0f;
        [SerializeField] private float heartbeatSeconds = 0.5f;
        [SerializeField] private Sprite panelSprite, buttonSprite;
        private float nextBeat;
        private bool beatBusy, reviving;
        private Health2D heroHealth;
        private GameObject deathPanel;
        private AudioSource sfx;
        private AudioClip chime;

        public bool HeroDead { get; private set; }
        public int HeroHits { get; private set; }
        public int CoinChimes { get; private set; }
        public GameObject DeathPanel => deathPanel;

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
            foreach (var t in typedTemplates) if (t.view) t.view.gameObject.SetActive(false);
            float until = Time.time + 10f;
            while ((!(world = AdventureWorldSession.Current) || world.Character == null || !world.Player) && Time.time < until) yield return null;
            if (!world || world.Character == null) yield break;
            var melee = world.Player.GetComponent<PlayerMelee2D>();
            if (melee) melee.Attacked += OnSwing;
            heroHealth = world.Player.GetComponent<Health2D>();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.volume = 0.8f;
            chime = CoinChime();
            Poll();
        }

        /// <summary>Original bright two-note coin chime, synthesised in code (no external sound file).</summary>
        public static AudioClip CoinChime()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.42f);
            var data = new float[n];
            (float start, float freq)[] notes = { (0f, 1318.5f), (0.07f, 1975.5f) }; // E6 then B6
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, v = 0f;
                foreach (var (start, freq) in notes)
                {
                    float u = t - start;
                    if (u < 0f) continue;
                    float env = Mathf.Exp(-u * 9f) * Mathf.Clamp01(u * 400f);
                    v += env * (Mathf.Sin(2f * Mathf.PI * freq * u) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq * 2.76f * u) * 0.18f + Mathf.Sin(2f * Mathf.PI * freq * 5.4f * u) * 0.06f);
                }
                data[i] = v * 0.45f;
            }
            var clip = AudioClip.Create("coin_chime", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void PlayChime()
        {
            CoinChimes++;
            var a = AdventureAudioDirector.Instance;
            if (sfx && chime && !(a && a.Muted)) sfx.PlayOneShot(chime);
        }

        /// <summary>Server HP -> hero (HUD bar, hurt face/animation follow Health2D), monster lunges, death screen.</summary>
        /// <summary>Last server answer about the hero (HP, MP, skill cooldowns).</summary>
        public HeroCombatState LastHero { get; private set; }
        /// <summary>Time.time at which skills 1..3 are ready again, mirrored from the server's remaining cooldowns.</summary>
        public readonly float[] CooldownEnds = new float[3];

        internal void ApplyHero(HeroCombatState h)
        {
            if (h == null || h.maxHp <= 0) return;
            LastHero = h;
            if (heroHealth) heroHealth.SetFromServer(h.hp, h.maxHp);
            var mana = world.Player.GetComponent<PlayerMana>();
            if (mana && h.maxMp > 0) mana.SetFromServer(h.mp, h.maxMp);
            if (h.cooldownMs != null)
                for (int i = 0; i < 3 && i < h.cooldownMs.Length; i++) CooldownEnds[i] = Time.time + h.cooldownMs[i] / 1000f;
            if (h.hitBy != null)
                foreach (string id in h.hitBy)
                    if (monsters.TryGetValue(id, out FarmMonsterView m) && m) { m.Chase = world.Player.transform; m.Strike(); HeroHits++; }
            if (h.damageTaken > 0) Popup(Vector2.zero, "-" + h.damageTaken, new Color(1f, 0.38f, 0.35f), 34, world.Player.transform);
            if (h.PetDrank) PetGavePotion(h.petDrink);
            if (h.dead && !HeroDead) ShowDeath();
            HeroDead = h.dead;
        }

        private void Heartbeat()
        {
            if (beatBusy || HeroDead || !Net) return;
            beatBusy = true;
            Net.FarmPresence(CharId, HeroPos, (ok, h, err) =>
            {
                beatBusy = false;
                nextBeat = Time.time + heartbeatSeconds;
                if (ok) ApplyHero(h); else LastError = err;
            });
        }

        private void ShowDeath()
        {
            var input = world.Player.GetComponent<PlayerInput2D>();
            if (input) input.enabled = false;
            foreach (var m in monsters.Values) if (m) m.Chase = null;
            if (!deathPanel) deathPanel = BuildDeathPanel();
            deathPanel.SetActive(true);
        }

        /// <summary>"Hồi sinh" asks the server; only its answer revives the hero at the map's respawn point.</summary>
        public void Revive()
        {
            if (reviving || !HeroDead) return;
            reviving = true;
            Net.FarmRevive(CharId, (ok, h, err) =>
            {
                reviving = false;
                if (!ok || h == null) { LastError = err; return; }
                var rb = world.Player.GetComponent<Rigidbody2D>();
                Vector2 spot = TownLayout.For(world.MapId).Spawn; // this map's arrival point (server RespawnPoints)
                // move both: rb.position alone only syncs the transform at the next physics step
                world.Player.transform.position = new Vector3(spot.x, spot.y + 0.2f, world.Player.transform.position.z);
                if (rb) { rb.position = spot + Vector2.up * 0.2f; rb.linearVelocity = Vector2.zero; }
                HeroDead = false;
                if (heroHealth) heroHealth.SetFromServer(h.hp, h.maxHp);
                var input = world.Player.GetComponent<PlayerInput2D>();
                if (input) input.enabled = true;
                if (deathPanel) deathPanel.SetActive(false);
                nextBeat = Time.time + heartbeatSeconds;
            });
        }

        private GameObject BuildDeathPanel()
        {
            var canvasGo = new GameObject("FarmDeathCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var dim = new GameObject("FarmDeath", typeof(RectTransform)).AddComponent<Image>();
            dim.transform.SetParent(canvasGo.transform, false);
            UiKit.Stretch(dim.rectTransform);
            dim.color = new Color(0.05f, 0.02f, 0.06f, 0.6f);
            var card = new GameObject("Card", typeof(RectTransform)).AddComponent<Image>();
            card.transform.SetParent(dim.transform, false);
            card.rectTransform.sizeDelta = new Vector2(700f, 330f);
            if (panelSprite) { card.sprite = panelSprite; card.type = Image.Type.Sliced; card.color = Color.white; } else card.color = new Color(0.08f, 0.1f, 0.18f, 0.95f);
            Text Label(Transform p, string s, int size, Color c, float y, float h)
            {
                var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
                t.transform.SetParent(p, false);
                t.rectTransform.anchoredPosition = new Vector2(0f, y);
                t.rectTransform.sizeDelta = new Vector2(600f, h);
                t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
                return t;
            }
            Label(card.transform, "Bạn đã gục ngã!", 40, new Color(1f, 0.84f, 0.48f), 110f, 56f);
            Label(card.transform, "Không mất Vàng hay vật phẩm. Hồi sinh tại cổng làng với đầy máu.", 24, new Color(1f, 0.97f, 0.9f), 30f, 70f);
            var btnImg = new GameObject("ReviveButton", typeof(RectTransform)).AddComponent<Image>();
            btnImg.transform.SetParent(card.transform, false);
            btnImg.rectTransform.anchoredPosition = new Vector2(0f, -90f);
            btnImg.rectTransform.sizeDelta = new Vector2(300f, 72f);
            if (buttonSprite) { btnImg.sprite = buttonSprite; btnImg.type = Image.Type.Sliced; } else btnImg.color = new Color(0.26f, 0.6f, 0.5f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(Revive);
            Text bt = Label(btnImg.transform, "Hồi sinh", 28, new Color(1f, 0.97f, 0.9f), 0f, 60f);
            bt.rectTransform.sizeDelta = new Vector2(280f, 60f);
            return canvasGo;
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
                        FarmMonsterView tpl = typedTemplates.FirstOrDefault(x => x.type == s.type && x.view).view;
                        if (!tpl) tpl = s.type == "moss_slime" && mossTemplate ? mossTemplate : puffTemplate;
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
                ApplyHero(state.hero);
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
            if (!loaded || HeroDead || Time.time < nextSwing) return;
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
                if (r.monster != null && r.monster.alive) target.Chase = world.Player.transform; // it fights back (server timer)
                ApplyHero(r.hero);
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
            if (loaded && Time.time >= nextBeat) Heartbeat();
            // (no automatic pickup by the hero: Gold is collected with ↓/S or the "Nhặt" button — or later by a Pet)
        }

        /// <summary>A collectable drop of mine near the hero (what "Nhặt" would pick up), or null.</summary>
        public GoldDropView DropInReach()
        {
            Vector2 hero = HeroPos;
            return drops.Values.Where(g => g && !g.Pending && !g.Expired && g.Landed)
                .Where(g => { Vector2 rest = g.Ground + new Vector2(0.6f, 0f); return Mathf.Abs(hero.x - rest.x) <= pickupRadius + 0.4f && Mathf.Abs(hero.y - rest.y) <= 2f; })
                .OrderBy(g => Mathf.Abs(g.Ground.x + 0.6f - hero.x)).FirstOrDefault();
        }

        /// <summary>Manual pickup (↓/S or the "Nhặt" button): asks the server for the nearest of my drops in reach.
        /// Returns false when nothing is close enough (the press does nothing).</summary>
        public bool TryPickup()
        {
            if (!loaded || HeroDead || !Net) return false;
            var motor = world.Player.GetComponent<PlayerMotor2D>();
            if (motor && (motor.IsClimbing || motor.Ladder)) return false; // ↓ on a ladder means climb, not pick up
            GoldDropView drop = DropInReach();
            if (!drop) return false;
            drop.Pending = true;
            Net.FarmPickup(CharId, drop.DropId, HeroPos, (ok, r, err) =>
            {
                if (!ok || r == null)
                {
                    LastError = err;
                    if (drop) drop.Pending = false;
                    return;
                }
                drops.Remove(drop.DropId);
                world.Character.gold = r.gold; // HUD shows the server-confirmed balance
                GoldPickedUp += r.amount;
                if (drop) drop.Collect(world.Player.transform);
                PlayChime();
                Popup(Vector2.zero, "+" + r.amount + " Vàng", new Color(1f, 0.86f, 0.3f), 44, world.Player.transform); // big, rides above the hero
                ApplyHero(r.hero);
            });
            return true;
        }

        /// <summary>The summoned pet reached this drop: the server checks the pet (out, fed), the owner and the reach,
        /// and pays the same drop once. The coins fly into the pet.</summary>
        public void PetCollect(GoldDropView drop, Transform pet, System.Action<bool, string> done)
        {
            if (!loaded || HeroDead || !Net || !drop || drop.Pending) { done?.Invoke(false, "busy"); return; }
            drop.Pending = true;
            Net.FarmPetPickup(CharId, drop.DropId, HeroPos, (ok, r, err) =>
            {
                if (!ok || r == null) { if (drop) drop.Pending = false; LastError = err; done?.Invoke(false, err); return; }
                drops.Remove(drop.DropId);
                world.Character.gold = r.gold;
                GoldPickedUp += r.amount;
                PetGoldPickedUp += r.amount;
                if (drop) drop.Collect(pet ? pet : world.Player.transform);
                PlayChime();
                Popup(Vector2.zero, "+" + r.amount + " Vàng", new Color(1f, 0.86f, 0.3f), 40, pet ? pet : world.Player.transform);
                ApplyHero(r.hero);
                done?.Invoke(true, null);
            });
        }

        public long PetGoldPickedUp { get; private set; }

        /// <summary>Potions the pet gave on its own (server-decided on a heartbeat) and the last one.</summary>
        public int PetDrinks { get; private set; }
        public PetDrinkState LastPetDrink { get; private set; }
        private InventoryPanel bagPanel;

        private IEnumerator DelayedPopup(float delay, string text, Color color)
        {
            yield return new WaitForSeconds(delay);
            if (world && world.Player) Popup(Vector2.zero, text, color, 36, world.Player.transform);
        }

        private void PetGavePotion(PetDrinkState d)
        {
            PetDrinks++;
            LastPetDrink = d;
            bool hp = d.hp > 0;
            StartCoroutine(DelayedPopup(0.45f, hp ? $"+{d.hp} HP" : $"+{d.mp} MP", hp ? new Color(0.5f, 1f, 0.55f) : new Color(0.55f, 0.8f, 1f))); // after the hit number, not on top of it
            if (PetDirector.Current) PetDirector.Current.GivePotion(hp);
            if (d.left == 0) Toast(hp ? "Pet: hết thuốc HP trong túi!" : "Pet: hết thuốc MP trong túi!");
            if (!bagPanel) bagPanel = FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            if (bagPanel) bagPanel.Refresh(); // HUD potion counts follow the server bag
        }

        // ------------------------------------------------------------------ skills (the server decides everything)
        public int SkillsCast { get; private set; }
        public string LastSkillError { get; private set; }
        private bool skillBusy;

        /// <summary>A monster the skill could hit from here (client-side guess; the server re-checks with its own rules).</summary>
        public FarmMonsterView SkillTarget(SkillDefDto s)
        {
            if (s == null || !world || !world.Player) return null;
            var motor = world.Player.GetComponent<PlayerMotor2D>();
            int facing = motor ? motor.Facing : 1;
            Vector2 hero = HeroPos;
            if (s.shape == "self")
                return monsters.Values.Where(m => m && m.Alive && Mathf.Abs(m.Home.y - hero.y) < 2.2f && Mathf.Abs(m.Position.x - hero.x) <= s.radius + 0.6f)
                    .OrderBy(m => Mathf.Abs(m.Position.x - hero.x)).FirstOrDefault();
            return TargetFor(hero, facing, s.range);
        }

        public bool CanCastNow(int slot, SkillDefDto s, int mpCost)
        {
            if (!loaded || HeroDead || skillBusy || s == null || Time.time < CooldownEnds[slot - 1]) return false;
            var mana = world.Player.GetComponent<PlayerMana>();
            return mana && mana.Current >= mpCost && SkillTarget(s);
        }

        /// <summary>Casts skill 1/2/3 at the best target: the server validates cooldown, MP, range; the hero plays the
        /// class skill animation, the hit area flashes, damage numbers rise, drops fall. Errors come back as text.</summary>
        public void CastSkill(int slot, SkillDefDto s, System.Action<bool, string> done = null)
        {
            if (!loaded || HeroDead || !Net) { done?.Invoke(false, HeroDead ? "Nhân vật đã gục ngã." : "Đang tải bản đồ."); return; }
            if (skillBusy) { done?.Invoke(false, "Đang thi triển."); return; }
            FarmMonsterView target = SkillTarget(s);
            if (!target) { Toast("Không có mục tiêu trong tầm"); done?.Invoke(false, "Không có mục tiêu trong tầm."); return; }
            var motor = world.Player.GetComponent<PlayerMotor2D>();
            skillBusy = true;
            Net.FarmSkill(CharId, slot, target.SpawnId, HeroPos, motor ? motor.Facing : 1, (ok, r, err) =>
            {
                skillBusy = false;
                if (!ok || r == null) { LastSkillError = err; Toast(err); done?.Invoke(false, err); return; }
                SkillsCast++;
                var visual = world.Player.GetComponent<CharacterVisual3D>();
                if (visual) visual.PlaySkill();
                SkillFlash(new Vector2(r.cx, r.cy), s.shape == "line" ? Mathf.Max(1.2f, s.range * 0.5f) : Mathf.Max(1f, s.radius), slot);
                foreach (SkillHitState h in r.hits)
                {
                    if (h.monster == null || !monsters.TryGetValue(h.monster.id, out FarmMonsterView m) || !m) continue;
                    m.ShowHit();
                    m.Apply(h.monster);
                    if (h.monster.alive) m.Chase = world.Player.transform;
                    Popup(m.Position + Vector2.up * 1.6f, "-" + h.damage, slot == 3 ? new Color(1f, 0.55f, 0.3f) : new Color(1f, 0.85f, 0.5f), slot == 3 ? 40 : 34);
                    if (h.HasDrop) { Kills++; SpawnDrop(h.drop, m.Position); }
                }
                ApplyHero(r.hero);
                done?.Invoke(true, r.name);
            });
        }

        /// <summary>Short message above the hero (e.g. "Không đủ MP").</summary>
        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text) || !world || !world.Player) return;
            Popup(Vector2.zero, text, new Color(0.75f, 0.88f, 1f), 26, world.Player.transform);
        }

        private static Sprite ringSprite;

        /// <summary>Expanding ring + soft flash where the skill landed (class colour; skill 3 bigger and warmer).</summary>
        private void SkillFlash(Vector2 at, float radius, int slot)
        {
            if (!ringSprite)
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                        float ring = Mathf.Exp(-Mathf.Pow((r - 0.85f) / 0.08f, 2f)), fill = Mathf.Clamp01(1f - r) * 0.35f;
                        px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring + fill) * (r < 1f ? 1f : 0f));
                    }
                tex.SetPixels(px); tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
            }
            HeroClass c = world.Character.heroClass;
            Color col = c switch
            {
                HeroClass.Swordsman => new Color(0.5f, 1f, 0.95f), HeroClass.Mage => new Color(1f, 0.6f, 0.25f),
                HeroClass.Assassin => new Color(0.75f, 0.5f, 1f), _ => new Color(0.6f, 1f, 0.5f),
            };
            if (slot == 3) col = Color.Lerp(col, new Color(1f, 0.85f, 0.4f), 0.5f);
            var go = new GameObject("SkillFlash");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y + 0.6f, -0.4f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = col;
            sr.sortingOrder = 40;
            StartCoroutine(Flash(go, sr, radius));
        }

        private static IEnumerator Flash(GameObject go, SpriteRenderer sr, float radius)
        {
            Color c0 = sr.color;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.45f)
            {
                if (!go) yield break;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius, Mathf.Sqrt(k));
                sr.color = new Color(c0.r, c0.g, c0.b, 1f - k);
                yield return null;
            }
            if (go) Destroy(go);
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
            StartCoroutine(Rise(go, t, follow, follow && world && world.Player && follow != world.Player.transform ? 1.15f : 2.1f));
        }

        private static IEnumerator Rise(GameObject go, Text t, Transform follow, float above = 2.1f)
        {
            Vector3 start = go.transform.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.9f)
            {
                if (!go) yield break;
                Vector3 basePos = follow ? follow.position + new Vector3(0f, above, -0.5f) : start; // above the hero (or the smaller pet) // Gold text rides above the hero
                go.transform.position = basePos + Vector3.up * k * 0.9f;
                Color c = t.color; c.a = 1f - Mathf.Max(0f, k - 0.5f) * 2f; t.color = c;
                yield return null;
            }
            if (go) Destroy(go);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Layers of the 2D paper doll. Every slot has its OWN SpriteRenderer and its OWN sorting order, so changing one slot never touches another.</summary>
    public enum PaperSlot { Outfit, Bottom, Top, Mask, Hat, Weapon, Wings, Mount }

    /// <summary>
    /// Modular 2D equipment for the sprite bodies: Mũ (Hat), Mặt nạ (Mask), Áo (Top), Quần (Bottom), Cánh (Wings), Thú cưỡi (Mount). Wearing one piece updates ONLY that
    /// slot's SpriteRenderer; the other slots keep what they show. A whole Set is simply its pieces worn in their slots (the server writes them), so any piece can be taken off
    /// or replaced alone later. Every layer follows the body animation frame by frame: the body's sprite name ("Assassin_Run_3") selects the same clip + frame in a body strip
    /// (Áo / Quần), or the same anchor point (head / face / shoulder) for Mũ / Mặt nạ / Cánh; a mount lifts the whole body onto its saddle and runs with the hero.
    /// Draw order (behind -> front): Mount, Wings, body, Bottom, Top, Mask, Hat, Weapon.
    /// The pictures come only from the owner's FashionDatabase (Resources/Fashion/FashionDatabase.asset); an empty field simply shows nothing. The legacy strip loader
    /// (Resources/Chibi2D/Paperdoll/{itemId}) is kept for the older mage outfits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Paperdoll2D : MonoBehaviour
    {
        public static readonly string[] Clips = { "Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill" };
        /// <summary>Legacy 3D-era set ids mapped to strip items (the older mage outfits).</summary>
        public static readonly Dictionary<string, string> SetItems = new Dictionary<string, string> { ["fire_mage"] = "fire_mage", ["frost_mage"] = "frost_mage" };

        private sealed class Layer
        {
            public SpriteRenderer Renderer;
            public string ItemId;
            public bool MasksBody;
            public Dictionary<string, Sprite[]> Frames;      // legacy strip layer
            public FashionVisual Visual;                     // the owner's pictures (FashionDatabase)
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private string heroName = "Mage";
        private readonly Dictionary<PaperSlot, Layer> layers = new Dictionary<PaperSlot, Layer>();
        private Transform headMark;
        private float headBaseY, liftLocal;

        public SpriteRenderer Body => body;
        /// <summary>Metres the body is raised onto a mount's saddle (0 without a mount).</summary>
        public float Lift => liftLocal;

        private void Awake()
        {
            if (!body) { var m = GetComponent<Chibi2DMarker>(); body = m ? m.Renderer : GetComponentInChildren<SpriteRenderer>(); }
            headMark = transform.Find("Head");
            if (headMark) headBaseY = headMark.localPosition.y;
        }

        private string Hero => body && body.sprite && body.sprite.name.IndexOf('_') > 0 ? body.sprite.name.Substring(0, body.sprite.name.IndexOf('_')) : heroName;

        /// <summary>Key of the full-body skin worn ("sakura", "legend"...), null when none.</summary>
        public string SkinKey => layers.TryGetValue(PaperSlot.Outfit, out Layer sk) && sk.ItemId != null && sk.ItemId.StartsWith("skin:") ? sk.ItemId.Substring(sk.ItemId.LastIndexOf(':') + 1) : null;

        public string Equipped(PaperSlot slot) => layers.TryGetValue(slot, out Layer l) ? l.ItemId : null;
        /// <summary>The SpriteRenderer of a slot (null until something was worn there once): own renderer, own sorting order.</summary>
        public SpriteRenderer RendererOf(PaperSlot slot) => layers.TryGetValue(slot, out Layer l) ? l.Renderer : null;
        public static string SlotName(PaperSlot s) => s switch { PaperSlot.Hat => "head", PaperSlot.Mask => "mask", PaperSlot.Top => "top", PaperSlot.Bottom => "bottom", PaperSlot.Wings => "wings", PaperSlot.Mount => "mount", PaperSlot.Weapon => "weapon", _ => "set" };

        /// <summary>Renderers currently showing a sprite (the body counts when it is not masked): for tests and sanity checks.</summary>
        public int VisibleRenderers()
        {
            int n = 0;
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(false))
                if (r.enabled && r.sprite) n++;
            return n;
        }

        /// <summary>Wears an item in ONE slot. A database entry counts as worn even while its pictures are still empty; returns false (changes nothing) when neither the database nor the legacy strips know it.</summary>
        public bool Equip(PaperSlot slot, string itemId, bool masksBody = false)
        {
            if (string.IsNullOrEmpty(itemId)) { Clear(slot); return true; }
            if (layers.TryGetValue(slot, out Layer cur) && cur.ItemId == itemId) return true;
            FashionVisual visual = FashionCatalog2D.Visual(itemId);
            if (visual != null)
            {
                Layer l = LayerFor(slot);
                l.ItemId = itemId; l.MasksBody = false; l.Frames = null; l.Visual = visual;
                l.Renderer.enabled = true;
                if (slot == PaperSlot.Mount) ApplyLift(visual.liftPixels / (body && body.sprite ? body.sprite.pixelsPerUnit : 101f));
                RefreshBody();
                Sync();
                return true;
            }
            var frames = new Dictionary<string, Sprite[]>();
            foreach (string clip in Clips)
            {
                Sprite[] sp = Resources.LoadAll<Sprite>($"Chibi2D/Paperdoll/{itemId}/{Hero}_{clip}");
                if (sp.Length == 0) continue;
                System.Array.Sort(sp, (a, b) => Index(a.name).CompareTo(Index(b.name)));
                frames[clip] = sp;
            }
            if (frames.Count == 0) return false;
            Set(slot, itemId, masksBody, frames);
            return true;
        }

        /// <summary>Equips an item built at runtime (user content / tests): frames by clip name.</summary>
        public void EquipRuntime(PaperSlot slot, string itemId, Dictionary<string, Sprite[]> frames, bool masksBody = false) => Set(slot, itemId, masksBody, frames);

        public void Clear(PaperSlot slot)
        {
            if (!layers.TryGetValue(slot, out Layer l)) return;
            l.ItemId = null; l.Frames = null; l.MasksBody = false; l.Visual = null;
            l.Renderer.sprite = null; l.Renderer.enabled = false;
            if (slot == PaperSlot.Mount) ApplyLift(0f);
            RefreshBody();
        }

        public void ClearAll() { foreach (PaperSlot s in System.Enum.GetValues(typeof(PaperSlot))) Clear(s); }

        /// <summary>
        /// Wears what the server confirmed, slot by slot: only the slots that differ are touched (an unchanged slot keeps its renderer and frame).
        /// A set arrives as its pieces in the slots, so nothing special happens for it here.
        /// </summary>
        public void ApplyLoadout(OutfitLoadout loadout)
        {
            if (layers.TryGetValue(PaperSlot.Outfit, out Layer o) && o.ItemId != null && !o.ItemId.StartsWith("skin:") && !(loadout != null && loadout.set == o.ItemId)) Clear(PaperSlot.Outfit);   // legacy whole-body outfit
            Wear(PaperSlot.Top, loadout?.Get("top"));
            Wear(PaperSlot.Bottom, loadout?.Get("bottom"));
            Wear(PaperSlot.Wings, loadout?.Get("wings"));
            Wear(PaperSlot.Mount, loadout?.Get("mount"));
            string set = loadout != null ? loadout.set : null;            // legacy mage sets still map to a strip layer
            if (!string.IsNullOrEmpty(set) && SetItems.TryGetValue(set, out string legacy)) Equip(PaperSlot.Outfit, legacy, masksBody: true);
            else if (!string.IsNullOrEmpty(set) && set.StartsWith("set2d_")) EquipSkin(set);
            else if (layers.TryGetValue(PaperSlot.Outfit, out Layer sk) && sk.ItemId != null && sk.ItemId.StartsWith("skin:")) Clear(PaperSlot.Outfit);
        }

        /// <summary>
        /// Full-body skin ("set2d_{class}_{key}"): the pictures live in Resources/Fashion/Skins/{Hero}/{key}/{Hero}_{Clip}_{n}.png (Idle, Run, Attack made per skin).
        /// The skin replaces the whole body. Clips it has no pictures for (Jump, Fall, Climb, Hit, Death) hold its Idle pose; Skill plays its Attack frames.
        /// A skin with no pictures at all leaves the normal body visible.
        /// </summary>
        private void EquipSkin(string setId)
        {
            string key = setId.Substring(setId.LastIndexOf('_') + 1);
            string id = "skin:" + Hero + ":" + key;
            if (layers.TryGetValue(PaperSlot.Outfit, out Layer cur) && cur.ItemId == id) return;
            var frames = new Dictionary<string, Sprite[]>();
            foreach (string clip in new[] { "Idle", "Run", "Attack" })
            {
                var list = new List<Sprite>();
                for (int i = 0; i < 12; i++)
                {
                    Sprite s = Resources.Load<Sprite>($"Fashion/Skins/{Hero}/{key}/{Hero}_{clip}_{i}");
                    if (!s) break;
                    list.Add(s);
                }
                if (list.Count > 0) frames[clip] = list.ToArray();
            }
            if (frames.Count == 0) { Clear(PaperSlot.Outfit); return; }
            if (frames.TryGetValue("Idle", out Sprite[] idle)) foreach (string c in new[] { "Jump", "Fall", "Climb", "Hit", "Death" }) frames[c] = new[] { idle[0] };
            if (frames.TryGetValue("Attack", out Sprite[] atk)) frames["Skill"] = atk;
            Set(PaperSlot.Outfit, id, true, frames);
        }

        private void Wear(PaperSlot slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) { Clear(slot); return; }
            if (!Equip(slot, itemId)) Clear(slot);
        }

        private void Set(PaperSlot slot, string itemId, bool masksBody, Dictionary<string, Sprite[]> frames)
        {
            Layer l = LayerFor(slot);
            l.ItemId = itemId; l.MasksBody = masksBody; l.Frames = frames; l.Visual = null;
            l.Renderer.enabled = true;
            RefreshBody();
            Sync();
        }

        private static int OrderOf(PaperSlot slot) => slot switch
        {
            PaperSlot.Mount => -3, PaperSlot.Wings => -2, PaperSlot.Outfit => 1, PaperSlot.Bottom => 1, PaperSlot.Top => 2,
            PaperSlot.Mask => 3, PaperSlot.Hat => 4, PaperSlot.Weapon => 5, _ => 1,
        };

        private Layer LayerFor(PaperSlot slot)
        {
            if (layers.TryGetValue(slot, out Layer l)) return l;
            var go = new GameObject("Paper_" + slot);
            go.transform.SetParent(body ? body.transform.parent : transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (body) { sr.sharedMaterial = body.sharedMaterial; sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + OrderOf(slot); }
            l = new Layer { Renderer = sr };
            layers[slot] = l;
            return l;
        }

        private void RefreshBody()
        {
            bool mask = false;
            foreach (Layer l in layers.Values) if (l.ItemId != null && l.MasksBody) mask = true;
            if (body) body.enabled = !mask;
        }

        private void ApplyLift(float lift)
        {
            liftLocal = lift;
            if (body) body.transform.localPosition = new Vector3(0f, lift, 0f);
            if (headMark) headMark.localPosition = new Vector3(headMark.localPosition.x, headBaseY + lift, headMark.localPosition.z);
        }

        private static int Index(string spriteName) => int.TryParse(spriteName.Substring(spriteName.LastIndexOf('_') + 1), out int i) ? i : 0;

        private void LateUpdate() => Sync();

        private void Sync()
        {
            if (!body || !body.sprite || layers.Count == 0) return;
            string n = body.sprite.name;                       // "{Hero}_{Clip}_{frame}"
            int last = n.LastIndexOf('_');
            if (last < 0) return;
            int frame = Index(n);
            string hero = Hero;
            string clip = n.Substring(hero.Length + 1, last - hero.Length - 1);
            Vector2 pivot = body.sprite.pivot;
            float ppu = body.sprite.pixelsPerUnit;
            foreach (KeyValuePair<PaperSlot, Layer> kv in layers)
            {
                Layer l = kv.Value;
                if (l.ItemId == null) continue;
                Transform t = l.Renderer.transform;
                if (l.Frames != null)                           // legacy strip
                {
                    if (!l.Frames.TryGetValue(clip, out Sprite[] sp) || sp.Length == 0) { l.Renderer.sprite = null; continue; }
                    l.Renderer.sprite = sp[Mathf.Min(frame, sp.Length - 1)];
                    t.localPosition = new Vector3(0f, liftLocal, 0f);
                    continue;
                }
                FashionVisual v = l.Visual;
                if (v == null) continue;
                switch (kv.Key)
                {
                    case PaperSlot.Top:
                    case PaperSlot.Bottom:                      // body-aligned strip of this class
                    {
                        Sprite[] sp = v.StripFor(hero, clip);
                        l.Renderer.sprite = sp != null && sp.Length > 0 ? sp[Mathf.Min(frame, sp.Length - 1)] : null;
                        t.localPosition = new Vector3(0f, liftLocal, 0f);
                        break;
                    }
                    case PaperSlot.Mount:                       // pivot = saddle; runs while the hero runs
                    {
                        Sprite[] cycle = clip == "Run" && v.runFrames != null && v.runFrames.Length > 0 ? v.runFrames : v.frames;
                        l.Renderer.sprite = cycle != null && cycle.Length > 0 ? cycle[Mathf.FloorToInt(Time.time * v.fps) % cycle.Length] : null;
                        t.localPosition = new Vector3(0f, liftLocal, 0f);
                        break;
                    }
                    default:                                    // Wings: pivot on the shoulder anchor, root on the back
                    {
                        if (kv.Key != PaperSlot.Wings || v.frames == null || v.frames.Length == 0 || !FashionCatalog2D.TryAnchor(hero, clip, frame, out BodyAnchor a)) { l.Renderer.sprite = null; break; }
                        Vector2 p = a.Shoulder;
                        p.y -= 20f;                             // the measured shoulder point sits high (it follows the baked hat)
                        p.x += 8f;                              // wing root on the back, not behind the head
                        t.localPosition = new Vector3((p.x - pivot.x) / ppu, (p.y - pivot.y) / ppu + liftLocal, 0f);
                        float fps = clip == "Run" ? v.fps * 1.5f : v.fps;
                        l.Renderer.sprite = v.frames[v.frames.Length > 1 ? Mathf.FloorToInt(Time.time * fps) % v.frames.Length : 0];
                        break;
                    }
                }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    public enum PaperSlot { Outfit, Hat, Weapon }

    /// <summary>
    /// Modular 2D equipment (paper doll) for the sprite bodies. Every slot is ONE SpriteRenderer layer, so equipping an item replaces
    /// the previous item's sprite in that layer (it can never stack or drift: there is nothing left of the old sprite). All layers
    /// follow the body animation frame by frame: the body's current sprite name ("Mage_Run_3") selects the same clip + frame in the
    /// item's strip, so parts stay locked to the pose. An item that masks the body (a full outfit set) hides the base body while worn;
    /// removing it brings the body back. Art lives in Resources/Chibi2D/Paperdoll/{itemId}/{Class}_{Clip}.png (strips sliced to the
    /// body's frame size and pivot). Pure data, no gameplay or server dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Paperdoll2D : MonoBehaviour
    {
        public static readonly string[] Clips = { "Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill" };
        /// <summary>Set ids the server may send as OutfitLoadout.set mapped to paper-doll outfit items (extend as sets are added).</summary>
        public static readonly Dictionary<string, string> SetItems = new Dictionary<string, string> { ["fire_mage"] = "fire_mage", ["frost_mage"] = "frost_mage" };

        private sealed class Layer
        {
            public SpriteRenderer Renderer;
            public string ItemId;
            public bool MasksBody;
            public Dictionary<string, Sprite[]> Frames;
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private string heroName = "Mage";
        private readonly Dictionary<PaperSlot, Layer> layers = new Dictionary<PaperSlot, Layer>();

        public SpriteRenderer Body => body;

        private void Awake()
        {
            if (!body) { var m = GetComponent<Chibi2DMarker>(); body = m ? m.Renderer : GetComponentInChildren<SpriteRenderer>(); }
        }

        /// <summary>Class prefix of the body sprites ("Mage", "Archer", ...): items are named per class.</summary>
        private string Hero => body && body.sprite && body.sprite.name.IndexOf('_') > 0 ? body.sprite.name.Substring(0, body.sprite.name.IndexOf('_')) : heroName;

        public string Equipped(PaperSlot slot) => layers.TryGetValue(slot, out Layer l) ? l.ItemId : null;

        /// <summary>Renderers currently showing a sprite (the body counts when it is not masked): for tests and sanity checks.</summary>
        public int VisibleRenderers()
        {
            int n = 0;
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(false))
                if (r.enabled && r.sprite) n++;
            return n;
        }

        /// <summary>Equips an item from Resources (strips per clip). Returns false (and changes nothing) when the art is missing.</summary>
        public bool Equip(PaperSlot slot, string itemId, bool masksBody = false)
        {
            if (string.IsNullOrEmpty(itemId)) { Clear(slot); return true; }
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
            l.ItemId = null; l.Frames = null; l.MasksBody = false;
            l.Renderer.sprite = null; l.Renderer.enabled = false;
            RefreshBody();
        }

        /// <summary>Maps the server's confirmed outfit set to the Outfit layer (unknown sets clear it, the base body shows).</summary>
        public void ApplyLoadout(OutfitLoadout loadout)
        {
            string set = loadout != null ? loadout.set : null;
            if (!string.IsNullOrEmpty(set) && SetItems.TryGetValue(set, out string item) && Equip(PaperSlot.Outfit, item, masksBody: true)) return;
            Clear(PaperSlot.Outfit);
        }

        private void Set(PaperSlot slot, string itemId, bool masksBody, Dictionary<string, Sprite[]> frames)
        {
            Layer l = LayerFor(slot);
            l.ItemId = itemId; l.MasksBody = masksBody; l.Frames = frames;
            l.Renderer.enabled = true;
            RefreshBody();
            Sync();                                            // show the right frame this very frame
        }

        private Layer LayerFor(PaperSlot slot)
        {
            if (layers.TryGetValue(slot, out Layer l)) return l;
            var go = new GameObject("Paper_" + slot);
            go.transform.SetParent(body ? body.transform.parent : transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (body) { sr.sharedMaterial = body.sharedMaterial; sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + 1 + (int)slot; }
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
            foreach (Layer l in layers.Values)
            {
                if (l.ItemId == null || l.Frames == null) continue;
                if (!l.Frames.TryGetValue(clip, out Sprite[] sp) || sp.Length == 0) { l.Renderer.sprite = null; continue; }
                l.Renderer.sprite = sp[Mathf.Min(frame, sp.Length - 1)];
            }
        }
    }
}
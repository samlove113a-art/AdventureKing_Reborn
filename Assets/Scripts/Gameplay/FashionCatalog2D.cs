using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>One 2D fashion item as listed by Resources/Fashion/catalog.json (data only: names, slot, set pieces, speed bonus; no image).</summary>
    [Serializable]
    public sealed class FashionItem2D
    {
        public string id, slot, name;
        public string[] pieces = new string[0];   // a set: the slots it fills (top / bottom / head / mask)
        public int moveSpeedPct;                  // passive run-speed bonus of this set / mount / wings (the server applies the same number)
    }

    [Serializable] public sealed class FashionCatalogFile { public int version; public FashionItem2D[] items = new FashionItem2D[0]; }
    [Serializable] public sealed class AnchorEntry { public string key; public float[] v = new float[0]; }
    [Serializable] public sealed class AnchorFile { public int cell; public AnchorEntry[] e = new AnchorEntry[0]; }

    /// <summary>Head / face / shoulder point of a hero frame (pixels, y up): hats, masks and wings follow the body frame by frame.</summary>
    public readonly struct BodyAnchor
    {
        public readonly Vector2 Head, Face, Shoulder;
        public BodyAnchor(float[] a, int i) { Head = new Vector2(a[i], a[i + 1]); Face = new Vector2(a[i + 2], a[i + 3]); Shoulder = new Vector2(a[i + 4], a[i + 5]); }
    }

    /// <summary>Client side of the 2D fashion data: item list, the owner's pictures (FashionDatabase), anchors, set pieces and the movement bonus of a worn outfit.</summary>
    public static class FashionCatalog2D
    {
        private static Dictionary<string, FashionItem2D> items;
        private static Dictionary<string, float[]> anchors;
        private static FashionDatabase db;
        public const int AnchorStride = 6;
        public const int MaxMovePct = 40;

        private static void Load()
        {
            if (items != null) return;
            items = new Dictionary<string, FashionItem2D>();
            anchors = new Dictionary<string, float[]>();
            TextAsset t = Resources.Load<TextAsset>("Fashion/catalog");
            if (t) foreach (FashionItem2D i in JsonUtility.FromJson<FashionCatalogFile>(t.text).items) items[i.id] = i;
            TextAsset a = Resources.Load<TextAsset>("Fashion/anchors");
            if (a) foreach (AnchorEntry e in JsonUtility.FromJson<AnchorFile>(a.text).e) anchors[e.key] = e.v;
            db = Resources.Load<FashionDatabase>("Fashion/FashionDatabase");
        }

        public static void Reset() { items = null; anchors = null; db = null; }

        public static IEnumerable<FashionItem2D> All { get { Load(); return items.Values; } }
        public static bool Has(string id) { Load(); return !string.IsNullOrEmpty(id) && items.ContainsKey(id); }
        public static FashionItem2D Get(string id) { Load(); return !string.IsNullOrEmpty(id) && items.TryGetValue(id, out FashionItem2D i) ? i : null; }
        public static FashionDatabase Database { get { Load(); return db; } }

        /// <summary>The owner's pictures of this item or set piece (null when the entry does not exist at all).</summary>
        public static FashionVisual Visual(string id) { Load(); return db ? db.Get(id) : null; }

        /// <summary>Shop / wardrobe icon from the database (null until the owner assigns it).</summary>
        public static Sprite Icon(string id, HeroClass cls) => Visual(id)?.icon;

        public static string PieceId(string setId, string slot) => setId + "." + slot;

        /// <summary>Wears a whole set as separate pieces (each in its own slot, replacing only those slots) — the same rule the server applies.</summary>
        public static OutfitLoadout WithSet(OutfitLoadout o, string setId)
        {
            FashionItem2D set = Get(setId);
            OutfitLoadout next = o.WithSet(setId);
            if (set != null) foreach (string p in set.pieces) next = next.With(p, PieceId(setId, p));
            return next;
        }

        /// <summary>Takes a set off: the set and every piece of it (pieces replaced by other items stay).</summary>
        public static OutfitLoadout WithoutSet(OutfitLoadout o, string setId)
        {
            OutfitLoadout next = o.WithSet("");
            FashionItem2D set = Get(setId);
            if (set != null) foreach (string p in set.pieces) if (next.Get(p) == PieceId(setId, p)) next = next.With(p, null);
            return next;
        }

        /// <summary>Run-speed bonus (%) of the worn set (only while whole) + mount + wings, capped like the server.</summary>
        public static int MoveSpeedPct(OutfitLoadout outfit)
        {
            if (outfit == null) return 0;
            int pct = 0;
            FashionItem2D set = Get(outfit.set);
            if (set != null && set.pieces.All(p => outfit.Get(p) == PieceId(set.id, p))) pct += set.moveSpeedPct;
            pct += Get(outfit.Get("mount"))?.moveSpeedPct ?? 0;
            pct += Get(outfit.Get("wings"))?.moveSpeedPct ?? 0;
            return Mathf.Min(pct, MaxMovePct);
        }

        /// <summary>Anchor of this frame, or false when the clip has no reliable head point (Climb, Death): overlays hide there.</summary>
        public static bool TryAnchor(string hero, string clip, int frame, out BodyAnchor anchor)
        {
            Load();
            anchor = default;
            if (!anchors.TryGetValue(hero + "_" + clip, out float[] v) || v.Length < AnchorStride) return false;
            int n = v.Length / AnchorStride;
            anchor = new BodyAnchor(v, Mathf.Clamp(frame, 0, n - 1) * AnchorStride);
            return true;
        }
    }
}

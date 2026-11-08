using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>How an outfit piece moves on top of the skeleton animation (all purely visual).</summary>
    public enum PieceMotion { None, WingFlap, CapeSway, Pendulum, SkirtSway }

    /// <summary>An extra joint an outfit piece adds under a hero bone (cape chain, wing root, bell pivot).</summary>
    [Serializable]
    public sealed class ExtraBone
    {
        public string name;
        public string parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
    }

    /// <summary>
    /// One renderable part of a cosmetic, fitted to one hero class: a skinned mesh bound to the hero's own skeleton
    /// (bone names + bind poses baked by OutfitArtPipeline), optionally with extra joints and a motion style,
    /// or a prefab (particles) mounted on a bone.
    /// </summary>
    [Serializable]
    public sealed class OutfitPiece
    {
        public Mesh mesh;
        public Material material;
        public string[] boneNames = new string[0];
        public ExtraBone[] extraBones = new ExtraBone[0];
        public PieceMotion motion;
        public GameObject prefab;
        public string mountBone;
    }

    [Serializable]
    public sealed class ClassFit
    {
        public HeroClass heroClass;
        public OutfitPiece[] pieces = new OutfitPiece[0];
    }

    [Serializable]
    public sealed class CosmeticArt
    {
        public string itemId;
        public string slot;
        public Sprite icon;
        [Tooltip("For a premium set: the Gold slots it covers (hidden while the set is worn).")]
        public string[] coversSlots = new string[0];
        public ClassFit[] fits = new ClassFit[0];

        public ClassFit For(HeroClass c)
        {
            foreach (ClassFit f in fits) if (f.heroClass == c) return f;
            return null;
        }
    }

    /// <summary>
    /// Client-side art for the server catalog (Resources/CosmeticLibrary). The server owns names, prices, ownership
    /// and what is worn; this only knows how each item id looks on each class. An item the server lists but this
    /// library cannot draw is never offered for sale by the client.
    /// </summary>
    [CreateAssetMenu(menuName = "AdventureKing/Cosmetic Library")]
    public sealed class CosmeticLibrary : ScriptableObject
    {
        public List<CosmeticArt> items = new List<CosmeticArt>();

        private static CosmeticLibrary cached;
        public static CosmeticLibrary Load()
        {
            if (!cached) cached = Resources.Load<CosmeticLibrary>("CosmeticLibrary");
            return cached;
        }

        public CosmeticArt Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (CosmeticArt a in items) if (a.itemId == itemId) return a;
            return null;
        }

        public bool CanDraw(string itemId, HeroClass c) => Get(itemId)?.For(c) is { pieces: { Length: > 0 } };
    }

    /// <summary>A worn outfit as the server stores it (field names = server OutfitDto).</summary>
    [Serializable]
    public sealed class OutfitLoadout
    {
        public string set = "";
        public OutfitSlotItem[] slots = new OutfitSlotItem[0];

        public string Get(string slot)
        {
            if (slots != null) foreach (OutfitSlotItem s in slots) if (s.slot == slot) return s.itemId;
            return null;
        }

        public OutfitLoadout With(string slot, string itemId)
        {
            var list = new List<OutfitSlotItem>();
            if (slots != null) foreach (OutfitSlotItem s in slots) if (s.slot != slot && !string.IsNullOrEmpty(s.itemId)) list.Add(s);
            if (!string.IsNullOrEmpty(itemId)) list.Add(new OutfitSlotItem { slot = slot, itemId = itemId });
            return new OutfitLoadout { set = set, slots = list.ToArray() };
        }

        public OutfitLoadout WithSet(string setId) => new OutfitLoadout { set = setId ?? "", slots = (OutfitSlotItem[])(slots ?? new OutfitSlotItem[0]).Clone() };

        public OutfitLoadout Clone() => WithSet(set);

        public bool SameAs(OutfitLoadout o)
        {
            if (o == null) return false;
            if ((set ?? "") != (o.set ?? "")) return false;
            int n = 0;
            if (slots != null) foreach (OutfitSlotItem s in slots) { if (o.Get(s.slot) != s.itemId) return false; n++; }
            return n == (o.slots?.Length ?? 0);
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder("set=" + set);
            if (slots != null) foreach (OutfitSlotItem s in slots) sb.Append(' ').Append(s.slot).Append('=').Append(s.itemId);
            return sb.ToString();
        }
    }

    [Serializable]
    public sealed class OutfitSlotItem
    {
        public string slot;
        public string itemId;
    }
}

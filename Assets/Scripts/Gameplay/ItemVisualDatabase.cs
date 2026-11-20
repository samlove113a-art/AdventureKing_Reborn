using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Resources/ItemVisuals.asset: icon + worldSprite per item id (also the coin pictures "coin_small" / "coin_medium" / "coin_bag") and the pixel pets' pictures.</summary>
    [CreateAssetMenu(menuName = "AdventureKing/Item Visuals")]
    public sealed class ItemVisualDatabase : ScriptableObject
    {
        public List<ItemVisual> items = new List<ItemVisual>();
        public List<PetVisual> pets = new List<PetVisual>();

        private Dictionary<string, ItemVisual> byId;
        public ItemVisual Item(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null || byId.Count != items.Count) { byId = new Dictionary<string, ItemVisual>(); foreach (ItemVisual v in items) if (v != null && !string.IsNullOrEmpty(v.itemId)) byId[v.itemId] = v; }
            return byId.TryGetValue(id, out ItemVisual r) ? r : null;
        }
        public PetVisual Pet(string id) => string.IsNullOrEmpty(id) ? null : pets.FirstOrDefault(p => p != null && p.petId == id);
    }
}

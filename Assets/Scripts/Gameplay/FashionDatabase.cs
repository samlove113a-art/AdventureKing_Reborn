using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Resources/Fashion/FashionDatabase.asset: one entry per fashion item and per set piece (created from catalog.json by the editor tool, art slots empty).</summary>
    [CreateAssetMenu(menuName = "AdventureKing/Fashion Database")]
    public sealed class FashionDatabase : ScriptableObject
    {
        public List<FashionVisual> items = new List<FashionVisual>();
        public FashionVisual Get(string id) => string.IsNullOrEmpty(id) ? null : items.FirstOrDefault(i => i != null && i.id == id);
    }
}

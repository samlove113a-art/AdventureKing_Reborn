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

        [Tooltip("BaseBody per class: the hero strips WITHOUT the default hat (bare head with full face and hair), same clips / frame count / size / pivot as Assets/Art/Chibi2D/<Class>. Drag the PNG frames in; empty = the original hero is shown and nothing is cut.")]
        public List<FashionClassArt> noHatBaseSprites = new List<FashionClassArt>();

        public Sprite[] BaseBodyFor(string hero, string clip)
        {
            foreach (FashionClassArt a in noHatBaseSprites)
                if (a != null && a.heroClass.ToString() == hero)
                    foreach (FashionClipFrames c in a.clips) if (c != null && c.clip == clip && c.frames != null && c.frames.Length > 0) return c.frames;
            return null;
        }
        public FashionVisual Get(string id) => string.IsNullOrEmpty(id) ? null : items.FirstOrDefault(i => i != null && i.id == id);
    }
}

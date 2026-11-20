using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    // ======================================================================= fashion
    /// <summary>Body-aligned strip of one clip for ONE class (Áo / Quần): same frame size, pivot and frame count as the hero's own clip.</summary>
    [Serializable] public sealed class FashionClipFrames { public string clip; public Sprite[] frames = new Sprite[0]; }
    [Serializable] public sealed class FashionClassArt { public HeroClass heroClass; public FashionClipFrames[] clips = new FashionClipFrames[0]; }

    /// <summary>
    /// The pictures of ONE wearable piece. Nothing is drawn by code: the owner drags the pixel art in (all fields start empty and the game simply shows nothing for an empty one).
    ///  - icon: shop / wardrobe card.
    ///  - frames: Mũ / Mặt nạ / Cánh (one or more frames; cycled). The sprite PIVOT is the attach point: head top (hat), face centre (mask), shoulder (wings).
    ///  - runFrames + frames: Thú cưỡi idle / run cycle; the sprite pivot is the saddle; liftPixels = saddle height above the ground contact (the hero is raised by it).
    ///  - bodyArt: Áo / Quần strips per class, same size and pivot as the hero strips.
    /// A set's pieces have the id "&lt;setId&gt;.&lt;slot&gt;" (top / bottom / head / mask) and are filled the same way.
    /// </summary>
    [Serializable]
    public sealed class FashionVisual
    {
        public string id;
        public string slot;
        public Sprite icon;
        public Sprite[] frames = new Sprite[0];
        public Sprite[] runFrames = new Sprite[0];
        [Min(1f)] public float fps = 8f;
        [Min(0f)] public float liftPixels = 52f;
        public FashionClassArt[] bodyArt = new FashionClassArt[0];

        public Sprite[] StripFor(string hero, string clip)
        {
            foreach (FashionClassArt a in bodyArt)
                if (a != null && a.heroClass.ToString() == hero)
                    foreach (FashionClipFrames c in a.clips) if (c != null && c.clip == clip) return c.frames;
            return null;
        }
    }

    // ======================================================================= items / loot / pets
    /// <summary>Pictures of one item: icon (bag, shops) and the sprite shown on the ground when it drops. Empty = falls back to the shared icon of its kind.</summary>
    [Serializable] public sealed class ItemVisual { public string itemId; public Sprite icon; public Sprite worldSprite; }

    /// <summary>Pictures of one pet: idle animation frames, the hungry picture and a portrait for the pet window. Empty fields show nothing.</summary>
    [Serializable]
    public sealed class PetVisual
    {
        public string petId;
        public Sprite portrait;
        public Sprite[] idleFrames = new Sprite[0];
        public Sprite hungry;
        [Min(0.5f)] public float fps = 3f;
        [Min(0.1f)] public float scale = 1f;
    }
}

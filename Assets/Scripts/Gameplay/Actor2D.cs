using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// 2D chibi bodies for NPCs and monsters: swaps the 3D model of an actor for its sprite body (Resources/Chibi2D/Actors/{name}_Visual2D)
    /// when the 2D style is on. Same Animator state names (Idle, Walk, Wave, Talk, Bow / Idle, Walk, Attack, Hit, Death), so NpcActor and
    /// FarmMonsterView keep driving them unchanged; the 3D model stays in the prefab, only hidden.
    /// </summary>
    public static class Actor2D
    {
        /// <summary>Monster type id (server config) -> sprite actor.</summary>
        public static string ForMonster(string type) => type switch
        {
            "young_mushroom" => "MonsterNamNon", "pollen_slime" => "PollenSlime", "puff_slime" => "PuffSlime", "moss_slime" => "MossSlime",
            "glow_moss_mushroom" => "GlowMoss", "mushroom_chief" => "MushroomChief", "flower_bee" => "FlowerBee", "crystal_bat" => "CrystalBat", "jade_turtle" => "JadeTurtle",
            "jade_guardian" => "JadeGuardian", "shadow_leaf" => "ShadowLeaf", "toxic_slime" => "ToxicSlime", "toxic_elite" => "ToxicElite", "stone_guard" => "StoneGuard",
            "dark_mushroom" => "DarkMushroom", "royal_guard" => "RoyalGuard", "mushroom_king" => "MushroomKing", _ => null,
        };

        /// <summary>Returns the sprite body (already parented where the 3D model was), or null when the style is 3D / no art exists.</summary>
        public static GameObject Swap(Transform host, string actorName, Animator model3D)
        {
            if (!HeroVisualStyle.Chibi2D || string.IsNullOrEmpty(actorName)) return null;
            GameObject prefab = Resources.Load<GameObject>("Chibi2D/Actors/" + actorName + "_Visual2D");
            if (!prefab) return null;
            Transform parent = model3D && model3D.transform.parent ? model3D.transform.parent : host;
            if (model3D) model3D.gameObject.SetActive(false);                 // the 3D model stays in the prefab, hidden
            GameObject body = Object.Instantiate(prefab, parent);
            body.name = actorName + "_2D";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * Chibi2DMarker.ScaleOf(body);
            return body;
        }

        /// <summary>Sprite bodies face right by default: mirror to face left.</summary>
        public static void Flip(GameObject body, bool left)
        {
            if (!body) return;
            Vector3 s = body.transform.localScale;
            float want = Mathf.Abs(s.x) * (left ? -1f : 1f);
            if (!Mathf.Approximately(s.x, want)) body.transform.localScale = new Vector3(want, s.y, s.z);
        }
    }
}
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Creates / updates the two ScriptableObjects the owner fills with pixel art (this tool draws nothing; every Sprite field starts empty and is never overwritten):
    ///   Assets/Resources/Fashion/FashionDatabase.asset   one entry per 2D fashion item (hat / mask / top / bottom / wings / mount / set) and per set piece ("&lt;setId&gt;.&lt;slot&gt;")
    ///   Assets/Resources/ItemVisuals.asset               icon + worldSprite per item (items.json) + coin entries, and the pictures of every pet (pets.json)
    /// Existing entries keep their sprites; only missing entries are added. Run again after the catalogues change.
    ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.VisualDatabases.BuildFromCommandLine
    /// </summary>
    public static class VisualDatabases
    {
        private const string FashionPath = "Assets/Resources/Fashion/FashionDatabase.asset", ItemsPath = "Assets/Resources/ItemVisuals.asset";

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/Art/Create Fashion + Item Visual Databases")]
        public static void Build()
        {
            BuildFashion();
            BuildItems();
            AssetDatabase.SaveAssets();
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static void BuildFashion()
        {
            var db = Load<FashionDatabase>(FashionPath);
            var cat = JsonUtility.FromJson<FashionCatalogFile>(File.ReadAllText("Assets/Resources/Fashion/catalog.json"));
            var have = new HashSet<string>(db.items.Where(i => i != null).Select(i => i.id));
            int added = 0;
            foreach (FashionItem2D it in cat.items)
            {
                if (have.Add(it.id)) { db.items.Add(NewVisual(it.id, it.slot, it.slot == "mount")); added++; }
                foreach (string piece in it.pieces)
                    if (have.Add(FashionCatalog2D.PieceId(it.id, piece))) { db.items.Add(NewVisual(FashionCatalog2D.PieceId(it.id, piece), piece, false)); added++; }
            }
            EditorUtility.SetDirty(db);
            Debug.Log($"[VISUALS] FashionDatabase: {db.items.Count} entries ({added} added), sprites left empty for the owner");
        }

        private static FashionVisual NewVisual(string id, string slot, bool mount)
        {
            var v = new FashionVisual { id = id, slot = slot };
            if (slot is "top" or "bottom")
                v.bodyArt = new[] { HeroClass.Swordsman, HeroClass.Mage, HeroClass.Assassin, HeroClass.Archer }
                    .Select(c => new FashionClassArt { heroClass = c, clips = Paperdoll2D.Clips.Select(cl => new FashionClipFrames { clip = cl }).ToArray() }).ToArray();
            return v;
        }

        private static void BuildItems()
        {
            var db = Load<ItemVisualDatabase>(ItemsPath);
            string json = File.ReadAllText("Server/AdventureKing.Server/config/items.json");
            int added = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"id\":\\s*\"([a-z0-9_]+)\",\\s*\"name\""))
            {
                string id = m.Groups[1].Value;
                if (db.items.All(i => i == null || i.itemId != id)) { db.items.Add(new ItemVisual { itemId = id }); added++; }
            }
            foreach (string coin in new[] { "coin_small", "coin_medium", "coin_bag" })
                if (db.items.All(i => i == null || i.itemId != coin)) { db.items.Add(new ItemVisual { itemId = coin }); added++; }
            string pets = File.ReadAllText("Server/AdventureKing.Server/config/pets.json");
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(pets, "\"id\":\\s*\"(pet_[a-z0-9_]+)\""))
            {
                string id = m.Groups[1].Value;
                if (db.pets.All(p => p == null || p.petId != id)) { db.pets.Add(new PetVisual { petId = id }); added++; }
            }
            EditorUtility.SetDirty(db);
            Debug.Log($"[VISUALS] ItemVisuals: {db.items.Count} items, {db.pets.Count} pets ({added} added), sprites left empty for the owner");
        }
    }
}

using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>2D pet body: one side-view sprite facing right plus a hungry variant (and optional idle frames). Pets with a prefab in Resources/Chibi2D/Pets use it;
    /// pets added later are built from the owner's pictures in Resources/ItemVisuals.asset (PetVisual) — nothing is drawn by code, an empty field shows nothing.
    /// PetDirector drives hop / squash / flip; this only swaps the sprite and mirrors it.</summary>
    public class Pet2DBody : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        public Sprite Normal, Hungry;
        [Tooltip("Optional idle animation: cycled while the pet is fed.")] public Sprite[] Frames = new Sprite[0];
        public float Fps = 3f;

        private static GameObject LoadPrefab(string petId) => Resources.Load<GameObject>("Chibi2D/Pets/" + petId + "_Visual2D");

        /// <summary>The older prefab of a pet, unless the owner already put pictures into the database for it (those win).</summary>
        public static GameObject Load(string petId) => OwnerArt(petId) ? null : LoadPrefab(petId);

        private static bool OwnerArt(string petId)
        {
            PetVisual v = ItemIconBook.Database ? ItemIconBook.Database.Pet(petId) : null;
            return v != null && (v.portrait || (v.idleFrames != null && v.idleFrames.Length > 0));
        }

        /// <summary>A pet that has no prefab: a body built at runtime from its PetVisual (pictures from the owner's database; may be empty).</summary>
        public static GameObject Create(string petId)
        {
            PetVisual v = ItemIconBook.Database ? ItemIconBook.Database.Pet(petId) : null;
            var root = new GameObject(petId + "_Visual2D");
            var sg = new GameObject("Sprite");
            sg.transform.SetParent(root.transform, false);
            var sr = sg.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;
            var template = LoadPrefab("pet_cloud_cat");                                   // same unlit pixel material as the other pets
            var tr = template ? template.GetComponentInChildren<SpriteRenderer>() : null;
            if (tr) sr.sharedMaterial = tr.sharedMaterial;
            var body = root.AddComponent<Pet2DBody>();
            body.Renderer = sr;
            if (v != null)
            {
                body.Frames = v.idleFrames ?? new Sprite[0];
                body.Normal = body.Frames.Length > 0 ? body.Frames[0] : v.portrait;
                body.Hungry = v.hungry ? v.hungry : body.Normal;
                body.Fps = v.fps;
                root.transform.localScale = Vector3.one * v.scale;
            }
            sr.sprite = body.Normal;
            return root;
        }

        /// <summary>The pet's picture for menus: prefab sprite, else the owner's portrait / first idle frame.</summary>
        /// <summary>The owner's database picture of this pet (portrait, else first idle frame), or null.</summary>
        public static Sprite OwnerPortrait(string petId)
        {
            PetVisual v = ItemIconBook.Database ? ItemIconBook.Database.Pet(petId) : null;
            if (v == null) return null;
            return v.portrait ? v.portrait : v.idleFrames != null && v.idleFrames.Length > 0 ? v.idleFrames[0] : null;
        }

        public static Sprite PortraitOf(string petId)
        {
            GameObject g = Load(petId);
            var b = g ? g.GetComponent<Pet2DBody>() : null;
            if (b && b.Normal) return b.Normal;
            PetVisual v = ItemIconBook.Database ? ItemIconBook.Database.Pet(petId) : null;
            if (v == null) return null;
            return v.portrait ? v.portrait : v.idleFrames != null && v.idleFrames.Length > 0 ? v.idleFrames[0] : null;
        }

        public void Set(bool hungry, int facing)
        {
            if (!Renderer) return;
            Sprite s = hungry && Hungry ? Hungry : Normal;
            if (!hungry && Frames != null && Frames.Length > 1) s = Frames[Mathf.FloorToInt(Time.time * Fps) % Frames.Length];
            if (Renderer.sprite != s) Renderer.sprite = s;
            Renderer.flipX = facing < 0;
            // pets with a single picture still breathe: a gentle squash and stretch from the feet (slower and smaller when hungry). Movement only, no drawing.
            bool single = Frames == null || Frames.Length <= 1;
            float t = Time.time * (hungry ? 1.2f : 3f);
            float k = single ? (hungry ? 0.012f : 0.035f) * Mathf.Sin(t) : 0f;
            Renderer.transform.localScale = new Vector3(1f - k * 0.5f, 1f + k, 1f);
        }
    }
}

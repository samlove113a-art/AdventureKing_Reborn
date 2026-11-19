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
            if (!hungry && Frames != null && Frames.Length > 1) s = Moving ? Frames[Mathf.FloorToInt(Time.time * 7f) % Frames.Length] : Frames[0];   // frame B only while the pet walks / flies
            if (Renderer.sprite != s) Renderer.sprite = s;
            Renderer.flipX = facing < 0;
        }

        public enum MoveStyle { Walk, Fly, Glide }
        /// <summary>How the pet gets around: dragons fly (the big war dragon, phoenix, owl, lantern, cloud hover), baby dragons and animals walk, the Eastern dragon glides in S waves.</summary>
        public static MoveStyle StyleOf(string id) => id switch
        {
            "pet_war_dragon" or "pet_phoenix" or "pet_night_owl" or "pet_lantern_wisp" or "pet_rainbow_cloud" => MoveStyle.Fly,
            "pet_ink_dragon" => MoveStyle.Glide,
            _ => MoveStyle.Walk,
        };
        public MoveStyle Style;
        public bool Moving;          // set by the director before Set(): walk / flap frames only play while moving
        public float HoverHeight = 0.9f;

        /// <summary>Procedural movement of the (single) picture: returns the extra height above the ground. Walk = step rocking, Fly = hovering with wing-beat pulses, Glide = S-wave.</summary>
        public float Animate(bool hungry, bool moving, float step, float worldX, int facing)
        {
            if (!Renderer) return 0f;
            float t = Time.time, lift = 0f, rot = 0f, sx = 1f, sy = 1f;
            switch (Style)
            {
                case MoveStyle.Fly:
                {
                    lift = hungry ? 0.12f : HoverHeight + Mathf.Sin(t * 2.4f) * 0.08f;
                    float beat = Mathf.Sin(t * (moving ? 16f : 9f)) * (hungry ? 0.02f : 0.07f);
                    sy = 1f + beat; sx = 1f - beat * 0.6f;
                    rot = moving && !hungry ? -9f * facing : Mathf.Sin(t * 1.7f) * 2f;
                    break;
                }
                case MoveStyle.Glide:
                {
                    float wave = Mathf.Sin(t * (moving ? 3.2f : 1.6f) + worldX * 1.6f);
                    lift = hungry ? 0.15f : HoverHeight + wave * (moving ? 0.24f : 0.1f);
                    rot = 0f; sx = 1f; sy = 1f;
                    UpdateWave(hungry, moving, facing, t, worldX);          // the body slithers slice by slice
                    break;
                }
                default:
                {
                    float breathe = Mathf.Sin(t * (hungry ? 1.2f : 3f)) * (hungry ? 0.012f : 0.03f);
                    rot = moving ? Mathf.Sin(step) * 4f : 0f;                 // left-right rocking with every step
                    sy = 1f + (moving ? Mathf.Abs(Mathf.Sin(step)) * -0.05f : breathe); sx = 1f - (sy - 1f) * 0.5f;
                    break;
                }
            }
            Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            Renderer.transform.localScale = new Vector3(sx, sy, 1f);
            return lift;
        }

        // ---- Eastern dragon: the picture is cut into vertical slices (a runtime cut of the owner's sprite, nothing drawn) and a sine wave travels along the body: a full slither cycle
        private const int WaveSlices = 16;
        private Transform waveRoot;
        private SpriteRenderer[] waveParts = new SpriteRenderer[0];
        private Sprite waveSource;

        private void UpdateWave(bool hungry, bool moving, int facing, float t, float worldX)
        {
            Sprite src = hungry && Hungry ? Hungry : Normal;
            if (!src || !src.texture) { Renderer.enabled = true; return; }
            if (waveSource != src)
            {
                if (waveRoot) Destroy(waveRoot.gameObject);
                waveRoot = new GameObject("Wave").transform;
                waveRoot.SetParent(Renderer.transform.parent, false);
                Rect r = src.textureRect; float w = r.width / WaveSlices;
                waveParts = new SpriteRenderer[WaveSlices];
                for (int i = 0; i < WaveSlices; i++)
                {
                    var sp = Sprite.Create(src.texture, new Rect(r.x + i * w, r.y, w, r.height), new Vector2(0.5f, src.pivot.y / r.height), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    var go = new GameObject("S" + i);
                    go.transform.SetParent(waveRoot, false);
                    go.transform.localPosition = new Vector3(((i + 0.5f) * w - src.pivot.x) / src.pixelsPerUnit, 0f, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp; sr.sharedMaterial = Renderer.sharedMaterial; sr.sortingLayerID = Renderer.sortingLayerID; sr.sortingOrder = Renderer.sortingOrder;
                    waveParts[i] = sr;
                }
                waveSource = src;
            }
            Renderer.enabled = false;
            waveRoot.localScale = new Vector3(facing, 1f, 1f);
            float amp = hungry ? 0.012f : moving ? 0.075f : 0.035f, speed = moving ? 5.5f : 2.4f;
            for (int i = 0; i < waveParts.Length; i++)
                waveParts[i].transform.localPosition = new Vector3(waveParts[i].transform.localPosition.x, Mathf.Sin(t * speed - i * 0.55f) * amp * (0.3f + 0.7f * i / (WaveSlices - 1f)), 0f);   // the tail swings more than the head
        }
    }
}

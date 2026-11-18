using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Four-layer 2D parallax for a side-scrolling map: Sky, Far mountains, Mid forest, Foreground. Put it on an empty "Background" object, assign the
    /// camera (or leave empty = Camera.main) and one sprite per layer; layers are tiled sideways automatically (3 copies) so they never run out.
    /// Factor = how much of the camera's movement the layer follows: 0 = glued to the screen (sky), 1 = moves with the world (the playable tileset),
    /// above 1 = moves faster than the world (foreground grass/flowers at the screen edge). Movement is smoothed and snapped to the 100 PPU pixel grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxBackground4 : MonoBehaviour
    {
        public enum Slot { Sky, FarMountains, MidForest, Foreground }

        [Serializable]
        public sealed class Layer
        {
            public Slot slot;
            public Sprite sprite;
            [Tooltip("Share of the camera's horizontal movement this layer follows (0 sky .. 1 world .. 1.3 foreground).")]
            [Range(0f, 1.5f)] public float factorX = 0.5f;
            [Tooltip("Share of the camera's vertical movement (keep small so the horizon does not slide).")]
            [Range(0f, 1.5f)] public float factorY = 0.1f;
            [Tooltip("World Y of the layer's centre when the camera is at its start position.")]
            public float baseY;
            [Tooltip("Z depth behind the playable plane (z = 0). Foreground uses a negative value.")]
            public float z = 20f;
            public int sortingOrder = -100;
            [Tooltip("Constant drift in world units per second (clouds).")]
            public float autoScrollX;
            [Tooltip("Tint (a little lighter for the mid layer, as the Art Bible asks).")]
            public Color tint = Color.white;
            [NonSerialized] public Transform root;
            [NonSerialized] public Transform[] tiles;
            [NonSerialized] public float width, scrolled;
        }

        [SerializeField] private Camera cam;
        [SerializeField] private Layer[] layers = new Layer[4];
        [SerializeField, Tooltip("Higher = snappier. The layer eases toward its target position instead of jumping with the camera.")] private float smoothing = 14f;
        [SerializeField, Tooltip("Pixels per unit of the project's sprites (the Art Bible fixes 100).")] private float pixelsPerUnit = 100f;
        [SerializeField, Tooltip("Shader/material for the layers; empty = Sprites/Default.")] private Material material;

        private Vector3 camStart;
        private bool ready;

        private void Awake() { Build(); }

        private void OnEnable() { if (!ready) Build(); }

        /// <summary>Creates the tiled sprite rows. Safe to call again after assigning new sprites.</summary>
        public void Build()
        {
            if (!cam) cam = Camera.main;
            if (!cam) return;
            foreach (Layer l in layers)
            {
                if (l == null) continue;
                if (l.root) Destroy(l.root.gameObject);
                if (!l.sprite) continue;
                l.root = new GameObject("Layer_" + l.slot).transform;
                l.root.SetParent(transform, false);
                l.width = l.sprite.bounds.size.x;
                l.tiles = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("Tile" + i);
                    go.transform.SetParent(l.root, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = l.sprite; sr.color = l.tint; sr.sortingOrder = l.sortingOrder;
                    sr.sharedMaterial = material ? material : sr.sharedMaterial;
                    go.transform.localPosition = new Vector3((i - 1) * l.width, 0f, 0f);
                    l.tiles[i] = go.transform;
                }
                l.root.position = new Vector3(cam.transform.position.x, l.baseY, l.z);
                l.scrolled = 0f;
            }
            camStart = cam.transform.position;
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready || !cam) return;
            Vector3 c = cam.transform.position;
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            float px = 1f / Mathf.Max(1f, pixelsPerUnit);
            foreach (Layer l in layers)
            {
                if (l == null || !l.root) continue;
                l.scrolled += l.autoScrollX * Time.deltaTime;
                // where the layer's pattern origin should be: it follows part of the camera's travel since the start
                float targetX = camStart.x + (c.x - camStart.x) * l.factorX + l.scrolled;
                float targetY = l.baseY + (c.y - camStart.y) * l.factorY;
                // keep the three tiles around the camera: shift the tile row by whole widths so the screen is always covered
                float rel = c.x - targetX;                                   // how far the camera is from the pattern origin
                float shift = Mathf.Round(rel / l.width) * l.width;
                Vector3 p = l.root.position;
                float nx = Mathf.Lerp(p.x, targetX + shift, k);
                // a wrap changes `shift` by a whole tile: do not let the smoothing slide across that jump
                if (Mathf.Abs(nx - (targetX + shift)) > l.width * 0.5f) nx = targetX + shift;
                float ny = Mathf.Lerp(p.y, targetY, k);
                l.root.position = new Vector3(Mathf.Round(nx / px) * px, Mathf.Round(ny / px) * px, l.z);
            }
        }

        /// <summary>Default Art-Bible layout for a map: sets slots, factors, depths and tints, keeps the sprites you already assigned.</summary>
        [ContextMenu("Apply Art Bible defaults")]
        public void ApplyDefaults()
        {
            Layer[] old = layers;
            layers = new Layer[4];
            for (int i = 0; i < 4; i++) layers[i] = old != null && i < old.Length && old[i] != null ? old[i] : new Layer();
            Set(layers[0], Slot.Sky, 0.00f, 0.00f, 40f, -400, 0.004f, Color.white);                       // glued sky, clouds drift
            Set(layers[1], Slot.FarMountains, 0.15f, 0.04f, 30f, -300, 0f, new Color(0.92f, 0.95f, 1f));  // slow, a touch hazier by distance
            Set(layers[2], Slot.MidForest, 0.45f, 0.08f, 15f, -200, 0f, new Color(1f, 1f, 1f, 1f));       // lighter than the tileset (Art Bible)
            Set(layers[3], Slot.Foreground, 1.25f, 0.00f, -2f, 200, 0f, Color.white);                     // faster than the world, at the screen edge
        }

        private static void Set(Layer l, Slot slot, float fx, float fy, float z, int order, float auto, Color tint)
        {
            l.slot = slot; l.factorX = fx; l.factorY = fy; l.z = z; l.sortingOrder = order; l.autoScrollX = auto; l.tint = tint;
        }

        private void Reset() { ApplyDefaults(); }
    }
}

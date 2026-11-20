using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Optional prefab-based equipment renderer for characters whose clothing is made of
    /// SEPARATE renderers. Replaces the previous item in the same slot before showing the new one.
    /// IMPORTANT: Adventure King Reborn already has HeroOutfit.Apply() for server-owned outfits;
    /// do not make both systems render the same slot simultaneously. See integration guide.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentManager : MonoBehaviour
    {
        public enum EquipmentSlot { Top, Bottom, Head, Hair, Gloves, Shoes, Back, Accessory, Weapon }

        [Serializable]
        public sealed class SlotBinding
        {
            public EquipmentSlot slot = EquipmentSlot.Top;
            [Tooltip("Dedicated container for this slot ONLY. Existing direct children are treated as default clothes.")]
            public Transform mount;
            [Tooltip("Optional old shirt meshes located outside this slot container; never drag the whole character/body here.")]
            public GameObject[] legacyVisuals = new GameObject[0];
            [Tooltip("Show the original garment again when this slot is unequipped.")]
            public bool restoreDefaultsOnUnequip = true;

            [NonSerialized] internal GameObject equippedInstance;
            [NonSerialized] internal List<DefaultVisual> defaults;
        }

        internal sealed class DefaultVisual
        {
            public GameObject go;
            public bool wasActive;
        }

        [Header("Character rig (drag from the existing player prefab)")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private SkinnedMeshRenderer referenceBodyRenderer;
        [Header("One container per equipment slot")]
        [SerializeField] private SlotBinding[] slots = { new SlotBinding() };
        [Header("Diagnostics")]
        [SerializeField] private bool verboseWarnings = true;

        public event Action<EquipmentSlot, GameObject> EquipmentChanged;

        private void Awake()
        {
            if (!rigRoot) rigRoot = transform;
            if (!referenceBodyRenderer)
            {
                foreach (var smr in rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr && !smr.name.StartsWith("Outfit_") && !smr.name.StartsWith("Weapon_"))
                    { referenceBodyRenderer = smr; break; }
                }
            }

            var usedSlots = new HashSet<EquipmentSlot>();
            foreach (var s in slots)
            {
                if (s == null) continue;
                if (!usedSlots.Add(s.slot))
                {
                    Debug.LogError($"[EquipmentManager] Duplicate slot: {s.slot}", this);
                    enabled = false;
                    return;
                }
                if (!s.mount)
                {
                    var container = new GameObject("Equipment_" + s.slot);
                    container.transform.SetParent(transform, false);
                    s.mount = container.transform;
                }
                s.defaults = new List<DefaultVisual>();
                for (int i = 0; i < s.mount.childCount; i++)
                {
                    var child = s.mount.GetChild(i).gameObject;
                    s.defaults.Add(new DefaultVisual { go = child, wasActive = child.activeSelf });
                }
                foreach (var g in s.legacyVisuals)
                {
                    if (!g || ContainsDefault(s.defaults, g)) continue;
                    s.defaults.Add(new DefaultVisual { go = g, wasActive = g.activeSelf });
                }
            }
        }

        /// <summary>Equip a new shirt prefab, replacing every old Top visual owned by this slot.</summary>
        public bool EquipTop(GameObject newTopPrefab) => Equip(EquipmentSlot.Top, newTopPrefab);

        /// <summary>
        /// Equips only the requested slot. The new item is validated off-screen first; on failure
        /// the existing outfit stays visible. Ownership and purchases MUST be verified by server code.
        /// </summary>
        public bool Equip(EquipmentSlot slot, GameObject prefab)
        {
            if (!enabled) return false;
            SlotBinding binding = FindSlot(slot);
            if (binding == null || !binding.mount || !referenceBodyRenderer || !rigRoot)
            {
                Debug.LogError($"[EquipmentManager] Missing slot '{slot}', mount, rig, or body renderer.", this);
                return false;
            }
            if (!prefab) { Unequip(slot); return true; }

            // The temporary parent is inactive: no flash of overlapping clothing while we remap.
            var staging = new GameObject("Equipment_Staging");
            staging.transform.SetParent(binding.mount, false);
            staging.SetActive(false);
            GameObject candidate = null;
            try
            {
                candidate = Instantiate(prefab, staging.transform, false);
                candidate.name = "Equipped_" + slot + "_" + prefab.name;
                if (!TryBindSkin(candidate, out string error))
                {
                    Debug.LogError("[EquipmentManager] Cannot equip " + prefab.name + ": " + error, this);
                    DestroySafely(staging);
                    return false;
                }
                // Do not let an Animator copied with a garment animate a second skeleton.
                foreach (var animator in candidate.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var animation in candidate.GetComponentsInChildren<Animation>(true)) animation.enabled = false;

                // Hide the previous item immediately (Destroy() itself waits until end of frame).
                GameObject previous = binding.equippedInstance;
                if (previous) previous.SetActive(false);
                if (binding.defaults != null)
                {
                    foreach (var visual in binding.defaults)
                    {
                        if (visual.go && IsSafeToHide(visual.go)) visual.go.SetActive(false);
                    }
                }
                // A dedicated mount may contain old outfits not tracked by this manager.
                // Never touch other slots, the player's body mesh, or animation rig.
                for (int i = binding.mount.childCount - 1; i >= 0; --i)
                {
                    var old = binding.mount.GetChild(i).gameObject;
                    if (old == staging || old == previous) continue;
                    if (IsSafeToHide(old)) old.SetActive(false);
                }

                candidate.transform.SetParent(binding.mount, false);
                candidate.SetActive(true);
                binding.equippedInstance = candidate;
                if (previous) { previous.transform.SetParent(null, false); DestroySafely(previous); }
                DestroySafely(staging);
                EquipmentChanged?.Invoke(slot, candidate);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                DestroySafely(staging);
                return false;
            }
        }

        /// <summary>Remove the equipped garment and optionally restore scene-authored defaults.</summary>
        public void UnequipTop() => Unequip(EquipmentSlot.Top);

        public void Unequip(EquipmentSlot slot)
        {
            SlotBinding binding = FindSlot(slot);
            if (binding == null) return;
            GameObject old = binding.equippedInstance;
            binding.equippedInstance = null;
            if (old) { old.SetActive(false); old.transform.SetParent(null, false); DestroySafely(old); }
            if (binding.restoreDefaultsOnUnequip && binding.defaults != null)
                foreach (var v in binding.defaults)
                    if (v.go && IsSafeToHide(v.go)) v.go.SetActive(v.wasActive);
            EquipmentChanged?.Invoke(slot, null);
        }

        public GameObject GetEquipped(EquipmentSlot slot) => FindSlot(slot)?.equippedInstance;

        private SlotBinding FindSlot(EquipmentSlot slot)
        {
            foreach (var s in slots) if (s != null && s.slot == slot) return s;
            return null;
        }

        private bool TryBindSkin(GameObject instance, out string error)
        {
            error = null;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                error = "Prefab has no SkinnedMeshRenderer; a skinned garment is required.";
                return false;
            }
            var boneMap = BuildCharacterBoneMap();
            foreach (var renderer in renderers)
            {
                Transform[] sourceBones = renderer.bones;
                Mesh mesh = renderer.sharedMesh;
                if (!mesh || sourceBones == null || sourceBones.Length == 0)
                {
                    error = $"{renderer.name}: missing mesh or source skeleton.";
                    return false;
                }
                if (mesh.bindposes == null || mesh.bindposes.Length != sourceBones.Length)
                {
                    error = $"{renderer.name}: mesh bind-pose count differs from bone count; re-export on the target rig.";
                    return false;
                }
                var mapped = new Transform[sourceBones.Length];
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    if (!sourceBones[i] || !boneMap.TryGetValue(sourceBones[i].name, out mapped[i]))
                    {
                        error = $"{renderer.name}: missing character bone at index {i} ('{(sourceBones[i] ? sourceBones[i].name : "null")}').";
                        return false;
                    }
                }
                Transform mappedRoot = referenceBodyRenderer.rootBone ? referenceBodyRenderer.rootBone : rigRoot;
                if (renderer.rootBone && !boneMap.TryGetValue(renderer.rootBone.name, out mappedRoot))
                {
                    error = $"{renderer.name}: root bone '{renderer.rootBone.name}' not found on target character.";
                    return false;
                }
                renderer.bones = mapped;
                renderer.rootBone = mappedRoot;
                renderer.quality = SkinQuality.Bone4;
                // Small prefab bounds may cull clothing during Jump/Climb; use character bounds as a floor.
                Bounds b = renderer.localBounds;
                Bounds characterBounds = referenceBodyRenderer.localBounds;
                characterBounds.Expand(characterBounds.size.magnitude * 0.5f);
                b.Encapsulate(characterBounds);
                renderer.localBounds = b;
                if (verboseWarnings && renderer.transform.lossyScale != referenceBodyRenderer.transform.lossyScale)
                    Debug.LogWarning($"[EquipmentManager] Check scale/bind poses on {renderer.name}; identical bone names alone cannot fix an incompatible mesh.", this);
            }
            return true;
        }

        private Dictionary<string, Transform> BuildCharacterBoneMap()
        {
            var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in referenceBodyRenderer.bones)
                if (t && !map.ContainsKey(t.name)) map.Add(t.name, t);
            if (referenceBodyRenderer.rootBone && !map.ContainsKey(referenceBodyRenderer.rootBone.name))
                map.Add(referenceBodyRenderer.rootBone.name, referenceBodyRenderer.rootBone);
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t || IsInsideEquipmentSlot(t)) continue;
                if (!map.ContainsKey(t.name)) map.Add(t.name, t);
            }
            return map;
        }

        private bool IsInsideEquipmentSlot(Transform t)
        {
            foreach (var s in slots)
                if (s != null && s.mount && (t == s.mount || t.IsChildOf(s.mount))) return true;
            return false;
        }

        private bool IsSafeToHide(GameObject go)
        {
            if (!go || go == gameObject || go == rigRoot.gameObject || go == referenceBodyRenderer.gameObject)
                return false;
            // NEVER disable any parent of the original body or rig.
            if (rigRoot.IsChildOf(go.transform) || referenceBodyRenderer.transform.IsChildOf(go.transform))
            {
                Debug.LogError($"[EquipmentManager] Refusing to hide '{go.name}': it contains the player rig/body.", this);
                return false;
            }
            return true;
        }

        private static bool ContainsDefault(List<DefaultVisual> list, GameObject candidate)
        {
            foreach (var v in list) if (v.go == candidate) return true;
            return false;
        }

        private static void DestroySafely(GameObject go)
        {
            if (!go) return;
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}

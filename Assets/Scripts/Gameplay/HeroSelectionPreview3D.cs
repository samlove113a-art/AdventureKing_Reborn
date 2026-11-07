using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    [Serializable]
    public sealed class HeroPreview3DEntry
    {
        public HeroClass heroClass;
        [Tooltip("Visual-only, original rigged 3D hero prefab. No gameplay scripts/physics.")]
        public GameObject modelPrefab;
        public string idleState = "Idle";
        public string attackState = "Attack";
        public string skillState = "Skill";
    }

    /// <summary>
    /// Renders an actual animated 3D hero to a UI RawImage via a dedicated camera.
    /// Requires final 3D model prefabs, Animator controllers, a preview layer and
    /// render texture configured in Unity. Never substitutes static portraits.
    /// </summary>
    public sealed class HeroSelectionPreview3D : MonoBehaviour, IDragHandler
    {
        [Header("Stage")]
        [SerializeField] private Transform modelMount;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private RenderTexture previewTexture;
        [SerializeField] private RawImage targetImage;
        [SerializeField] private Text previewMessage;
        [SerializeField, Range(0, 31)] private int previewLayer = 29;

        [Header("Original 3D hero models")]
        [SerializeField] private HeroPreview3DEntry[] classes = new HeroPreview3DEntry[4];

        [Header("Presentation")]
        [SerializeField, Range(0.1f, 2f)] private float degreesPerDragPixel = 0.55f;
        [SerializeField] private float defaultYaw = 0f;

        private GameObject activeModel;
        private Animator activeAnimator;
        private HeroPreview3DEntry activeEntry;
        private HeroClass? activeClass;

        private void Awake()
        {
            if (previewCamera)
            {
                if (previewTexture) previewCamera.targetTexture = previewTexture;
                previewCamera.cullingMask = 1 << previewLayer;
            }

            if (targetImage && previewTexture) targetImage.texture = previewTexture;
        }

        /// <returns>True only when the requested 3D visual prefab exists and was instantiated.</returns>
        public bool ShowClass(HeroClass heroClass)
        {
            if (activeClass.HasValue && activeClass.Value == heroClass && activeModel)
                return true;

            ClearModel();
            activeClass = heroClass;

            if (!modelMount)
            {
                SetMessage("Chưa cấu hình sân khấu xem trước nhân vật.");
                return false;
            }

            HeroPreview3DEntry entry = null;
            foreach (HeroPreview3DEntry candidate in classes)
            {
                if (candidate != null && candidate.heroClass == heroClass)
                {
                    entry = candidate;
                    break;
                }
            }

            if (entry == null || !entry.modelPrefab)
            {
                SetMessage("Nhân vật 3D nghề này chưa được tích hợp.");
                return false;
            }

            activeEntry = entry;
            activeModel = Instantiate(entry.modelPrefab, modelMount);
            activeModel.transform.localPosition = Vector3.zero;
            activeModel.transform.localRotation = Quaternion.Euler(0f, defaultYaw, 0f);
            activeModel.transform.localScale = Vector3.one;
            ApplyPreviewLayer(activeModel.transform);

            activeAnimator = activeModel.GetComponentInChildren<Animator>(true);
            if (activeAnimator)
            {
                activeAnimator.applyRootMotion = false;
                TryPlay(entry.idleState);
            }
            SetMessage("");
            return true;
        }

        public void PlayAttackPreview()
        {
            if (activeEntry != null && TryPlay(activeEntry.attackState) &&
                AdventureAudioDirector.Instance)
                AdventureAudioDirector.Instance.PlayClassAttack(activeEntry.heroClass);
        }

        public void PlaySkillPreview()
        {
            if (activeEntry != null && TryPlay(activeEntry.skillState) &&
                AdventureAudioDirector.Instance)
                AdventureAudioDirector.Instance.PlayClassSkillCast(activeEntry.heroClass);
        }

        public void PlayIdlePreview()
        {
            if (activeEntry != null) TryPlay(activeEntry.idleState);
        }

        public void ResetRotation()
        {
            if (activeModel)
                activeModel.transform.localRotation = Quaternion.Euler(0f, defaultYaw, 0f);
        }

        /// <summary>
        /// Attach to the model's RawImage; enable Raycast Target and add EventSystem.
        /// Drag horizontally with mouse or finger to inspect the genuine 3D model.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!activeModel || eventData == null) return;
            float delta = eventData.delta.x * degreesPerDragPixel;
            activeModel.transform.Rotate(0f, -delta, 0f, Space.Self);
        }

        private bool TryPlay(string state)
        {
            if (!activeAnimator || string.IsNullOrWhiteSpace(state)) return false;
            int hash = Animator.StringToHash(state);
            if (!activeAnimator.HasState(0, hash))
            {
                SetMessage("Animator thiếu hoạt ảnh " + state + ".");
                return false;
            }
            activeAnimator.CrossFadeInFixedTime(hash, 0.14f, 0);
            return true;
        }

        private void ClearModel()
        {
            if (activeModel) Destroy(activeModel);
            activeModel = null;
            activeAnimator = null;
            activeEntry = null;
        }

        private void SetMessage(string value)
        {
            if (previewMessage) previewMessage.text = value;
        }

        private void ApplyPreviewLayer(Transform root)
        {
            root.gameObject.layer = previewLayer;
            foreach (Transform child in root) ApplyPreviewLayer(child);
        }

        private void OnDestroy()
        {
            if (previewCamera && previewCamera.targetTexture == previewTexture)
                previewCamera.targetTexture = null;
        }
    }
}

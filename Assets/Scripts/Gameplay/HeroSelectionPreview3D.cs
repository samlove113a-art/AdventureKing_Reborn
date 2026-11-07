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
        [Tooltip("Projectile range multiplier in the hall so arrows/orbs land on the stage, not in the lens.")]
        [SerializeField, Range(0.05f, 1f)] private float previewProjectileRange = 0.45f;
        [Tooltip("Yaw used while previewing attacks/skills; matches CharacterVisual3D.rightFacingYaw in gameplay.")]
        [SerializeField] private float combatPreviewYaw = 110f;
        [SerializeField, Min(30f)] private float turnDegreesPerSecond = 540f;

        private GameObject activeModel;
        private Animator activeAnimator;
        private HeroPreview3DEntry activeEntry;
        private HeroClass? activeClass;

        public HeroClass? ActiveClass => activeClass;
        public GameObject ActiveModel => activeModel;

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
            HeroCombatPresentation presentation = activeModel.GetComponent<HeroCombatPresentation>();
            if (presentation) presentation.ProjectileRangeScale = previewProjectileRange;

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
            if (activeEntry == null) return;
            TurnToCombatProfile(2.2f);
            if (TryPlay(activeEntry.attackState) && AdventureAudioDirector.Instance)
                AdventureAudioDirector.Instance.PlayClassAttack(activeEntry.heroClass);
        }

        public void PlaySkillPreview()
        {
            if (activeEntry == null) return;
            TurnToCombatProfile(3.6f);
            if (TryPlay(activeEntry.skillState) && AdventureAudioDirector.Instance)
                AdventureAudioDirector.Instance.PlayClassSkillCast(activeEntry.heroClass);
        }

        // Attack/skill clips are authored for the in-game side view, so the hall shows them from that same
        // profile (projectiles then cross the stage instead of flying into the lens), then turns back.
        private float? targetYaw;
        private float restoreYaw, restoreAt = -1f;

        private void TurnToCombatProfile(float holdSeconds)
        {
            if (!activeModel) return;
            if (restoreAt < 0f) restoreYaw = activeModel.transform.localEulerAngles.y;
            targetYaw = combatPreviewYaw;
            restoreAt = Time.time + holdSeconds;
        }

        private void Update()
        {
            if (!activeModel) return;
            if (restoreAt >= 0f && Time.time >= restoreAt)
            {
                targetYaw = restoreYaw;
                restoreAt = -1f;
            }
            if (targetYaw is float yaw)
            {
                float current = activeModel.transform.localEulerAngles.y;
                float next = Mathf.MoveTowardsAngle(current, yaw, turnDegreesPerSecond * Time.deltaTime);
                activeModel.transform.localRotation = Quaternion.Euler(0f, next, 0f);
                if (Mathf.Abs(Mathf.DeltaAngle(next, yaw)) < 0.1f) targetYaw = null;
            }
        }

        public void PlayIdlePreview()
        {
            if (activeEntry != null) TryPlay(activeEntry.idleState);
        }

        public void ResetRotation()
        {
            targetYaw = null;
            restoreAt = -1f;
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
            targetYaw = null; // the player's own rotation always wins over the automatic turn
            restoreAt = -1f;
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
            targetYaw = null;
            restoreAt = -1f;
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

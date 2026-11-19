using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    [Serializable]
    public sealed class HeroPreview2DEntry
    {
        public HeroClass heroClass;
        [Tooltip("Pixel-art frames of the hero (Assets/Art/Chibi2D/{Class}/{Class}_Idle|Attack|Skill.png).")]
        public Sprite[] idle, attack, skill;
        public float idleFps = 8f, attackFps = 14f, skillFps = 12f;
    }

    /// <summary>
    /// 100% 2D hero preview for the character hall: plays the class's pixel-art Idle / Attack / Skill strips on a UI Image
    /// (frame by frame, unscaled time). No camera, no render texture, no model, no light. Tapping the hero turns it around.
    /// Attack / Skill fire <see cref="AttackImpact"/> / <see cref="SkillImpact"/> at their contact frame (same timing as the gameplay clips).
    /// </summary>
    public sealed class HeroSelectionPreview2D : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image target;
        [SerializeField] private Text previewMessage;
        [SerializeField] private HeroPreview2DEntry[] classes = new HeroPreview2DEntry[4];
        [SerializeField, Range(0.05f, 1f)] private float attackImpactAt = 0.35f, skillImpactAt = 0.68f;

        public enum PreviewState { Idle, Attack, Skill }

        private HeroPreview2DEntry active;
        private float stateStarted;
        private bool impactFired;
        private bool facingLeft;

        public HeroClass? ActiveClass { get; private set; }
        public PreviewState State { get; private set; }
        public int Frame { get; private set; }
        public Image Target => target;
        public bool FacingLeft => facingLeft;
        public event Action AttackImpact, SkillImpact;

        /// <returns>True when the class has frames to show.</returns>
        public bool ShowClass(HeroClass heroClass)
        {
            if (ActiveClass.HasValue && ActiveClass.Value == heroClass && active != null) return true;
            active = null;
            ActiveClass = heroClass;
            foreach (HeroPreview2DEntry e in classes)
                if (e != null && e.heroClass == heroClass && e.idle != null && e.idle.Length > 0) { active = e; break; }
            if (active == null)
            {
                if (previewMessage) previewMessage.text = "Sprite 2D của nghề này chưa được tích hợp.";
                if (target) target.enabled = false;
                return false;
            }
            if (previewMessage) previewMessage.text = "";
            if (target) target.enabled = true;
            Begin(PreviewState.Idle);
            return true;
        }

        public void PlayIdlePreview() { if (active != null) Begin(PreviewState.Idle); }

        public void PlayAttackPreview()
        {
            if (active == null || active.attack == null || active.attack.Length == 0) return;
            Begin(PreviewState.Attack);
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayClassAttack(active.heroClass);
        }

        public void PlaySkillPreview()
        {
            if (active == null || active.skill == null || active.skill.Length == 0) return;
            Begin(PreviewState.Skill);
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayClassSkillCast(active.heroClass);
        }

        /// <summary>Turns the hero around (mirrors the sprite, like the gameplay body does).</summary>
        public void TurnAround()
        {
            facingLeft = !facingLeft;
            Apply();
        }

        public void ResetFacing()
        {
            facingLeft = false;
            Apply();
        }

        public void OnPointerClick(PointerEventData eventData) => TurnAround();

        private void Begin(PreviewState s)
        {
            State = s;
            stateStarted = Time.unscaledTime;
            impactFired = false;
            Frame = 0;
            Apply();
        }

        private Sprite[] Frames => active == null ? null : State == PreviewState.Attack ? active.attack : State == PreviewState.Skill ? active.skill : active.idle;
        private float Fps => active == null ? 8f : State == PreviewState.Attack ? active.attackFps : State == PreviewState.Skill ? active.skillFps : active.idleFps;

        private void Update()
        {
            Sprite[] frames = Frames;
            if (active == null || frames == null || frames.Length == 0) return;
            float t = (Time.unscaledTime - stateStarted) * Mathf.Max(1f, Fps);
            int index = Mathf.FloorToInt(t);
            if (State != PreviewState.Idle)
            {
                float progress = Mathf.Clamp01(t / frames.Length);
                float at = State == PreviewState.Attack ? attackImpactAt : skillImpactAt;
                if (!impactFired && progress >= at)
                {
                    impactFired = true;
                    if (State == PreviewState.Attack) AttackImpact?.Invoke(); else SkillImpact?.Invoke();
                }
                if (index >= frames.Length) { Begin(PreviewState.Idle); return; }
            }
            else index %= frames.Length;
            if (index != Frame) { Frame = index; Apply(); }
        }

        private void Apply()
        {
            Sprite[] frames = Frames;
            if (!target || frames == null || frames.Length == 0) return;
            target.sprite = frames[Mathf.Clamp(Frame, 0, frames.Length - 1)];
            Vector3 s = target.rectTransform.localScale;
            target.rectTransform.localScale = new Vector3(facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }
    }
}

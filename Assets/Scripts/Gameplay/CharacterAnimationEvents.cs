using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Receiver for Animation Events authored on hero/monster clips (placed on the same GameObject
    /// as the Animator). Contact frames come from the clips themselves, so VFX, hit sounds and damage
    /// presentation fire on the frame the weapon actually connects instead of when the button is pressed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationEvents : MonoBehaviour
    {
        /// <summary>Weapon contact / projectile release of the basic attack.</summary>
        public event Action AttackImpact;
        /// <summary>Wind-up of the class skill (glyphs, charge sounds).</summary>
        public event Action SkillCast;
        /// <summary>Each damaging beat of the class skill (combo skills fire several).</summary>
        public event Action SkillImpact;
        public event Action Footstep;

        // Names below are referenced by the clip import settings; keep them stable.
        public void OnAttackImpact() => AttackImpact?.Invoke();
        public void OnSkillCast() => SkillCast?.Invoke();
        public void OnSkillImpact() => SkillImpact?.Invoke();
        public void OnFootstep() => Footstep?.Invoke();
    }
}

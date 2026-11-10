using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Locomotion facts measured from this hero's own clips by the art pipeline (unscaled model, 1x playback).
    /// CharacterVisual3D uses them to play Run/Climb exactly as fast as the body really moves, so planted feet
    /// don't skate and hands don't slide along the ladder.
    /// </summary>
    public sealed class HeroMotionProfile : MonoBehaviour
    {
        [Tooltip("Backward speed of the planted foot in the Run clip at 1x (m/s, model scale 1).")]
        public float runStrideSpeed = 1.4f;
        [Tooltip("Backward speed of the planted foot in the Walk clip at 1x (m/s, model scale 1; cinematic walks).")]
        public float walkStrideSpeed = 0.28f;
        [Tooltip("Seconds per full Climb cycle at 1x (two hand-over-hand pulls).")]
        public float climbCycleSeconds = 1.27f;
        [Tooltip("Ladder height gained by one full climb cycle (two rungs).")]
        public float climbAdvancePerCycle = 0.84f;

        [Header("Rung-accurate climb (ClimbClipBaker)")]
        [Tooltip("Climb clip time follows the hero's height on the ladder instead of playback speed.")]
        public bool climbPhaseDriven;
        [Tooltip("Hand grip height at the start of a grip, above the feet (model scale 1).")]
        public float climbGripTop = 0.6f;
        [Tooltip("World distance between ladder rungs.")]
        public float rungSpacing = 0.42f;
        [Tooltip("World height of the first rung above the ladder's foot.")]
        public float firstRung = 0.35f;

        /// <summary>
        /// Normalized climb-clip time for feet at <paramref name="feetY"/> on a ladder whose foot is at
        /// <paramref name="ladderBottom"/>: one cycle = two rungs, phase 0 = left hand closing on a rung.
        /// </summary>
        public float ClimbPhase(float feetY, float ladderBottom, float heroScale)
        {
            float rung0 = ladderBottom + firstRung;
            float u = (feetY + climbGripTop * heroScale - rung0) / (2f * rungSpacing);
            return u - Mathf.Floor(u);
        }
    }
}

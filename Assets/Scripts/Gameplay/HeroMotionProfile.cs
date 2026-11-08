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
        [Tooltip("Seconds per full Climb cycle at 1x (two hand-over-hand pulls).")]
        public float climbCycleSeconds = 1.27f;
        [Tooltip("Ladder height gained by one full climb cycle (two rungs).")]
        public float climbAdvancePerCycle = 0.84f;
    }
}

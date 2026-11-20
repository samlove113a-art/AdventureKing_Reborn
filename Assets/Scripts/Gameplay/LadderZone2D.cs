using UnityEngine;

namespace AdventureKing.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LadderZone2D : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider2D>().isTrigger = true;

        /// <summary>World height of the ladder's foot (where climbing down ends).</summary>
        public float Bottom => GetComponent<Collider2D>().bounds.min.y;

        /// <summary>World height of the top of the climb zone.</summary>
        public float Top => GetComponent<Collider2D>().bounds.max.y;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<PlayerMotor2D>();
            if (motor) motor.SetLadder(this, true);
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<PlayerMotor2D>();
            if (motor) motor.SetLadder(this, false);
        }
    }
}

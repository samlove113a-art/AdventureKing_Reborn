using UnityEngine;

namespace AdventureKing.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LadderZone2D : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider2D>().isTrigger = true;
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

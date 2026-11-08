using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Endless decorative motion: spin, bob and light pulse (portal rings, crystals, lanterns, sails).</summary>
    public sealed class AmbientMotion : MonoBehaviour
    {
        public Vector3 spinDegreesPerSecond;
        public float bobHeight;
        public float bobSpeed = 1f;
        public float phase;
        public Light pulseLight;
        public float pulseAmount;
        public float pulseSpeed = 1f;
        public float swayDegrees;
        public float swaySpeed = 1f;

        private Vector3 basePos;
        private Quaternion baseRot;
        private float baseIntensity;

        private void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            if (pulseLight) baseIntensity = pulseLight.intensity;
        }

        private void Update()
        {
            float t = Time.time + phase;
            if (spinDegreesPerSecond != Vector3.zero) transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
            else if (swayDegrees != 0f) transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * swaySpeed) * swayDegrees);
            if (bobHeight != 0f) transform.localPosition = basePos + Vector3.up * Mathf.Sin(t * bobSpeed) * bobHeight;
            if (pulseLight) pulseLight.intensity = baseIntensity * (1f + Mathf.Sin(t * pulseSpeed) * pulseAmount);
        }
    }
}

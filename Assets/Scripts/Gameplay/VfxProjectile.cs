using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Visual projectile (spell orb, arrow). Flies along its forward axis and spawns an impact effect
    /// when it reaches its range or touches a Collider2D on the hit mask. Arrived fires exactly once,
    /// so impact audio is tied to the arrival, not to the launch.
    /// </summary>
    public sealed class VfxProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 11f;
        [SerializeField, Min(0.1f)] private float range = 5.5f;
        [SerializeField] private LayerMask hitMask = 0;
        [SerializeField] private GameObject impactEffect;
        [SerializeField, Min(0f)] private float lingerSeconds = 0.6f;
        [Tooltip("Children (e.g. trails) kept alive after impact so they fade out instead of popping.")]
        [SerializeField] private ParticleSystem[] detachOnImpact = new ParticleSystem[0];
        [SerializeField] private GameObject[] hideOnImpact = new GameObject[0];

        public event Action Arrived;
        /// <summary>Multiplier on range (the lobby preview shortens flights so they stay on the stage).</summary>
        public float RangeScale { get; set; } = 1f;
        private float travelled;
        private bool done;

        private void Update()
        {
            if (done) return;
            float step = speed * Time.deltaTime;
            Vector3 dir = transform.forward;
            if (hitMask.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, step, hitMask);
                if (hit.collider)
                {
                    transform.position = (Vector3)hit.point + Vector3.forward * transform.position.z;
                    Arrive();
                    return;
                }
            }
            transform.position += dir * step;
            travelled += step;
            if (travelled >= range * RangeScale * Mathf.Max(0.01f, transform.lossyScale.y)) Arrive();
        }

        private void Arrive()
        {
            done = true;
            if (impactEffect)
            {
                GameObject fx = Instantiate(impactEffect, transform.position, Quaternion.LookRotation(-transform.forward));
                fx.transform.localScale = impactEffect.transform.localScale * transform.lossyScale.y;
                HeroCombatPresentation.SetLayerRecursive(fx.transform, gameObject.layer);
            }
            foreach (ParticleSystem ps in detachOnImpact)
                if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (GameObject go in hideOnImpact)
                if (go) go.SetActive(false);
            Arrived?.Invoke();
            Arrived = null;
            Destroy(gameObject, lingerSeconds);
        }
    }
}

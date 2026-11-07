using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    [RequireComponent(typeof(PlayerInput2D), typeof(Health2D))]
    public sealed class PlayerMelee2D : MonoBehaviour
    {
        [SerializeField] private PlayerInput2D controls;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private Health2D attacker;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask targets = ~0;
        [SerializeField, Min(0.05f)] private float radius = 0.7f;
        [SerializeField, Min(0f)] private float forward = 0.6f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField, Min(0.05f)] private float cooldown = 0.45f;
        private float readyAt;
        private readonly HashSet<Health2D> hit = new HashSet<Health2D>();
        public event Action Attacked;

        private void Awake()
        {
            if (!controls) controls = GetComponent<PlayerInput2D>();
            if (!motor) motor = GetComponent<PlayerMotor2D>();
            if (!attacker) attacker = GetComponent<Health2D>();
        }

        private void Update()
        {
            if (!controls.ConsumeAttack() || Time.time < readyAt || attacker.IsDead) return;
            readyAt = Time.time + cooldown;
            hit.Clear();
            Vector2 origin = attackOrigin ? (Vector2)attackOrigin.position : (Vector2)transform.position;
            Vector2 center = origin + Vector2.right * (motor ? motor.Facing : 1) * forward;
            foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius, targets))
            {
                Health2D victim = col.GetComponentInParent<Health2D>();
                if (victim && hit.Add(victim)) victim.TakeDamage(damage, attacker);
            }
            Attacked?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = attackOrigin ? attackOrigin.position : transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin + Vector3.right * (motor ? motor.Facing : 1) * forward, radius);
        }
    }
}

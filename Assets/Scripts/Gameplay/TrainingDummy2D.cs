using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Hero training dummy: wobbles when the hero strikes near it (visual feedback only; combat stays server-side).</summary>
    public sealed class TrainingDummy2D : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        private float wobble;
        private PlayerMelee2D melee;

        private void OnDisable() { if (melee) melee.Attacked -= OnHeroAttack; melee = null; }
        private void OnHeroAttack() { if (melee && Mathf.Abs(melee.transform.position.x - transform.position.x) < 2.6f) wobble = 1f; }

        private void Update()
        {
            if (!melee) { melee = FindAnyObjectByType<PlayerMelee2D>(); if (melee) melee.Attacked += OnHeroAttack; }
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 1.6f);
            if (visual) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 22f) * 7f * wobble);
        }
    }
}

using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    public enum CombatFaction { Neutral, Player, Enemy }

    [DisallowMultipleComponent]
    public sealed class Health2D : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private CombatFaction faction = CombatFaction.Neutral;
        [SerializeField] private bool disableOnDeath;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public CombatFaction Faction => faction;
        public bool IsDead => CurrentHealth <= 0;

        public event Action<Health2D, int> Damaged;
        public event Action<Health2D> Died;

        private void Awake() => CurrentHealth = maxHealth;

        public bool TakeDamage(int amount, Health2D attacker = null)
        {
            if (amount <= 0 || IsDead) return false;
            if (attacker && (ReferenceEquals(attacker, this) ||
                (faction != CombatFaction.Neutral && faction == attacker.Faction))) return false;

            int prior = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Damaged?.Invoke(this, prior - CurrentHealth);
            if (IsDead)
            {
                Died?.Invoke(this);
                if (disableOnDeath) gameObject.SetActive(false);
            }
            return true;
        }

        public void Heal(int amount)
        {
            if (amount > 0 && !IsDead)
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        public void RestoreFull() => CurrentHealth = maxHealth;

        /// <summary>Sets the maximum (e.g. from the hero class) and refills.</summary>
        public void SetMax(int value)
        {
            maxHealth = Mathf.Max(1, value);
            CurrentHealth = maxHealth;
        }
        /// <summary>Server-authoritative HP (farm maps): shows exactly what the server decided, firing the usual events.</summary>
        public void SetFromServer(int hp, int max)
        {
            maxHealth = Mathf.Max(1, max);
            int prior = CurrentHealth;
            CurrentHealth = Mathf.Clamp(hp, 0, maxHealth);
            if (CurrentHealth < prior) Damaged?.Invoke(this, prior - CurrentHealth);
            if (CurrentHealth <= 0 && prior > 0) Died?.Invoke(this);
        }

        private void OnValidate() => maxHealth = Mathf.Max(1, maxHealth);
    }
}

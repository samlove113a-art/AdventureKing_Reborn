using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Mana pool of the player hero (max by class, steady regeneration). Skills draw from it with TrySpend.
    /// Values are a balance PROPOSAL (Docs/SESSION_HANDOFF.md) until the skill system and server balance land.
    /// </summary>
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float max = 60f;
        [SerializeField, Min(0f)] private float regenPerSecond = 2f;
        public float Max => max;
        public float Current { get; private set; }

        public static float MaxFor(HeroClass c) => c switch
        {
            HeroClass.Mage => 120f, HeroClass.Archer => 70f, HeroClass.Assassin => 60f, _ => 45f,
        };

        public void Configure(float maxMana, float regen) { max = Mathf.Max(1f, maxMana); regenPerSecond = Mathf.Max(0f, regen); Current = max; }
        private void Awake() => Current = max;
        private void Update() => Current = Mathf.Min(max, Current + regenPerSecond * Time.deltaTime);

        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true;
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }
    }
}


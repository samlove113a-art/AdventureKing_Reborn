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

        /// <summary>Level-1 pool, mirroring server config/skills.json (the server's value always wins on farm maps).</summary>
        public static float MaxFor(HeroClass c) => c switch
        {
            HeroClass.Mage => 140f, HeroClass.Archer => 100f, HeroClass.Assassin => 90f, _ => 80f,
        };
        public static float RegenFor(HeroClass c) => c switch
        {
            HeroClass.Mage => 3f, HeroClass.Archer => 2.4f, HeroClass.Assassin => 2.2f, _ => 2f,
        };

        /// <summary>Server-authoritative MP (farm maps): shows exactly what the server decided; local regen only
        /// animates the bar between two server answers.</summary>
        public void SetFromServer(int mp, int maxMp)
        {
            if (maxMp <= 0) return;
            max = maxMp;
            Current = Mathf.Clamp(mp, 0, maxMp);
        }

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


using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    public enum HeroClass { Swordsman, Mage, Assassin, Archer }

    [Serializable]
    public struct HeroClassStats
    {
        public HeroClass heroClass;
        public string displayName;
        public string description;
        public int startingHealth;
        public int attackDamage;
        public float attackIntervalSeconds;
        public float attackRange;
        public int startingDefense;

        public float NominalDamagePerSecond =>
            attackIntervalSeconds > 0f ? attackDamage / attackIntervalSeconds : 0f;
    }

    /// <summary>
    /// One source of client-facing class previews. Online combat uses SERVER-authoritative
    /// values and validation; never trust local catalog numbers as server authority.
    /// </summary>
    public static class HeroClassCatalog
    {
        private static readonly HeroClassStats[] Classes =
        {
            new HeroClassStats
            {
                heroClass = HeroClass.Swordsman,
                displayName = "Kiếm Sĩ",
                description = "Cận chiến bền bỉ, đỡ đòn và phản công bằng kiếm.",
                startingHealth = 140,
                attackDamage = 18,
                attackIntervalSeconds = 0.85f,
                attackRange = 1.6f,
                startingDefense = 12
            },
            new HeroClassStats
            {
                heroClass = HeroClass.Mage,
                displayName = "Pháp Sư",
                description = "Pháp thuật tầm xa, sát thương bùng nổ, ít máu.",
                startingHealth = 75,
                attackDamage = 34,
                attackIntervalSeconds = 1.6f,
                attackRange = 6f,
                startingDefense = 3
            },
            new HeroClassStats
            {
                heroClass = HeroClass.Assassin,
                displayName = "Sát Thủ",
                description = "Song đao nhanh, liên kích và né đòn linh hoạt.",
                startingHealth = 90,
                attackDamage = 13,
                attackIntervalSeconds = 0.4f,
                attackRange = 1.8f,
                startingDefense = 5
            },
            new HeroClassStats
            {
                heroClass = HeroClass.Archer,
                displayName = "Cung Thủ",
                description = "Cung tên tầm xa, giữ khoảng cách và ngắm bắn.",
                startingHealth = 100,
                attackDamage = 22,
                attackIntervalSeconds = 0.9f,
                attackRange = 7f,
                startingDefense = 6
            }
        };

        public static int Count => Classes.Length;

        public static HeroClassStats At(int index)
        {
            if (index < 0 || index >= Classes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Classes[index];
        }

        public static HeroClassStats Get(HeroClass heroClass)
        {
            for (int i = 0; i < Classes.Length; i++)
                if (Classes[i].heroClass == heroClass)
                    return Classes[i];
            throw new ArgumentOutOfRangeException(nameof(heroClass));
        }
    }
}

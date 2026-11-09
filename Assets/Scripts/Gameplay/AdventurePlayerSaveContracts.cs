using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Serializable response models for server-owned character progression.
    /// Data supplied by the client must NEVER grant XP, cosmetics, premium
    /// currency, inventory, level or rewards without authoritative validation.
    /// </summary>
    [Serializable]
    public sealed class AdventureAccountProfile
    {
        public string accountId;
        public AdventureCharacterSnapshot[] characters = new AdventureCharacterSnapshot[0];
    }

    [Serializable]
    public sealed class AdventureCharacterSnapshot
    {
        public string characterId;
        public string characterName;
        public int slotIndex;   // 0..3, unique within an account
        public HeroClass heroClass;

        // Level progression: save both experience and the current skill allocation.
        public int level = 1;
        public long experience;
        public int unspentSkillPoints;
        public LearnedSkill[] skills = new LearnedSkill[0];

        // Real equipped cosmetics and ownership are server validated.
        public AdventureCharacterAppearance appearance = new AdventureCharacterAppearance();
        // Worn outfit as the server validated it (wardrobe slots + optional premium set).
        public OutfitLoadout outfit = new OutfitLoadout();
        public AdventureItemStack[] inventory = new AdventureItemStack[0];
        public AdventureEquippedItem[] equipment = new AdventureEquippedItem[0];
        public string[] ownedSkinIds = new string[0];
        public AdventurePetSnapshot[] ownedPets = new AdventurePetSnapshot[0];
        public string activePetId;

        // Wallet and paid transactions must be server-authoritative.
        public long gold;
        public long diamonds;

        // Quest / travel state.
        public AdventureQuestProgress[] quests = new AdventureQuestProgress[0];
        public string lastSafeCheckpointId;
        public string lastMapId;
        // Server-validated last position inside lastMapId (only meaningful when hasPosition).
        public float posX;
        public float posY;
        public bool hasPosition;

        // Server-saved new-player tutorial progress (never restarts once completed).
        public int tutorialStep;
        public bool tutorialCompleted;

        // Server-issued revision and timestamp for conflict resolution / auditing.
        public long serverRevision;
        public long lastSavedUnixMilliseconds;
    }

    [Serializable]
    public sealed class AdventureCharacterAppearance
    {
        public string faceId;
        public string hairId;
        public string hairColorId;
        public string skinToneId;
        public string equippedSkinId;
        public string outfitId;
        public string weaponVisualId;
        public string wingId;
        public string nameplateId;
    }

    /// <summary>One catalog row as the server sends it: names, prices and ownership are server data, never client data.</summary>
    [Serializable]
    public sealed class WardrobeItem
    {
        public string id, name, description, slot, kind, currency;
        public long price;
        public int[] classes = new int[0];
        public bool owned, equipped, usableByCharacter;
        public string rarity = "common"; // display only (common / rare / epic / legendary)
        public bool IsSet => kind == "cosmetic_set";
        public bool ForDiamonds => currency == "diamonds";
    }

    /// <summary>Server-confirmed wardrobe state of one character (after load, purchase or equip).</summary>
    [Serializable]
    public sealed class WardrobeSnapshot
    {
        public int catalogVersion;
        public string characterId;
        public int heroClass;
        public long gold, diamonds, serverRevision;
        public WardrobeItem[] items = new WardrobeItem[0];
        public string[] owned = new string[0];
        public OutfitLoadout outfit = new OutfitLoadout();
        public string purchaseId;
        public bool replayed;

        public bool Owns(string id) => Array.IndexOf(owned, id) >= 0;
        public WardrobeItem Find(string id) => Array.Find(items, i => i.id == id);
    }

    // ------------------------------------------------------------------ farm (server-authoritative monsters + Gold drops)
    [Serializable]
    public sealed class FarmMonsterState
    {
        public string id, type, name;
        public float x, y;
        public int hp, maxHp;
        public bool alive;
        public long respawnInMs;
    }

    [Serializable]
    public sealed class FarmDropState
    {
        public string id;
        public float x, y;
        public long amount, expiresInMs;
    }

    [Serializable]
    public sealed class FarmState
    {
        public string mapId;
        public FarmMonsterState[] monsters = new FarmMonsterState[0];
        public FarmDropState[] drops = new FarmDropState[0];
    }

    [Serializable]
    public sealed class FarmAttackResult
    {
        public FarmMonsterState monster;
        public int damage;
        public bool killed;
        public FarmDropState drop;
        public bool HasDrop => killed && drop != null && !string.IsNullOrEmpty(drop.id);
    }

    [Serializable]
    public sealed class FarmPickupResult
    {
        public string dropId;
        public long amount, gold;
    }

    [Serializable]
    public sealed class LearnedSkill
    {
        public string skillId;
        public int rank;
    }

    [Serializable]
    public sealed class AdventureItemStack
    {
        public string itemInstanceId;
        public string itemDefinitionId;
        public int quantity;
    }

    [Serializable]
    public sealed class AdventureEquippedItem
    {
        public string slotId;
        public string itemInstanceId;
        public string definitionId;
        public int upgradeLevel;
    }

    [Serializable]
    public sealed class AdventurePetSnapshot
    {
        public string petInstanceId;
        public string petDefinitionId;
        public int level;
        public int hunger;
        public string equippedSkinId;
    }

    [Serializable]
    public sealed class AdventureQuestProgress
    {
        public string questId;
        public int stepIndex;
        public bool completed;
    }

    /// <summary>
    /// Interface to a real secure account/character backend.
    /// All callbacks must return on Unity's main thread.
    ///
    /// All operations validate the currently authenticated account on SERVER;
    /// client-supplied account IDs or character ownership are never trusted.
    /// </summary>
    public interface IAdventureProfileService
    {
        void LoadProfile(Action<bool, AdventureAccountProfile, string> completed);

        // Atomically create in an empty slot, with unique character ID.
        void CreateCharacter(string displayName, HeroClass heroClass, int slotIndex,
            Action<bool, AdventureCharacterSnapshot, string> completed);

        // Re-authorize ownership and load authoritative snapshot on each entry.
        void EnterCharacter(string characterId,
            Action<bool, AdventureCharacterSnapshot, string> completed);
    }
}

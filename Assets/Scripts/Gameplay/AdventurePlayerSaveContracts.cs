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

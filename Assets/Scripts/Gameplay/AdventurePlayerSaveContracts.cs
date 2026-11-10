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

    /// <summary>The hero's HP on a farm map, as the server decided it (monster hits, regeneration, death).</summary>
    [Serializable]
    public sealed class HeroCombatState
    {
        public int hp, maxHp, damageTaken;
        public int mp, maxMp;
        public long[] cooldownMs = new long[3]; // skills 1..3, remaining (server clock)
        public bool dead;
        public string[] hitBy = new string[0];
    }

    // ---------------------------------------------------------------- skills (server-decided)
    [Serializable] public sealed class SkillDefDto { public int slot; public string id, name, shape; public float cooldownSeconds, range, radius, damageMultiplier; public int mpCost, maxTargets; }
    [Serializable] public sealed class ClassSkillsDto { public int heroClass; public string name; public int maxMp; public float mpRegenPerSecond; public SkillDefDto[] skills = new SkillDefDto[0]; }
    [Serializable] public sealed class SkillConfigDto { public float globalCooldownSeconds, mpCostPerLevel, maxMpPerLevel; public ClassSkillsDto[] classes = new ClassSkillsDto[0]; }
    [Serializable] public sealed class SkillHitState { public FarmMonsterState monster; public int damage; public bool killed; public FarmDropState drop; public bool HasDrop => killed && drop != null && !string.IsNullOrEmpty(drop.id); }
    [Serializable]
    public sealed class SkillCastResult
    {
        public int slot, mpSpent;
        public string skillId, name;
        public float cx, cy;
        public SkillHitState[] hits = new SkillHitState[0];
        public HeroCombatState hero;
    }

    // ---------------------------------------------------------------- inventory (server-owned bag)
    [Serializable]
    public sealed class BagSlotState
    {
        public int index, qty, attack, defense;
        public string itemId, name, kind, icon, desc, equipSlot;
        public bool usableByClass = true;
    }
    [Serializable] public sealed class EquippedState { public string equipSlot; public BagSlotState item; }
    [Serializable]
    public sealed class InventoryState
    {
        public int capacity, stackMax, attackBonus, defenseBonus;
        public BagSlotState[] slots = new BagSlotState[0];
        public EquippedState[] equipped = new EquippedState[0];
    }
    [Serializable] public sealed class UseItemResult { public string itemId; public int restoredHp, restoredMp; public HeroCombatState hero; public InventoryState inventory; }

    // ---------------------------------------------------------------- NPC shop + tickets
    [Serializable] public sealed class NpcShopItem { public string itemId, name, kind, icon, desc, destMap, destName; public long priceGold; public int minLevel; }
    [Serializable] public sealed class NpcShopTab { public string id, name; public NpcShopItem[] items = new NpcShopItem[0]; }
    [Serializable] public sealed class NpcShop { public string id, npc; public int maxPerPurchase = 99; public NpcShopTab[] tabs = new NpcShopTab[0]; }
    [Serializable] public sealed class BuyResult { public string itemId; public int qty; public long totalGold, gold; public bool replayed; public InventoryState inventory; }
    [Serializable] public sealed class TicketResult { public AdventureCharacterSnapshot character; public InventoryState inventory; }

    // ---------------------------------------------------------------- pets
    [Serializable] public sealed class PetDefState { public string id, name, tier, currency, desc; public long price; }
    [Serializable] public sealed class OwnedPetState { public string petId, name, tier; public bool summoned, hungry; public long satietyMs; }
    [Serializable]
    public sealed class PetsState
    {
        public PetDefState[] catalog = new PetDefState[0];
        public OwnedPetState[] owned = new OwnedPetState[0];
        public string summoned;
        public long maxSatietyMs, gold, diamonds;
        public float pickupReach;
    }

    // ---------------------------------------------------------------- events / welfare, mail, giftcode, rankings, titles
    [Serializable] public sealed class RewardStepState { public string key, text, reward; public int have, need; public bool claimable, claimed; }
    [Serializable] public sealed class WelfareTabState { public string id, title, banner, desc, period, lockedReason; public bool locked; public RewardStepState[] steps = new RewardStepState[0]; }
    [Serializable] public sealed class WelfareState { public bool rewardsEnabled, anyClaimable; public string status; public long gold; public WelfareTabState[] tabs = new WelfareTabState[0]; }
    [Serializable] public sealed class MailState { public string id, kind, title, body, attachText; public long attachGold, createdMs; public bool hasAttachment, read, claimed; }
    [Serializable] public sealed class MailboxState { public MailState[] mails = new MailState[0]; public int unread, unclaimed; public long gold; }
    [Serializable] public sealed class GiftcodeResult { public string code, reward; public long gold, balance; }
    [Serializable] public sealed class RankRowState { public int rank, heroClass, level; public string characterId, name; public long value; public bool me; }
    [Serializable] public sealed class RankBoardState { public string board, title, valueLabel, emptyReason; public RankRowState[] rows = new RankRowState[0]; public RankRowState mine; public int total; }
    [Serializable] public sealed class TitleState { public string id, category, name, source; public int level; }
    [Serializable] public sealed class TitlesState { public TitleState[] owned = new TitleState[0]; public string equipped, note; }

    // ---------------------------------------------------------------- quests
    [Serializable] public sealed class QuestObjectiveState { public string text; public int have, need; }
    [Serializable]
    public sealed class QuestState
    {
        public string id, type, title, desc, mapId, mapName, npc, rewardItems;
        public QuestObjectiveState[] objectives = new QuestObjectiveState[0];
        public long rewardExp, rewardGold;
        public bool complete, claimed;
        public int step;
    }
    [Serializable]
    public sealed class QuestLog
    {
        public QuestState[] main = new QuestState[0], side = new QuestState[0], daily = new QuestState[0];
        public int mainTotal, level;
        public long exp, expLevelStart, expNextLevel;
        public string dailyDay, turnInNpc;
    }
    [Serializable] public sealed class QuestClaimResult { public string questId; public long exp, gold; public int level; public bool leveledUp; public QuestLog log; }

    [Serializable]
    public sealed class FarmState
    {
        public string mapId;
        public FarmMonsterState[] monsters = new FarmMonsterState[0];
        public FarmDropState[] drops = new FarmDropState[0];
        public HeroCombatState hero;
    }

    [Serializable]
    public sealed class FarmAttackResult
    {
        public FarmMonsterState monster;
        public int damage;
        public bool killed;
        public FarmDropState drop;
        public HeroCombatState hero;
        public bool HasDrop => killed && drop != null && !string.IsNullOrEmpty(drop.id);
    }

    [Serializable]
    public sealed class FarmPickupResult
    {
        public string dropId;
        public long amount, gold;
        public HeroCombatState hero;
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

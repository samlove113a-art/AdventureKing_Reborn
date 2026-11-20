# Adventure King Reborn — Permanent online account + character saves

**Approved requirement 2026-11-07:** Each player MUST keep their own progress across logout, reinstall, phone changes and future game updates. This includes selected 3D chibi hero, name, level, EXP, skill upgrades, inventory, equipment, all purchased/unlocked skins, equipped costume appearance, pet state, currency, quests and safe checkpoint. Switching skins is a reversible equip action; ownership survives unequip and login. The four character slots belong to the same authenticated player account.

## Verified UI flow when a real backend exists
1. Branded logo → login/register (password, Google or Facebook with legitimate provider SDK and backend token validation).
2. Server authenticates and issues a short-lived session. On every sign-in, call **LoadProfile** and load 0–4 saved characters for that account. No new character should be made automatically.
3. The selection lobby shows each existing slot with actual **equipped 3D hero/skin preview, class, name, level and relevant gear**. A genuinely empty slot can offer Create Character; existing slot offers Enter Game (using EnterCharacter to check ownership and fetch latest snapshot).
4. Create and enter a new class only after server validates account, class, unique character name, empty slot and 4-slot limit. Reconnecting returns that same unique character.
5. Use the original 3D preview prefabs and apply **saved appearance/equipped skin IDs to actual meshes/materials**, not just display text or base class model.
6. On Enter Game, fetch authoritative XP, quest, equipment, map and safe checkpoint state from server; scene starts at an approved spawn/checkpoint. Gameplay action results are validated on server.

## Required server tables (recommended relational baseline)
- **accounts** (server account_id, registration, verification, status); **provider_identities** (provider, provider_subject, account_id) so Google/Facebook can link to the correct account without creating duplicates;
- **characters** (character_id, account_id, slot_index, class, name, level, xp, allocated points, safe_checkpoint, map, updated_at, revision). UNIQUE(account_id,slot_index) and unique character names as appropriate, CHECK slot_index BETWEEN 0 AND 3;
- **skills / quests / inventory_items / character_equipment / skins_owned / equipped_appearance / pets_owned** keyed by account/character and durable item instance ID where needed;
- **wallet_ledger** (append-only per-account transaction IDs, balance consistency, idempotency keys, purchase evidence) for Gold/Diamonds, rather than client-reported balance;
- **player_actions or journal** for recoverable progress updates, cloud save checkpoints, last-seen revision and audit history.

## Security and reliability acceptance
- **Server-authoritative**: client cannot set its own level, EXP, skins, pet ownership, money, damage or loot. The server validates rewards, consumes, equip ownership, skill points and upgrade requests.
- Character ID is a server-generated random/opaque identifier; server verifies it is owned by current authenticated account on every request; enumeration or account ID spoofing does not expose another player's data.
- Google/Facebook: do not fake login or store SDK client secrets/API keys in the game or repo; tokens validated with the providers and linked to exactly the intended account. Changing login methods must not silently create a second character account.
- Database: atomic transactions for equipment/purchases/progression, unique constraints on slot and item ownership, append-only currency ledger, conflict-safe revisioning for simultaneous sessions, rate limiting, audit trail and regular encrypted backups with restore tests.
- Autosave must occur on server-authoritative transitions (quest completion, XP gain, level-up, loot pickup, equip/unequip, cosmetics purchase, pet feed, boss reward, character creation) and at safe periodic checkpoints. Don't rely only on quit/logout: apps can crash and phone networks disconnect.
- New builds use versioned migrations and backwards-compatible schema changes. Restore correct hero/class, level, appearance and purchases after reinstalling the client.
- Release QA: create character; reach Lv.7; acquire and equip skin; collect pet and item; disconnect forcefully; log in on same and different device; confirm exact state; test another account cannot access or equip these; test simultaneous session and transaction retries. Include automatic database backup/restore drill.
- Do not store full permanent saves in PlayerPrefs/Unity client JSON/Google login alone. Local cache may optimize UI but **is never source of truth**.

## GitHub implementation now
- AdventurePlayerSaveContracts.cs defines serializable character/account models and secure server service interface.
- AdventureEntryFlow.cs now **loads server profile after sign-in**, displays up to four persisted slots, supports entering existing characters, and creating only in an empty slot. It **fails closed** if no real account/profile provider exists.
- AdventurePlayerSession.cs holds in-memory data for the gameplay scene, **not** a persisted save. It must be cleared on logout/disconnect.
- Skin preview component currently only shows class models; **applying owned/equipped skin to the 3D rig still requires authoring actual models, textures/materials and appearance mapping**.
- **Not done**: secure authenticated backend, production database, wallet ledger, real social OAuth, server persistence, real hero models, save/load integration tests. Do not claim player progression is safely stored until these exist and tests pass.

## Deployment cost
Owner is willing to pay for a server when needed. Do NOT buy/lease anything without a separate explicit server/vendor/budget approval. Develop with local or free test infrastructure and production-like database architecture first.

# Core Loop & Inventory UI — implementation handoff (2026-11-18)

## Scope and base
- Branch: `feat/mmorpg-coreloop-ui-20261118`, branched from `feat/chapter1-mushroom-kingdom` at `214a5442`.
- This is a scoped enhancement of existing `InventoryPanel`, `FarmField`, `FarmService` and `InventoryService`. It does **not** replace account, farm, network, world travel, cosmetics or multiplayer systems.
- Design reference: user's Học viện Ninja inventory screenshot; **do not reuse or reproduce copyrighted art, icons or layout pixel-for-pixel**.

## Implemented
1. **Inventory UI** (`Assets/Scripts/Gameplay/InventoryPanel.cs`): responsive two-column overlay. Left: actual 2D hero sprite and pedestal, existing **weapon/armor/accessory** slots; future hat/shoes/necklace/pet/transformation slots are *explicitly locked and visually labeled* until server-owned equip behavior exists. Right: existing server-backed bag, resized to six columns. Rarity edges use white, blue, purple, orange and red; red/orange outlines gently pulse while open.
2. **Combat power**: `FarmService.CombatPowerFromBonuses` reuses the established server formula; `InventoryDto.combatPower` delivered by the server and animated by the client. Power changes after equipping or upgrading: never computed from an unverified client action.
3. **Equipment enhancement** (+0 to +15): V14 SQLite schema stores level per *individual piece* in bag and equipped slots. `InventoryService.Enhance` executes one atomic transaction for materials, success/fail outcome, gear level and receipt. Anonymous per-attempt request IDs are idempotent, so network retries do not duplicate consumption. +1..+15 draft success rates: 100,95,90,85,80,70,65,55,45,40,35,30,25,20,15 (%). One `enhance_stone` per attempt. Optional `protection_charm` prevents decrease; failures below +6 keep level even without charm. Server RNG decides outcomes. Enhance odds are displayed before pressing; **these are balancing DRAFTS**.
4. **Loot, rarity and critical hits**: item rarity defined in `config/items.json`, sent with `DropDto` and bag snapshots. Stones and charms are real monster/boss drops via `config/monsters.json`. Purple/orange/red drops display an upright light column and chime in `GoldDropView`. Melee and skill critical hits roll server-side at draft 12% chance for 1.5× damage; client only displays verified results. Existing skill VFX, hit-stop and camera shake reused; small bonus shake on crit versus mini-boss or boss.
5. **Goal hints**: `QuickGoalToast` shows server-confirmed material availability, rare item drop and boss respawn (only during final 30s from reported server timer).

## Files
- Server: `Domain/FarmService.cs`, `Domain/InventoryService.cs`, `Persistence/Database.cs`, `Program.cs`, `config/items.json`, `config/monsters.json`.
- Unity: `InventoryPanel.cs`, `GoldDropView.cs`, `FarmField.cs`, `AdventurePlayerSaveContracts.cs`, `AdventureServerConnection.cs`, `QuickGoalToast.cs`.
- Regression: `Server/AdventureKing.Server.Tests/EnhancementTests.cs`, `DropInventoryTests.cs`, `BackupAndHttpTests.cs`.
- Continuous checks: `.github/workflows/coreloop-server-tests.yml` runs `dotnet test`.

## QA / limitations
- GitHub CI validates .NET server compilation and xUnit tests; **Unity C# compile, sprites, UI layout on PC/mobile, animations, actual device FPS and 2-client multiplayer must still be verified using Unity Editor/builds**. Do not report them as tested.
- **No merge to the main feature branch yet**. Review the draft PR first, inspect and test the Windows Editor and mobile landscape overlay. Use safe-area checks and confirm there is no screen overlap.
- The only functional equipment slots still are weapon, armor and accessory: the new additional visual slots are not clickable and are explicitly labeled as unavailable. Adding separate rings, necklaces, shoes, hats, pet and transformation equip logic requires new server+data+sprite art work.
- Existing outfits/pets continue through their existing systems. Paperdoll preview displays current `SpriteRenderer.sprite`; check whether active outfits/weapon layers appear in the preview and improve with render-to-texture/combined layer rendering only if needed, without recoding ownership.
- Item rarities and enhancement odds/drop chances are draft balance values, not user-approved. Hard cap +15 and downgrade rules should be approved before production.
- Do not re-run Meshy pipelines or overwrite previously approved art.

# AdventureKing_Reborn — VISUAL-ONLY 2D PIXEL ART REPLACEMENT

**Scope: GRAPHICS ONLY. No gameplay refactor.** Read this instruction in the existing local Unity project, working from branch `feat/mmorpg-coreloop-ui-20261118` or the current newer feature branch with all existing code. GitHub `main` currently only contains README, so never assume main has the project.

## Existing code verified
- `Assets/Scripts/Gameplay/HeroClassCatalog.cs`: Swordsman, Mage, Assassin, Archer.
- `Assets/Scripts/Gameplay/PlayerMotor2D.cs`: existing 2D movement and facing; do not modify.
- `Assets/Scripts/Gameplay/CharacterVisual3D.cs`: player visual bridge, animator sync; visual integration may require carefully scoped adapter, but do not alter network/gameplay behavior.
- `Assets/Scripts/Gameplay/HeroSelectionPreview3D.cs`: class preview uses 3D; replace **visual presentation only**, preserving class selection.
- `Assets/Scripts/Gameplay/SpriteFlipbook.cs`: existing sprite frame player for VFX.
- `Assets/Art/Characters/`: existing character visuals / controllers; DO NOT delete or overwrite until replacement has been verified.

## Art direction
Use the user-provided **blond warrior pixel-art sprite reference** as the canonical visual target for the entire game: high-detail 2D fantasy chibi, crisp discrete pixels, readable silhouette, detailed class weapons/armor, rich saturated colors, hand-pixel-style clustered shadows, no 3D, blur or smooth gradient. Keep all classes visually distinct. A previously generated gold-knight overview image is a **concept board only** (contains text/background, frames not validated) and must NOT be imported as ready-made animation PNG.

## Exact task for Claude
1. Inspect current character sprite/model references, renderers, prefabs, animator parameters, class previews, Sorting Layers and graphical VFX references **in the local project**. Keep every gameplay component and existing GUID relationship intact.
2. Create **real transparent PNG** sprites with consistent pixel scale, silhouette, colors and feet/pivot alignment; separate frame sequences for Idle, Run, Jump, Fall, Attack, Skill, Hit, Death and Climb (only for actions game already has). Draw/produce original graphics; no empty placeholders, label panels, checkerboard backgrounds, duplicated pseudo-frames, or cutting off weapons.
3. Set import options appropriately: Sprite (2D and UI), Point (no filter), appropriate PPU, no lossy compression; slice frames using verified bounds; create AnimationClips/Animator visual states as needed.
4. Replace only **rendered visual assets** (SpriteRenderers, animation sprite tracks, class preview images or visual child prefab). If existing 3D visual components must be disabled or adapted, modify only the view layer while preserving animation signals and all existing nonvisual systems.
5. Keep existing physics, player prefab root, input mapping, attack hitboxes, damage, skills, VFX logic, item stats, currency, character selection, save data, UI functionality, server/network synchronization, and multiplayer behavior unchanged. Do NOT regenerate maps, rewrite gameplay systems, swap whole scenes, or delete existing scripts/FBX files.
6. Test in Unity Play Mode: graphics display correctly in lobby/selection/gameplay; animated sprites stay grounded/aligned; attacks/skills retain existing timing and hit logic; no Missing Script/Reference or compile errors; PC and mobile sizing. If graphics are not ready, report blockers instead of claiming complete.
7. Before modifications, inspect `git status` and preserve uncommitted changes. Make changes on a dedicated branch, review diffs, do not automatically merge. Do not spend Meshy credits without asking the user first.

## Reporting format
**Visuals replaced | exact assets/prefabs modified | Play Mode results | remaining visual issues.** Do not mark pass without running checks.
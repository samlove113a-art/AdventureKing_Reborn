# Unity engineering brief — 3D graphics on a 2D gameplay plane

## Preserve working foundation
Claude's QA commit on `feat/foundation-unity2d-20261107` tested a 2D physics/input/combat core. Leave the proven `PlayerInput2D`, `PlayerMotor2D`, `Health2D`, `PlayerMelee2D`, `EnemyChase2D` and UGUI touch input in place for the first migration. The old greybox scene is **internal QA only**.

## Visual layer separation
- Player GameObject: Rigidbody2D + Collider2D + player movement/control/combat scripts, XY gameplay position, Z=0.
- Child VisualRoot3D: model, rig, Animator, material + hit/death/attack visual state; **no Rigidbody3D required**.
- CharacterVisual3D (optional bridge component): map 2D velocity, grounded/climbing, facing, health and attack events to a 3D model rig and Animator.
- Physics-ground layer and ladder triggers remain Collider2D; build actual multi-level gameplay platforms. Keep decorative 3D architecture separate from 2D colliders.
- Orthographic main Camera points at side-view stage. Validate appearance of rigged meshes/materials and shadows using an appropriate renderer (URP Universal/Forward renderer if needed); a Sprite-only 2D Renderer may not correctly handle 3D lit models.
- Never mirror rigged humanoids with a negative root scale; rotate the visual root 180° around Y for left/right facing. Establish art pipeline orientation in the real scene.

## Required animator parameters for bridge component
- Float `Speed`: absolute horizontal movement speed
- Float `VerticalSpeed`: 2D Y speed
- Bool `Grounded`
- Bool `Climbing`
- Bool `Dead`
- Trigger `Attack`
- Trigger `Hit`
The animator controller, avatar and animation clips are production assets that **do not exist yet**; author them explicitly, then test transitions. Do not claim the bridge makes those animations automatically.

## Practical art production sequence
1. Produce ONE original chibi 3D hero in Meshy/Blender, with clean separate fingers and riggable face.
2. Rig / skin / animate and make Unity import and Animator controller actually work.
3. Integrate into the EXISTING physics and controls. Verify running, landing, ladder climb, attack windup, weapon grip, enemy hit and camera at real FPS.
4. Replace the current greybox environment with a coherent 2.5D village section. Validate scale, collisions, physics and FPS; capture a true Unity video.
5. Expand to elder NPC, monster, boss, pet, cosmetic equipment, UI and online backend only once quality is established.

## Known validation limits
This direction and optional code are **not** proof of a commercial 2.5D build. GitHub edits have not been compiled in the owner’s Unity editor by ChatGPT. Claude must run and document the migration tests and confirm editor/scene results. No online purchase/server work is necessary yet.

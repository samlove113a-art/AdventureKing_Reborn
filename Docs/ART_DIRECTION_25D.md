# ADVENTURE KING REBORN — PURE 2D CHIBI ART DIRECTION
**2026-11-18 (owner): direction changed to 100% PURE 2D. This supersedes the 2026-11-07 "chibi 3D / 2.5D" direction below** (kept as history; the 3D models stay in git/Resources only as a fallback and are never shipped as the look).

## Current direction (binding)
- **Everything is pure 2D: sprite / 2D-bone characters, NPCs, monsters, bosses, pets, tilesets, items, UI.** No 3D, no fake 3D (no sprites baked from 3D models, no 3/4 views), no placeholder, no blurry AI-looking art.
- **2026-11-18 (owner, after reference sheet): characters are detailed PIXEL-ART with HEROIC proportions (head ≈ 1/5–1/6 of height, long legs) — replaces "super-chibi 1:1" for characters.** Reference = layout/quality bar only (idle, run, crouch, hit, KO, attack, skill, strong slash VFX); never copy that game's characters/sprites — every hero is an original design. Monsters/NPC/pets/tiles must be redrawn to the same pixel density and scale so the whole game matches (still to do).
- Style: classic bright side-scroller look (own designs, no clone), hard dark outline, saturated colours, light from above, flat shading in pixel clusters. No blur / soft gradient / 3D effects. (The older "super-chibi 1:1" wording elsewhere in this file is superseded for characters.)
- Map: side-scrolling, 4 parallax layers (Far BG → Midground → Gameplay Tileset 100 PPU → Foreground), platforms, ropes/ladders, portals, 2D colliders.
- Animation per character: Idle / Run / Jump / Attack / Hit / Death + class-specific states.
- Global HUD: Screen-Space Canvas, screen-anchored, responsive PC/Mobile, never tied to world coordinates.
- Status in repo (2026-11-18): the 2D body system exists (`HeroVisualStyle` default "2d", `Resources/Chibi2D/*_Visual2D`, `Chibi2DArt`), but those sprites were **baked/pixelated from Meshy 3D models** → they do NOT meet "no fake 3D / sharp outline" and are not owner-approved. They must be replaced by true hand-built 2D art (see `CHIBI2D_PLAN.md`). One sample (Swordsman) first → review → then the rest.

## (History) 2026-11-07 direction — superseded
Approved by the owner on 2026-11-07. Supersedes previous direction requiring 2D sprites.

## Non-negotiable visual direction (2026-11-07, superseded)
- **Characters / NPCs / monsters / bosses / pets: original, rigged chibi 3D meshes.**
- **Gameplay: side-scrolling 2.5D.** Player position and collisions remain on an X/Y plane; depth in Z is used for staged environment and presentation, not uncontrolled free-roaming movement.
- Orthographic fixed side-view camera, with deliberately staged 3D geometry, lighting and parallax.
- Genre inspiration: the joy and fantasy of classic MapleStory, **not a remake or asset clone**. Do not copy character designs, silhouettes, UI, monsters, skills, map layouts, names, images, audio, code or ripped data. Create Adventure King's own recognizable design language.
- Target Windows and phones. Optimize mesh topology, texture sets, shaders, animation/rig count and draw calls for mobile.

## Character identity and animation
- Chibi: expressive large head, compact body, readable facial expression and distinctive original hair, eye, costume and weapon silhouettes. Choose a consistent style across the roster.
- Facial performance must show personality; no printed-on facial expressions masquerading as rigged movement.
- Required animation states for the first hero: Idle, Run, Jump, Fall, Climb, Attack, Hit, Death; transition timing, foot plant and weapon grip are reviewed in-engine.
- Equipment and weapon attach to authored bone sockets. No decorative items pasted into the rendered background.
- First completed hero must be evaluated in motion against the final village lighting, not approved using a static poster or one-off render.

## Environmental identity
- Original beginner village: **Làng Chuông Gió**. Curved-roof timber cottages, hanging wind chimes, luminous seed lamps, raised bridges, mossy stone terraces and whimsical wind-powered mechanisms. No replica of familiar MapleStory mushroom-town composition.
- Include true multi-level traversable platforms, ladders, portals, NPC triggers, water/mist particles and spatial depth, with gameplay/physics grounded on the plane.
- Maintain clear foreground/background separation so the hero remains readable on mobile screens.

## Render style lock (owner request 2026-11-18 — applies to ALL assets)
Classic bright side-scroller feel, own designs (still no clone, see above). **Everything is drawn 2D** (characters included, per the 2026-11-18 change); the rules below are met by the drawing itself.
1. **Outline:** hard-edged dark outline (dark brown `#2B1A10` / near-black), 2–3 px at 1080p, on characters, monsters, interactive objects and the edge of the walkable tileset — separates them from the background.
2. **Colour & light:** vibrant, saturated palette; one fixed light from straight above; cel-shading in clear flat bands (2 tones + 1 crisp highlight). No blur, no soft 3D shadow, no gradient wash on gameplay layers.
3. **Map = 4 parallax layers** (`ParallaxBackground4`):
   - L1 Foreground — grass, flowers, rocks at the screen edge (factor ≈ 1.25, in front of the player, must not hide the hero).
   - L2 Playable tileset — ground, stairs, ladders/vines, jump platforms; **100 Pixels Per Unit**, 100×100 px grid (factor 1, the world itself).
   - L3 Midground — houses, statues, ancient trees; ~10–15 % lighter / lower contrast than L2 (factor ≈ 0.45).
   - L4 Far background — sky, clouds, distant mountains, slow drift (factors 0 / 0.15).
4. **Scale:** every asset is sized against the in-game super-chibi hero (head:body ≈ 1:1) — measure the hero's height in Unity units first, then size doors, steps and platforms from it (door ≈ 1.2× hero height).
Prompts for generating 2D tiles/props: `Docs/ART_PROMPTS_2D_TILESET.md` (DRAFT).

## Meshy permission and limitations
The owner explicitly authorizes use of **already available Meshy HQ subscription/credits** without individual per-asset approval. Use Meshy when it improves original model/texture quality, then use Blender for retopology, UV, geometry cleanup, rigging, blendshapes, skinning, animation, sockets and optimization. Each asset needs a traceable source, commercial-use rights verification and a real Unity view/animation check.

**No new purchase, subscription renewal, server lease or credit top-up is authorized.** Never expose or commit API keys or payment credentials. If credit/tools run out, report the specific blocker.

## Acceptance gate
1. One coherent final-quality 3D hero, rig and expressive animation in Unity.
2. Final-quality 3D elder NPC, one original enemy, one small pet and their motion.
3. A genuinely 2.5D multi-level village with orthographic camera, working gameplay physics and cinematic-but-readable art.
4. Character moves smoothly without unwanted oscillation, foot sliding or broken hands.
5. Windows and Android real-device testing with FPS/memory measured. iOS claims require Apple test pipeline.
6. **Do not send owner greybox QA scenes or an unfinished build.** Internal tests and status logs remain welcome; final owner-facing delivery requires commercial quality with verified evidence.

## Scope discipline
Do not regenerate and discard the proven C# gameplay core solely to change its presentation. First produce and verify the best hero and one village area, then scale. Preserve old projects and stable source via Git; no deletion without backup and rollback.

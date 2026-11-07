# ADVENTURE KING REBORN — CHIBI 3D / 2.5D ART DIRECTION
Approved by the owner on 2026-11-07. Supersedes previous direction requiring 2D sprites.

## Non-negotiable visual direction
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

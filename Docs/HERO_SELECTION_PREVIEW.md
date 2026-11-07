# Hero selection visual quality bar — approved 2026-11-07

## The experience the player must actually see
After the official Adventure King splash and authenticated login/register, open a high-quality fantasy-class hall **with an actual animated chibi 3D hero in the center**, not a flat picture or a fake 2D render. Four distinct class cards are interactive: Kiếm Sĩ, Pháp Sư, Sát Thủ, Cung Thủ.

The selected hero is visually prominent, occupying the central focal area, standing on a themed 3D platform in a distinctive scene with atmospheric depth, controlled lighting and readable original UI. Switch to another class with a short polished transition. The hero loops a genuine Idle animation; the player can **drag left/right to rotate** and inspect costume, weapon, face and silhouette. Show **Try Attack**, **Try Skill** and **Reset View** interactions. Previews show real attack/skill motions and distinct VFX when finished, not static images or canned unrelated clips. Each class has distinctive timing, poses and weapons, with per-class stat panel showing HP, damage, defense, range/attack interval or role.

## Balance identity (initial draft; server owns authority later)
- Kiếm Sĩ: sword, durable melee defense, heavier hit timing.
- Pháp Sư: staff/catalyst, impactful ranged magic, lower HP.
- Sát Thủ: twin short blades, fast chained melee attacks.
- Cung Thủ: bow, ranged arrow attack and nimble positioning.
Use HeroClassCatalog as the provisional client-side definition, but do not equate damage per hit to actual DPS. Final balance requires animation, hitboxes, cooldowns and real enemy tests.

## Originality
Reference MapleStory only for a classic side-scrolling fantasy game's fun, character readability and delight of class preview. No copies of character designs, map layouts, UIs, logos, monsters, skills, animation or music. Art direction follows Docs/ART_DIRECTION_25D.md.

## Unity setup for Claude Code (real art integration required)
1. Build a 16:9 responsive Canvas safe-area-friendly for Windows and landscape mobile (verify different aspect ratios). Layout idea: 4 distinct class cards at side/bottom, dominant model preview at center, class stats/details in a right pane, Confirm / Enter Game available only after genuine authentication and character validity.
2. Make a dedicated **HeroPreview** camera, orthographic or low-distortion perspective, pointed at a separate lit platform in a preview stage. Create a RenderTexture with transparent/appropriate color and assign it to camera and RawImage. Hide the Preview layer from the actual gameplay camera. Assign **previewLayer = 29** and configure Tags and Layers accordingly. Ensure the preview camera shows ONLY the preview layer.
3. Place HeroSelectionPreview3D on the RawImage (or UI object receiving pointer drag) and wire modelMount, camera, renderTexture, rawImage, previewMessage. Enable **Raycast Target** on the RawImage and EventSystem in scene.
4. Create four **different visual-only model prefabs** with original 3D rig, materials, face, weapons and actual **Animator** clips. Avoid player Rigidbody2D/colliders/Health/AI/scripts on preview prefabs; they are display assets, not playable entities. Preview assets must fit a consistent preview camera framing and relative scale.
5. Fill 4 entries in HeroSelectionPreview3D (one per HeroClass). Each Animator needs Idle, Attack, Skill states named exactly as inspector fields. **Attack/Skill states must transition back to Idle on clip completion**, or connect PlayIdlePreview to an animation event/controller transition. Audio/VFX must be authored, intentionally timed and preview-only.
6. Wire AdventureEntryFlow.heroPreview to the preview component. Its SelectSwordsman, SelectMage, SelectAssassin, SelectArcher handlers already choose both stats and 3D model. Wire Preview UI buttons to PlayAttackPreview, PlaySkillPreview, ResetRotation.
7. Create character naming / slot selection / create-or-select workflow backed by a real account service. Do not bypass server auth or fabricate Google/Facebook login. Verify 4 distinct player slots and save after reconnect.
8. Run Unity compile and Play Mode tests. Record working preview video (all 4 heroes, rotation and animation) as internal QA; do not deliver incomplete visuals to owner.

## Release acceptance — no shortcut
- All 4 **original 3D** hero models actually exist, visibly distinct and rigged; real Idle / Attack / Skill animation works for each.
- Clicking a class switches live 3D hero, animations, stats, skill description and hero name **without freezing or flickering**.
- Drag rotation works with mouse AND phone touch; no stretched limbs, upside-down rigs, broken IK, unstable costume pieces or pixelated preview.
- Preview animation, UI, camera and VFX consistently feel like a polished commercial RPG.
- Confirmed character slot and account data persist server-side. No fake social login.
- No public or owner-facing "finished" demo until the complete acceptance criteria are actually evidenced by Unity/device tests.

## Current truth
The current GitHub implementation is a **preview UI code hook only**. It has not generated actual rigged characters, scenes, prefabs, animations or VFX, and has not been compiled in Unity by ChatGPT. Claude Code on the PC must integrate and validate. No claim of final quality is authorized by the repository update.

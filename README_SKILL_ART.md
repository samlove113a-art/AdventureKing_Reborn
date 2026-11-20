# ADVENTURE KING REBORN — Skill HUD reference package

This is a **visual art reference**, not a production-ready UI or installed Unity functionality.

- `ArtSource/OwnerProvided/UIReferences/Skill_HUD_Approved_Concept.png` is an ImageGen concept of the desired visual quality and arrangement.
- `Assets/Art/UI/SkillButtons/ExtractedConcept/` has isolated circular crops for visual comparison **only**. Some of these still have cooldown numerals or captions baked into the art, so do NOT use them directly in final gameplay.
- For final Unity buttons, create unique **clean, text-free, transparent, 256×256+ PNG** icons for 3 skills per class (Swordsman/Mage/Archer/Assassin), plus basic attack and utility buttons. Use royalty-clear original visuals.
- Cooldown overlays 5/15/20 sec, MP costs, auto status, and tap-feedback must be dynamic Unity UI overlays and actual server-validated gameplay. No text or countdown baked into sprite files.
- Follow photo reference for gold-ringed fantasy buttons in a crescent around the large attack button, use safe-area responsive anchors on PC/mobile.
- Existing game has custom skill presentation and UI builder: study existing code first and DO NOT duplicate skill server logic or replace existing art blindly.

**WARNING**: merely extracting concept icons is NOT the same as inserting working buttons into your Unity scene. ChatGPT cannot access your local `C:\AdventureKing_Reborn` folder; Claude Code should import or redraw the source icons and wire the real buttons, then provide Play Mode screenshots and interaction tests.

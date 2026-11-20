# Adventure King Reborn — First-time player flow and four chibi 3D heroes

Owner-approved 2026-11-07. This **extends** the chibi 3D / 2.5D pivot. Previous Unity 2D collision/gameplay scripts remain in use. This is a functional architecture, NOT completed commercial artwork or an authenticated online game.

## Player-facing sequence
1. **Splash/branding:** display the user-approved Adventure King logo/wordmark. Logo graphic must be supplied as its original source asset, not guessed/replaced with unauthorized art. Display branded splash then transition to authentication.
2. **Authentication:** login / register with username or email + password; additionally present clearly labeled **Continue with Google** and **Continue with Facebook**. These are real authentication actions, not fake local "success" buttons. Google/Facebook tokens must be validated by trusted backend, not by client, before a session is issued.
3. **Lobby / character selection:** after valid server session, display exactly **four distinct chibi 3D class previews**:
   - **Kiếm Sĩ (Swordsman):** resilient close-range sword fighter. Starting HP 140; skill damage 18; attack interval 0.85s; defense 12.
   - **Pháp Sư (Mage):** ranged magic bursts; low HP. Starting HP 75; skill damage 34; attack interval 1.60s; defense 3.
   - **Sát Thủ (Assassin):** quick dual-blade combo; high attack frequency. Starting HP 90; skill damage 13; attack interval 0.40s; defense 5.
   - **Cung Thủ (Archer):** ranged bow attacks and accurate aim. Starting HP 100; skill damage 22; attack interval 0.90s; defense 6.
   These are **provisional local balancing values**, not proof of live server balance. Distinct roles differ in damage-per-hit, attack interval, HP, range, skill VFX and silhouette. Do not imply that the biggest single hit is the highest DPS.
4. After player selects a class, enter a character name; server validates name/class/slots and creates or selects persisted character. Then show **Enter Game**. Gameplay scene receives server-authorized character data. Prevent game entry while the backend is unconfigured or unauthenticated.
5. Re-login restores all existing character slots and progression. Do not create a new character every time. User retains up to four slots, one per distinct hero/class if desired; class selection UI is separate from persistent character slot view.

## Implementation split
- Unity front-end coordinator controls splash, authentication UI and hero selection UI, never performs password hashing/token validation.
- Interface IAdventureAccountService is an injection point for a trusted backend. Default behavior is NOT CONFIGURED and must **fail closed**. Google/Facebook sign-in must obtain SDK/provider authorization response using platform-valid OAuth/OIDC flow; server validates it. No shipping client secrets in Unity assets or repository.
- Character class data lives in an explicit catalog for UI, balancing and server schema design. Server enforces actual combat and progression.
- Android/iOS providers may require developer app IDs, verified redirect URIs, SDK integration and privacy/compliance configuration; **not implemented yet**.
- For **internal** scene QA only, development test controls may be used behind UNITY_EDITOR and never compiled into release builds.

## Visual acceptance
The four original class heroes must be rendered as independent 3D models/rigs with facial expressions and genuine in-game animations (Idle, Run, Jump, Attack, Skill, Hit, Death; Climb where appropriate). Full 2.5D village stage has layered depth and a fixed lateral camera. Must feel cute, expressive and fantasy-rich without copying MapleStory assets/designs, UI, names, icons or sounds. Use Meshy HQ + Blender when helpful; do not buy credits without permission.

## Unity handoff tasks
- Build Splash, Login/Register and CharacterSelection scenes or Canvas roots; use a responsive safe-area-conscious layout.
- Wire actual logo supplied by owner, registration and provider buttons, 3D hero preview cameras and animator controllers.
- Backend connectivity MUST be real before social login, character creation or persistent play can be called complete.
- Build internal smoke tests for scene transitions / class stats / input validation / failure messages. Test Windows and phone scaling once devices are supported.
- Do not present this branch as a playable login system, finished character previews or commercial build.

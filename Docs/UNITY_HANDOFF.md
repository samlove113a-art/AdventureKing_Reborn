# Adventure King Reborn — Unity integration & acceptance

## Editor setup (Claude Code)
1. Clone this repository to a NEW directory; open as a Unity project with editor 6000.6.7f1.
2. Import packages, let Unity generate .meta and any required project settings. Never delete/change files in the prior projects. Commit new .meta files with corresponding Assets.
3. Configure Input Handling to use legacy Input Manager or install/enable the modern Input System. PlayerInput2D has compile-time branches for both. Mobile touch control uses UGUI.
4. Create layer named Ground (suggest index 8). Platforms must use Ground. Set PlayerMotor2D GroundMask only to Ground, not the player's layer.
5. Add Player object with SpriteRenderer, Rigidbody2D (Dynamic, Freeze Rotation Z), CapsuleCollider2D, Health2D faction Player, PlayerInput2D, PlayerMotor2D, PlayerMelee2D. Set GroundProbe child just below feet and wire fields.
6. Add a camera with CameraFollow2D pointing at player. Add LadderZone2D to trigger Collider2D. Add EnemyChase2D, Rigidbody2D, collider, Health2D faction Enemy, and target player. For NPC, use trigger Collider2D and NpcInteraction2D with a real UI handler in onInteracted.
7. For mobile UI, create Canvas + EventSystem, buttons for left, right, jump, attack, up, down, interact, and add TouchButton2D with correct enum action/PlayerInput target.
8. Design final mushroom village with distinct collision geometry and original sprite animation (Idle, Run, Jump, Climb, Attack). The code here does NOT supply pixel art, prefabs or scenes. Final assets must be independently authored/licensed.

## Internal editor smoke tests — NOT A PUBLIC DEMO
- Clean C# compile, zero Console errors.
- Keyboard and touch left/right movement with no stuck keys.
- Jump and land on elevated platforms; jump cutoff, buffered jump and coyote timing.
- Ladder climbs up and down; exiting restores gravity.
- Attack an enemy: damage once per swing and not to player; cooldown works.
- Enemy pursues within detect range, hits player within contact distance and stops on death.
- E/touch Interact fires the NPC UnityEvent once per press.
- Camera follow is visually smooth; compare in-game at real target frame rate.
- Build and test on Windows and Android before release claims. iOS also requires an appropriate Apple build environment.
- Record Unity editor version, test date, device, FPS, warnings, known defects in a QA report.

## Architecture / online safety
These are LOCAL game-mechanics foundations, not server-authoritative combat. Actual online login, inventory, Gold, Diamonds, transactions, damage, drops, character state and trade must be validated on a backend; never trust client-reported rewards. Do not buy or rent servers yet.

## Quality / delivery rule
Owner does not want unfinished demos or placeholder art submitted as the game. Keep QA test scenes internal. No assertion of commercial quality before genuinely tested builds, original final art and acceptance by owner. Production priorities: polished character animation and a single multi-layer mushroom-village vertical slice, then multiplayer and content expansion.

## Verified status
Repository commits only; no Unity compile, Play Mode, build, FPS test or visual QA has been performed in this ChatGPT environment.

2026-11-07 (Claude Code, Windows 10 PC, Unity 6000.6.7f1): clean compile; 4/4 EditMode and 19/19
project PlayMode tests pass; internal Windows QA probe builds and runs at avg 59.5 FPS (target 60).
Two defects fixed (stale Interact press, climbing state persisting on the floor). Android/iOS and
human visual QA NOT yet done. Full details: Docs/QA/QA_REPORT_2026-11-07.md.

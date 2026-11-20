# Adventure King Reborn — Music, sound design and readable skills

Owner's release requirement (2026-11-07): **the game must have background music and convincing, distinct, CLEAR skill sound + VFX**. Silence, generic repeated sound, visually indistinguishable skill spam, and unlicensed game audio are unacceptable.

## Music by scene
- Branded splash: short original sting (only when legally owned).
- Login/register: gentle fantasy theme, soft and welcoming.
- Hero selection lobby: memorable uplifting orchestral/fantasy loop, no abrupt clip changes.
- Village: cheerful village BGM + wind, chimes, foliage and water ambience.
- Exploration: restrained adventure loop that leaves space for combat audio.
- Boss: energetic musical transition tied to boss engagement, smoothly return on exit.
- Both PC and mobile: clean looping, transition fades, streamed/compressed appropriately; never clip/distort. Separate BGM/ambience/SFX volume controls with mute button and saved preferences.

## Each of the four classes must have its OWN skill identity
| Class | Cast animation / VFX | Critical audible cues |
| --- | --- | --- |
| Kiếm Sĩ | Sword arc, clear red/gold hit shape, strong impact frame, controlled trails | metal draw/air swing + heavy steel impact |
| Pháp Sư | Cast anticipation, distinct elemental glyph and projectile/explosion | magical charge + release + elemental hit |
| Sát Thủ | Crisp dash and twin-blade combo, sharp readable hits | agile blade swishes + clean double impact |
| Cung Thủ | Bow draw, actual released arrow, clear projectile trail/hit | bow tension + string snap + arrow impact |

Art director must ensure these are ORIGINAL signature effects, **not** copied from MapleStory. Timing includes anticipation, cast/attack release, hit confirmation, cooldown; hit SFX and damage numbers sync to actual authoritative gameplay events, not static trailers. Skill gameplay should be understandable even if music is muted, with readable VFX silhouette and timing. Respect accessibility: never depend solely on color or sound.

## Mix and runtime
- Music, environment ambience and combat SFX are distinct channels.
- Combat skills/hits should remain audible without forcing the BGM too low. Use headroom, sensible limiting, voice budget and priority. Prevent multiple overlapping hits from saturating audio, especially for assassin combos.
- Optional dynamic ducking and spatialization can be introduced later based on verified performance. No autoplay of unrelated external media.
- Real Unity animation event at contact frame triggers impact cue and VFX, preferably with proper skill event interfaces and server-validated combat; no fake hit audio before impact.
- All clips must be ORIGINAL or licensed for commercial distribution; track source, license, author, revision and usage scope in asset manifest. **Never rip tracks or SFX from MapleStory.**
- No low-value placeholder MP3 or silent blank clip should be called complete; placeholders may exist only in private internal QA, never a release build.

## Code added to GitHub
- AdventureAudioDirector.cs: persistent non-destructive three-channel mix (BGM, ambience, SFX), stage track switching with fade, four class sound sets, separate sliders/mute, local volume settings.
- HeroSelectionPreview3D.cs: attack/skill test buttons call unique class audio cue only when a real Animator state is available. True 3D models and sound files must be authored and assigned.
- Actual **VFX prefabs, sounds, animator impact events, scene composition and audio QA have not been produced/tested by ChatGPT**. Claude must create and integrate them in Unity and do a real listening/visual QA. This code alone is not a completed audio feature.

## QA gate before an owner-facing build
1. Test original BGM across splash/login/selection/village/field/boss including crossfades, looping, interruptions and pausing.
2. All four chibi 3D hero previews have attack and skill animation with **recognizably different synchronized audio/VFX**.
3. Real battle showcases clear cast/windup, hitboxes, damage number, impact flash, target reaction, death animation with correct audio timing.
4. Test adjustable BGM/ambience/SFX, mute, reopen settings, restart and confirm volume preferences; verify that mute does not affect gameplay state.
5. Listen with headphones and phone speakers; check no distortion, repeated SFX overload, cut-off loops or silent scenes; document licenses/ownership.
6. Record actual captured Unity/gameplay video for internal evaluation. Do NOT send unfinished demos, music-only loops or empty previews to the owner.

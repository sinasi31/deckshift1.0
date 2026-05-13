# Deckshift — Claude Code Context

This file is loaded automatically into Claude Code at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Movement actions (jumping, dashing, special movements) consume **Shift**, a non-regenerating resource per-room. Attacks and most actions are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. ~5 hand-crafted levels, ~10 cards in the game. Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete.

**Active scene:** `SampleScene.unity` (build index 2). Other scene files exist (`GameScene`, `MasterLevel`, `Hub`) but are inactive/legacy. When debugging "is this in the scene?" issues, always check SampleScene first.

---

## Critical Architecture Rules

These are absolute. Do not suggest alternatives without explicit user approval.

1. **No Cinemachine.** It was removed early due to confiner issues with multi-shape rooms. The custom system is in `CameraFollow.cs` plus per-level `CameraBounds` zones. The only surviving Cinemachine reference is in `CameraPeek.cs`, which is flagged for refactor.

2. **Manager-singleton pattern.** All major systems are singleton MonoBehaviour managers (GameManager, DeckManager, LevelManager, etc.). This pattern has known issues (cyclic dependencies, flat global state) but is the architecture. Do not propose dependency injection, ECS, or other paradigms.

3. **Game runs in a single scene currently.** Most managers do not have `DontDestroyOnLoad`. If scene transitions are added later, this must be revisited. Do not add `DontDestroyOnLoad` to existing managers without discussing the implications.

4. **Comment language convention.** Older code has Turkish comments (Gemini-era). Going forward, **new comments should be in English** for clarity. Do not retranslate existing Turkish comments unless they're factually misleading.

5. **No assets the user can't afford.** Solo developer with limited budget, no freelance artist. Work with existing Cainos asset packs and pixel-art conventions. Don't propose solutions that require commissioning new art.

---

## Player System

### PlayerController.cs

This is a large script (~1,200 lines). It currently handles movement, jumping, card action execution, gravity reversal, VFX spawning, audio, health, gold, shift, knockback, portal state, cannon enter/exit, death, and respawn.

**Known issue:** It is a God Object and is scheduled for refactor. The `ExecuteAction()` method (~100 lines, switch over `CardActionType`) will be extracted to a separate `CardActionExecutor` component. **When adding new cards, add them to the existing switch, but be aware this is temporary.**

### Player Prefab Specifics

- **Physics collider:** `CapsuleCollider2D` (size 1.085 × 2.282, offset (0.229, 1.161)). A `BoxCollider2D` was previously present but disabled and has been removed. Do not re-add it.
- **Rigidbody2D:** Dynamic. Gravity scale flips sign during gravity reversal — do NOT modify `Physics2D.gravity` globally.
- **Visual model child:** `PF Skeleton - Mage`. Assigned to `PlayerController.visualModel` field. Uses `SkinnedMeshRenderer`. Cainos shader may not expose `_Color` for damage flash — defensive null/property guards are in place.

### Ground / Wall / Ceiling Detection

The player has three check Transforms, all parented to the player root (NOT to visualModel):

- **`groundCheck`** at local (0, -0.803, 0). Used for normal grounded detection via `Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)`.
- **`wallCheck`** for horizontal collision detection.
- **`ceilingCheck`** at approximately local (0, 2.30, 0) — added during gravity reversal work. Used when `isGravityReversed` is true.

`IsGroundedCheck()` switches probe based on `isGravityReversed`. The original implementation used a mirror-math formula (`2 * pivot - groundCheck.position`) but that was fragile (only 0.16 units of overlap margin); the dedicated `ceilingCheck` Transform replaced it.

`groundLayer` mask is `2057` = layers 0 (Default), 3 (level geometry), 11. Level geometry pieces are on layer 3. Enemies are mixed: **AeroBat and MeleeEnemy are on the Default layer; RangedEnemy is on the Enemy layer.** This inconsistency is a known issue but currently load-bearing.

### Gravity Reversal System

Triggered by the "Floor is Lava" card (`CardActionType.ReverseGravity`). Lasts 5 seconds with a 0.5s warning flash + audio cue before expiration.

Key fields on PlayerController:
- `isGravityReversed` — runtime flag
- `originalGravityScale` — cached at effect start, restored at end
- `gravityReversalCoroutine` — reference for stop-and-restart on re-play
- `visualFlipYOffset` — serialized field (default 2.0), tuned in Inspector. Translates visualModel up so the 180° rotation pivots around the collider center instead of the feet.
- `originalVisualLocalPos`, `originalVisualScaleX` — cached for restoration
- `warningSoundClip` — AudioClip Inspector field, played at t=4.5s

`GravityReversalRoutine()` handles the full timeline. `LerpVisualTransform` uses a tracked Z-angle float (never reads back from `localEulerAngles`, which Unity normalizes unpredictably).

### Facing System

**Critical:** `transform.localScale` of the player root is ALWAYS `(1, 1, 1)`. Never modify it directly for facing.

Use the `isFacingRight` private bool instead. The `ApplyVisualFacing()` method writes to `visualModel.localScale.x` with this formula:

```
sign = (isFacingRight ? 1 : -1) * (isGravityReversed ? -1 : 1)
visualModel.localScale.x = originalVisualScaleX * sign
```

The gravity reversal factor compensates for the 180° Z rotation inverting the visual X axis.

**Every system that needs world-space facing direction (dash, wall jump, wall check raycast, fireball, etc.) must read `isFacingRight`**, never `transform.localScale.x`.

---

## Card System

### Data Architecture

- **`CardData`** (ScriptableObject) — card templates. Created as assets via Unity menu.
- **`RuntimeCard`** — instance, tracks `currentUses`, `isInfinite`, etc.
- **`CardActionType`** (enum in `GameEnums.cs`) — dispatch identifier.

### Adding a New Card

1. Add a new value to `CardActionType` enum if no existing action covers it.
2. Add a case to the switch in `PlayerController.ExecuteAction()` (until the planned refactor extracts this).
3. Create a `CardData` asset in Unity (right-click in Project view → Create → Card Data).
4. Set the asset's `actionType`, `maxUses`, `shiftCost`, sprite, etc. in the Inspector.
5. Add the card to the relevant reward pools / starter deck as needed.

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh action — costs Shift, redraws the hand, cost increases each use within a level.

### Stagger Mechanic

When Shift is 0 AND no playable cards exist, a Stagger card is auto-added to the hand. Three Stagger plays in one run = death.

---

## Manager Layer

There are 13+ singleton managers. This is a known architectural smell flagged in audit but currently load-bearing. Do not propose merging or restructuring without explicit user approval.

### List of Managers

- **GameManager** — top-level state, player reference, **pause counter (new)**
- **DeckManager** — card piles, draw/discard logic
- **LevelManager** — room spawning, transitions, zone/camera setup
- **RewardManager** — end-of-level card selection screen
- **RelicManager** — owned relics, `HasRelic(string id)` polling pattern
- **SkillManager**, **SkillRewardManager** — skill tree / skill selection
- **QuestSystem** — quest tracking (currently only used in hub which doesn't exist yet)
- **ShopManager** — in-game shop UI and purchases
- **SlotMachineUI** — gambling system UI (the in-world NPC object is `WorldSlotMachine`; planned to be replaced with Dice Broker — see TODO)
- **AchievementManager** — achievement tracking
- **PauseMenu** / **MainMenuController** — menu systems
- **MusicManager** — background music
- **CameraShake**, **HitStop** — game-feel singletons (camera shake + freeze frames)

### Pause Counter System (Important)

`GameManager` has a centralized pause counter (`pauseDepth`) that any UI/menu system uses instead of writing `Time.timeScale` directly.

```csharp
GameManager.instance.RequestPause();   // increments depth, sets timeScale=0 if depth becomes 1
GameManager.instance.ReleasePause();   // decrements depth, sets timeScale=1 if depth becomes 0
```

**Use this for any new UI that should pause the game.** Do not write `Time.timeScale = 0` directly in new code.

Exceptions that intentionally bypass the counter:
- `HitStop.Stop()` — sets timeScale=0 briefly for hit freezes. Not a "pause" semantically.
- `PlayerController.AdrenalineSlowMoRoutine` — slow motion at timeScale=0.4f. Not a pause.
- `PauseMenu.LoadMenu()` — hard reset before scene transition.

### Known Manager Issues

- **Cyclic dependencies:** PlayerController → DeckManager → PlayerController. Don't add more cycles.
- **Most managers lack `DontDestroyOnLoad`**, intentional for single-scene operation.
- **`GameManager.instance.player` is accessed from many UI scripts** with inconsistent null guarding. Add null guards when touching these sites.

---

## UI System

### Canvas Hierarchy

Active scene's main Canvas contains:
- **`GameplayHUD`** (new parent) — contains all in-game HUD elements (gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer trigger zone). Toggle with `SetActive(false)` to hide HUD during shop, slot machine, etc.
- Various menu panels (PauseMenu, ShopUI, SlotMachineUI, RewardScreen, etc.) as direct children of Canvas.

When adding new full-screen UI panels, hide GameplayHUD when they open by adding a `[SerializeField] GameObject gameplayHUD;` reference and toggling SetActive.

### HandUIDrawer

The hand drawer at the bottom of the screen auto-slides up on hover and down when idle.

**Critical raycast behavior:** The drawer's `Image` component has `raycastTarget` enabled to detect hover (`IPointerEnterHandler`). This means it absorbs clicks in its rect. The `SetLocked(bool)` method:

- Sets `isLocked` (stops slide animation)
- Sets `isHovered = false`
- **Toggles `raycastTarget` on the Image component** so the drawer stops absorbing clicks when locked.

**When opening any full-screen UI panel, call `HandUIDrawer.instance.SetLocked(true)`** and `SetLocked(false)` when closing. ShopManager, SlotMachineUI, and DeckViewUI already do this.

---

## Camera System

### CameraFollow.cs (custom)

Replaces Cinemachine entirely. Each level prefab contains a `CameraBounds` child GameObject with `BoxCollider2D` zone children. `LevelManager` finds this object by name and passes its colliders to `CameraFollow.SetZones()` on spawn.

- Camera clamps to the zone the player is currently in.
- Zone transitions use hysteresis (zone doesn't change until player leaves current zone).
- No lerp on zone transition — direct follow (lerp was tried, caused jitter).

**Naming is case-sensitive:** the child must be named exactly `CameraBounds`. The `Transform.Find("CameraBounds")` lookup lives in `LevelManager.cs` — `CameraFollow.SetZones()` itself receives the colliders directly and does no string lookup.

### CameraShake.cs

Rewritten to work without Cinemachine. Uses a `shakeOffset` Vector2 that `CameraFollow.LateUpdate` adds to the final clamped position (so shake can briefly push past zone bounds, which feels correct).

- Uses `unscaledDeltaTime` so shake still plays during HitStop freezes.
- Call sites: `CameraShake.instance.Shake(intensity, duration)`. Always null-guard `instance`.

**The CameraShake component must be present in the active scene** (on the Main Camera) and **enabled**. If it's missing or disabled, every Shake call silently no-ops. This caused a 9-month "no shake anywhere" bug that wasn't discovered until the audit.

### CameraPeek.cs

Still uses Cinemachine. Flagged for reimplementation as an offset on `CameraFollow`. Don't touch unless explicitly tasked.

---

## Enemy System

### Pattern

- **`EnemyHealth`** base script — handles damage, flash, death, drops.
- **AeroBat (BatMan)** — uses Cainos pack visual + custom `AeroBatAI`. Parent has Kinematic Rigidbody2D + Polygon trigger collider. Raycast LOS aimed at player chest (+0.5 Y), shortened by 0.3 to avoid hitting tile at player's feet. State machine: Idle → Preparing → Diving → Returning.
- **MeleeEnemyAI**, **RangedEnemyAI** — based on Cainos pack patterns. Also: `PatrolEnemy`, `ShieldEnemy`.

### Layer Convention Mismatch (Known Issue)

- **AeroBat, MeleeEnemy:** on the **Default** layer (0).
- **RangedEnemy:** on the **Enemy** layer.

Many systems check via `enemyLayer` mask, which misses Default-layer enemies. The workaround in PlayerController is to use `GetComponentInParent<EnemyHealth>()` instead of relying on layer masks for head-bounce detection. **Be aware of this when adding new enemies — pick a layer and stick with it, or use the EnemyHealth-component approach.**

### Head Bounce (Pogo Boots Relic)

- 8 damage, `defaultJumpForce * 0.7f` upward force, camera shake (intensity 0.1, duration 0.2s), 0.3s cooldown.
- Gated behind `RelicManager.HasRelic("PogoBoots")`.
- Uses both `OnCollisionEnter2D` and `OnTriggerEnter2D` (AeroBat has trigger collider, others have solid).
- Contact normal check: `contact.normal.y > 0.7`.

**Known gap:** the velocity sign check (`rb.linearVelocity.y < -0.1f`) doesn't account for gravity reversal. Will silently fail during reversed-gravity head-bounce attempts. Low priority — gravity reversal duration is short and head-bouncing during it is an edge case.

---

## Audio System

Currently minimal. `MusicManager` handles background music. Individual scripts play SFX via `AudioSource.PlayOneShot()` or `AudioSource.PlayClipAtPoint()`. No central SFX manager.

When adding audio cues for new cards/effects, follow the existing pattern: expose a `[SerializeField] AudioClip` field and play it with a null guard.

---

## Common Pitfalls (Hard-Won Lessons)

### "The system exists in code but doesn't work"

Check whether the component is **actually in the scene and enabled**. Multiple times during development, scripts were perfect but the GameObject was missing or the component was disabled. Examples:
- CameraShake was in the scene but disabled for 9 months.
- HitStop was missing from some scenes entirely.

When a system "doesn't seem to work," verify scene presence and enabled state BEFORE assuming the code is wrong.

### "Idempotent operations hide bugs"

Setting `Time.timeScale = 0` is idempotent — calling it twice does the same as once. This hid the ExitDoor double-fire bug for months. Now that the pause counter is in place, redundant calls become visible (pauseDepth goes to 2). **If you find redundant-but-harmless calls, audit whether they should be redundant.**

### "Reading back transform.localEulerAngles is unreliable"

Unity normalizes Euler angles and may return unexpected combinations after 180° rotations. **For rotation tracking, store the angle in a float field and write it via `Quaternion.Euler(0, 0, currentZ)`.** Never read back from `localEulerAngles`.

### "Camera.main is slow and can be null"

`Camera.main` does a tag-based lookup every call. **Cache it in Awake.** Add null guards at call sites.

### "Visual flip ≠ Physics flip"

When rotating a sprite 180° around Z to simulate gravity reversal, the collider does NOT rotate. The capsule remains upright. Don't try to rotate the collider — translate the visual instead (this is what `visualFlipYOffset` does).

### "Cinemachine values don't translate to direct camera offsets"

A Cinemachine `AmplitudeGain` of 0.15 looks very different from a direct `transform.position` offset of 0.15 world units. When porting away from Cinemachine, expect to retune all magnitudes.

---

## Workflow Notes

### Two-Claude Collaboration

The user often consults a separate Claude instance (the conversational one in claude.ai) for design discussion and prompt drafting, then sends prompts to Claude Code for execution. When the user references "what Claude said" or "the other Claude," that's the source. The user is fluent enough to course-correct, but defer to user intent when their explanation differs from a previous prompt.

### User Skill Profile

- Designer/developer with strong design intuition
- Limited coding background — code-level explanations should be in plain language with concrete examples
- Cannot evaluate code quality directly; relies on Claude Code's plans and the other Claude's review
- **When proposing a refactor, explain the consequence in plain English BEFORE the technical detail.**

### Confirmation Patterns

- Default to small, targeted changes. Refactors require explicit approval.
- When a plan changes scope mid-task ("while I'm in there..."), STOP and confirm with the user.
- For multi-file changes, show the affected file list before making edits.
- Diagnostic-only prompts ("don't fix yet, report") must be respected — never make changes when asked to diagnose only.

### Language

- New code comments: **English**.
- Older code comments: often Turkish — leave alone unless misleading.
- User communicates in English now (was Turkish in earlier sessions).

---

## Known Issues / Deferred Work

### Architecture (planned)

- **PlayerController.ExecuteAction() extraction** — extract the 14-case card-action switch into a dedicated `CardActionExecutor` component. Highest-leverage refactor before content scales to 60+ cards. Scheduled as the first task in the next session.
- **CameraPeek Cinemachine dependency** — replace with offset on CameraFollow.
- **Manager dependency graph** — undocumented. Long-term docs task.

### Bugs (deferred)

- **Phase card wall-stuck:** if Phase ends while player is inside a wall, player gets stuck. Plan: prevent Phase expiration inside collider.
- **Fall damage zeroing into floor:** at high fall speeds player clips into ground. Plan: **remove fall damage entirely.**
- **Spike knockback always sends right-up:** ignores incoming angle. Plan: velocity reflection.
- **Comet Dive identity loss:** does the same thing as head-bounce relic. Plan: redesign.
- **Head bounce + gravity reversal:** velocity sign check doesn't account for reversed gravity. Low priority.
- **Duplicate ExitDoor possible in some room prefabs:** defensive guards now in place but the scene-side duplicate (if any) hasn't been cleaned up.

### Content (TODO)

- Scale to 60+ cards (currently ~10).
- Glass archetype: cards exist in theory, not implemented.
- Expand Vampiric archetype.
- Three-act structure: Act 1 prototype exists; Acts 2-3 not started.
- Boss encounters per act (3 bosses per act, randomly selected from pool).
- Chunk-based level system (currently hand-crafted levels).

### Replace SlotMachine with "Dice Broker"

A character-driven gambling NPC replacing the current slot machine. Same gameplay outcome (random relic from a dice roll) but rethemed:
- A grimy character (sprite needed) who shakes a dice cup
- Reuses RewardManager's relic-grant flow
- Implementation note: **roll the result in code first, then play an animation that ends on the correct face**. Don't depend on physics simulation.
- Dice animation: sprite-sheet of 6-12 tumble frames ending on each face (cheaper and more readable than physics dice).
- Voice/banter potential — give the broker personality.

### Hub Rework

Old hub asset is deprecated; new asset pack acquired but hub not built yet. Quest board lives in hub. Both pending.

---

## File / Path Reference

- Active scene: `Assets/Scenes/SampleScene.unity`
- Player prefab: `Assets/Prefabs/Player.prefab`
- Scripts: `Assets/Scripts/` (75+ files, flat structure)
- Level prefabs: `Assets/LevelSinasi/*.prefab`
- Card asset directory: (project-specific, check user's setup)

---

## When in Doubt

- Ask the user for clarification before making sweeping changes.
- Verify scene presence of components before assuming code is broken.
- Read related scripts before refactoring shared systems.
- For visual/UI work, confirm the canvas hierarchy and parenting before moving GameObjects.
- The user wants quality over speed. "Make this one of the greats" is the stated goal — push back gently on quick-fix patterns when a slightly larger correct fix is appropriate.

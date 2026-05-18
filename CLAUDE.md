# Deckshift — Claude Code Context

This file is loaded automatically into Claude Code at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Jumping consumes **Shift**, a non-regenerating resource per-room. Most other actions (attacks, special movement, utility) are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. ~5 hand-crafted levels, ~10 cards in the game (after deleting 4 unused: `DashBackward`, `WallCling`, `DrawCards`, `GainJumpCharges` — see "Card System" for the surviving 12). Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete.

The player character was swapped from a SkinnedMeshRenderer-based rig (`PF Skeleton - Mage`) to the Cainos `PF Pixel Character - Mage M` pack. The wizard identity is now the canonical character. The skeleton remains in the Player prefab disabled, intended for future use as an enemy. **Note despite the "Pixel Character" name, the Cainos pack uses SkinnedMeshRenderers, not SpriteRenderers** — see Common Pitfalls.

**Active scene:** `Assets/Scenes/SampleScene.unity` (build index 2). Other scene files exist (`GameScene`, `MasterLevel`, `Hub`) but are inactive/legacy. When debugging "is this in the scene?" issues, always check SampleScene first.

---

## User Profile and Workflow

The user is **designer-first, not a developer**. Strong design intuition, limited coding background. They cannot evaluate code quality directly. All implementation flows through Claude Code; conversational Claude reviews plans before they're sent to Claude Code.

**Never ask the user to read or edit code directly.** Never instruct them to "tweak this value in the script" or "change line X yourself." Either:
- Give them an Inspector step in Unity (drag this, click that, change this number),
- Or give them a prompt to send to Claude Code that does the change.

**Code-level explanations should be in plain language with concrete examples.** When proposing a refactor or technical change, explain the consequence in plain English BEFORE the technical detail.

The user works with a **separate conversational Claude instance** (claude.ai) for design discussion and prompt drafting, then sends prompts to Claude Code for execution. When the user references "what Claude said" or "the other Claude," that's the source. The user is fluent enough to course-correct, but defer to user intent when their explanation differs from a previous prompt.

---

## Critical Architecture Rules

These are absolute. Do not suggest alternatives without explicit user approval.

1. **No Cinemachine currently in use.** It was removed early due to confiner issues with multi-shape rooms. The custom system is in `CameraFollow.cs` plus per-level `LevelBounds` zones. The policy is "currently removed, can be revisited if a clean approach is found" — not absolute prohibition. The surviving Cinemachine reference is in `CameraPeek.cs`, which is currently broken and slated for rebuild.

2. **Manager-singleton pattern.** All major systems are singleton MonoBehaviour managers (GameManager, DeckManager, LevelManager, etc.). This pattern has known issues (cyclic dependencies, flat global state) but is the architecture. Do not propose dependency injection, ECS, or other paradigms.

3. **Game runs in a single scene currently.** Most managers do not have `DontDestroyOnLoad`. **Exception: QuestSystem currently has `DontDestroyOnLoad` set, which is inconsistent with the rest of the codebase — flagged for review.** If scene transitions are added later, this must be revisited. Do not add `DontDestroyOnLoad` to existing managers without discussing the implications.

4. **Comment language convention.** Older code has Turkish comments (Gemini-era). Going forward, **new comments should be in English** for clarity. Do not retranslate existing Turkish comments unless they're factually misleading.

5. **No assets the user can't afford.** Solo developer with limited budget, no freelance artist. Work with existing Cainos asset packs and pixel-art conventions. Don't propose solutions that require commissioning new art.

6. **Asset pack imports require extreme care.** The Cainos Customizable Pixel Character pack ships as a "complete project" that wants to overwrite `ProjectSettings/`. Always uncheck `ProjectSettings/` in the import dialog and uncheck any duplicate packs you already have. See "Common Pitfalls" for the full story.

7. **`CardActionType` enum values must have explicit integer assignments.** Every value is pinned (`Jump = 0`, `Dash = 1`, etc.). Deleted values are left as comments documenting their retired slot. **Never reuse a retired slot for a new action.** Reason: CardData assets serialize `actionType` as the integer, not the name. Renumbering re-binds every existing asset to the wrong action silently. New CardActionType values must be added at the end (16, 17, etc.). This rule was learned the hard way — the entire card system broke once because deletion shifted indices.

8. **For per-renderer runtime property changes, use `MaterialPropertyBlock`, not `renderer.material.color`.** Writing to `.material` clones the material every frame (breaks batching, leaks). MaterialPropertyBlock is the proper Unity pattern: one allocation, `SetFloat`/`SetColor`, then `renderer.SetPropertyBlock(block)`. The Cainos `PixelCharacter.Alpha` setter is the reference implementation.

---

## Player System

### PlayerController.cs

This is a large script (~1,200 lines). It currently handles movement, jumping, gravity reversal, VFX spawning, audio, health, gold, shift, knockback, portal mid-card state, cannon enter/exit, death, and respawn. Card dispatch has been extracted (see Card System below) but most action helper methods still live here.

**Known issue:** It is a God Object. Card dispatch was extracted to `CardActionExecutor`, but most action logic remains in `PlayerController` as helper methods (`TryPlacePortal`, `PerformVampiricBite`, `FireballCastRoutine`, etc.) that the CardAction classes delegate into. Three further extractions are queued in deferred work: **PlayerHealth** (HP/damage/death/knockback), **PortalController** (firstPortalInstance + TryPlacePortal), **GravityController** (full ReverseGravity routine + visualFlipYOffset). These will reduce coupling significantly.

### Player Prefab Specifics

- **Active visual model:** `PF Pixel Character - Mage M` at `Assets/Cainos/Customizable Pixel Character/Prefab/Character Preset/PF Pixel Character - Mage M.prefab`. This is a child of the Player root and is assigned to `PlayerController.visualModel`.
- **Disabled fallback:** `PF Skeleton - Mage` is still parented under Player but disabled. Kept as backup and for future reuse as an enemy.
- **Physics collider:** `CapsuleCollider2D` on the Player root with **Offset (0.122, 0.871) and Size (0.5075, 1.6848)**. Direction: Vertical. These values were honest-refactored from pre-fix design-intent values that had been compounding with a non-(1,1,1) root scale for 9 months. A `BoxCollider2D` was previously present but disabled and has been removed. Do not re-add it.
- **Rigidbody2D:** Dynamic. Gravity scale flips sign during gravity reversal — do NOT modify `Physics2D.gravity` globally.
- **Player root Transform:** Position (0, 0, 0), Rotation (0, 0, 0), **Scale (1, 1, 1)**. Hard rule. To change character size, scale `visualModel`.

### Visual Model Internals (PF Pixel Character - Mage M)

- The visualModel itself is scaled to **(0.8, 0.8, 0.8)** to fit the collider.
- **Important: the Cainos "Pixel Character" pack uses SkinnedMeshRenderer, not SpriteRenderer**, despite the pixel-art aesthetic. The rig is a 3D FBX with one SkinnedMeshRenderer per body part (body, eye, hair, hat, cloth, pants, shoes, shoesFront, back, expression, eyeBase — for Mage M).
- The prefab has its own root-level scripts (`PixelCharacter`, `PixelCharacterController`, `PixelCharacterInputMouseAndKeyboard`, plus its own Rigidbody2D and BoxCollider2D). When the visualModel was integrated, the controller scripts and physics components were removed; only the `PixelCharacter` (customization) script remains. Do not re-add the removed components.
- The Animator component lives on the child GameObject named `Animator`, found via `GetComponentInChildren<Animator>()`. There is only one Animator in the hierarchy.
- The Animator Controller is `Assets/Cainos/Customizable Pixel Character/Animation/AC Character.controller`.
- **`Cainos.CustomizablePixelCharacter.AnimationEventReceiver` component on the Animator GameObject must remain DISABLED.** It throws NullReferenceExceptions on the built-in footstep animation events. Re-enabling it floods the console with errors during the cast animation. Has been disabled twice — watch for re-enabling on prefab reimport.

### Cainos Shader Properties

The shaders used by the Cainos character (`ASE Pixel Character Body.shader`, `ASE Pixel Character Alpha Cut.shader`) expose:
- `_MainTex` — Texture (always)
- `_Alpha` — Float, 0=invisible (dithered), 1=opaque (always)
- `_SkinMaskTex`, `_SkinTint` — Body shader only

**`_Color` does not exist on these shaders.** `renderer.material.color = X` or `material.SetColor("_Color", ...)` does nothing visible — the shader doesn't read it. Use `_Alpha` for fades, `_SkinTint` for body recoloring. Wrote a feature that doesn't appear in play? Check that the property name matches what the shader actually exposes.

### Animator Parameter Map

PlayerController writes to these parameters on the Animator:

| Parameter        | Type    | Driven by                                                | Purpose                                            |
|------------------|---------|----------------------------------------------------------|----------------------------------------------------|
| `MoveBlendX`     | Float   | `UpdateAnimations()` — 0.0 still, 1.0 running            | Walk/idle blend                                    |
| `VelocityY`      | Float   | `UpdateAnimations()` — `rb.linearVelocity.y`             | Jump/fall vertical state                           |
| `IsGrounded`     | Bool    | `UpdateAnimations()`                                     | Land/airborne distinction                          |
| `InjuredFront`   | Trigger | `TakeDamage()` on every damage hit                       | Hurt reaction                                      |
| `IsDead`         | Bool    | `Die()` set to true                                      | Death state                                        |
| `AttackAction`   | **Int** | `FireballCastRoutine` sets to **14**                     | Dispatch value selecting which attack animation    |
| `IsAttacking`    | Bool    | `FireballCastRoutine` toggles                            | Gate for AttackAction transitions                  |

**Critical:** `AttackAction` is an **Int**, not a Float. Calling `SetFloat("AttackAction", ...)` throws a runtime type mismatch. Use `SetInteger("AttackAction", value)`.

### Cast Animation (Fireball Card)

The Cainos Animator Controller has a "Cast" animation at `AttackAction == 14`, playing on both the "Attack Action - Arm" and "Attack Action - Body" layers simultaneously. The clip is 1.0 seconds long and self-exits at ~80% via unconditional ExitTime.

`PlayerController.FireballCastRoutine`:
1. Sets `IsAttacking = true` and `AttackAction = 14`.
2. Waits **0.36 seconds** — the `OnAttackCast` event timestamp authored by Cainos. This delay was originally chosen for designer-authored sync but **the user has flagged that 0.36s of input-to-action lag feels bad and wants instant-spawn**; this is on the deferred work list.
3. Calls `PerformFireball(value)` to spawn the projectile.
4. Waits an additional 0.15 seconds, then sets `IsAttacking = false`.

### Ground / Wall / Ceiling Detection

The player has check Transforms parented to the player root (NOT to visualModel):

- **`groundCheck`** at local (0, 0.015, 0). Normal grounded detection.
- **`wallCheck`** at local (0, -0.00975, 0). Horizontal collision.
- **`ceilingCheck`** at local (0, 1.725, 0). Used when `isGravityReversed` is true.
- **`firepoint`** at local Position (0.499, 1.263, 0).

`IsGroundedCheck()` switches probe based on `isGravityReversed`.

`groundLayer` mask is `2057` = layers 0 (Default), 3 (level geometry), 11. **Enemies are mixed: AeroBat and MeleeEnemy are on the Default layer; RangedEnemy is on the Enemy layer.** This inconsistency is load-bearing — see Enemy System.

### Gravity Reversal System

Triggered by the "Floor is Lava" card (`CardActionType.ReverseGravity`). Lasts 5 seconds with audio warning at t=4.5s before expiration.

Key fields on PlayerController:
- `isGravityReversed` — **`internal`** (not private — read by `JumpAction` and others). Runtime flag checked by grounded detection, facing, jump direction, head-bounce direction, etc.
- `originalGravityScale` — cached at effect start, restored at end
- `gravityReversalCoroutine` — reference for stop-and-restart on re-play
- `visualFlipYOffset` — serialized field, current value **1.6875**.
- `originalVisualLocalPos`, `originalVisualScaleX` — cached for restoration
- `warningSoundClip` — AudioClip Inspector field, played at t=4.5s

**Cards/systems that respect gravity reversal:** `JumpAction`, `PerformJump` (space bar), `PerformStagger`, head-bounce (all three checks: velocity sign, contact normal, position Y), Phase wall-stuck eject direction. All flip via `isGravityReversed ? -1f : 1f`. When adding new vertical-direction mechanics, follow this pattern.

**Known gap:** the 0.5s warning flash is implemented against `SkinnedMeshRenderer.material.SetColor("_Color", ...)` — but Cainos shaders don't expose `_Color` (see Cainos Shader Properties above). The flash silently no-ops. Audio still plays. Fixing this requires rewriting against `_Alpha` or `_SkinTint` via MaterialPropertyBlock. Low priority but noted.

### Facing System

`transform.localScale` of the player root is ALWAYS `(1, 1, 1)`. Never modify it for facing.

Use `isFacingRight` (`internal`). `ApplyVisualFacing()` writes to `visualModel.localScale.x`:

```
sign = (isFacingRight ? 1 : -1) * (isGravityReversed ? -1 : 1)
visualModel.localScale.x = originalVisualScaleX * sign
```

**Every system that needs world-space facing direction must read `isFacingRight`**, never `transform.localScale.x`.

---

## Card System

### Architecture

Card dispatch was extracted from a switch in `PlayerController.ExecuteAction` into a polymorphic system. Components:

- **`CardAction`** (abstract base, `Assets/Scripts/CardActions/CardAction.cs`) — every card action inherits this.
- **One concrete subclass per action** in `Assets/Scripts/CardActions/Actions/` (12 currently — see list below).
- **`CardActionExecutor`** (MonoBehaviour on Player) — owns a `Dictionary<CardActionType, CardAction>`, dispatches `TryExecute(type, value)` calls.
- **`ConflictFlags`** (flags enum) — each action declares which shared player state it touches (`GravityScale`, `TimeScale`, `MoveSpeed`, `LayerCollisionMatrix`, `VisualTransform`, `PlayerVelocity`, `Invincibility`, `AnimatorAttackState`). Used by the executor's running-effects registry.

The executor does NOT block conflicts; both colliding effects coexist. The flag system exists so future logic can react to overlap without preventing combos (which are the point of a deckbuilder). **Caveat: Phase, Adrenaline, and ReverseGravity currently start their coroutines inside PlayerController helpers, not the CardAction.** Their flags are declared honestly but they don't fully participate in `runningEffects` tracking. Restructuring those three helpers to return IEnumerator is deferred work — it's the real fix for the Phase/ReverseGravity gravity-corruption bug class.

### CardActionType Enum (Pinned Integer Values)

```
Jump = 0,
Dash = 1,
// 2 (DashBackward) intentionally retired — never reuse
// 3 (WallCling) intentionally retired
// 4 (DrawCards) intentionally retired
// 5 (GainJumpCharges) intentionally retired
PlatformCreate = 6,
Fireball = 7,
Portal = 8,
VampiricBite = 9,
GlassWail = 10,
Phase = 11,
CometDive = 12,
Adrenaline = 13,
Stagger = 14,
ReverseGravity = 15,
```

**Never reuse the retired slots.** CardData assets serialize the integer; reusing a slot silently re-binds orphaned save data or scene references. New actions go at 16, 17, etc.

### Adding a New Card

1. Add a new value to `CardActionType` enum at the next unused integer (currently 16). Do not renumber existing values.
2. Create a new class in `Assets/Scripts/CardActions/Actions/<Name>Action.cs` inheriting `CardAction`. Implement `ActionType`, `ModifiedState`, and `Execute` (instant) or `ExecuteCoroutine` (timed).
3. Register the action in `CardActionExecutor.cs` (dictionary registration list).
4. Create a `CardData` asset in Unity. Set `actionType`, `maxUses`, `shiftCost`, sprite.
5. Add the card to relevant reward pools / starter deck as needed.

### Action Class Conventions

- Action classes call back into PlayerController helpers (e.g., `player.PerformVampiricBite()`). Helpers are kept on PlayerController for now; future refactors will extract them per-component.
- Where actions need to read/write PlayerController fields, those fields are `internal`, not `public`. Don't weaken to `public`.
- Direction-dependent actions (Jump, Stagger, etc.) MUST consult `player.isGravityReversed` to compute their direction. World-space `Vector2.up` constants without flipping is a bug.

### Data

- **`CardData`** (ScriptableObject) — card templates.
- **`RuntimeCard`** — instance, tracks `currentUses`, `isInfinite`.

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh — costs Shift, redraws the hand, cost increases each use within a level.

### Stagger Mechanic

When Shift is 0 AND no playable cards exist, a Stagger card is auto-added to the hand. Three Stagger plays in one run = death.

### Dash (Current Design)

The Dash card is **impulse-based**, not velocity-based. `DashAction.Execute` applies a single `AddForce` impulse in the facing direction (`dashImpulse` field, default 18), starts a brief invincibility coroutine (`dashIFrameDuration`, default 0.15s), spawns VFX, plays audio. Critically, **Dash does NOT touch `rb.gravityScale`** — the previous coroutine implementation captured and restored gravity, which corrupted state when ReverseGravity expired mid-dash. The impulse model composes correctly with every other mechanic. The dash-then-jump combo is intentional and feels good.

### Comet Dive (Current Design)

Dead Cells-style ground slam. Airborne-only (card fails silently if grounded). On play, vertical velocity becomes `-cometSpeed`, horizontal velocity is preserved (momentum carries). On ground collision, AOE damage in `cometRadius` (using all-layers OverlapCircleAll + `GetComponentInParent<EnemyHealth>` + HashSet dedup). Spawns `cometImpactEffect`, camera shakes, trail cleans up via `EndCometDive()`. Trail cleanup also fires on death, knockback, and fall-respawn — every dive-ending path.

### Card Effect Conflict Class of Bug (Partial Resolution)

Originally: playing multiple state-modifying cards in close succession (Phase + ReverseGravity + Adrenaline) could permanently corrupt player state because each card's "capture original, restore on exit" pattern was unaware of the others.

**Current status:** the CardActionExecutor + ConflictFlags scaffolding is in place. Most actions are tracked correctly. Three actions (Phase, Adrenaline, ReverseGravity) still manage coroutines inside PlayerController helpers and don't fully populate `runningEffects` — their declared flags exist but aren't actively tracked. **The Phase/ReverseGravity gravity-capture interaction can still corrupt gravity scale** if both expire in a specific order. Reachable in hub; mostly gated by Shift cost in normal play. **Fix is in deferred work:** restructure the three helpers to return IEnumerator so the executor can manage their lifecycle and detect conflicts.

---

## Hub Mode (Sandbox)

The hub is a sandbox room where the player tests cards, jumps freely, and experiments without consequence. The hub prefab is at `Assets/LevelEfeS/hub.prefab`. **It is currently the always-first room in every run.**

### HubMarker Component

`Assets/Scripts/HubMarker.cs` is a marker MonoBehaviour signaling "this is a hub." `LevelManager.IsCurrentRoomHub()` is the single source of truth.

### Umbrella Rule: No Consumption In Hub

The hub gates every player-resource consumption call. **No resource is consumed and no permanent state changes** while the player is in a hub.

Specifically gated:
- Shift consumption from jumping (`PerformJump`)
- Shift consumption from playing cards (`DeckManager.PlayCard` → `player.SpendShift(cost)`)
- Shift consumption from portal second-placement (`TryPlacePortal`)
- Card charge decrement (`playedCard.currentUses--`)
- Card exhaust routing
- Recall shift cost (`TryRecall` → `SpendShift`)
- Recall cost escalation (`currentRecallCost++`)
- Stagger card injection (`CheckForStaggerCondition`)

(Fall damage was removed entirely from the game and is no longer in this list.)

Pattern: `if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub()) { ... do the consumption ... }`.

**When adding new player-resource consumption code,** ask "should this be free in a sandbox?" — almost always yes.

### What Hub Does NOT Hide

UI is intentionally unchanged in hub. Only the underlying mechanics are gated.

---

## Manager Layer

13+ singleton managers. Architectural smell but load-bearing.

### List of Managers

- **GameManager** — top-level state, player reference, centralized pause counter
- **DeckManager** — card piles, draw/discard logic
- **LevelManager** — room spawning, transitions, zone/camera setup, first-room-is-hub logic
- **RewardManager** — end-of-level card selection screen
- **RelicManager** — owned relics, `HasRelic(string id)` polling pattern, `OnRelicAdded` event
- **SkillManager**, **SkillRewardManager** — skill tree / skill selection
- **QuestSystem** — quest tracking, board UI, accept/progress/complete events
- **ShopManager** — in-game shop UI and purchases
- **SlotMachineManager** / **SlotMachineUI** — gambling system (planned replacement: Dice Broker)
- **AchievementManager** — achievement tracking
- **MenuManager** / **PauseMenu** / **MainMenuController** — menu systems
- **EffectManager** — VFX spawning helper
- **MusicManager** — background music
- **CameraShake**, **HitStop** — game-feel singletons

### Pause Counter System

`GameManager` has a centralized pause counter:

```csharp
GameManager.instance.RequestPause();   // increments depth, sets timeScale=0 if depth becomes 1
GameManager.instance.ReleasePause();   // decrements depth, sets timeScale=1 if depth becomes 0
```

**Use this for any new UI that should pause the game.** Do not write `Time.timeScale = 0` directly in new code.

Exceptions: `HitStop.Stop()`, `PlayerController.AdrenalineSlowMoRoutine`, `PauseMenu.LoadMenu()`.

### Known Manager Issues

- **Cyclic dependencies:** PlayerController → DeckManager → PlayerController. Don't add more cycles.
- **Most managers lack `DontDestroyOnLoad`**, intentional for single-scene operation.
- **QuestSystem has `DontDestroyOnLoad`** — inconsistent. Flagged for review.
- **`GameManager.instance.player` is accessed from many UI scripts** with inconsistent null guarding.

---

## Quest System

Functional: data model, accept/progress/complete events, board UI, live tracker HUD all working.

### Data

- **`QuestData`** (ScriptableObject) — `questName`, `description`, `type` (QuestType enum), `targetAmount`, `rewardText`, `rewardType` (RewardType enum), `rewardAmount`.
- **`QuestType` enum:** `GoldAccumulate`, `KillEnemy`, `AirKill`, `NoDamageRoom`, `UseCardCount`. Only `KillEnemy` and `AirKill` fire events (from `EnemyHealth.Die()`). Others are defined but unwired.
- **`RewardType` enum:** `Gold`, `ShiftCharge`, `Heal`. All wired in `QuestSystem.GiveReward`.
- **Three quest assets exist** at `Assets/Quests/`. `New Quest 1` (NoDamageRoom) won't progress until the type is wired.

### QuestSystem Singleton

In SampleScene. `ToggleBoard`, `GenerateQuests` (always picks first 3, no randomization yet), `AcceptQuest`, `ReportEvent`, `CheckCompletion`, `GiveReward` (immediate, not deferred to level-end).

### Events

```csharp
public event System.Action<ActiveQuest> OnQuestAccepted;
public event System.Action<ActiveQuest> OnQuestProgress;
public event System.Action<ActiveQuest> OnQuestCompleted;
```

### Live Tracker HUD (QuestTrackerHUD)

`Assets/Scripts/QuestTrackerHUD.cs`. Two TMP children named exactly `Title` and `Progress` (case-sensitive — typos make rows blank).

---

## Relic System

Currently SOTS-style additive: unlimited relics, no slots. **Slated for major redesign — see "Future: Slot-Constrained Relic Redesign" in deferred work.** Don't invest heavily in relic UX or content; it'll be reworked.

### RelicManager

Singleton. `OwnedRelics` accessor, `OnRelicAdded` event. Grant paths: `ShopItemUI`, `SlotMachineUI`, `DebugTools.cs` F1.

No starting-relic infrastructure exists yet.

### RelicData

`relicID`, `relicName`, `description`, `relicArt`, `rarity`.

Wired relics: VampireTooth (heal on kill), Kinetic (+2 Shift on kill), SpikedCarapac (reflect on damage), Pogo Boots (head-bounce), LavaBoots (hazard immunity). Placeholders: "Oops! All 7's", Helly.

### Relic HUD (RelicHUD.cs)

Middle-left vertical column. 48×48 icons. No tooltip, no activation flash — deferred polish.

---

## UI System

### Canvas Hierarchy

- **`GameplayHUD`** — gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer, RelicHUD, QuestTracker. Toggle `SetActive(false)` for full-screen UI.
- **`QuestBoardOverlay`** — quest board panel.
- Menu panels (PauseMenu, ShopUI, etc.) as direct children of Canvas.

**When adding new full-screen UI**, hide GameplayHUD with a reference and SetActive.

### Never Scale UI Containers — Resize Them

Change Width and Height in the RectTransform, not Scale. Scaling cascades to children and breaks Layout Groups.

### HandUIDrawer

Auto-slides up on hover. The `Image` has `raycastTarget = true` for hover detection. **When opening any full-screen UI panel, call `HandUIDrawer.instance.SetLocked(true)`** and `SetLocked(false)` when closing.

---

## Camera System

### CameraFollow.cs (custom)

Replaces Cinemachine. Each level prefab has a `LevelBounds` child with `BoxCollider2D` zones. Zone transitions use hysteresis, no lerp.

**Naming case-sensitive:** child must be exactly `LevelBounds`.

### CameraShake.cs

Uses `shakeOffset` Vector2 added by `CameraFollow.LateUpdate`. `unscaledDeltaTime` so it plays during HitStop. `CameraShake.instance.Shake(duration, intensity)`. Null-guard `instance`.

**Must be present and enabled** on the Main Camera. If missing, every Shake call silently no-ops.

### CameraPeek.cs (BROKEN)

Currently does not work. Left Ctrl bind dead. Cinemachine-dependent. Slated for rebuild as `CameraFollow` offset.

---

## Level System

### Room Pool

`LevelManager.roomPrefabs`. Element 0 is the hub by convention.

### First-Room Logic

Bool `hasSpawnedFirstRoom`. First call: force index 0 (hub), mark spawned. Subsequent calls: strip index 0 from pool every call. Net effect: hub is first room every run, never spawns again.

---

## Enemy System

### Pattern

- **`EnemyHealth`** base script — damage, flash, death, drops, stun. `Die()` calls `RelicManager.OnEnemyKilled()`, `QuestSystem.ReportEvent(QuestType.KillEnemy, 1)`, optionally `AirKill`. **No C# event for death.**
- **AeroBat (BatMan)** — Cainos visual + custom `AeroBatAI`. Raycast LOS aimed at player chest. State: Idle → Preparing → Diving → Returning.
- **MeleeEnemy, RangedEnemy** — SkinnedMeshRenderer-based skeleton rigs.
- **ShieldEnemy, Turret (Taret), PatrolEnemy** — additional types.

### Stun System

Glass Wail and any future stun source go through `EnemyHealth.Stun(float duration)`. Pattern:

- `EnemyHealth.IsStunned` is a `public bool { get; private set; }` property.
- `Stun(duration)` cancels any running stun, sets `IsStunned = true`, starts `StunRoutine(duration)` which clears the flag after the wait.
- **Every AI script polls the flag.** Pattern at the top of Update/FixedUpdate: `if (health != null && health.IsStunned) return;`. Coroutines that yield can either skip-then-continue (like Turret's FireRoutine) or abort cleanly to Idle on detection (like AeroBat's PrepareAttackRoutine).
- Visual feedback uses dual arrays on EnemyHealth: `SpriteRenderer[] stunSpriteRenderers` and `SkinnedMeshRenderer[] stunSkinnedRenderers`. Both get tinted blue on stun, restored on exit. Arrays handle Cainos multi-body-part rigs and mixed renderer types in one pattern. **Note: the current implementation uses `.material.color` writes, which is wasteful — should be rewritten to MaterialPropertyBlock when next touched.**
- AI scripts with stun guards wired: AeroBatAI, MeleeEnemyAI, RangedEnemyAI, PatrolEnemy, Turret, ShieldEnemy.

When adding a new enemy: implement EnemyHealth, add the stun-guard one-liner in your AI's Update, wire the appropriate renderer array in Inspector. That's it.

### Layer Convention Mismatch (Known)

- **AeroBat, MeleeEnemy:** on **Default** layer (0).
- **RangedEnemy:** on **Enemy** layer.

**Workaround:** use `GetComponentInParent<EnemyHealth>()` for hit detection instead of layer masks. The Vampiric Bite, Comet Dive, and head-bounce systems all use this pattern.

### Head Bounce (Pogo Boots Relic)

- 8 damage, `defaultJumpForce * 0.7f` upward force, 0.1s camera shake, 0.3s cooldown.
- Gated behind `RelicManager.HasRelic("PogoBoots")`.
- Uses both `OnCollisionEnter2D` and `OnTriggerEnter2D` (AeroBat has trigger; others solid).
- **Fully respects gravity reversal:** velocity sign check, contact normal check, position Y check, and bounce direction all flip via `isGravityReversed`. (Was previously partially broken; now correct.)

---

## Audio System

Currently minimal. `MusicManager` handles BGM. SFX via `AudioSource.PlayOneShot()`. No central SFX manager.

When adding audio: `[SerializeField] AudioClip` field, play with null guard.

---

## Common Pitfalls (Hard-Won Lessons)

### "Importing an asset pack can overwrite ProjectSettings and break everything"

Cainos packs ship as "complete projects" with `ProjectSettings/` overrides. **Always click "None" on the Step-2 overrides screen.** Always uncheck duplicate packs in Step-1. Accepting overrides destroys URP config, tags, physics, input bindings.

### "Cainos shaders use _Alpha and _SkinTint, NOT _Color"

This bit us writing the Phase visual effect. `renderer.material.color =` calls `SetColor("_Color", ...)` which the Cainos shaders ignore — they expose `_Alpha` (float) for fades and `_SkinTint` (Color, body shader only) for body recoloring. If a renderer effect "doesn't appear," check the shader source first. Read the `Properties { }` block of the .shader file.

### "Cainos 'Pixel Character' is SkinnedMeshRenderer, not SpriteRenderer"

The pixel aesthetic implies 2D sprites. The Cainos pack is actually a 3D FBX rig with one SkinnedMeshRenderer per body part. Code that assumes SpriteRenderer (like the old gravity reversal warning flash) silently no-ops. Mixed-renderer features should use parallel arrays (`SpriteRenderer[] foo; SkinnedMeshRenderer[] bar;`) like the stun system does.

### "Use MaterialPropertyBlock, not .material.color, for per-frame renderer writes"

Writing to `renderer.material` clones the material every frame. Breaks batching, leaks memory. The correct pattern:

```csharp
MaterialPropertyBlock block = new MaterialPropertyBlock();
block.SetFloat("_Alpha", 0.5f);
renderer.SetPropertyBlock(block);
```

Cainos's own `PixelCharacter.Alpha` setter is the reference implementation.

### "Enum integer pinning is required for serialized enum fields"

`CardActionType` values MUST have explicit `= N` assignments. ScriptableObject assets (CardData) serialize the integer. Deleting an enum entry shifts every later value down, silently re-binding every existing asset to a different action. Discovered after a bulk deletion broke every card except Dash.

Apply this convention to any future enum whose values appear in serialized assets.

### "The system exists in code but doesn't work"

Check whether the component is **actually in the scene and enabled** before assuming code is broken. CameraShake was disabled for 9 months. HitStop was missing from scenes. QuestSystem was missing entirely after a scene reorganization. **Verify scene presence first.**

Same lesson applies to required Inspector wiring: a feature that depends on a serialized array won't work if the array is empty. Check the Inspector before debugging the code.

### "Idempotent operations hide bugs"

`Time.timeScale = 0` is idempotent — calling it twice does the same as once. This hid the ExitDoor double-fire bug for months. The pause counter makes redundant calls visible.

### "Reading back transform.localEulerAngles is unreliable"

Unity normalizes Euler angles unpredictably. For rotation tracking, store the angle in a float and write via `Quaternion.Euler(0, 0, currentZ)`. Never read back from `localEulerAngles`.

### "Camera.main is slow and can be null"

Tag-based lookup every call. Cache in Awake. Null-guard.

### "Visual flip ≠ Physics flip"

When rotating a sprite 180° around Z for gravity reversal, the collider does NOT rotate. Translate the visual instead (`visualFlipYOffset`).

### "Cinemachine values don't translate to direct camera offsets"

Magnitudes differ. Retune when porting away from Cinemachine.

### "Animator parameter type errors are silent until used"

The `AC Character.controller` YAML uses `m_Type` integers: 1=Float, 3=Int, 4=Bool, 9=Trigger. Read the .controller YAML directly when in doubt; don't guess.

### "First diagnostics can be wrong; always verify"

Claude Code's first diagnostic on a complex script can be wrong. For animator parameter types, the YAML m_Type integer is the source of truth.

### "Transform.Find is case-sensitive and silent"

A typo, trailing space, or capitalization mismatch returns null and the calling code skips silently. When tracker rows / popups / instantiated UI elements appear blank, check child naming inside the prefab first.

### "GetComponentInChildren can return null"

Always null-guard before dereferencing. The QuestPaper.OnAccept silent-fail bug was a missing TMP child causing the entire AcceptQuest flow to never run.

### "The object picker (+) only lists project assets, not scene/prefab GameObjects"

When wiring a serialized GameObject/Component reference, drag from the Hierarchy. The Inspector's `+` picker won't show GameObjects inside the open prefab.

### "Different rig types need different renderer references"

SpriteRenderer and SkinnedMeshRenderer are different component types. A serialized `SpriteRenderer` field can't hold a SkinnedMeshRenderer reference (and vice versa). When a project has mixed enemy types, features that tint/fade them need parallel arrays.

---

## Workflow Notes

### Two-Claude Collaboration

The user consults a separate Claude instance for design and prompt drafting, then sends prompts to Claude Code for execution. When the user references "what Claude said," that's the conversational instance.

### Confirmation Patterns

- Default to small, targeted changes. Refactors require explicit approval.
- When scope changes mid-task ("while I'm in there..."), STOP and confirm.
- For multi-file changes, show the file list before editing.
- Diagnostic-only prompts must be respected — never make changes when asked to diagnose.
- **Commit between meaningful steps.** A working state is worth checkpointing. Saved us multiple times already.

### Language

- New code comments: **English**.
- Older code comments: often Turkish — leave alone unless misleading.

### Don't Save Before Discarding

If discarding uncommitted Unity changes, close Unity with "Don't Save" first.

---

## Known Issues / Deferred Work

### Architecture (planned)

- **PlayerHealth extraction** — pull HP, damage, death, knockback, invincibility, fall-respawn into its own component. Highest-leverage refactor after the CardActionExecutor pass.
- **PortalController extraction** — `firstPortalInstance` mid-card state currently lives on PlayerController; should be its own component with proper cleanup on room/death.
- **GravityController extraction** — full ReverseGravity routine, `visualFlipYOffset` machinery, the gravity-reversal warning flash (currently broken).
- **CardActionExecutor conflict-tracking completion** — Phase, Adrenaline, and ReverseGravity helpers need restructuring to return IEnumerator so the executor can track them in `runningEffects`. This is the real fix for the Phase/ReverseGravity gravity-corruption bug.
- **CameraPeek rebuild** — currently broken (Left Ctrl dead). Rebuild as `CameraFollow` offset, like CameraShake.
- **Manager dependency graph** — undocumented. Long-term docs task.
- **QuestSystem DontDestroyOnLoad inconsistency** — pending scene-flow design decision.

### Developer Experience (planned)

- **In-game debug console** — text input overlay with commands like `give relic X`, `spawn enemy Y`, `set shift 99`, `play card Z`, `toggle gravity`. Grown-up version of DebugTools.cs hotkeys. Pays off recursively for every test session.
- **Runtime tuning panel** — in-game dropdown listing cards, sliders for their tunable fields (damage, duration, cost, radius), apply button. Cuts the prompt → wait → review → test cycle for number tweaks. Lower priority than the debug console; really pays off once card design pace picks up.

### Future: Slot-Constrained Relic Redesign (MAJOR DESIGN DIRECTION)

The current relic system (SOTS-style additive, unlimited) is slated to be replaced with a Balatro-style slot-constrained system.

**Design intent:** fixed slots (~5), sell to make room, bigger interactive effects, real acquisition decisions. Extends "Movement is a Resource" to relics.

**Scope when undertaken (multi-session):**
- New data model (slots, sell prices)
- Rework of RelicManager and RelicHUD into a slot manager UI
- Rebalance or redesign of existing relics
- 15-25 new relics
- Economy tuning
- Possibly new acquisition events (shop vs. pack vs. voucher)

**Until then: don't invest heavily in relic UX or new SOTS-style relics.** Small fixes fine; large investments not.

**Approach when starting:** paper design first, code second.

### Quest System Expansion

- Wire `NoDamageRoom`, `GoldAccumulate`, `UseCardCount` quest types.
- Add card-reward type.
- **Rich Man's Dagger card** — damage based on player gold. Needs design pass.
- Defer reward delivery to level-end (hook: `RewardManager.SelectCard` before `SpawnNextRoom`).
- Randomize `GenerateQuests()`.
- Enforce 3-quest cap on `AcceptQuest`.
- Visual feedback on quest accept.
- Wire QuestBoard's `SimpleInteract.prompt` field for the "press E" hint.

### Scene Flow

- Player starts in hub from main menu, transitions to runs, returns after death.
- Currently hacked: hub is `LevelManager.roomPrefabs[0]` and first-room logic forces it.
- When implemented: review every manager for `DontDestroyOnLoad` needs.

### Card Polish (deferred from this session)

- **Fireball instant-spawn** — user wants the projectile spawned the moment the card is played, not 0.36s in. Need to either fire-and-forget the animation (keep the 0.36s delay only for visual sync, but spawn immediately) or rework the animation timing. Feel call.
- **Enemy healthbars** — UI elements above enemies showing current/max HP. User flagged this mid-session; needs design for show-always vs. show-on-damage with fade. Worldspace canvas per enemy.

### Bugs (deferred)

- **Card effect conflict (residual):** Phase, Adrenaline, ReverseGravity helpers don't fully participate in `runningEffects` tracking. Phase/ReverseGravity gravity-corruption interaction is still reachable. See "CardActionExecutor conflict-tracking completion" in Architecture.
- **Spike knockback always sends right-up:** ignores incoming angle. Plan: velocity reflection.
- **Duplicate ExitDoor possible in some room prefabs:** defensive guards in place; scene-side duplicate may need cleanup.
- **AnimationEventReceiver may re-enable on prefab reimport.** Disabled twice already.
- **Gravity reversal warning flash is invisible** — relies on SkinnedMeshRenderer + `_Color` that don't apply to current rig. Audio still fires. Rewrite via `_Alpha` MaterialPropertyBlock when touching GravityController.
- **Stun visual tinting uses `.material.color` writes** — wasteful, clones materials. Rewrite to MaterialPropertyBlock when next touched.

### Resolved this session

(Kept for short-term reference; can be deleted once stale.)
- ✅ Phase wall-stuck bug (auto-extends + safety eject)
- ✅ Fall damage removal
- ✅ Comet Dive redesign (area damage, momentum preserved)
- ✅ Head-bounce gravity reversal blindness (all 3 checks fixed)
- ✅ Jump card / Stagger gravity reversal blindness
- ✅ Vampiric Bite layer mismatch
- ✅ Comet Dive trail leak on miss
- ✅ Portal HUD cost display mismatch
- ✅ Glass Wail stun (full system rewrite: IsStunned property + dual renderer arrays)
- ✅ Turret + ShieldEnemy stun guards
- ✅ Phase visibility feedback (alpha pulse via MaterialPropertyBlock + `_Alpha`)
- ✅ CardActionExecutor scaffolding (16 → 12 actions, polymorphic dispatch)
- ✅ Four-card deletion + Dash impulse rewrite + enum integer pinning

### Content (TODO)

- Scale to 60+ cards (currently 12).
- Glass archetype: cards exist in theory, not implemented.
- Expand Vampiric archetype.
- Three-act structure: Act 1 prototype exists; Acts 2-3 not started.
- Boss encounters per act.
- Chunk-based level system.
- Starting relic system + Fireball relic for wizard identity. Deferred when broader relic redesign was prioritized.

### Replace SlotMachine with "Dice Broker"

Character-driven gambling NPC. Same gameplay outcome (random relic). Implementation: roll first, then play animation ending on correct face. Sprite-sheet 6-12 tumble frames.

### Documentation Tasks

- Eventually: proper GDD. Worth doing once relic system, act structure, and card list are locked.

---

## File / Path Reference

- Active scene: `Assets/Scenes/SampleScene.unity`
- Player prefab: `Assets/Prefabs/Player.prefab`
- Scripts: `Assets/Scripts/` (75+ files, flat structure)
- Card actions: `Assets/Scripts/CardActions/Actions/`
- Level prefabs: `Assets/LevelSinasi/*.prefab` and `Assets/LevelEfeS/*.prefab` (hub)
- Quest assets: `Assets/Quests/`
- Relic assets: `Assets/Relics/`
- Card assets: `Assets/Cards/`
- Hub prefab: `Assets/LevelEfeS/hub.prefab`
- Customizable Pixel Character pack: `Assets/Cainos/Customizable Pixel Character/`

---

## When in Doubt

- Ask the user for clarification before sweeping changes.
- Verify scene presence and Inspector wiring before assuming code is broken.
- Read related scripts before refactoring shared systems.
- For visual/UI work, confirm the canvas hierarchy and parenting before moving GameObjects.
- For Animator parameter types, read the `.controller` YAML directly.
- For shader property writes, read the `.shader` file's Properties block — don't assume `_Color` exists.
- The user wants quality over speed. "Make this one of the greats" — push back gently on quick-fix patterns when a slightly larger correct fix is appropriate.
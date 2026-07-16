# Deckshift — Claude Code Context

This file is loaded automatically into Claude Code at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Jumping consumes **Shift**, which does not regenerate on its own — and **Shift CARRIES OVER between rooms** (designer-confirmed 2026-07-13: it is a run-long resource, and this persistence is "the whole identity of the game" — spending Shift now means having less for the rest of the run). Do NOT describe or implement Shift as a per-room resource. Most other actions (attacks, special movement, utility) are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. ~5 hand-crafted levels, ~10 cards in the game. Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete.

The player character was recently swapped from a SkinnedMeshRenderer-based rig (`PF Skeleton - Mage`) to a sprite-based one (`PF Pixel Character - Mage M`) from the Cainos Customizable Pixel Character pack. The wizard identity is now the canonical character. The skeleton remains in the Player prefab disabled, intended for future use as an enemy.

**Active scene:** `Assets/Scenes/SampleScene.unity` (build index 2). Other scene files exist (`GameScene`, `MasterLevel`, `Hub`) but are inactive/legacy. When debugging "is this in the scene?" issues, always check SampleScene first.

---

## Tone & Voice (designer-stated 2026-07-15 — applies to ALL player-facing text)

**Deckshift does not take itself too seriously.** Player-facing names and flavor — relics, cards, items, enemies, quests, UI — should have **personality and a wink**, not dry functional labels. The goal is that players get *attached* to specific things partly because the name is fun ("I love running Loot Goblin"). The world-building is currently thin, so this is where character comes from.

**The line to walk:** playful, NOT a complete joke. Mix registers so it feels like a real world with a sense of humor, not a parody:
- **Cool-with-personality** (the default): evocative names with a slight grin — "Pocket Lightning", "Blood Money", "Pay in Blood", "Glass Heart". These carry the world.
- **Straight-up fun** (sprinkle, don't flood): the occasional pure wink — "Bubble Wrap", "Do Not Pet", "Loot Goblin". These are the ones players quote.
- **Keep the genuinely cool ones cool:** if a name already lands (Phoenix Cog, Executioner's Seal, Meteor Greaves, Glass Heart), leave it — don't jokify everything, or nothing stands out.

Names should still *hint at what the thing does* where possible (First One's Free = first card free; Do Not Pet = touch it and get hurt). Keep mechanical **descriptions** clear and literal — the humor lives in the NAME and any short flavor line, never at the cost of the player understanding the effect. **`relicID` / enum / code identifiers NEVER change for flavor** — only the display `relicName` / `cardName` / description text.

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

1. **No Cinemachine currently in use.** It was removed early due to confiner issues with multi-shape rooms. The custom system is in `CameraFollow.cs` plus per-level `LevelBounds` zones. The policy is "currently removed, can be revisited if a clean approach is found" — not absolute prohibition. `CameraPeek.cs` has since been rebuilt without Cinemachine and works (see Camera System); the Cinemachine package itself is still installed and two dead `using Unity.Cinemachine;` directives remain (`PlayerController.cs`, `LevelManager.cs`) — cleanup pending.

2. **Manager-singleton pattern.** All major systems are singleton MonoBehaviour managers (GameManager, DeckManager, LevelManager, etc.). This pattern has known issues (cyclic dependencies, flat global state) but is the architecture. Do not propose dependency injection, ECS, or other paradigms.

3. **Game runs in a single scene currently.** Most managers do not have `DontDestroyOnLoad`. **QuestSystem's `DontDestroyOnLoad` was REMOVED (2026-06-10): quests are per-run by design and reset on death/restart; each scene uses its own QuestSystem instance, whose serialized UI references match that scene.** If quest meta-progression is ever wanted, persist it through the save system (PlayerPrefs, like AchievementManager) — do not re-add `DontDestroyOnLoad`. If scene transitions are added later, this must be revisited. Do not add `DontDestroyOnLoad` to existing managers without discussing the implications.

4. **Comment language convention.** Older code has Turkish comments (Gemini-era). Going forward, **new comments should be in English** for clarity. Do not retranslate existing Turkish comments unless they're factually misleading.

5. **No assets the user can't afford.** Solo developer with limited budget, no freelance artist. Work with existing Cainos asset packs and pixel-art conventions. Don't propose solutions that require commissioning new art.

6. **Asset pack imports require extreme care.** The Cainos Customizable Pixel Character pack ships as a "complete project" that wants to overwrite `ProjectSettings/`. Always uncheck `ProjectSettings/` in the import dialog and uncheck any duplicate packs you already have. See "Common Pitfalls" for the full story.

---

## Player System

### PlayerController.cs

This is a large script (~1,200 lines). It currently handles movement, jumping, card action execution, gravity reversal, VFX spawning, audio, health, gold, shift, knockback, portal state, cannon enter/exit, death, and respawn.

**Refactor status: the `CardActionExecutor` extraction is DONE.** `ExecuteAction()` is now a one-line delegate to `CardActionExecutor.TryExecute()`. All card actions live in `Assets/Scripts/CardActions/Actions/` as `CardAction` subclasses, registered in a dictionary in `CardActionExecutor.Awake()`. There is no switch statement anymore — do not look for one. The conflict-flag half of the system is only partially built; see "Card Effect Conflict Class of Bug" below for the audited current state.

### Player Prefab Specifics

- **Active visual model:** `PF Pixel Character - Mage M` at `Assets/Cainos/Customizable Pixel Character/Prefab/Character Preset/PF Pixel Character - Mage M.prefab`. This is a child of the Player root and is assigned to `PlayerController.visualModel`.
- **Disabled fallback:** `PF Skeleton - Mage` is still parented under Player but disabled (checkbox off). Kept as backup and for future reuse as an enemy.
- **Physics collider:** `CapsuleCollider2D` on the Player root with **Offset (0.122, 0.871) and Size (0.5075, 1.6848)**. Direction: Vertical. These values were honest-refactored from pre-fix design-intent values that had been compounding with a non-(1,1,1) root scale for 9 months. A `BoxCollider2D` was previously present but disabled and has been removed. Do not re-add it.
- **Rigidbody2D:** Dynamic. Gravity scale flips sign during gravity reversal — do NOT modify `Physics2D.gravity` globally.
- **Player root Transform:** Position (0, 0, 0), Rotation (0, 0, 0), **Scale (1, 1, 1)**. This is now a hard rule again — the prior non-(1,1,1) scale was an accidental drift that compounded into a real bug. Do not modify the root scale to adjust character size; scale `visualModel` instead.

### Visual Model Internals (PF Pixel Character - Mage M)

- The visualModel itself is scaled to **(0.8, 0.8, 0.8)** to fit the collider. If the character ever needs to appear larger or smaller, change this value, not the root.
- The prefab has its own root-level scripts (`PixelCharacter`, `PixelCharacterController`, `PixelCharacterInputMouseAndKeyboard`, plus its own Rigidbody2D and BoxCollider2D). When the visualModel was integrated, the controller scripts and physics components were removed; only the `PixelCharacter` (customization) script remains. Do not re-add the removed components.
- The Animator component lives on the child GameObject named `Animator`, found via `GetComponentInChildren<Animator>()`. There is only one Animator in the hierarchy.
- The Animator Controller is `Assets/Cainos/Customizable Pixel Character/Animation/AC Character.controller`.
- **`Cainos.CustomizablePixelCharacter.AnimationEventReceiver` component on the Animator GameObject must remain DISABLED.** It throws NullReferenceExceptions on the built-in footstep animation events (the pack expects a footstep audio system that we don't use). Re-enabling it floods the console with errors during the cast animation.
- **`PlayerAnimEventSink` component (added 2026-07-01) must stay on that same Animator GameObject.** With the Cainos receiver disabled, the pack's ~20 animation events (`OnFootstep`, `OnAttackCast`, etc.) had no receiver, spamming `"'OnFootstep' has no receiver!"` every step. `Assets/Scripts/PlayerAnimEventSink.cs` is a sink with a method for every event name (including the pack's `OnLedgeClimbFinised` typo) so the events land harmlessly. Do NOT delete it or the spam returns. It is no longer fully inert: as of 2026-07-02 its `OnFootstep(AnimationEvent)` relays to `PlayerController.PlayFootstep()` (footstep SFX). The receiver MUST stay on this Animator child (that's where Unity delivers the events); the footstep *fields* (`footstepClips[]`, `footstepVolume`, `footstepPitchRange`) live on `PlayerController` (the player root) per the designer's request. Other event methods remain empty; hook new anim-driven SFX here.

### Animator Parameter Map

PlayerController writes to these parameters on the Animator:

| Parameter        | Type    | Driven by                                                | Purpose                                            |
|------------------|---------|----------------------------------------------------------|----------------------------------------------------|
| `MoveBlendX`     | Float   | `UpdateAnimations()` — 0 idle / `locomotionPose` moving  | Locomotion pose blend: idle(0)/walk(1)/run(3)      |
| `MoveSpeedMul`   | Float   | `UpdateAnimations()` — `speed * animCadenceScale` clamped | Scales walk-cycle PLAYBACK to real ground speed (kills foot-slide) |
| `VelocityY`      | Float   | `UpdateAnimations()` — `rb.linearVelocity.y`             | Jump/fall vertical state                           |
| `IsGrounded`     | Bool    | `UpdateAnimations()`                                     | Land/airborne distinction                          |
| `InjuredFront`   | Trigger | `TakeDamage()` on every damage hit                       | Hurt reaction                                      |
| `IsDead`         | Bool    | `Die()` set to true                                      | Death state                                        |
| `AttackAction`   | **Int** | `FireballCastRoutine` sets to **14**                     | Dispatch value selecting which attack animation    |
| `IsAttacking`    | Bool    | `FireballCastRoutine` toggles                            | Gate for AttackAction transitions                  |

**Critical:** `AttackAction` is an **Int**, not a Float. Calling `SetFloat("AttackAction", ...)` throws a runtime type mismatch. Use `SetInteger("AttackAction", value)`.

### Cast Animation (Fireball Card)

The Cainos Animator Controller has a "Cast" animation at `AttackAction == 14`, playing on both the "Attack Action - Arm" and "Attack Action - Body" layers simultaneously. The clip is 1.0 seconds long and self-exits at ~80% via unconditional ExitTime.

`PlayerController.FireballCastRoutine` (~line 800-826):
1. Sets `IsAttacking = true` and `AttackAction = 14`.
2. Waits **0.36 seconds** — this is the `OnAttackCast` animation event timestamp authored by Cainos themselves, the designer's intended projectile release frame.
3. Calls `PerformFireball(value)` to spawn the projectile.
4. Waits an additional 0.15 seconds, then sets `IsAttacking = false`.

The animation will self-exit even if the bool isn't released, but releasing it explicitly prevents an Empty→Cast re-trigger loop.

**Cainos's own attack system (idle, unused):** The pack prefab has a `CharacterBehaviour` script on its root with an `attackAction` field (set to 14 in Mage M preset) and UnityEvent callbacks `onAttackCast`, `onAttackStart`, `onAttackEnd`. These exist but are not currently wired. If perfect frame-accurate cast spawn timing becomes a priority, hooking into `onAttackCast` via an Animation Event is the right path — but not today.

### Ground / Wall / Ceiling Detection

The player has check Transforms parented to the player root (NOT to visualModel). Post-refactor honest values:

- **`groundCheck`** at local (0, 0.015, 0). Used for normal grounded detection via `Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)`.
- **`wallCheck`** at local (0, -0.00975, 0). For horizontal collision detection.
- **`ceilingCheck`** at local (0, 1.725, 0) — added during gravity reversal work. Used when `isGravityReversed` is true.
- **`firepoint`** at local Position (0.499, 1.263, 0), Scale (1, 1, 1). Fireball/bite origin point.

`IsGroundedCheck()` switches probe based on `isGravityReversed`. The original implementation used a mirror-math formula (`2 * pivot - groundCheck.position`) but that was fragile (only 0.16 units of overlap margin); the dedicated `ceilingCheck` Transform replaced it.

`groundLayer` mask is `2057` = layers 0 (Default), 3 (level geometry), 11. Level geometry pieces are on layer 3. Enemies are mixed: **AeroBat and MeleeEnemy are on the Default layer; RangedEnemy is on the Enemy layer.** This inconsistency is a known issue but currently load-bearing.

### Gravity Reversal System

Triggered by the "Floor is Lava" card (`CardActionType.ReverseGravity`). Lasts 5 seconds with a 0.5s warning flash + audio cue before expiration.

Key fields on PlayerController:
- `isGravityReversed` — runtime flag
- `originalGravityScale` — cached at effect start, restored at end
- `gravityReversalCoroutine` — reference for stop-and-restart on re-play
- **`visualFlipYOffset`** — serialized field, current value **1.6875** (tuned in Inspector after the scale refactor). Translates visualModel up so the 180° rotation pivots around the collider center instead of the feet.
- `originalVisualLocalPos`, `originalVisualScaleX` — cached for restoration
- `warningSoundClip` — AudioClip Inspector field, played at t=4.5s

`GravityReversalRoutine()` handles the full timeline. `LerpVisualTransform` uses a tracked Z-angle float (never reads back from `localEulerAngles`, which Unity normalizes unpredictably).

The 0.5s **warning flash** is implemented against `SkinnedMeshRenderer` via `material.SetColor("_Color", ...)`. The new visualModel uses SpriteRenderers, so `GetComponentInChildren<SkinnedMeshRenderer>()` returns null and the flash silently no-ops. **The expiration audio cue still plays**, so the warning is still audible — just not visible. Fixing this would require a SpriteRenderer flash path; not done yet, low priority.

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
2. Create a `CardAction` subclass in `Assets/Scripts/CardActions/Actions/` and register it in the dictionary in `CardActionExecutor.Awake()`. Declare an honest `ModifiedState` (ConflictFlags) for any state the action touches.
3. Create a `CardData` asset in Unity (right-click in Project view → Create → Card Data).
4. Set the asset's `actionType`, `maxUses`, `shiftCost`, sprite, etc. in the Inspector.
5. Add the card to the relevant reward pools / starter deck as needed.

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh action — costs Shift, redraws the hand, cost increases each use within a level.

### Stagger Mechanic

When Shift is 0 AND no playable cards exist, a Stagger card is auto-added to the hand. Three Stagger plays in one run = death.

### Card Effect Conflict Class of Bug (KNOWN)

Discovered when hub mode allowed free card spamming: playing multiple state-modifying cards in close succession (e.g., Floor is Lava + Adrenaline + Phase) can leave the player in a permanently broken state (flying, frozen gravity, etc.). Each card's effect captures "original" state at start and restores it at end, but **none of them know about each other**. Card A captures the current state (already modified by still-active Card B), then later restores to that mid-effect snapshot — corrupting baseline.

**Current state (updated 2026-07-06): RESOLVED.** The CardActionExecutor extraction is done AND conflict-flag enforcement is live. Each `CardAction` declares a `ModifiedState` (`ConflictFlags`); the executor accumulates flags in `activeFlags` (via `ManagedCoroutine` for coroutine actions, via `SetManualFlag` for the manual-lifecycle ones) and **`TryExecute` now checks them: if an action's `ModifiedState` overlaps `activeFlags`, it is refused up front with `CardExecuteResult.Blocked` and none of its code runs.** A blocked play costs no Shift and no charge, and the card stays in hand (`DeckManager.PlayCard` only spends/consumes on `Success`). The state-corruption bug class (Floor is Lava + Adrenaline + Phase leaving the player flying/frozen) can no longer occur — the conflicting second card is refused instead of corrupting the baseline snapshot.

Per-effect conversion status:
- **Dash** ✅ converted — managed coroutine; flags `PlayerVelocity | Invincibility` held for the whole dash. **Reworked 2026-07-06 into a driven dash** (`PlayerController.DashRoutine`): enters `PlayerState.Dashing` and holds a flat horizontal velocity for `dashDuration` (re-asserted each FixedUpdate with y forced to 0), so it works on the ground too — the old one-shot `AddForce` impulse was erased the next frame by the grounded movement line (`rb.linearVelocity = moveInput * moveSpeed`). Never touches `gravityScale` (composes cleanly with Floor is Lava). Procedural afterimages via `DashAfterimage.cs`; tunables `dashSpeed`/`dashDuration`/`dashEndSpeed`/`dashIFrameDuration`/`dashAfterimages` on PlayerController.
- **Phase** ✅ converted — managed coroutine; flags `GravityScale | LayerCollisionMatrix | PlayerVelocity`.
- **Adrenaline** ✅ converted (manual-flag pattern) — `UseAdrenaline`'s two sub-coroutines are mutually exclusive (`if/else` on health %), and each calls `SetManualFlag(TimeScale | MoveSpeed, …)` at start/end. The old "not refcounted / overlapping plays clear flags early" caveat is now moot: a second Adrenaline play while one is active is Blocked (its flags overlap), so concurrent same-flag effects can't happen.
- **Fireball** ✅ converted — managed coroutine; `AnimatorAttackState`.
- **ReverseGravity** ✅ converted (manual-flag pattern) — `StartGravityReversal`/`GravityReversalRoutine` now call `SetManualFlag(GravityScale | VisualTransform, …)` with a restart-safe lifecycle: flags are cleared BEFORE `StopCoroutine` and re-set synchronously inside the new `StartCoroutine`, so there is never a flags-set-but-no-routine window and the clear can't stomp the new set. The same-card timer-refresh branch is now unreachable (a replay while active is Blocked because its flags overlap `activeFlags`); it's kept deliberately in case the policy later allows same-card refresh.

**Known interaction (found 2026-07-06):** enforcement makes the **Echo Chamber** skill's instant double-cast (`DeckManager.PlayCard` re-calls `ExecuteAction` immediately after the first play) silently no-op for *stateful* cards — the second cast's `ModifiedState` overlaps the first's still-live flags and is Blocked. It still works on instant cards (Jump, Glass Wail, etc.). Fix options if this becomes design-relevant: defer the echo cast until the first effect ends, or let a same-card replay bypass the block. Not yet done — flagged, not urgent.

**Enforcement applies everywhere, including the hub** (where free card spamming used to make this bug trivially reproducible). The class is now handled centrally in `TryExecute`, so there is no need to patch individual cards.

---

## Hub Mode (Sandbox)

The hub is a sandbox room where the player tests cards, jumps freely, and experiments without consequence. The hub prefab is at `Assets/LevelEfeS/hub.prefab`. **It is currently the always-first room in every run** (see "First-Room Logic" under Level System).

### HubMarker Component

`Assets/Scripts/HubMarker.cs` is a marker MonoBehaviour with no fields or methods — its presence on a room prefab's root signals "this is a hub" to the rest of the codebase. Currently attached to the hub prefab's root.

`LevelManager.IsCurrentRoomHub()` returns true if the currently spawned room has a HubMarker. This is the single source of truth.

### Umbrella Rule: No Consumption In Hub

The hub gates every player-resource consumption call. The umbrella principle: **no resource is consumed and no permanent state changes** while the player is in a hub.

Specifically gated:
- Shift consumption from jumping (`PerformJump`)
- Shift consumption from playing cards (`DeckManager.PlayCard` → `player.SpendShift(cost)`)
- Shift consumption from portal second-placement (`TryPlacePortal`)
- Card charge decrement (`playedCard.currentUses--`)
- Card exhaust routing (cards play but don't go to exhaust pile when depleted)
- Recall shift cost (`TryRecall` → `SpendShift`)
- Recall cost escalation (`currentRecallCost++`)
- Stagger card injection (`CheckForStaggerCondition`)
- ~~Fall damage~~ — no longer applicable: fall damage has been removed from the game entirely (`FallAndRespawn` only teleports; see Resolved bugs)

All guards use the pattern: `if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub()) { ... do the consumption ... }`.

**When adding new player-resource consumption code,** check whether it should also be gated by `IsCurrentRoomHub()`. The pattern is: at every consumption site, ask "should this be free in a sandbox?" — almost always yes.

### What Hub Does NOT Hide

UI is intentionally unchanged in hub. The shift counter, card hand, recall button — all visible and operate normally. Only the underlying mechanics are gated. This is so the hub can act as a tutorial space where the player sees the UI react.

---

## Manager Layer

There are 13+ singleton managers. This is a known architectural smell flagged in audit but currently load-bearing. Do not propose merging or restructuring without explicit user approval.

### List of Managers

- **GameManager** — top-level state, player reference, centralized pause counter
- **DeckManager** — card piles, draw/discard logic
- **LevelManager** — room spawning, transitions, zone/camera setup, first-room-is-hub logic
- **RewardManager** — end-of-level card selection screen
- **RelicManager** — owned relics, `HasRelic(string id)` polling pattern, `OnRelicAdded` event
- **SkillManager**, **SkillRewardManager** — skill tree / skill selection
- **QuestSystem** — quest tracking, board UI, accept/progress/complete events
- **ShopManager** — in-game shop UI and purchases
- **SlotMachineManager** / **SlotMachineUI** — gambling system (planned to be replaced with Dice Broker — see deferred work)
- **AchievementManager** — achievement tracking
- **MenuManager** / **PauseMenu** / **MainMenuController** — menu systems
- **EffectManager** — VFX spawning helper
- **MusicManager** — background music
- **CameraShake**, **HitStop** — game-feel singletons (camera shake + freeze frames)

### Pause Counter System

`GameManager` has a centralized pause counter that any UI/menu system uses instead of writing `Time.timeScale` directly.

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
- **QuestSystem is now scene-local too** — its `DontDestroyOnLoad` was removed 2026-06-10 (quests are per-run by design; the survivor's dead UI references broke the quest board after the first death).
- **`GameManager.instance.player` is accessed from many UI scripts** with inconsistent null guarding. Add null guards when touching these sites.

---

## Quest System

The quest system is **functional**: data model, accept/progress/complete events, board UI, and live tracker HUD all working as of the most recent session.

### Data

- **`QuestData`** (ScriptableObject) — quest templates. Fields: `questName`, `description`, `type` (QuestType enum), `targetAmount`, `rewardText`, `rewardType` (RewardType enum), `rewardAmount`.
- **`QuestType` enum:** `GoldAccumulate`, `KillEnemy`, `AirKill`, `NoDamageRoom`, `UseCardCount`. **Of these, only `KillEnemy` and `AirKill` currently fire events** (from `EnemyHealth.Die()`). The others are defined but unwired.
- **`RewardType` enum:** `Gold`, `ShiftCharge`, `Heal`. All three are wired in `QuestSystem.GiveReward`.
- **Three quest assets currently exist** at `Assets/Quests/`:
  - `New Quest 1` — "Invincible" — NoDamageRoom (1) → 300 Gold. **Objective type not wired, won't progress yet.**
  - `New Quest 2` — "Hit a Clip" — AirKill (3) → +10 Shift. Fully functional.
  - `New Quest 3` — "Bounty Hunter" — KillEnemy (3) → 100 Gold. Fully functional.

### QuestSystem Singleton

Located on a `QuestSystem` GameObject in SampleScene. Holds:
- `allQuests` — list of QuestData assets the board can pull from (currently the 3 above).
- `activeQuests` — `List<ActiveQuest>` (inner serializable class). Each `ActiveQuest` has `data` (QuestData), `currentAmount` (int), `isCompleted` (bool).
- Serialized fields: `overlayPanel` (GameObject), `container` (Transform), `paperPrefab` (GameObject).

Key methods:
- `ToggleBoard()` / `CloseBoard()` — opens/closes the QuestBoardOverlay UI; uses `RequestPause`/`ReleasePause`.
- `GenerateQuests()` — spawns up to 3 QuestPaper prefabs into the container. **Currently always picks the first 3 in `allQuests` — no randomization.**
- `AcceptQuest(QuestData)` — adds to `activeQuests` (deduplicates by data reference), fires `OnQuestAccepted`.
- `ReportEvent(QuestType, int)` — iterates activeQuests, increments `currentAmount` on matching quests, fires `OnQuestProgress`, then calls `CheckCompletion`.
- `CheckCompletion(ActiveQuest)` — if `currentAmount >= targetAmount`, sets `isCompleted = true`, fires `OnQuestCompleted`, calls `GiveReward`.
- `GiveReward(QuestData)` — delivers reward immediately. **Not deferred to level-end yet** (on the deferred list).

### Events (for HUDs and other listeners)

```csharp
public event System.Action<ActiveQuest> OnQuestAccepted;   // fired after successful add (not on duplicate-accept)
public event System.Action<ActiveQuest> OnQuestProgress;   // fired after currentAmount++, before CheckCompletion
public event System.Action<ActiveQuest> OnQuestCompleted;  // fired after isCompleted=true, before GiveReward
```

### Quest Board UI

Lives under `Canvas` as `QuestBoardOverlay` → `Panel` → (`QuestContainer`, `LeaveButton`). The board uses a hand-painted background sprite with three painted parchment slots and three painted ACCEPT buttons; the spawned `QuestItemTemplate` prefabs (one per quest) sit inside those painted slots with transparent backgrounds, and the invisible Accept buttons inside each QuestPaper are sized to overlay the painted ACCEPT graphics. The Leave button is also an invisible button over a painted graphic.

The QuestBoard in `Assets/LevelEfeS/hub.prefab` has a `SimpleInteract` component on it (implements `IInteractable`) that calls `QuestSystem.ToggleBoard()` on player interact (press E within `interactionRange`). The board's Layer must be in PlayerController's `interactableLayer` mask. Currently the mask is set to "Interactable" only, and the QuestBoard is on Layer 12. **Verify in Inspector that Layer 12 corresponds to Interactable, or that interactableLayer includes both.**

### Live Tracker HUD (QuestTrackerHUD)

`Assets/Scripts/QuestTrackerHUD.cs`, attached to a `QuestTracker` GameObject under `Canvas/GameplayHUD/`, top-right of screen. Subscribes to the three QuestSystem events. Maintains a `Dictionary<ActiveQuest, GameObject>` mapping quests to their instantiated row GameObjects.

- Row prefab: `Assets/Prefabs/QuestRowPrefab.prefab`. Two TMP children named exactly `Title` and `Progress` (case-sensitive).
- On accept: instantiate row, set Title to quest name, set Progress to "0/X".
- On progress: update the row's Progress text to "current/target".
- On complete: destroy the row.

Because the tracker is parented under GameplayHUD, it inherits the auto-hide behavior when Shop / SlotMachine / QuestBoard open.

### Known Quest Pitfall (Resolved)

`QuestPaper.OnAccept` previously crashed at line 32 trying to assign text to a TextMeshProUGUI child that didn't exist on the Accept button (the button was stripped of its text label during UI styling). The crash happened BEFORE `QuestSystem.AcceptQuest` was called, so the quest never actually got added and the event never fired. Fixed by null-guarding the GetComponentInChildren result. If you ever see a quest accept silently fail again, check the error trace for `QuestPaper.OnAccept` first.

---

## Relic System

Currently a Slay-the-Spire-style additive system: every relic is a passive bonus, the player can own unlimited relics, no slot constraints. **This is slated for a major redesign — see "Future: Slot-Constrained Relic Redesign" in the deferred work section.** Do not invest heavily in new relic content or relic UX features until the redesign happens; that work will likely be reworked.

### RelicManager

Singleton. Holds:
- `ownedRelics` — private list of owned `RelicData`.
- Public `OwnedRelics` — `IReadOnlyList<RelicData>` accessor.
- Public event `OnRelicAdded` — `System.Action<RelicData>`, fired after a successful add (not on duplicate-add).

Grant paths (only two are accessible in normal play):
- `ShopItemUI` — buying a shop item with a relic reference.
- `SlotMachineUI` — slot machine payout.
- `DebugTools.cs` F1 key — debug only.

**No starting-relic infrastructure exists yet.** Every run begins with zero relics. Adding a starting relic system (e.g., a wizard who begins with a Fireball relic) is on the deferred list.

### RelicData ScriptableObject

Fields: `relicID` (string, used for `HasRelic` polling), `relicName`, `description`, `relicArt` (Sprite, used by the HUD), `rarity` (enum).

Current relic assets at `Assets/Relics/`:
- `VampireTooth` — kills heal 5 HP. Wired in `RelicManager.OnEnemyKilled`.
- `Kinetic` — kills grant +2 Shift. Wired.
- `SpikedCarapac` — taking damage reflects 20 to nearby enemies. Wired.
- `Pogo Boots` — head-bounce on enemies. Wired in `PlayerController` (see Enemy System).
- `LavaBoots` — protects from hazard zones. Wired in `HazardZone.cs`.
- `New Relic 1` ("Oops! All 7's", Legendary) — no behavior, placeholder.
- `Helly` (Common) — no behavior, placeholder/junk.

### Relic HUD (RelicHUD.cs)

`Assets/Scripts/RelicHUD.cs`, attached to a `RelicHUD` GameObject under `Canvas/GameplayHUD/`, anchored middle-left, vertical column.

- Subscribes to `RelicManager.OnRelicAdded` in `Start()`.
- Also iterates existing `RelicManager.instance.OwnedRelics` at Start so it populates on late wake-up.
- Each new relic instantiates `RelicIconPrefab.prefab` (unchanged 48×48 root) as a child, then `AddComponent<RelicIcon>().Build(relic)` styles it (2026-07-02). **`RelicIcon.cs`** disables the root Image and builds 4 procedural UGUI child Images back-to-front — rarity **glow** aura / dark rounded **plate** / **icon** art (`relicArt`, preserveAspect) / rarity **frame** border — with the SAME rarity colour language as the chest burst (Legendary gold / Epic purple / Rare blue / Common pale-grey). Pop-in is **Update-driven** (EaseOutBack, unscaled) so it survives being built while GameplayHUD is inactive (relics granted from a hidden shop/slot pop in when the HUD reshows); Epic/Legendary get an idle glow pulse.
- Still visual-only: no tooltip, no activation flash, no interaction (deliberate — see the slot-constrained relic redesign caveat below; don't invest further in relic UX until that lands).

---

## UI System

### Canvas Hierarchy

SampleScene's main Canvas contains:
- **`GameplayHUD`** — contains all in-game HUD elements (gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer trigger zone, **RelicHUD**, **QuestTracker**). Toggle with `SetActive(false)` to hide HUD during full-screen UI.
- **`QuestBoardOverlay`** — quest board panel (full-screen).
- Various menu panels (PauseMenu, ShopUI, SlotMachineUI, RewardScreen, etc.) as direct children of Canvas.

**When adding new full-screen UI panels**, hide GameplayHUD when they open by adding a `[SerializeField] GameObject gameplayHUD;` reference and toggling SetActive. ShopManager, SlotMachineUI, and QuestSystem already follow this pattern.

### Never Scale UI Containers — Resize Them

When a UI element needs to be bigger or smaller, **change Width and Height in the RectTransform, not Scale.** Scaling a UI container cascades to children and fights with Layout Groups, producing wildly incorrect sizes (twice during the last session we hit this — once with the RelicHUD container scaled 5.44× on Y, once nearly happened with the QuestBoardOverlay). The honest fix is always Width/Height, sometimes anchor/pivot. Leave Scale at (1, 1, 1) on UI elements.

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

Replaces Cinemachine for the main follow camera. Each level prefab contains a **`CameraBounds`** child GameObject with `BoxCollider2D` zone children (the shared `Assets/Prefabs/CameraBounds.prefab` carries one zone collider on its root). `LevelManager` finds it via `transform.Find("CameraBounds")` on spawn and passes the zones to `CameraFollow`.

- Camera clamps to the zone the player is currently in.
- Zone transitions use hysteresis (zone doesn't change until player leaves current zone).
- No lerp on zone transition — direct follow (lerp was tried, caused jitter).

**Naming is case-sensitive:** the child must be named exactly **`CameraBounds`** — verified against `LevelManager.cs` (`Find("CameraBounds")`) and the real level prefabs on 2026-07-13. (An earlier version of this file claimed the name was `LevelBounds`; that was stale/backwards — `LevelBounds` appears nowhere in the codebase.)

### CameraShake.cs

Rewritten to work without Cinemachine. Uses a `shakeOffset` Vector2 that `CameraFollow.LateUpdate` adds to the final clamped position (so shake can briefly push past zone bounds, which feels correct).

- Uses `unscaledDeltaTime` so shake still plays during HitStop freezes.
- Call sites: `CameraShake.instance.Shake(duration, intensity)`. Always null-guard `instance`.

**The CameraShake component must be present in the active scene** (on the Main Camera) and **enabled**. If it's missing or disabled, every Shake call silently no-ops. This caused a 9-month "no shake anywhere" bug that wasn't discovered until the audit.

### CameraPeek.cs (REBUILT — working, verified by code audit 2026-06-10)

Rebuilt without Cinemachine, along the planned CameraShake-style design: holding Left Ctrl computes a mouse-direction `peekOffset` (clamped to `maxOffset`, smoothed with unscaled time) that `CameraFollow.LateUpdate` adds after zone clamping. Input is blocked while paused, while the hand drawer is locked, or when the player is dead. If peek "doesn't seem to work," verify scene presence and enabled state of the component first (per Common Pitfalls) — the code is fine. Note: the rebuilt CameraPeek does NOT set `PlayerController.isPeeking`; that flag is dead code.

Related: a missing-script warning for `CameraBoundsController` appears in the console at scene load — this is part of the same Cinemachine-era cleanup that's pending. Cosmetic; doesn't affect gameplay.

---

## Level System

### LEVEL DESIGN LAWS (designer-stated 2026-07-14 — absolute)

1. **Every level must be completable with ONLY jumping and moving.** Cards, fans, elevators, trapdoors, and any other mechanic may only gate OPTIONAL things: loot, shortcuts, Shift savings. If a mechanic fails or the player has no cards, the exit must still be reachable. (Violation that prompted this rule: GenLevel3's first draft made a fan relay the only way over a tall wall.)
2. Mandatory-path geometry (**recalibrated from designer playtest 2026-07-14: the character jumps ~5-6 tiles**, not the 4 the old physics math said): design mandatory rises at **4** (comfortable), 5 only for optional challenge, card-gated pockets need rises ≥ 8. Flat gaps ≤ 5-6 tiles. ≥ 5 tiles of clear air above launch surfaces. **Don't crowd platforms** — same-column vertical spacing between floating ledges ≥ 7 tiles; GenLevel5's 3-tile ladder spacing read as clutter.
3. Hazard pits on the mandatory path must be escapable (shallow enough to jump out) and crossable without aid platforms.
4. **NO one-way (`=`) platforms in levels** (designer 2026-07-14: "they feel wrong and also work bad and buggy, and there is no visual clearance for them"). The importer still supports `=` but don't place it — use solid 1-thick `#` strips (the `Extra_112/113/114` platform-strip look) and route jumps AROUND them, zig-zag ladder style on alternating shaft walls.
5. **Turrets (`t`) only on walls or ceilings** — that's how the hand-made levels use them, so they're hard to kill. The importer can only floor-ground them, so generated levels must NOT use `t` at all; use a melee (`m`) or ranged (`r`) enemy instead. (Designer 2026-07-14, after GenLevel5's exposed floor turret.)
6. The player has **no wall-breaking attack** (fireballs don't break walls) — never design a secret that requires destroying terrain. Card-gated secrets = Phase through a 1-thick wall, Portal, or an 8+ tile rise.
7. **Entry and exit must be far apart in the map** (designer 2026-07-14, after GenLevel6 v1 put the exit directly above the spawn behind a 2-thick slab): a Phase/Portal card must never be able to skip the level. Keep the spawn and the ExitDoor in different regions — roughly 20+ tiles apart, separated by whole chambers of solid rock, never by a thin wall or single floor slab.

### Level Text Importer (NEW 2026-07-13 — Stage 1)

`Assets/Scripts/Editor/LevelTextImporter.cs` adds menu **Deckshift → Import Level From Text…**: it reads an ASCII grid `.txt` (legend + example: `Assets/LevelTexts/TestRoom1.txt`) and builds a room prefab into `Assets/LevelGenerated/` satisfying the room contract (`CameraBounds` zone auto-sized to the grid, `GirisNoktasi` spawn, ExitDoor). Markers: `#` ground, `S` spawn (exactly one), `X` exit, `m/r/l/M/b` enemies (`b` = `YeniLeveller/BatMan.prefab` — the real flying bat with AeroBatAI; **`Assets/Prefabs/AeroBat.prefab` is a legacy husk with NO AI**, its dead missing-script component was removed 2026-07-13 because Unity refuses to save any new prefab containing missing scripts, which broke level import), `^/T/W` hazards, `+/g/C` pickups, and mechanics (added 2026-07-13): `E` Elevator (Cainos prop, floats at cell center — tune travel in Inspector), `F` UpdraftFan (draft zone ~3 tall, liftForce 20 ≈ 5-7 tiles of lift — chain fans as relays for taller climbs), `w` AcidWater (~6 wide pool, damage+slow), `K` WreckingBall (floats at cell center, tune anchor/swing), `c` CrumblingPlatform (**do NOT use in levels — its sprites are outdated; use `T` Trapdoor instead, designer 2026-07-14**), `t` Taret turret, `$` Shopkeeper_NPC (its TMP/UI scripts live in Library/PackageCache — an Assets-only guid scan wrongly flags them "missing").

**Interactive structure markers (2026-07-14):** `=` one-way platform tiles (own tilemap: TilemapCollider2D via CompositeCollider2D + one-way PlatformEffector2D on Ground layer; painted with the thin `_144` lip so they read differently from solid strips) · `G` gate cells (vertical G-runs become one sliding **Gate** — `Assets/Scripts/Gate.cs`, solid Ground-layer collider, slides down + fades on Open, Cainos Gate 01 sprite scaled to height) · `L` Lever (`YeniLeveller/Lever.prefab`; its `OnFlippedOn/Off` UnityEvents are now public) · `A` **Shift Altar** (`Assets/Scripts/ShiftAltar.cs`: IInteractable on the Interactable layer (12), pays `shiftCost` Shift via `player.SpendShift`, free in hub per the umbrella rule, procedural floating TMP cost label, fires public `OnPaid`). **The importer auto-wires each `L` and `A` to its NEAREST `G` gate** (lever On→Open/Off→Close, altar OnPaid→Open) via `UnityEventTools.AddPersistentListener` — rewire in Inspector if a level needs different pairing. Only header directive besides `!backwall` is `!name`. The importer pre-checks for missing scripts before saving and names the culprit object.

**Tile painting reproduces the hand-built visual language** (learned by auditing EfeVrl7's 546 painted tiles, 2026-07-13): an optional "BackWall" backdrop tilemap (**opt-in via `!backwall: on`** — the designer prefers adding backdrop/decoration by hand; when on it must be on the **"Background" sorting LAYER**, NOT Default: ExitDoor's sprite is Default order -1 and gets swallowed by a Default-layer backdrop), plus a "Ground" tilemap (layer 3, TilemapCollider2D, Default sortingOrder 1, z=1). Any 1-tile-thick run (air above AND below, wall-attached or floating) gets the `_112/_113/_114` strip treatment with caps on open ends; the gappy `_186` fill goes in exactly ONE row under a surface, deeper cells get dark `_185` (repeating `_186` looks like a broken colonnade). Frame cells (`#` connected to the grid edge) get role tiles from `Assets/LevelSinasi/biseyler/`: air-above → floor surface `_144`, air-below → ceiling face `_96`, wall faces → inner accent tiles `_188`/`_157` ONLY when backed by a real solid tile (2-thick walls), else the clean outer tiles `_189`/`_156` (the inner tiles have protruding brick nubs + bumpy collision — wrong for 1-thick walls), buried → `_153/_154` top rows, `_156/_189` outer walls, `_186/_185` floor fill. Free-standing `#` platforms: horizontal runs of 2+ get the **platform strip set `Extra_112/_113/_114`** (left cap / middle / right cap — learned from EfeVrl6's interior platforms); lone blocks and 1-wide pillars get chunky `Ground Dirt` block tiles (`#..#..#` = the hand-made stepping-stone style); buried rows of thick platforms get floor fill. NOTE: the edge-strip tiles look like sparse floating crumbs if painted in mid-air, and adjacent Dirt blocks melt into dark blobs — never tile either as strips.

**Entity placement:** most enemies have kinematic physics and do NOT fall, so the importer auto-grounds standing markers (`X m r l M C ^ W T` + the spawn): after instantiating, it measures the instance's combined renderer bounds (ignoring particles/trails, collider fallback) and shifts it so bounds-bottom sits exactly on the cell floor. Floaty pickups (`+ g`) and flyers (`b`) stay at cell center. Decoration (props) stays a manual pass by design. Planned next stages: movement-metrics doc (jump/dash distances in tiles) then batch room drafting.

### Room Pool

`LevelManager.roomPrefabs` holds the pool of room prefabs. **Element 0 must be the hub;** elements 1..n are the run's combat levels. The boss room is NOT in this list — it has its own `bossRoomPrefab` slot.

### Run Order — finite run: hub → levels → boss (reworked 2026-07-02)

`LevelManager` was changed from an endless-refill pool (which repeated the same level forever) into a **finite, structured run**. Driven by `PickNextRoomPrefab()`:

1. **First room is always the hub** (`roomPrefabs[0]`), and `BuildLevelQueue()` fills `availableRoomIndices` with indices `1..n`.
2. **Then every other pool level, once each, in random order (no repeats)** — pulled from `availableRoomIndices` until empty.
3. **Pool exhausted → the boss room** (`bossRoomPrefab`, gated by a `bossSpawned` flag so it only happens once).
4. **After the boss (or if no boss is assigned) → reset the flags and loop back to the hub** for a fresh run.

So a run is: **hub → each combat level once (random) → boss → (loop to hub)**. The old `RefillRoomPool()` and index-stripping logic are gone; `hasSpawnedFirstRoom` + `bossSpawned` are the state.

**Inspector requirements:** assign the BossRoom prefab to the new **`Boss Room Prefab`** slot (and REMOVE it from `roomPrefabs` if it was ever in the pool). The boss room prefab must satisfy the same room contract as every other room — a **`CameraBounds`** child (zone `BoxCollider2D`s) and a **`GirisNoktasi`** entry-point child — or the camera/spawn won't set up. Leaving `Boss Room Prefab` empty just loops hub→levels→hub.

If/when proper scene flow gets built (player starts in hub from main menu, returns after death/run completion), this loop-back should be revisited.

---

## Enemy System

### Card & Enemy Numbers — see `CardAnchors.md`

All card and enemy numbers derive from the anchor table in **`CardAnchors.md`** (project root, 2026-07-15). Key facts: damage unit = **15** (one Fireball); **player starts with 40 Shift** (Player.prefab overrides the `maxShift = 3` script default — do NOT treat Shift as scarce at base; lowering the pool is the planned ascension difficulty knob); enemy HP is tiered so **fodder ≈ 12 HP dies to one Fireball**, up to Moss Knight 300. Early "fodder" enemies are being built from the Cainos zombie prefabs (**Shambler** first). Two open art/tuning TODOs live in that doc:
- **ShieldEnemy has no sprite** → it's unused in levels. Compose one from the Cainos packs (armored humanoid + shield prop) when convenient. The enemy *logic* works; it's purely missing art.
- **Fireball sails over short enemies** (slimes/mimics): its collider is a tiny 0.137-radius circle spawned at wand height. Fix = bigger collider + lower launch, tuned so the hitbox bottom sits between floor and enemy chest (can't be full-tall or it explodes on the floor).

### Pattern

- **`EnemyHealth`** base script — handles damage, flash, death, drops. **Currently the only callsite that reports KillEnemy/AirKill to QuestSystem.** `Die()` calls `RelicManager.OnEnemyKilled()`, `QuestSystem.ReportEvent(QuestType.KillEnemy, 1)`, and (if airborne) `QuestSystem.ReportEvent(QuestType.AirKill, 1)`. It now also exposes C# events: **`OnDamaged`**, **`OnDamagedAmount(float)`** (carries the hit size — the boss flinches on big hits), and **`OnDied`** (fired inside `Die()` right before the GameObject is destroyed — the boss uses it to hand music back and to spawn its death VFX). **CRITICAL: `Die()` fires `OnDied` and then `Destroy(gameObject)` in the SAME frame**, so an `OnDied` handler must NOT rely on the enemy surviving — anything that needs to outlive the death (VFX, loot) has to run on its own separate object (see `BossDeathVFX`). Non-event death consequences are still direct calls inside `Die()`.
- **AeroBat (BatMan)** — uses Cainos pack visual + custom `AeroBatAI`. Parent has Kinematic Rigidbody2D + Polygon trigger collider. Raycast LOS aimed at player chest (+0.5 Y), shortened by 0.3 to avoid hitting tile at player's feet. State machine: Idle → Preparing → Diving → Returning.
- **MeleeEnemy**, **RangedEnemy** — based on Cainos pack patterns.

**`TakeDamage(float damage, Transform damageSource = null)` does not currently track damage source.** Spike or hazard kills would credit the player's kill counter the same as direct kills. Minor concern; flag if it becomes design-relevant.

### Layer Convention Mismatch (Known Issue)

- **AeroBat, MeleeEnemy:** on the **Default** layer (0).
- **RangedEnemy:** on the **Enemy** layer.

Many systems check via `enemyLayer` mask, which misses Default-layer enemies. The workaround in PlayerController is to use `GetComponentInParent<EnemyHealth>()` instead of relying on layer masks for head-bounce detection. **Be aware of this when adding new enemies — pick a layer and stick with it, or use the EnemyHealth-component approach.**

### Head Bounce (Pogo Boots Relic)

- 8 damage, `defaultJumpForce * 0.7f` upward force, 0.1s camera shake, 0.3s cooldown.
- Gated behind `RelicManager.HasRelic("PogoBoots")`.
- Uses both `OnCollisionEnter2D` and `OnTriggerEnter2D` (AeroBat has trigger collider, others have solid).
- Contact normal check: `contact.normal.y > 0.7`.

**Known gap:** the velocity sign check (`rb.linearVelocity.y < -0.1f`) doesn't account for gravity reversal. Will silently fail during reversed-gravity head-bounce attempts. Low priority — gravity reversal duration is short and head-bouncing during it is an edge case.

### Enemy Healthbars (EnemyHealthBar.cs + EnemyHealthBar.prefab)

Wired and working across all six enemy types (AeroBat, MeleeEnemy, RangedEnemy, ShieldEnemy, Turret, PatrolEnemy).

**Architecture:** `EnemyHealth` instantiates `healthBarPrefab` (assigned per-enemy in Inspector) in `Start()`, calls `Initialize(transform, headBarOffset, computedWidth)`. The bar parents itself to nothing (free in world space), follows the enemy via its own `LateUpdate`, and is destroyed in `Die()` before the enemy GameObject. Width is computed from `Collider2D.bounds.size.x * 1.2`. `EnemyHealth.headBarOffset` is the per-enemy Y offset; tune in Inspector if the bar sits in the middle of the model instead of above its head.

**The prefab itself is intentionally near-empty:** `Assets/Prefabs/UI/EnemyHealthBar.prefab` has only a `RectTransform` + the `EnemyHealthBar` MonoBehaviour. `BuildCanvas()` in Awake constructs the Canvas, CanvasGroup, border Image, FillImmediate (dark red, snaps), FillDelayed (orange, lerps), and HealthText (TMP) procedurally. WorldSpace canvas at `CANVAS_SCALE = 0.01f`.

**Two pitfalls already hit and fixed — do not regress:**

1. **`UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` does NOT work at runtime.** Returns null with logged errors. The current solution: `EnemyHealthBar` builds a 1×1 white sprite procedurally in a static `GetWhiteSprite()` helper (cached in `cachedWhiteSprite`), assigned to every Image's `sprite` field in `MakeChildImage`. **Required for `fillAmount` to render** — Filled-mode Images with no sprite silently ignore fillAmount and just render as flat colored rectangles.
2. **Sorting fallback for SkinnedMeshRenderer enemies.** `Initialize` first checks for SpriteRenderer (for any future sprite-based enemies), then falls back to SkinnedMeshRenderer for Cainos-based rigs. Without this fallback, AeroBat/MeleeEnemy/RangedEnemy/etc. would stay at default `sortingOrder = 100` regardless of their actual rendering layer.

**Settings integration:** `EnemyHealthBar` subscribes to `SettingsMenu.OnShowNumbersChanged` and reads `PlayerPrefs.GetInt("ShowEnemyNumbers", 1)` on start. Only the text label toggles; bar visuals always render.

**Shield-block damage leak (RESOLVED — verified by code audit 2026-06-10):** `EnemyHealth.TakeDamage` now runs the `shield.IsBlocking()` check and returns BEFORE deducting health. Blocked hits no longer lose HP. Do not re-fix.

---

## Audio System

`MusicManager` handles background music (incl. `PlayBossMusic()`/`StopBossMusic()`).

**There IS a central SFX helper now: `SfxManager` (singleton).** Two static entry points, both multiplying a per-call `localVolume` by a global `SfxManager.Volume`:
- **`SfxManager.PlayOn(AudioSource source, AudioClip clip, float localVolume = 1f)`** — a `PlayOneShot` on a **2D** source you own. Use this for sounds that must be clearly audible regardless of distance (boss abilities, player footsteps, the crusher slam). Because it's a one-shot on a 2D source, `localVolume` can go **past 1** for headroom, and the source can be `.Stop()`ped for looping/sustained sounds (e.g. the boss charge).
- **`SfxManager.PlayAtPoint(AudioClip clip, Vector3 pos, float localVolume = 1f)`** — positional/3D (`PlayClipAtPoint`), **distance-attenuated and clamped to [0,1]**. Fine for small local pickups (gold), but it goes quiet in a big arena and can't be boosted — that's exactly why the crusher slam was switched to a 2D `PlayOn` source with a `[0,2]` slider.

When adding audio cues: expose a `[SerializeField] AudioClip` (+ optional `[Range]` volume) field, and route it through `SfxManager` with a null guard. For a runtime-built source, add an `AudioSource` in code with `playOnAwake=false` and `spatialBlend=0` (2D). Animation-driven SFX (footsteps) come in via `PlayerAnimEventSink` relaying to `PlayerController.PlayFootstep()`; boss ability SFX are frame-synced via the Cainos `AnimationEventReceiver.onAttack` event.

---

## Common Pitfalls (Hard-Won Lessons)

### "Importing an asset pack can overwrite ProjectSettings and break everything"

The Cainos Customizable Pixel Character pack is distributed as a "complete project." Its first import dialog warns about overwriting project settings; the second dialog ("Step 2 of 2: Import Settings Overrides") lists 15+ ProjectSettings files marked **Override**. Accepting these overwrites your URP renderer config (shaders go pink), tags (custom tags vanish), physics (gravity/layer matrix changes), and input bindings.

**Always click "None" on the Step-2 overrides screen.** Always uncheck duplicate Cainos packs you already have in the Step-1 file tree (overwriting shared files in `Common/` can break other Cainos packs too). The pack itself is fine to import once these are excluded.

### "The system exists in code but doesn't work"

Check whether the component is **actually in the scene and enabled**. Multiple times during development, scripts were perfect but the GameObject was missing or the component was disabled. Examples:
- CameraShake was in the scene but disabled for 9 months.
- HitStop was missing from some scenes entirely.
- QuestSystem was missing from SampleScene after the old hub scene was deleted — the script existed and was complete, the manager just wasn't instantiated anywhere.

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

### "Animator parameter type errors are silent until used"

`AC Character.controller` lists `AttackAction` as `m_Type: 3` in YAML, which is **Int**, not Float. The mapping is: 1=Float, 3=Int, 4=Bool, 9=Trigger. When in doubt about an Animator parameter type, read the .controller YAML directly rather than guessing from the parameter's appearance in the Animator window.

### "First diagnostics can be wrong; always verify"

During the character swap session, Claude Code's first diagnostic incorrectly described `AttackAction` as a Float. The error only surfaced at runtime as a type mismatch. **For Animator parameter types specifically, the YAML `m_Type` integer is the source of truth.**

### "Transform.Find is strict and silent"

The QuestTrackerHUD looks for children named exactly `Title` and `Progress` (case-sensitive). A typo, trailing space, or different capitalization causes Transform.Find to return null, and the defensive code skips text assignment silently. When a tracker, popup, or instantiated UI element appears blank, the first thing to check is child naming inside the prefab.

### "GetComponentInChildren can return null"

`acceptButton.GetComponentInChildren<TextMeshProUGUI>().text = "ACCEPTED"` crashes if the button has no TMP descendant. This caused a silent quest-acceptance failure: the exception fired BEFORE the actual AcceptQuest logic ran, so the system looked like "nothing happened on click." Always null-guard before dereferencing GetComponentInChildren results.

---

## Workflow Notes

### Two-Claude Collaboration

The user often consults a separate Claude instance (the conversational one in claude.ai) for design discussion and prompt drafting, then sends prompts to Claude Code for execution. When the user references "what Claude said" or "the other Claude," that's the source. Defer to user intent when their explanation differs from a previous prompt.

### Confirmation Patterns

- Default to small, targeted changes. Refactors require explicit approval.
- When a plan changes scope mid-task ("while I'm in there..."), STOP and confirm with the user.
- For multi-file changes, show the affected file list before making edits.
- Diagnostic-only prompts ("don't fix yet, report") must be respected — never make changes when asked to diagnose only.
- **Commit between meaningful steps.** A working state is worth checkpointing even if more work remains. The discipline of "commit per logical change" has saved the project from cascading errors multiple times.

### Language

- New code comments: **English**.
- Older code comments: often Turkish — leave alone unless misleading.
- User communicates in English now (was Turkish in earlier sessions).

### Don't Save Before Discarding

If the user is about to discard uncommitted Unity changes via GitHub Desktop, **Unity should be closed first with "Don't Save"** on the unsaved-changes prompt. Saving the broken state right before throwing it away is pointless and can interfere with the discard.

---

## Known Issues / Deferred Work

### Architecture (planned, highest priority)

- ~~CardActionExecutor conflict-flag enforcement~~ — **DONE (2026-07-06).** The ExecuteAction() extraction, all per-effect flag registration (incl. ReverseGravity via `SetManualFlag`), AND enforcement in `TryExecute` (Blocked on flag overlap) are complete. The card-effect-conflict bug class is resolved. Only remaining nuance: the Echo Chamber double-cast no-ops on stateful cards (see Card System → Known interaction) — flagged, not urgent.
- ~~CameraPeek rebuild~~ — **done**; rebuilt without Cinemachine (see Camera System).
- **Manager dependency graph** — undocumented. Long-term docs task.
- ~~QuestSystem DontDestroyOnLoad inconsistency~~ — **resolved 2026-06-10**: removed; QuestSystem is scene-local like every other manager, and quests are per-run by design. Quest meta-progression, if ever wanted, should go through the save system (PlayerPrefs, like AchievementManager), not DontDestroyOnLoad.

### Future: Slot-Constrained Relic Redesign (MAJOR DESIGN DIRECTION)

The current relic system follows Slay-the-Spire conventions: strictly additive, free accumulation, every relic is a small passive bonus. **This is slated to be replaced with a Balatro-style slot-constrained system.**

**Design intent:**
- Fixed number of relic slots (probably 5 to start, may tune).
- To acquire a new relic when slots are full, the player must **sell** one of their current relics.
- Each acquisition becomes a real decision (synergy, swap-out math, what to give up).
- Existing relics will likely be rebalanced or redesigned — current SOTS-style relics (small passive bonuses) won't shine in a slot-constrained system; bigger, more interactive effects will.

**Why this fits the game's DNA:** Deckshift's core philosophy is "Movement is a Resource" — resources matter. A slot-constrained relic system extends that principle to relics: they become a curated resource pool the player manages, not a pile that grows passively.

**Scope when undertaken (multi-session work):**
- New data model (slots, sell prices, possibly slot states like "negative")
- Rework of RelicManager and RelicHUD into a slot manager UI (display, sell button, drag/swap)
- Rebalance or redesign of the 5 currently-functional relics
- 15-25 new relics to make slot decisions meaningful
- Economy tuning (sell refund %, relic offer frequency vs. 45-50 min run length)
- Possibly new relic-acquisition events (shop vs. pack vs. voucher distinction)

**Until the redesign happens: do not invest heavily in relic UX features (tooltips, activation flashes, etc.) or in adding many new SOTS-style relics.** That work will likely be reworked. Small fixes and one-off relic additions are fine; large investments are not.

**Approach when starting:** paper design first, code second.

### Quest System Expansion (deferred)

- Wire `NoDamageRoom` quest type — needs an event fired from PlayerController's damage path that resets a per-room "no damage" flag; on level end, if flag is true, fire `ReportEvent(QuestType.NoDamageRoom, 1)`.
- Wire `GoldAccumulate` and `UseCardCount` quest types similarly.
- Add card-reward type. Currently only Gold/Heal/ShiftCharge are supported.
- **Rich Man's Dagger card** — a card that deals damage based on current player gold. Was discussed as a quest reward. Needs design pass: damage formula, balance against scaling gold pools, mid-fight gold loss interaction.
- Defer reward delivery to **level-end** instead of firing immediately on quest completion. Hook point identified: `RewardManager.SelectCard()` just before `SpawnNextRoom()`.
- Add randomization to `GenerateQuests()` — currently always shows the first 3 in `allQuests`. As content grows, this becomes a real problem.
- Enforce the 3-quest cap on `AcceptQuest`. Currently you can accept more than 3.
- Visual feedback on quest accept (button flash, "ACCEPTED" overlay, hide accepted quests from board).
- Wire the "press E" prompt GameObject on the QuestBoard's `SimpleInteract.prompt` field (currently null — no hover hint appears).

### Scene Flow (deferred)

- Player should start in hub from main menu, transition to run levels, and return to hub after death/run completion.
- Currently a hack: hub is `LevelManager.roomPrefabs[0]` and first-room logic forces it. Works for testing/demo but isn't proper scene flow.
- When implemented: review every manager for `DontDestroyOnLoad` needs. Most currently lack it; that becomes a real concern with scene transitions.

### Bugs (deferred)

- ~~Card effect conflict class of bug~~ — **RESOLVED (2026-07-06).** `TryExecute` now refuses (Blocked) any card whose `ModifiedState` overlaps a live effect's flags; blocked plays cost nothing and stay in hand. Stacking Floor is Lava + Adrenaline + Phase can no longer corrupt player state. See Card System for detail.
- **Phase card wall-stuck:** if Phase ends while player is inside a wall, player gets stuck. Plan: prevent Phase expiration inside collider.
- **Comet Dive identity loss:** does the same thing as head-bounce relic. Plan: redesign.
- **Head bounce + gravity reversal:** velocity sign check doesn't account for reversed gravity. Low priority.
- **Duplicate ExitDoor possible in some room prefabs:** defensive guards now in place but the scene-side duplicate (if any) hasn't been cleaned up.
- **AnimationEventReceiver may re-enable on prefab reimport.** Has been disabled twice. If OnFootstep NullRefs reappear in the console, check that the component on the visualModel's Animator child is unchecked. (Separately, the "'OnFootstep' has no receiver!" *warning* spam — the flip side of keeping the receiver disabled — is now absorbed by the `PlayerAnimEventSink` component on that same GameObject; see Visual Model Internals.)
- **Gravity reversal warning flash is invisible** — relies on SkinnedMeshRenderer that no longer exists on the new sprite-based rig. Audio cue still fires. Fix: add a SpriteRenderer flash path.

### Resolved bugs (verified by code audit 2026-06-10 — do NOT re-fix)

These were previously listed as open in this file; the audit (`audit_report.md`) confirmed they are already fixed in code:

- ✅ **Shield-block damage leak** — `EnemyHealth.TakeDamage` checks the shield BEFORE deducting health; blocked hits no longer lose HP.
- ✅ **Spike knockback always sends right-up** — `Spike.cs` now reflects incoming velocity off `transform.up` with a minimum-force floor (correct for floor, wall, and ceiling spikes).
- ✅ **Fall damage** — removed entirely; `FallAndRespawn` teleports to the room entry point and fires `OnFallRespawn`, no damage is applied.
- ✅ **CameraPeek** — fully rebuilt without Cinemachine as a `peekOffset` consumed by `CameraFollow.LateUpdate` (see Camera System).

### CardTemplate prefab rebuild (BLOCKED on art)

The `CardTemplate` prefab has fundamental scale corruption: root scale is non-uniform (0.119, 0.568, 0.92) and ShiftCostContainer compensates with inverse scale (7.40, 1.55, 0.96). On-screen layout works only because the scales partially cancel; any position/spacing change looks broken because the cancellation is non-uniform.

**Measurements taken from a 1024×1536 sample card art** (deckshift_card_03):
- Shift slot painted centers: PNG pixels (411, 138), (511, 138), (610, 138) — 99px horizontal spacing.
- Charge slot painted center: PNG pixels (245, 150) — slightly lower and left.
- Honest Point Spacing in a 120×180 card rect: ~11.72 units (current Inspector value is 20, also wrong).

**Plan:** rebuild from scratch with all scales at (1, 1, 1), Width 120 / Height 180, all positioning via RectTransform Width/Height/Position only. **Blocked: user is hiring an artist for new card art. Not all current cards are the same exact size. Rebuilding now means rebuilding again once consistent art is back.** Hold until then.

### Resolved this session (Enemy Healthbars)

(Kept for short-term reference; can be deleted once stale.)
- ✅ EnemyHealthBar.prefab created at `Assets/Prefabs/UI/EnemyHealthBar.prefab` (root + script only — Canvas built procedurally in Awake).
- ✅ Wired and assigned via Inspector to all six enemy prefabs (AeroBat, MeleeEnemy, RangedEnemy, ShieldEnemy, Turret, PatrolEnemy). Per-enemy `headBarOffset` tuned by user.
- ✅ Fixed silent fill-amount bug — `EnemyHealthBar` now uses a procedurally-built 1×1 white sprite (cached static helper) so Image.fillAmount works. The earlier attempt using `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` failed at runtime with "Failed to find" errors and has been replaced.
- ✅ Added SkinnedMeshRenderer fallback to sorting layer detection in `Initialize` (Cainos rigs have no SpriteRenderer to inherit sortingLayer from).
- ✅ Bumped `BAR_HEIGHT_PX` from 16 to 24 for better readability.
- ✅ Removed two diagnostic `Debug.Log` calls used during the fill-amount bug hunt.

### Content (TODO)

- Scale to 60+ cards (currently ~10).
- Glass archetype: cards exist in theory, not implemented.
- Expand Vampiric archetype.
- Three-act structure: Act 1 prototype exists; Acts 2-3 not started.
- Boss encounters per act (3 bosses per act, randomly selected from pool). **Act 1's Moss Knight is a playable encounter** (moveset, gated fight start, awaken cinematic, SFX, boss health bar, and a death celebration that drops real collectible gold + shift crystals). It's the run finale (`LevelManager.bossRoomPrefab`). Full doc: `BossDesign_MossKnight.md`. Still open there: the acid arena (flank pools + platforms) and an optional post-kill RewardManager card/relic screen. The other Act-1 bosses and the pool/random-select aren't built.
- Chunk-based level system (currently hand-crafted levels).
- **Starting relic system** + **Fireball relic** for the wizard identity (auto-fires fireball every 10s). Deferred when the broader relic redesign was prioritized — may be revisited as a small early demo polish.

### Replace SlotMachine with "Dice Broker"

A character-driven gambling NPC replacing the current slot machine. Same gameplay outcome (random relic from a dice roll) but rethemed:
- A grimy character (sprite needed) who shakes a dice cup
- Reuses RewardManager's relic-grant flow
- Implementation note: **roll the result in code first, then play an animation that ends on the correct face**. Don't depend on physics simulation.
- Dice animation: sprite-sheet of 6-12 tumble frames ending on each face (cheaper and more readable than physics dice).
- Voice/banter potential — give the broker personality.

### Documentation Tasks

- Eventually: a proper GDD (Game Design Document). Currently the design is fluid enough that a GDD would be obsolete fast. Worth doing once: the relic system is finalized, the act structure is locked, the card list is more complete.

---

## File / Path Reference

- Active scene: `Assets/Scenes/SampleScene.unity`
- Player prefab: `Assets/Prefabs/Player.prefab`
- Scripts: `Assets/Scripts/` (75+ files, flat structure)
- Level prefabs: `Assets/LevelSinasi/*.prefab` and `Assets/LevelEfeS/*.prefab` (hub)
- Quest assets: `Assets/Quests/`
- Relic assets: `Assets/Relics/`
- Card asset directory: (project-specific, check user's setup)
- Hub prefab: `Assets/LevelEfeS/hub.prefab`
- Customizable Pixel Character pack: `Assets/Cainos/Customizable Pixel Character/`

---

## When in Doubt

- Ask the user for clarification before making sweeping changes.
- Verify scene presence of components before assuming code is broken.
- Read related scripts before refactoring shared systems.
- For visual/UI work, confirm the canvas hierarchy and parenting before moving GameObjects.
- For Animator parameter types, read the `.controller` YAML directly (m_Type integer).
- The user wants quality over speed. "Make this one of the greats" is the stated goal — push back gently on quick-fix patterns when a slightly larger correct fix is appropriate.
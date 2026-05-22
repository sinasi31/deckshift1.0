# Deckshift — Claude Code Context

This file is loaded automatically into Claude Code at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Jumping consumes **Shift**, a non-regenerating resource per-room. Most other actions (attacks, special movement, utility) are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. ~5 hand-crafted levels, ~10 cards in the game. Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete.

The player character was recently swapped from a SkinnedMeshRenderer-based rig (`PF Skeleton - Mage`) to a sprite-based one (`PF Pixel Character - Mage M`) from the Cainos Customizable Pixel Character pack. The wizard identity is now the canonical character. The skeleton remains in the Player prefab disabled, intended for future use as an enemy.

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

---

## Player System

### PlayerController.cs

This is a large script (~1,200 lines). It currently handles movement, jumping, card action execution, gravity reversal, VFX spawning, audio, health, gold, shift, knockback, portal state, cannon enter/exit, death, and respawn.

**Known issue:** It is a God Object and is scheduled for refactor. The `ExecuteAction()` method (~100 lines, switch over `CardActionType`) will be extracted to a separate `CardActionExecutor` component. **This is the TOP architectural priority right now.** With 60+ cards planned, this needs to happen before content scales further. **When adding new cards, add them to the existing switch, but be aware this is temporary.** See "Card Effect Conflict Class of Bug" below for one of the reasons the refactor matters.

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
2. Add a case to the switch in `PlayerController.ExecuteAction()` (until the planned refactor extracts this).
3. Create a `CardData` asset in Unity (right-click in Project view → Create → Card Data).
4. Set the asset's `actionType`, `maxUses`, `shiftCost`, sprite, etc. in the Inspector.
5. Add the card to the relevant reward pools / starter deck as needed.

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh action — costs Shift, redraws the hand, cost increases each use within a level.

### Stagger Mechanic

When Shift is 0 AND no playable cards exist, a Stagger card is auto-added to the hand. Three Stagger plays in one run = death.

### Card Effect Conflict Class of Bug (KNOWN)

Discovered when hub mode allowed free card spamming: playing multiple state-modifying cards in close succession (e.g., Floor is Lava + Adrenaline + Phase) can leave the player in a permanently broken state (flying, frozen gravity, etc.). Each card's effect captures "original" state at start and restores it at end, but **none of them know about each other**. Card A captures the current state (already modified by still-active Card B), then later restores to that mid-effect snapshot — corrupting baseline.

**This is one of the strongest reasons for the CardActionExecutor refactor.** A proper extractor will let each action declare what state it modifies and check for conflicts. Patching individual cards is wasted work that the refactor would supersede.

**In normal play, Shift cost gates spamming heavily enough that this is rarely reachable.** It is fully reachable in the hub. For now: known issue, do not patch individual cards.

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
- Fall damage (`FallAndRespawn` → `TakeDamage(fallDamage)`)

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
- **QuestSystem has `DontDestroyOnLoad`** — inconsistent with other managers. Flagged for review.
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
- Each new relic instantiates `RelicIconPrefab.prefab` as a child, setting the `Image` component's sprite to the relic's `relicArt`.
- 48×48 icon size. No tooltip, no activation flash — both deferred as future polish.

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

Replaces Cinemachine for the main follow camera. Each level prefab contains a `LevelBounds` child GameObject with `BoxCollider2D` zone children. `LevelManager.SetZones()` passes these to `CameraFollow` on spawn.

- Camera clamps to the zone the player is currently in.
- Zone transitions use hysteresis (zone doesn't change until player leaves current zone).
- No lerp on zone transition — direct follow (lerp was tried, caused jitter).

**Naming is case-sensitive:** the child must be named exactly `LevelBounds`. Earlier code looked for `CameraBounds` and silently failed.

### CameraShake.cs

Rewritten to work without Cinemachine. Uses a `shakeOffset` Vector2 that `CameraFollow.LateUpdate` adds to the final clamped position (so shake can briefly push past zone bounds, which feels correct).

- Uses `unscaledDeltaTime` so shake still plays during HitStop freezes.
- Call sites: `CameraShake.instance.Shake(duration, intensity)`. Always null-guard `instance`.

**The CameraShake component must be present in the active scene** (on the Main Camera) and **enabled**. If it's missing or disabled, every Shake call silently no-ops. This caused a 9-month "no shake anywhere" bug that wasn't discovered until the audit.

### CameraPeek.cs (BROKEN)

**Currently does not work.** Default bind is Left Ctrl; pressing it produces no effect. Still depends on Cinemachine, which is no longer present in the scene. Slated for **full rebuild**, likely as an offset on `CameraFollow` (similar to how CameraShake works). Don't touch unless explicitly tasked.

Related: a missing-script warning for `CameraBoundsController` appears in the console at scene load — this is part of the same Cinemachine-era cleanup that's pending. Cosmetic; doesn't affect gameplay.

---

## Level System

### Room Pool

`LevelManager.roomPrefabs` holds the pool of room prefabs that can be spawned. Element 0 is the hub by convention.

### First-Room Logic

`LevelManager` has a private bool `hasSpawnedFirstRoom` that defaults to false. On first call to the room-pick block:
- Forces `selectedRoomIndex = 0` (the hub).
- Sets `hasSpawnedFirstRoom = true`.
- Removes index 0 from the available pool immediately.

On all subsequent calls:
- Strips index 0 from the available pool every call (no-op if not present; covers cases where pool refill re-adds it).
- If the strip empties the pool, refill and strip again.
- Pick from whatever remains with a normal `Random.Range(0, count)`.
- Falls back to index 0 only if `roomPrefabs` has a single entry (the only physical possibility).

Net effect: **the hub is the first room of every run and never spawns again during the same run.** If/when proper scene flow gets built (player starts in hub from main menu and returns after death), this logic should be reviewed.

---

## Enemy System

### Pattern

- **`EnemyHealth`** base script — handles damage, flash, death, drops. **Currently the only callsite that reports KillEnemy/AirKill to QuestSystem.** `Die()` calls `RelicManager.OnEnemyKilled()`, `QuestSystem.ReportEvent(QuestType.KillEnemy, 1)`, and (if airborne) `QuestSystem.ReportEvent(QuestType.AirKill, 1)`. **No C# event for death** — death consequences are direct calls inside `Die()`. If you need to react to enemy death from a new system, add a call inside `Die()`; don't try to subscribe to a non-existent event.
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

**Shield-block damage leak (REAL BUG, not yet fixed):** In `EnemyHealth.TakeDamage`, `currentHealth -= damage` runs BEFORE the shield-block check, then the shield check returns early. Blocked hits silently deduct health but skip the popup and bar update — so the ShieldEnemy doesn't actually shield from damage, only from feedback. Move the deduction to AFTER the shield check when this is touched next. Trivial fix, scope-isolated to one method.

---

## Audio System

Currently minimal. `MusicManager` handles background music. Individual scripts play SFX via `AudioSource.PlayOneShot()` or `AudioSource.PlayClipAtPoint()`. No central SFX manager.

When adding audio cues for new cards/effects, follow the existing pattern: expose a `[SerializeField] AudioClip` field and play it with a null guard.

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

- **PlayerController.ExecuteAction() extraction** — extract the card-action switch into a dedicated `CardActionExecutor` component. **TOP architectural priority.** Also resolves the card-effect-conflict class of bug (multiple effects modifying shared state without coordination). Scheduled as the next major work item.
- **CameraPeek rebuild** — currently broken (Left Ctrl does nothing). Rebuild without Cinemachine, likely as a temporary offset on `CameraFollow` matching the CameraShake pattern.
- **Manager dependency graph** — undocumented. Long-term docs task.
- **QuestSystem DontDestroyOnLoad inconsistency** — should be removed or all other managers should adopt the same convention. Pending scene-flow design decision.

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

- **Shield-block damage leak:** In `EnemyHealth.TakeDamage`, `currentHealth -= damage` runs BEFORE the `ShieldEnemy.IsBlocking` check. The check returns early but the HP has already been deducted. So blocked hits silently lose HP — the shield only blocks the popup and the bar update, not the actual damage. Fix: move the deduction to AFTER the shield check. Trivial.
- **Card effect conflict class of bug** — playing multiple state-modifying cards in close succession breaks player state permanently. Reachable in hub, mostly gated by Shift cost in normal play. Will be resolved as part of the CardActionExecutor refactor; do not patch individual cards.
- **Phase card wall-stuck:** if Phase ends while player is inside a wall, player gets stuck. Plan: prevent Phase expiration inside collider.
- **Fall damage zeroing into floor:** at high fall speeds player clips into ground. Plan: **remove fall damage entirely.**
- **Spike knockback always sends right-up:** ignores incoming angle. Plan: velocity reflection.
- **Comet Dive identity loss:** does the same thing as head-bounce relic. Plan: redesign.
- **Head bounce + gravity reversal:** velocity sign check doesn't account for reversed gravity. Low priority.
- **Duplicate ExitDoor possible in some room prefabs:** defensive guards now in place but the scene-side duplicate (if any) hasn't been cleaned up.
- **AnimationEventReceiver may re-enable on prefab reimport.** Has been disabled twice. If OnFootstep NullRefs reappear in the console, check that the component on the visualModel's Animator child is unchecked.
- **Gravity reversal warning flash is invisible** — relies on SkinnedMeshRenderer that no longer exists on the new sprite-based rig. Audio cue still fires. Fix: add a SpriteRenderer flash path.

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
- Boss encounters per act (3 bosses per act, randomly selected from pool).
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
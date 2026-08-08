# Deckshift — Claude Code Context

This file is loaded automatically into Claude Code at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Jumping consumes **Shift**, which does not regenerate on its own — and **Shift CARRIES OVER between rooms** (designer-confirmed 2026-07-13: it is a run-long resource, and this persistence is "the whole identity of the game" — spending Shift now means having less for the rest of the run). Do NOT describe or implement Shift as a per-room resource. Most other actions (attacks, special movement, utility) are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. **7 combat levels in the run pool** (+ hub + boss room; ~15 more contract-valid rooms exist unused — see Room Pool), **16 CardData assets in `Assets/Cards/` and 18 relics** (re-verified 2026-07-26; note 2 of the 16 are not normal reward cards — `Stagger` is the fail-state card and `AnaKartVeritabanı` is the card *database* asset, so the real playable pool is ~14). Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete.

⚠️ **Content is the project's real bottleneck, and it gates the two biggest planned systems.** The run map is explicitly blocked on level count (it's mediocre at ~7 rooms, sings at ~30), and card *enhancements* ("Blompo") are a multiplier on the card pool — both want more content underneath them before they pay off. When choosing between "build another system" and "author more cards/levels", the honest answer is usually the latter.

The player character was recently swapped from the skeleton rig (`PF Skeleton - Mage`) to `PF Pixel Character - Mage M` from the Cainos Customizable Pixel Character pack. The wizard identity is now the canonical character. The skeleton remains in the Player prefab disabled, intended for future use as an enemy. **Renderer facts (verified in-editor 2026-07-17): the Mage M body is 16 `SkinnedMeshRenderer` parts (Body, Hair, Hat, Cloth… — Cainos "Alpha Cut"/Body/Hair shaders); only the magic staff is a `SpriteRenderer`.** Any code that snapshots/copies the player's look must handle SkinnedMeshRenderers (e.g. `SkinnedMeshRenderer.BakeMesh`, as `CardAimIndicator`'s dash trail does) — a SpriteRenderer-only pass silently produces a staff-only ghost.

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

This is a large script (~1,200 lines). It currently handles movement, jumping, card action execution, gravity reversal, VFX spawning, audio, gold, shift, portal state, cannon enter/exit, and respawn. **Health/damage/knockback/parry were extracted into `PlayerHealth.cs`** (same GameObject); `PlayerController.TakeDamage` is a one-line delegate to it, and RelicManager recomputes HP passives from `PlayerHealth.BaseMaxHealth`.

**Refactor status: the `CardActionExecutor` extraction is DONE.** `ExecuteAction()` is now a one-line delegate to `CardActionExecutor.TryExecute()`. All card actions live in `Assets/Scripts/CardActions/Actions/` as `CardAction` subclasses, registered in a dictionary in `CardActionExecutor.Awake()`. There is no switch statement anymore — do not look for one. The conflict-flag half of the system is only partially built; see "Card Effect Conflict Class of Bug" below for the audited current state.

### Player Prefab Specifics

- **Active visual model:** `PF Pixel Character - Mage M` at `Assets/Cainos/Customizable Pixel Character/Prefab/Character Preset/PF Pixel Character - Mage M.prefab`. This is a child of the Player root and is assigned to `PlayerController.visualModel`.
- **Disabled fallback:** `PF Skeleton - Mage` is still parented under Player but disabled (checkbox off). Kept as backup and for future reuse as an enemy.
- **Physics collider:** `CapsuleCollider2D` on the Player root with **Offset (-0.0053, 0.8423) and Size (0.5075, 1.6848)**. Direction: Vertical. A `BoxCollider2D` was previously present but disabled and has been removed. Do not re-add it. **This capsule is the player's only ACTIVE solid collider (2026-07-16):** the Cainos rig's leftover bone colliders (capsules on `Rig Spine1`/`Rig Spine2`, circle on `Rig Head`) and the magic staff's `Rigidbody2D` + trigger `PolygonCollider2D` were removed from the prefab — they made the hitbox animation-dependent and cost physics rebakes every frame. Do not re-add them. **CAVEAT verified 2026-07-18:** a second solid (non-trigger, component-enabled) `BoxCollider2D` still exists on the **disabled** `PF Skeleton - Mage` child. It is inert only because that GameObject is inactive — **if you ever re-enable the skeleton (the doc below suggests reusing it as an enemy), the player instantly gains a second solid collider on its Rigidbody2D.** Delete that BoxCollider2D before re-enabling, or reuse the skeleton as a separate prefab rather than by re-activating this child. The root Rigidbody2D is confirmed the only Rigidbody2D in the prefab.
- **Rigidbody2D:** Dynamic. Gravity scale flips sign during gravity reversal — do NOT modify `Physics2D.gravity` globally.
- **Player root Transform:** Position (0, 0, 0), Rotation (0, 0, 0), **Scale (1, 1, 1)**. This is now a hard rule again — the prior non-(1,1,1) scale was an accidental drift that compounded into a real bug. Do not modify the root scale to adjust character size; scale `visualModel` instead.

### Visual Model Internals (PF Pixel Character - Mage M)

- The visualModel itself is scaled to **(0.8, 0.8, 0.8)** to fit the collider. If the character ever needs to appear larger or smaller, change this value, not the root.
- The prefab has its own root-level scripts (`PixelCharacter`, `PixelCharacterController`, `PixelCharacterInputMouseAndKeyboard`, plus its own Rigidbody2D and BoxCollider2D). When the visualModel was integrated, the controller scripts and physics components were removed; only the `PixelCharacter` (customization) script remains. Do not re-add the removed components.
- The Animator component lives on the child GameObject named `Animator`, found via `GetComponentInChildren<Animator>()`. There is only one Animator in the hierarchy.
- The Animator Controller is `Assets/Cainos/Customizable Pixel Character/Animation/AC Character.controller`.
- **`Cainos.CustomizablePixelCharacter.AnimationEventReceiver` has been REMOVED from the Mage M Animator GameObject entirely** (it used to be merely disabled). It throws NullReferenceExceptions on the built-in footstep animation events (the pack expects a footstep audio system we don't use). Do not re-add it; if a pack reimport resurrects it, remove it again.
- **`PlayerAnimEventSink` component must stay on that same Animator GameObject.** With no Cainos receiver, the pack's ~20 animation events (`OnFootstep`, `OnAttackCast`, etc.) would have no receiver, spamming `"'OnFootstep' has no receiver!"` every step. `Assets/Scripts/PlayerAnimEventSink.cs` is a sink with a method for every event name (including the pack's `OnLedgeClimbFinised` typo) so the events land harmlessly. Do NOT delete it or the spam returns. Its `OnFootstep(AnimationEvent)` relays to `PlayerController.PlayFootstep()` (footstep SFX). The sink MUST stay on this Animator child (that's where Unity delivers the events); the footstep *fields* (`footstepClips[]` — the three `Walk` mp3s from `Assets/LevelEfeVrl/Sprites/`, `footstepVolume`, `footstepPitchRange`) live on `PlayerController` (the player root) per the designer's request. Other event methods remain empty; hook new anim-driven SFX here. **History (2026-07-16): the sink + clip wiring were once scene-only, never committed, and silently lost — they are now serialized in `Player.prefab` itself. Keep it that way: player changes must be applied to the PREFAB, not left as scene overrides.**

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

`groundLayer` mask is **`2056` = layer 3 (`Ground`) + layer 11 (`Enemy`)** — verified against Player.prefab 2026-07-18. (An earlier version of this file claimed `2057` including layer 0 `Default`; that was WRONG — Default is NOT in the mask.) Consequences worth knowing: level geometry is on layer 3, and because layer 11 `Enemy` IS in the mask, the player can **stand on / ground-check against every Enemy-layer enemy**. See "Layer Convention Mismatch" under Enemy System for the verified per-enemy layer split — it is inconsistent and decides which enemies are walkable. This is a known issue but currently load-bearing.

### Gravity Reversal System

Triggered by the "Floor is Lava" card (`CardActionType.ReverseGravity`). Lasts 5 seconds with a 0.5s warning flash + audio cue before expiration.

Key fields on PlayerController:
- `isGravityReversed` — runtime flag
- `originalGravityScale` — cached at effect start, restored at end
- `gravityReversalCoroutine` — reference for stop-and-restart on re-play
- **`visualFlipYOffset`** — serialized field, current value **1.6875** (tuned in Inspector after the scale refactor). Translates visualModel up so the 180° rotation pivots around the collider center instead of the feet.
- `originalVisualLocalPos`, `originalVisualScaleX` — cached for restoration
- `warningSoundClip` — AudioClip Inspector field, played at t=4.5s (assigned 2026-07-16: the breaker-switch clip in `Assets/Audio/SFX/` — designer may swap; it had been null in the scene, making the warning fully silent). ⚠️ **This regressed once and was re-fixed 2026-07-22:** the SampleScene Player instance had re-acquired a `prefabOverride` setting this field back to NULL, silencing the warning again even though the prefab was correct. Reverted via `PrefabUtility.RevertPropertyOverride`. **If a player field mysteriously "stops working," check for a scene-instance override before touching the prefab or the code** — the prefab being right does not mean the scene is using it.

`GravityReversalRoutine()` handles the full timeline. `LerpVisualTransform` uses a tracked Z-angle float (never reads back from `localEulerAngles`, which Unity normalizes unpredictably).

The 0.5s **warning flash** is `WarningFlashRoutine` (3 rapid on/off cycles ≈ 0.5s) — **FIXED and screenshot-verified 2026-07-26.** History: it originally tinted `SkinnedMeshRenderer._Color`, which silently no-ops because the Cainos **"Alpha Cut"** shader (most outfit parts) exposes no color property at all (only `_MainTex` + `_Alpha`); a later "fix" switched it to `GetComponentsInChildren<SpriteRenderer>()`, but the Mage M rig is **16 SkinnedMeshRenderers (body/outfit) + 1 SpriteRenderer (the staff ONLY)**, so that flashed just the staff and the body never reacted. Current implementation strobes **`_Alpha`** (the one handle EVERY Cainos rig shader shares — the same one Phase uses) across all SkinnedMeshRenderers via a `MaterialPropertyBlock`, blinking the WHOLE character (a blink reads as "effect expiring"), and additionally red-tints the staff (which does support color). `_Alpha == 1` is the confirmed "normal" value. **Audio cue also plays** (clip re-assigned 2026-07-16). If you ever swap the character rig, re-check that its shaders still expose `_Alpha`.

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
6. If the card has a "where/how" (aim, range, placement, area), add a matching preview to `CardAimIndicator` (see "Card Aim Indicator System" below).

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh action — costs Shift, redraws the hand, cost increases each use within a level.

### Stagger Mechanic

When Shift is 0 AND no playable cards exist, a Stagger card is auto-added to the hand. Three Stagger plays in one run = death.

---

## Scrap System (BUILT 2026-08-03)

**Scrap is the card-maintenance currency.** Earned from kills and from your own cards wearing out; spent at a **Scrap Forge** to put charges back on cards and to drag cards out of the exhaust pile.

### Why it exists

Before this, **killing an enemy paid literally nothing** — `EnemyHealth` had no drop logic at all (an earlier version of this file wrongly claimed it "handles drops"), and gold comes only from piles placed in levels, never from enemies. So a kill cost you HP and card charges and returned zero, making "skip every fight and platform to the exit" the optimal play in a game built around a deck of attack cards. Scrap is the payment for engaging with combat.

It is also load-bearing for the **planned difficulty tiers** (see Deferred Work → Run map): without a combat reward, a harder room is pure downside and no player would ever route into one.

### The gold / scrap split (designer-set, do not blur)

| | Gold | Scrap |
|---|---|---|
| **Comes from** | piles placed in levels (exploration; usually off the mandatory path, so reaching it costs Shift) | enemy kills + a small rebate when a card exhausts |
| **Buys** | NEW power — cards, relics at the shop | SUSTAIN — charges on cards you already own |

⚠️ **Never let these merge.** If the shop starts selling charges, or scrap starts buying cards, one of the two currencies is redundant and should be deleted. The reason recovery got its own currency at all is that **maintenance always loses to acquisition when they share a wallet** — given one pool, players buy the shiny relic over repairing a card every time, and the exhaust problem stays unsolved.

### Files

- **`ScrapEconomy.cs`** — **THE tuning file. Every scrap number lives here and nowhere else.** Drop tiers (derived from `maxHealth`, matching the `CardAnchors.md` §5 HP tiers), `RECHARGE_PER_CHARGE`, `SALVAGE_COST`, `EXHAUST_REBATE`, plus `ScrapColor` and `UIFont()`.
- **`ScrapPickup.cs`** — the collectible. **Built entirely in code (no prefab)**, so there is nothing to wire and nothing to lose from a scene. Deliberately has **no Rigidbody2D**: the pop-out arc is hand-integrated against a ground raycast, because a solid collider would shove the player's capsule and a trigger-only rigidbody would fall through the floor. Carries `TemporaryObject`, so uncollected shards are wiped on room change.
- **`ScrapForgeScreen.cs`** — the spend UI. Self-instantiating procedural screen, same pattern as `BlompoScreen`, but styled with **`FlatUI`, not `RelicUISprites`** (see UI System → Flat theme).
- **`ScrapForge.cs`** — the `IInteractable` station that opens it. **Unlike Blompo it does NOT vanish after use** — it's a workbench, and the scrap cost is the limiter, not the visit.
- **`ScrapHUD.cs`** — the counter. Self-bootstraps via `RuntimeInitializeOnLoadMethod` and **positions itself relative to the existing `ExhaustPile` button** rather than at fixed coordinates.

### Design rules baked in

- ⚠️ ~~**Scrap sits with the deck/exhaust pile UI, NOT in the resource panel.**~~ **OVERRULED by the designer 2026-08-09.** The reasoning (scrap is deck-maintenance, so it belongs with the deck UI) did not survive contact with play: bottom-right it read as a stray widget, and having the two CURRENCIES in opposite corners made neither easy to check. **Scrap now sits directly under the gold counter**, and both are built from `HudChip` so they are one piece of geometry rather than two that happen to agree. The resource panel is now two **bars** (health, Shift — bounded, so a fill is the honest shape) above two **chips** (gold, scrap — unbounded counts, so a number in a plate). ⚠️ `ScrapHUD` re-anchors in `LateUpdate`, not once in `Build()`: it is created from a `sceneLoaded` bootstrap that runs before any `Start()`, so at build time `ResourcePanelHUD` has not laid the gold row out yet and a one-shot read parks it in the wrong place.
- **Kills must out-earn the exhaust rebate by roughly 10:1.** Kills are the lever that changes behaviour; the rebate is only a consolation so losing a card isn't a total loss. If the rebate ever dominates, you've accidentally incentivised burning your own deck down.
- **Salvage returns a card only HALF charged**, so a full recovery is salvage + repair. Exhaust must stay a real loss.
- **Target: one act of income rescues ONE OR TWO cards, never the whole deck.** Scarcity is the point — charges depleting is what feeds Stagger, which is the run's only real death pressure. Make repair comfortable and that pressure quietly disappears.
- **Scrap spending is NOT hub-exempt.** The umbrella "free in hub" rule covers resources the sandbox *drains* from you; a forge repair is a purchase that permanently improves the run, exactly like a shop buy (which the hub already charges for). Free repairs in the hub = infinite deck refills.
- Both `DeckManager.TryRechargeCard` / `TrySalvageCard` are **all-or-nothing** — verified by test that a refused operation never charges the player.

### Setup still needed in Unity (designer step)

The scrap *system* is live and needs no wiring — enemies drop it, the HUD appears, exhausting a card pays the rebate. **What doesn't exist yet is a placed `ScrapForge` object**, so there's currently nowhere to spend it. To add one: put the `ScrapForge` component on a prop, set its layer to **Interactable (12)**, give it a trigger `Collider2D` slightly wider than `PlayerController.interactionRange`, and optionally assign an `InteractPrompt` instance to `prompt`. Same recipe as `BlompoNPC`.

### Card Effect Conflict Class of Bug (KNOWN)

Discovered when hub mode allowed free card spamming: playing multiple state-modifying cards in close succession (e.g., Floor is Lava + Adrenaline + Phase) can leave the player in a permanently broken state (flying, frozen gravity, etc.). Each card's effect captures "original" state at start and restores it at end, but **none of them know about each other**. Card A captures the current state (already modified by still-active Card B), then later restores to that mid-effect snapshot — corrupting baseline.

**Current state (updated 2026-07-06): RESOLVED.** The CardActionExecutor extraction is done AND conflict-flag enforcement is live. Each `CardAction` declares a `ModifiedState` (`ConflictFlags`); the executor accumulates flags in `activeFlags` (via `ManagedCoroutine` for coroutine actions, via `SetManualFlag` for the manual-lifecycle ones) and **`TryExecute` now checks them: if an action's `ModifiedState` overlaps `activeFlags`, it is refused up front with `CardExecuteResult.Blocked` and none of its code runs.** A blocked play costs no Shift and no charge, and the card stays in hand (`DeckManager.PlayCard` only spends/consumes on `Success`). The state-corruption bug class (Floor is Lava + Adrenaline + Phase leaving the player flying/frozen) can no longer occur — the conflicting second card is refused instead of corrupting the baseline snapshot.

Per-effect conversion status:
- **Dash** ✅ converted — managed coroutine; flags `PlayerVelocity | Invincibility` held for the whole dash. **Reworked 2026-07-06 into a driven dash** (`PlayerController.DashRoutine`): enters `PlayerState.Dashing` and holds a flat horizontal velocity for `dashDuration` (re-asserted each FixedUpdate with y forced to 0), so it works on the ground too — the old one-shot `AddForce` impulse was erased the next frame by the grounded movement line (`rb.linearVelocity = moveInput * moveSpeed`). Never touches `gravityScale` (composes cleanly with Floor is Lava). Procedural afterimages via `DashAfterimage.cs`; tunables `dashSpeed`/`dashDuration`/`dashEndSpeed`/`dashIFrameDuration`/`dashAfterimages` on PlayerController. **Live Player.prefab values (verified 2026-07-18): dashSpeed 26, dashDuration 0.16, dashEndSpeed 9, dashIFrameDuration `0.15`.** ⚠️ Note `dashIFrameDuration (0.15) < dashDuration (0.16)`, which violates the field's own inline invariant ("keep >= dashDuration to stay safe through the dash") — the player is damageable for the last ~0.01s of the dash. The script default is 0.22; the prefab overrides it to 0.15. Harmless in practice but unintended; raise it to ≥ 0.16 if i-frames should truly cover the whole dash.
- **Phase** ✅ converted — managed coroutine; flags `GravityScale | LayerCollisionMatrix | PlayerVelocity`.
- **Adrenaline** ✅ converted (manual-flag pattern) — `UseAdrenaline`'s two sub-coroutines are mutually exclusive (`if/else` on health %), and each calls `SetManualFlag(TimeScale | MoveSpeed, …)` at start/end. The old "not refcounted / overlapping plays clear flags early" caveat is now moot: a second Adrenaline play while one is active is Blocked (its flags overlap), so concurrent same-flag effects can't happen.
- **Fireball** ✅ converted — managed coroutine; `AnimatorAttackState`.
- **ReverseGravity** ✅ converted (manual-flag pattern) — `StartGravityReversal`/`GravityReversalRoutine` now call `SetManualFlag(GravityScale | VisualTransform, …)` with a restart-safe lifecycle: flags are cleared BEFORE `StopCoroutine` and re-set synchronously inside the new `StartCoroutine`, so there is never a flags-set-but-no-routine window and the clear can't stomp the new set. The same-card timer-refresh branch is now unreachable (a replay while active is Blocked because its flags overlap `activeFlags`); it's kept deliberately in case the policy later allows same-card refresh.

**Known interaction (found 2026-07-06):** enforcement makes the **Echo Chamber** skill's instant double-cast (`DeckManager.PlayCard` re-calls `ExecuteAction` immediately after the first play) silently no-op for *stateful* cards — the second cast's `ModifiedState` overlaps the first's still-live flags and is Blocked. It still works on instant cards (Jump, Glass Wail, etc.). Fix options if this becomes design-relevant: defer the echo cast until the first effect ends, or let a same-card replay bypass the block. Not yet done — flagged, not urgent.

**Enforcement applies everywhere, including the hub** (where free card spamming used to make this bug trivially reproducible). The class is now handled centrally in `TryExecute`, so there is no need to patch individual cards.

### Card Aim Indicator System (2026-07-17)

`Assets/Scripts/CardAimIndicator.cs`, on the **Player prefab root**. Watches `DeckManager`'s selected card every frame and shows an honest world-space preview of what the card will do when cast. All visuals are procedural (house pattern: no prefabs, no art — like `DashAfterimage`/`EnemyHealthBar`). Hidden while paused, dead, or when nothing/a non-indicator card is selected; everything dims when the player can't afford the card's **effective** Shift cost (mirrors `PlayCard`'s gate exactly: KineticDiscount and `isNextCardFree` included — note the affordability GATE applies even in the hub; only the spend is hub-exempt).

Per-card previews (each mirrors the real mechanic's math — **if you change a card's range/center/cost, update the matching `Update*` method or the indicator becomes a lie**):

- **Fireball** — ember dots flowing along the true flight line (capsule-cast with the real fireball collider, so short targets register) + pulsing impact ring; ring is orange on walls, **hot red when the impact would be an enemy**.
- **Dash** — afterimage trail: 4 translucent silhouettes along the wall-clamped true path, strongest at the destination. **The Cainos body is SkinnedMeshRenderers, so parts are baked per frame via `SkinnedMeshRenderer.BakeMesh`** and drawn as tinted MeshRenderer copies (Sprites/Default material carrying each part's texture); the staff is the only SpriteRenderer.
- **Vampiric Bite** — ring + soft fill at the true radius; **green when an enemy is inside (play lands), dim red when it would be refused**. Validity re-scanned on a 0.08s timer with the exact same filter as `PerformVampiricBite`.
- **Portal** — ghost portal follows the cursor from selection; neutral gray before the first placement, **cyan in-range / red out-of-range** while the second is pending (reads `PlayerController.FirstPortalInstance`, an accessor added for this).
- **PlatformCreate** — ghost of the platform prefab's actual sprites at true size on the cursor. (The card itself has NO range limit or placement rules — the ghost shows that honestly.)
- **FreefallBlade** — the true ")" slash circle (forward-and-low, same offset math as `PerformFreefallBlade`); **grows while falling** (the empowered arc) and colors pale-blue neutral / orange falling / green enemy-inside.
- **GlassWail** — two expanding ripples from the body + a pulsing glint over every `EnemyHealth` in the scene (the wail is scene-wide; enemy list refreshed on a 0.25s timer).

**Adding an indicator for a new card:** add a `Kind`, an `Ensure*Visuals()` builder + `Update*(dim)` method, and a case in both the `LateUpdate` switch and `SetKind`. Read the real mechanic's code first and mirror its numbers exactly.

Related: `Assets/Scripts/PortalRangeRing.cs` — the first portal's range border is now a procedural rotating dashed ring + traveling wave (spawned by `Portal.ShowRangeCircle` at the EXACT gameplay radius, parent-scale-compensated). The old flat `rangeIndicator` sprite on the Portal prefab is kept assigned but permanently hidden — do not re-enable it.

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
- **SlotMachineUI** — gambling system (planned to be replaced with Dice Broker — see deferred work). ⚠️ **There is NO `SlotMachineManager` type** — verified 2026-07-18; an earlier version of this file listed one. Only `SlotMachineUI` exists.
- **AchievementManager** — achievement tracking
- **PauseMenu** / **MainMenuController** — menu systems. ⚠️ **There is NO `MenuManager` type** (verified 2026-07-18); it was listed here in error.
- ⚠️ **`EffectManager` DOES NOT EXIST as a type** (verified 2026-07-18) — this entry was a phantom. Confusingly there IS a GameObject *named* "EffectManager" in SampleScene, but it carries **HitStop**, not an EffectManager component. VFX are spawned ad-hoc by the callers (e.g. `Instantiate` of a VFX prefab) and by house-pattern procedural classes (`DashAfterimage`, `ShockwaveVFX`, `SpitGlob`, `CardAimIndicator`).
- **MusicManager** — background music
- **CameraShake**, **HitStop** — game-feel singletons (camera shake + freeze frames)

#### Scene presence in SampleScene — verified 2026-07-18

Because "the system exists in code but isn't in the scene" is this project's #1 recurring bug (see Common Pitfalls), here is the audited truth:

- **Present, component-enabled, active:** GameManager, DeckManager, LevelManager, RelicManager, SkillManager, SkillRewardManager, QuestSystem, ShopManager, AchievementManager, RewardManager, **CameraShake (on Main Camera — the 9-month "no shake" bug is genuinely fixed)**, CameraFollow (Main Camera), **CameraPeek (Main Camera ONLY — the Player duplicate is confirmed gone)**, HitStop (on the GameObject named "EffectManager").
- **NOT in SampleScene:** `MusicManager` and `SfxManager`. Both exist as MonoBehaviours but live elsewhere (MainMenu boot flow). Consequence: **entering Play mode directly in SampleScene gives you no MusicManager**, so no BGM — that's expected, not a bug. `SfxManager`'s entry points are `static` and work fine without a scene instance, which is why SFX still play.
- ⚠️ **Leftover `CinemachineCamera` GameObject still sits in SampleScene, INACTIVE, carrying a second `CameraShake` component.** Harmless while inactive (Awake never runs, so it can't hijack the singleton), but it is Cinemachine-era cruft and a trap if anyone activates it — that would create a duplicate CameraShake singleton. Part of the same pending Cinemachine cleanup as the dead `using Unity.Cinemachine;` directives.

**Build settings (verified):** `MainMenu`(0), `Hub`(1, disabled), **`SampleScene`(2)**, `GameOverScene`(3), `GameScene`(4).

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
- **`QuestType` enum:** `GoldAccumulate`, `KillEnemy`, `AirKill`, `NoDamageRoom`, `UseCardCount`. **Re-verified 2026-08-03: FOUR of the five now fire** — `KillEnemy` + `AirKill` (`EnemyHealth.Die()`), `NoDamageRoom` (`ExitDoor.PerformExit`), `GoldAccumulate` (`PlayerController.AddGold`). **Only `UseCardCount` is still unwired.** (An earlier version of this file claimed only the first two worked — that was stale and understated the state.)
- **`RewardType` enum:** `Gold`, `ShiftCharge`, `Heal`. All three are wired in `QuestSystem.GiveReward`.
- **Four quest assets exist** at `Assets/Quests/` (re-verified 2026-07-18 — an earlier version of this file said three):
  - `New Quest 1` — "Invincible" — NoDamageRoom (1) → 300 Gold. **Objective type not wired, won't progress yet.**
  - `New Quest 2` — "Hit a Clip" — AirKill (3) → +10 ShiftCharge. Fully functional.
  - `New Quest 3` — "Bounty Hunter" — KillEnemy (3) → 100 Gold. Fully functional.
  - `Scrooge` — "Scrooge" — GoldAccumulate (800) → Gold **0**. ⚠️ Two problems: `GoldAccumulate` is still an unwired objective type (won't progress), AND its `rewardAmount` is 0, so it would pay nothing even if completed. Looks unfinished.

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

The QuestBoard in `Assets/LevelEfeS/hub.prefab` has a `SimpleInteract` component on it (implements `IInteractable`) that calls `QuestSystem.ToggleBoard()` on player interact (press E within `interactionRange`). The board's Layer must be in PlayerController's `interactableLayer` mask. ✅ **VERIFIED 2026-07-18** (this was previously an open "someone please check" item): `interactableLayer` = **4096 = layer 12**, layer 12 **is** named `Interactable`, and the hub's `QuestBoard` object is on layer 12 with a `SimpleInteract` component. The wiring is correct — no action needed.

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

**The Balatro-style slot-constrained system is BUILT and live (corrected 2026-07-26 — this section previously claimed it was an unbuilt "future direction", which was badly stale).** The player owns at most **`RelicManager.MaxSlots` = 5** relics; acquiring one while full forces a sell-or-decline decision. It is no longer an unlimited additive pile.

What exists today:
- **Slots + selling** — `MaxSlots`, `IsFull`, `SellValueFor(relic)` (fixed refund by rarity: Legendary 150 / Epic 90 / Rare 50 / Common 25), `SellRelic(relic)` which removes, credits gold, fires `OnRelicRemoved` and calls `RecomputePassives()`.
- **Central grant entry point** — `TryGrantRelic(relic, onAcquired)`. Slot free → add immediately and run `onAcquired`. Slots full → open `RelicSwapScreen`; TAKE sells the chosen relic then adds the new one and runs `onAcquired`, LEAVE runs nothing. **`onAcquired` is where callers finalize side effects (e.g. the shop charges gold ONLY when the relic is actually taken), so a declined full-slot grant costs nothing.** New grant sources should route through `TryGrantRelic`, not `AddRelic`.
- **UI** — `RelicHUD` (top-centre loadout bar), `RelicSlotHover` + `RelicTooltip` (hover info), `RelicManagePanel` (inspect/sell, `I` key), `RelicSwapScreen` (the forced full-slot decision). All procedural, all sharing `RelicUISprites`.

**Passive recomputation rule (important):** `RecomputePassives()` recalculates stat relics from the player's BASE stats every time the loadout changes, so selling reverses exactly. **Never add/subtract stats incrementally** — that breaks the moment relics stack (Reinforced Plating + Glass Heart) or are sold out of order.

Still open (see deferred work): rebalancing the 18 relics *for* a slot economy — they were authored as small always-on Slay-the-Spire bonuses, which is the wrong shape for a 5-slot loadout where each pick should be a real decision.

### Card offer pool — `CardCatalogue` + `CardPool` (2026-08-09)

⚠️ **CARD AVAILABILITY IS NO LONGER GATED BY ACHIEVEMENTS.** `RewardManager` used to pull its pool from `AchievementManager.GetAvailableCardPool()`, which returned only `defaultUnlockedCards` (11 of 15) plus the reward cards of **completed** challenges — and exactly one challenge is authored. The shop drew from a separate hand-kept `ShopManager.allCardsPool` (10 of 15). Between them, **`DeadWeight`, `FreefallBlade` and `GlassParry` could never be obtained by any means**, silently.

The designer regrets putting the achievement system in this early and wants a proper one for cards/relics near release. `AchievementManager` still tracks and saves challenges — **it just no longer decides what exists**. Same machinery as the relics: `CardCatalogue` (auto-rebuilt asset) + `CardPool`.

⚠️ **`Stagger` must never be offered.** It is the fail-state card — three plays ends the run — so rewarding or selling it hands the player a way to lose. `CardPool.IsRewardable` excludes it by comparing against `DeckManager.staggerCardData`, **not by name**, so renaming the asset can't reintroduce it. Verified: 3000 reward draws surfaced all 14 legitimate cards including the three formerly unreachable ones, and Stagger zero times.

### Relic offer pool — `RelicCatalogue` + `RelicPool` (2026-08-08)

**Never hand-maintain a list of relics again.** The shop and the chests each carried their own Inspector list and both had silently fallen behind the roster: **18 relics existed, `ShopManager.allRelicsPool` held 3 and `Chest.prefab` held 5 across its four tiers**. Nothing was broken in code — the lists were simply never updated when relics were added, and there is no way to notice that from inside the game.

- **`RelicCatalogue`** — a ScriptableObject at `Assets/Resources/RelicCatalogue.asset` listing every `RelicData`. Rebuilt automatically by `Editor/RelicCatalogueBuilder` (an `AssetPostprocessor`) whenever a relic asset is added, removed, moved or renamed, plus a **Deckshift → Rebuild Relic Catalogue** menu item. It also warns about empty or duplicated `relicID`s, which silently break `HasRelic()`.
- **`RelicPool`** — the only thing that answers "what may be offered right now". `All`, `Offerable(rarity, restrictTo)`, `PickOfferable(rarity, …)` (steps down tiers, then up), `DrawDistinct(n, …)` for stocking a shelf.

⚠️ **An owned relic is never offered, and ownership is read AT THE MOMENT OF THE OFFER.** Chests used to hand back a relic you were already wearing — a dead reward for a room you paid to cross. Reading the live loadout also gives the sell-behaviour for free: **selling a relic puts it straight back in the pool**, with no bookkeeping. Comparison is by `relicID`, not asset reference.

⚠️ **`Chest`'s four per-tier relic lists were DELETED (2026-08-08) — do not reintroduce them.** They held 5 relics across four tiers, so a chest could only ever hand out those five. Once the player owned enough of them the chest had **nothing left to offer**, `PickRandomRelic` returned null, and the swap screen never appeared — the designer reported chests as broken after 5 relics, and this was why. Keeping them as an *optional* curated override did not help: they were populated, so the override was always on. A chest now draws the whole roster. If per-chest curation is ever wanted, add **one** list, not one per tier — a per-tier list also breaks the rarity fallback, because stepping to another tier re-searches the same single-tier list and finds nothing.

`Shopkeeper.specificRelicPool` survives as a genuine per-shop restriction (**empty = whole roster**, the normal case). `ShopManager.allRelicsPool` is deliberately no longer consulted; copying it into the shopkeeper is precisely what capped the stock at 3.

**A chest is never empty.** If the loadout is full the swap screen opens; if the player declines — or the screen cannot open at all — `onDeclined` pays the relic's **sell value** in gold, so the payout still scales with the rarity that was rolled. Verified: 6 consecutive chests at a full loadout all raised the swap screen, DECLINE paid the sell value, and TAKE swapped the loadout without double-paying.

Verified: 500 chest rolls returned zero owned relics; 200 shop restocks produced zero worn or duplicate offers; with a full 5-slot loadout the pool correctly reports 13 of 18 offerable, and selling restores the sold relic.

### RelicManager

Singleton. Holds:
- `ownedRelics` — private list of owned `RelicData`; **list index == slot index**.
- Public `OwnedRelics` — `IReadOnlyList<RelicData>` accessor.
- Public events `OnRelicAdded` / `OnRelicRemoved` — `System.Action<RelicData>`, fired after a successful add/sell (not on duplicate-add). HUD and panels rebuild on both.

Grant paths:
- `TryGrantRelic(relic, onAcquired)` — **the entry point everything should use** (handles the full-slot swap flow).
- `ShopItemUI` — buying a shop item with a relic reference.
- `SlotMachineUI` — slot machine payout.
- `DebugTools.cs` F1 key — debug only.

**No starting-relic infrastructure exists yet.** Every run begins with zero relics. Adding a starting relic system (e.g., a wizard who begins with a Fireball relic) is on the deferred list.

### RelicData ScriptableObject

Fields: `relicID` (string, used for `HasRelic` polling), `relicName`, `description`, `relicArt` (Sprite, used by the HUD), `rarity` (enum).

**Relic roster — re-audited 2026-07-18. There are 18 relics and ALL are wired.** (An earlier version of this file listed only 7, including `New Relic 1` / "Oops! All 7's" and `Helly` — those are gone, and the "only 5 are functional" claim was badly stale.) The roster was renamed to the playful house voice (see Tone & Voice), so **asset filename ≠ display name ≠ `relicID`** — always poll by `relicID`:

| Asset file | `relicID` | Display name | Rarity |
|---|---|---|---|
| ExecutionersSeal | `ExecutionersSeal` | Executioner's Seal | Epic |
| FluxRegulator | `FluxRegulator` | First One's Free | Common |
| FoundryRights | `FoundryRights` | Melt It Down | Epic |
| GlassHeart | `GlassHeart` | Glass Heart | Epic |
| **Kinetic** | **`KineticCapacitor`** ⚠️ | Hot Streak | Common |
| LavaBoots | `LavaBoots` | Hot Steppers | Common |
| MeteorGreaves | `MeteorGreaves` | Meteor Greaves | Epic |
| MidasRecoil | `MidasRecoil` | Blood Money | Rare |
| OverclockedRecall | `OverclockedRecall` | Offering | Epic |
| PhoenixCog | `PhoenixCog` | Phoenix Cog | Legendary |
| PocketBattery | `PocketBattery` | Pocket Lightning | Common |
| Pogo Boots | `PogoBoots` | Pogo Boots | Rare |
| ReclaimersClamp | `ReclaimersClamp` | Sticky Fingers | Rare |
| ReinforcedPlating | `ReinforcedPlating` | Bubble Wrap | Common |
| ScrapMagnet | `ScrapMagnet` | Loot Goblin | Common |
| **SpikedCarapac** | **`SpikedCarapace`** ⚠️ | Do Not Pet | Rare |
| VampireTooth | `VampireTooth` | Snack Fangs | Common |
| Whetstone | `Whetstone` | Whetstone | Common |

⚠️ **Two filename/ID traps:** the asset named `Kinetic` has `relicID` **`KineticCapacitor`**, and `SpikedCarapac` (no trailing "e") has `relicID` **`SpikedCarapace`** (with "e"). Using the filename in `HasRelic()` will silently never match.

**How each is wired** (verified): most via `RelicManager.HasRelic("<id>")` — including a damage-modifier path `RelicManager.ModifyPlayerDamage(...)` used by Fireball / Bite / Freefall that reads **Whetstone, MidasRecoil, GlassHeart**. Two are wired differently and will NOT show up if you grep for `HasRelic`: **LavaBoots** via `HazardZone.requiredRelicID` (default `"LavaBoots"`, also set by `AcidBlobProjectile`), and **ScrapMagnet** via the static `ScrapMagnet` class (`ScrapMagnet.Attract`, called from `GoldPickUp` and `Shift Crystal`).

### Relic HUD (RelicHUD.cs)

`Assets/Scripts/RelicHUD.cs`, attached to a `RelicHUD` GameObject under `Canvas/GameplayHUD/`. **It is a fixed TOP-CENTRE loadout bar of `MaxSlots` cells + an "N/5" count** (corrected 2026-07-26; it was previously a middle-left vertical column). The bar **self-positions in code**, so it needs no scene re-anchoring — the legacy left-column container is disabled on `Start()`. Note `iconContainer` in SampleScene points at the HUD's OWN transform, so `BuildBar()` deliberately never disables it when `iconContainer == transform` (that would switch off the object building the bar).

- Subscribes to both `OnRelicAdded` and `OnRelicRemoved`; rebuilds all cells on either.
- Filled cells instantiate `RelicIconPrefab.prefab` and call `RelicIcon.Build(relic)`; empty cells draw a dim, gemless stone socket so full and empty read as one crafted row.
- Each cell carries a transparent `RelicSlotHover` hit-target (RelicIcon's own graphics are non-raycast) which drives the shared `RelicTooltip` and opens `RelicManagePanel` on click. `I` also opens it.

**Relic chip visual language (rebuilt 2026-07-26):** `RelicIcon.cs` disables the prefab's root Image and builds the chip procedurally to match the game's OWN hand-painted HUD chrome (`Assets/Art/panel 1.png`, the top-left stat panel), rather than generic UI: rarity **glow** → mottled-**stone** socket → **icon** art (`relicArt`, + drop shadow) → ornate **gold border** → four corner **gem bosses**. **Rarity is carried by the GEM colour, not by recolouring the frame** (amber Legendary / amethyst Epic / sapphire Rare / ruby Common — ruby matches the HUD panel's own studs), so every relic reads as the same gold-on-stone object as the rest of the HUD. Pop-in is **Update-driven** (EaseOutBack, unscaled) so it survives being built while GameplayHUD is inactive (relics granted from a hidden shop/slot pop in when the HUD reshows); Epic/Legendary get an idle glow pulse.

All the shared sprites live in **`RelicUISprites`** (`GoldBorder()`, `StonePanel()`, `GemSetting()`, `Gem()`, `GemColor(rarity)`, plus `AddGemStuds(...)` which studs a panel's border). Procedural + statically cached, no art files. `GoldBorder` carries a 9-slice border so panels use it too; the medallion draws it as **Simple** (a 9-sliced bevel would stretch). The Manage/Swap/tooltip panels all use this same chrome.

⚠️ **When editing these panels, keep content inset clear of the border AND the gem studs** (~52px on the Manage panel). The ornate border is much thicker than the old flat frame, and the original insets left text visibly crowding it.

---

## UI System

### Canvas Hierarchy

SampleScene's main Canvas contains:
- **`GameplayHUD`** — contains all in-game HUD elements (gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer trigger zone, **RelicHUD**, **QuestTracker**). Toggle with `SetActive(false)` to hide HUD during full-screen UI.
- **`QuestBoardOverlay`** — quest board panel (full-screen).
- Various menu panels (PauseMenu, ShopUI, SlotMachineUI, RewardScreen, etc.) as direct children of Canvas.

**When adding new full-screen UI panels**, hide GameplayHUD when they open by adding a `[SerializeField] GameObject gameplayHUD;` reference and toggling SetActive. ShopManager, SlotMachineUI, and QuestSystem already follow this pattern.

### `FlatUI.cs` — the new UI direction (2026-08-03)

**The designer has disliked the ornate stone-and-gold chrome "since the beginning."** `FlatUI.cs` is the replacement, prototyped on the Scrap Forge screen.

**It took two passes, and the first one's failure is the useful part.** Pass 1 delivered the literal brief ("soothing, simple, understandable, but also cool") as flat slate-blue panels, uniform rounded corners, neutral greys, one accent. The designer's verdict: **"it screams AI."** That was right — it was the house style of every dev dashboard, and crucially it had no *place* in it. Simple and generic are not the same thing.

**Pass 2 keeps the restraint but points every choice at the world: a sheet of iron on a workbench, lit by the forge.**
- **Warm charcoal, not slate-blue.** Act 1 is the *Oxidation District* — rust, not brushed steel. This single palette shift did most of the work.
- **Chamfered corners, not rounded.** Cut plate reads as a made object; a uniform corner radius reads as a web card. Biggest silhouette cue.
- **Directional light.** A lit top lip plus an ember glow rising off the *bottom* edge (firelight under the bench), instead of a uniform glowing border. Uneven light = physical object in a place.
- **Rivets and faint scuffs.** Small, dark, functional — fasteners, not jewels. Imperfection is what kills the "generated" feel.
- **Rules score across and fade at the ends** rather than running edge to edge like a CSS border.
- **The only two colours on screen are the game's own two resources:** charges in Shift-blue, costs in scrap-orange.

API: `Panel(chamfer)` / `Outline(chamfer, thickness)` (9-sliced chamfered plates), `Rivet()`, `FadedRule()`, `SoftGlow()`, `BottomGlow()`, `VerticalFade()`, `EmberDot()`, `Pixel()`. **All shapes are WHITE and tinted via `Image.color`**, so one cached sprite serves every panel. Shared palette at the bottom of the file.

**`UIEmberField.cs`** — drifting embers for a panel background (`UIEmberField.Attach(rect, count, colour)`); builds and animates its own Image dots, no particle system. Two things that would break it: it must use **`Time.unscaledDeltaTime`** (every screen it belongs on pauses the game, so scaled time freezes the embers solid), and it must **re-read the parent rect every frame** (the forge window's height is dynamic, so a bounds snapshot would leave embers outside a collapsed panel).

Lessons already paid for, don't re-learn them:
- **Get the SDF right.** Rounded box is `inside + outside - radius`; the chamfer is that box distance `max`'d with a normalised diagonal half-plane. Naive versions pinch the outline at corners.
- Textures need `FilterMode.Bilinear` — Point aliases the chamfer edges badly.
- **Hairlines need to be brighter than theory says**, or they don't register on a dark surface.
- **Atmosphere effects want roughly half the alpha you first reach for.** The ember at 0.085/140px was an orange wash owning the bottom third; ~0.05 over 120px is firelight. Scuffs at 0.045 read as *rendering glitches*; 0.022 reads as wear.
- ⚠️ **A glow that doesn't reach its container's edge must fade on that axis too, or it draws its own border.** The bottom glow originally reused `VerticalFade` (which only falls off in Y) inset 14px from the window sides — the sprite's hard left/right ends produced a visible vertical seam down BOTH edges of the panel. That's what `BottomGlow()` exists for: falloff in both axes.
- **Keep wear out of content columns.** The first scuff pass ran a streak straight through the title. They belong in margins that are empty at any content count.
- **Small icons inside dense text don't work.** A 17px scrap shard beside each cost read as a smudge fused to the first digit; the accent colour alone carries it.
- **An emblem needs STRUCTURE, or it reads as a lens flare.** Blompo's offer marks were a plain four-point sparkle behind a big soft glow and looked cheap. `ArcaneSigil` fixed it with a containing ring, rays of two lengths, and ticks outside the ring — plus a much tighter, dimmer glow, since the haze was doing most of the damage.
- **Detail placed exactly on another element disappears.** `ArcaneSeal`'s four diamond glyphs originally sat at the inner ring's radius and merged into it invisibly; they now punctuate the outer ring on the diagonals, clear of the twelve ticks.
- **Show the numbers a decision depends on.** Blompo's card-pick step listed only a bare charge count — no Shift cost, no maximum — so you chose which card to permanently alter without seeing what it cost or how much life it had. Chips now carry labelled SHIFT / CHARGES stats, and `StampChip` refreshes *both* on the bind frame because several blessings visibly change them.
- **Empty states must collapse.** `LayoutSections` lays the screen out top-down and resizes the window to its content, so an empty section shrinks to one explanatory line. The fixed-height version had two large voids and looked broken — and that state is *common*, since early in a run nothing is damaged or exhausted.

### Themes — same ideology, never the same skin (2026-08-03)

**Screens must NOT all look alike.** Designer's rule: share the ideology (flat procedural plates, restraint, directional light, a subtle particle drift, one meaningful accent), but each place gets its own material, and **the material should say what the place DOES**.

`FlatUI.Theme` is the mechanism — a colour set (`Surface`, `Border`, `EdgeLight`, `Accent`, text ramp) picked per screen:

| | **Iron** (`ScrapForgeScreen`) | **Arcane** (`BlompoScreen`) | **Loadout** (`RelicHUD`, `RelicIcon`, `RelicTooltip`) |
|---|---|---|---|
| What it is | a workbench you repair cards at | a mythic creature granting a blessing | what you're **carrying** |
| Palette | warm charcoal (rust district) | cold indigo | near-**colourless** |
| Light | fire from **below** | descends from **above** | none — it's not a place |
| Particles | embers **rising**, fast | motes **settling**, slow, twinkling | none |
| Corner marks | **rivets** (fasteners) | **four-point stars** (light) | none |
| Surface | scuffed and worn | pristine | plain, recessed sockets |

**The inversions are the point.** Warm/cold, below/above, rising/falling, worn/clean. When adding a screen, pick a material and invert something — **do not just retint Iron**.

**The Marketplace (`ShopScreenUI`) keeps its own material** — warm wood, striped canvas awning, lamplight — and was already bespoke rather than old chrome. What it needed wasn't a reskin but a PERSON; see "The keeper talks back" below.

**Loadout inverts a different axis: it's the only theme where the chrome is NOT the subject.** The other two dress a place, so the material carries the character. The relic bar dresses your inventory, sits over gameplay permanently, and the relic art is colourful pixel work — so the sockets are deliberately near-colourless and the theme is the quietest by weight. **Do not add a hue to the relic bar.** A permanent HUD element cannot compete with the game behind it the way a modal panel can.

`UIEmberField.Settings` carries the motion half (`Settings.Embers` / `Settings.Motes`): rise speed (negative = falling), lateral spread, size, life, sway, twinkle.

⚠️ **RARITY MUST SEPARATE ON MORE THAN HUE (reworked 2026-08-09).** The first palette was amber / violet / azure / cool-slate and the designer could not tell the tiers apart at a glance. Three of the four sat in the blue-violet quadrant with near-identical **luminance**, so the only cue was a ~40° hue step — invisible on a small sigil over a dark panel, and gone entirely for a colour-blind player. `FlatUI.RarityColor` now separates on **three channels at once**: hue spread right around the wheel (neutral → **green** → violet → amber; green is the biggest possible jump from both violet and amber), strictly ascending luminance (0.42 → 0.56 → 0.66 → 0.82, so a better blessing is literally brighter and the order survives greyscale), and saturation climbing from near-zero. Common stays the dimmest, for the reason already established below.

⚠️ **Rarity also has its own GLYPH now — `FlatUI.RaritySigil(rarity)`.** Every Blompo offer used one shared sigil, so colour carried the tier alone. Shape is read faster than hue and survives greyscale, colour-blindness and a 40px icon, so the marks progress **bare ring** (Common) → **ring + 4 axial rays** (Rare) → **ring + 6 rays + inner ring** (Epic) → **the full ornate `ArcaneSigil`** (Legendary). Legendary deliberately reuses the established emblem so the lesser tiers read as reduced versions of it rather than unrelated symbols.

Rarity note: the old chrome carried rarity as a gem set in gold. Without that frame **colour has to carry rarity alone**, so `FlatUI.RarityColor` is brighter and more separated than jewel tones, and Blompo tints the sigil, border, name and label together — four quiet signals instead of one loud jewel. **Common is deliberately muted**: at a lighter slate it rendered near-white and made the *weakest* offer the brightest thing on screen.

**On the relic bar, rarity is a coloured STRIP along the bottom of each socket**, plus a muted tint on the socket outline and (Epic/Legendary only) a slow glow pulse. The strip is the load-bearing signal: at 52px over moving gameplay a tinted hairline is not reliably readable, but a solid bar is legible at a glance. The tooltip repeats the rarity in its border and name, confirming what the strip meant. **Only the two rarities worth noticing animate** — that's what makes a Legendary catch your eye in a row of five.

**Blompo's blessing animation (`BlompoForgeFX`) was rebuilt to match (2026-08-03).** It used to be a hammer-and-anvil forging: three blows, sparks, screen shake. Once his screen went arcane, a smithy sequence fought everything else on the panel — he grants a charm, he isn't a blacksmith. The motion vocabulary is inverted the same way the palette was:

> forging → strikes, impacts, gravity, sparks flying **out**, the window rattling
> binding → orbit, convergence, weightlessness, motes drawn **in**, nothing ever hit

Four beats: GATHER (rune ring forms, motes stream in) → DRAW (ring contracts, everything accelerates) → BIND (`onSet` fires here) → SETTLE, where an `ArcaneSeal` contracts **into** the card and snuffs out. Two procedural sounds accompany it (`ProcSfx.ArcaneGather`, `ArcaneBind`).

The settle originally used an *expanding* ring, which the designer called bland — and re-reading it, that was the one beat in the sequence pushing **outward** while everything else converged. Pressing a seal inward finishes the idea the rest of the animation sets up. **When a beat feels weak, check whether it contradicts the sequence's own vocabulary before reaching for more particles.**

⚠️ **UI children are NOT clipped, so FX geometry is bounded by the WINDOW, not the stage.** A first pass used a 520px ring radius and scattered runes across the whole screen, outside the panel, onto the backdrop. The stage sits 60px below centre in a 762-tall window, so there is only ~321px of room downward — anything that must travel further does so on an ellipse squashed in Y (`VERT_SQUASH`). Check this whenever you add UI FX.

**Sound design note:** magic is **harmonic** (bell/chime partials 1,2,3,4,5.1), metal is **inharmonic** (bar modes 1,2.76,5.40,8.93 — see `ProcSfx.ScrapPickup`). That ratio choice is the whole difference between "charm" and "clank"; keep the two families distinct so a blessing and a scrap pickup are never confusable.

### The keeper talks back (`ShopScreenUI`, 2026-08-03)

The designer's brief for the shop was **"make the player feel like they are talking to a person who is trying to sell them stuff."** The stall already looked like a stall; what was missing was a shopkeeper.

- **He has a face.** `Shopkeeper.ResolvePortrait()` returns an assignable `portrait` sprite, falling back to the shopkeeper's own world sprite — so a placed stall gets a face with zero wiring. ⚠️ The fallback grabs the whole stall prop, not a head; **assign `portrait` for a proper close-up.**
- **He reacts to what you do.** Barks used to be one array with a single line picked at open — decoration that never changed. They're now split by EVENT (`Greetings` / `BrowseCard` / `BrowseRelic` / `BrowseService` / `TooPoor` / `Bought` / `AlreadySold` / `Farewells`) and fired from hover, purchase, refusal and the Leave button. **Affordability outranks item type** on hover: being told you can't afford it is more useful than a joke about what it does, and it's what a real trader would say to you eyeing something out of your league.
- **Speech is typed out a character at a time.** A line that snaps in whole reads as a label changing; typed, it reads as *said*.
- **Small body language** — `Mood.Lean` on browse, `Nod` on a sale, `Slump` on a refusal, plus a constant idle bob. Deliberately tiny: a portrait that lurches around pulls focus off the prices, which is what the player is there to read.
- **No line repeats back-to-back** (`lastLine`), because with pools this small plain randomness repeats constantly and repetition is what makes barks feel canned.
- Lamplit **dust** drifts through the stall (`UIEmberField.Settings.Dust` — warm, very slow, no twinkle). A shop is a place with air in it; stillness is what made the panel feel like a menu.

⚠️ `ShopScreenUI` already had an `Update()`. The keeper's idle bob is a `TickKeeperIdle()` called from it, **not a second `Update`** — and it skips while a mood coroutine owns the transform, or the two fight over `anchoredPosition`.

**Status: converted —** `ScrapForgeScreen`, `ScrapHUD`, `BlompoScreen`, `RelicHUD`, `RelicIcon`, `RelicTooltip`, `RelicManagePanel`, `RelicSwapScreen`, `ResourceBarUI`/`ResourcePanelHUD`, `ShopScreenUI`, `CardUI`. **The pass is complete.** (`PixelUI` remains and is fine as-is — the shop uses it for grain/frames.)

### Cards: rarity colour is the ART's job, not the UI's (designer 2026-08-06)

**Card rarity is telegraphed in the card ARTWORK, in colour: dark grey Common, light grey Uncommon, yellow Rare, purple Epic. There are no Legendary cards.** The incoming art has this baked in, so **UI code must not invent a second rarity colour system on a card** — two colour codes on one object that disagree is worse than one.

This is a live constraint, not a preference: `CardUI`'s blessing mark originally tinted itself by the *blessing's* rarity via `FlatUI.RarityColor`. That's a different axis, but no player would read it as one — and it contradicted the art (calling Rare azure where the art calls it yellow). It is now **one fixed teal on every blessing**, chosen to sit outside the grey/grey/yellow/purple palette and pushed green of Shift-blue so it can't read as a cost either. Blessing hierarchy moved to a channel the art doesn't use: **only Epic/Legendary blessings pulse.**

### `CardUI` — the blessing mark (2026-08-06)

`CardUI`'s only procedural chrome was the blessing badge; the card frame, cost medallions, rarity tag and name plate are all **painted into the card art sprite**, so "converting CardUI" meant converting that one mark. Three things were wrong with it and all three are fixed:

- **It wasn't on the card.** It was anchored to the card ROOT, whose RectTransform is a **200×100 stub** — while `cardArtImage` is the real 200×300 card face. The mark floated off the card's right *edge* at mid-height. It is now parented to `cardArtImage.rectTransform`, the only honest geometry on the prefab.
- ⚠️ **`cardArtImage`'s sprite is the WHOLE CARD FACE** (1024×1536), not the inner picture — frame, medallions and name plate included. Measured on the real cards, the inner picture occupies roughly **10%–80% of the card height**, so a naive small inset lands the mark inside the painted *name plate*, on top of the card's title. `MARK_INSET_Y = 62` (of 300) clears it.
- **The look** was a jewel in an ornate gold ring — the chrome this pass exists to remove, and its bright gold setting drowned the gem so different rarities read identically. It is now Blompo's own `ArcaneSigil` glowing over a soft dark halo: light *inscribed on* the card rather than an object stuck to it, tying the mark to the screen that grants it. The dark halo (not a frame) is what keeps it legible over busy artwork.

The mark deliberately does **not** say which of the seven blessings it is — the hover text names it. Seven legible glyphs at ~24 screen px is a bespoke-art job, not a procedural one.

Verified in play mode across the hand and the deck view: blessed cards mark, unblessed cards build no mark at all.

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

Rebuilt without Cinemachine, along the planned CameraShake-style design: holding Left Ctrl computes a mouse-direction `peekOffset` (clamped to `maxOffset`, smoothed with unscaled time) that `CameraFollow.LateUpdate` adds after zone clamping. **The component lives on Main Camera ONLY** — a duplicate copy on the Player prefab was removed 2026-07-16 (unguarded `instance = this` singleton; two copies made the winner a coin flip). Do not re-add it to the Player. Input is blocked while paused, while the hand drawer is locked, or when the player is dead. If peek "doesn't seem to work," verify scene presence and enabled state of the component first (per Common Pitfalls) — the code is fine. Note: the rebuilt CameraPeek does NOT set `PlayerController.isPeeking`; that flag is dead code.

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
8. **THE SPAWN IS A SAFE BEACH** (designer 2026-08-07). The player must be able to arrive, look around, read their deck and decide *before* anything can touch them. **No enemies on the platform the player spawns on, and nothing able to target them there** — ranged/flying enemies must not have line of sight to the spawn. Enforced by `LevelValidator` (LAW 8): it finds the spawn's contiguous ground run and fails on any enemy standing on it, then ray-checks ranged (`r s t`) and flying (`b`) enemies within 26 tiles and melee within 10. Line of sight, not raw distance — a spitter 20 tiles down a clear corridor is aiming at you; one 6 tiles away behind rock is not.
7. **Entry and exit must be far apart in the map** (designer 2026-07-14, after GenLevel6 v1 put the exit directly above the spawn behind a 2-thick slab): a Phase/Portal card must never be able to skip the level. Keep the spawn and the ExitDoor in different regions — roughly 20+ tiles apart, separated by whole chambers of solid rock, never by a thin wall or single floor slab.

9. ⚠️ **PROVISIONAL — the designer said this was written up wrong and will restate it ("we can see about that later on", 2026-08-08). Do not treat it as settled; ask before designing to it.** The rough shape, from GenLevel8 where they placed a Blompo on such a platform themselves: a ledge reachable only by dropping onto it, or only along one narrow guarded approach, wants **something on it to claim** — loot, a shop, Blompo, an NPC — which is what makes the player accept the narrow path with an enemy in it. What is NOT yet confirmed is how far that generalises.

### Level Text Importer (NEW 2026-07-13 — Stage 1)

`Assets/Scripts/Editor/LevelTextImporter.cs` adds menu **Deckshift → Import Level From Text…**: it reads an ASCII grid `.txt` (legend + example: `Assets/LevelTexts/TestRoom1.txt`) and builds a room prefab into `Assets/LevelGenerated/` satisfying the room contract (`CameraBounds` zone auto-sized to the grid, `GirisNoktasi` spawn, ExitDoor). Markers: `#` ground, `S` spawn (exactly one), `X` exit, `m/r/l/M/b` enemies plus the zombie tiers `z` Shambler / `Z` Rotbrute / `s` Spitter (added 2026-07-16; `b` = `YeniLeveller/BatMan.prefab` — the real flying bat with AeroBatAI; **`Assets/Prefabs/AeroBat.prefab` is a legacy husk with NO AI**, its dead missing-script component was removed 2026-07-13 because Unity refuses to save any new prefab containing missing scripts, which broke level import), `^/T/W` hazards, `+/g/C` pickups, and mechanics (added 2026-07-13): `E` Elevator (Cainos prop, floats at cell center — tune travel in Inspector), `F` UpdraftFan (draft zone ~3 tall, liftForce 20 ≈ 5-7 tiles of lift — chain fans as relays for taller climbs), `w` AcidWater (~6 wide pool, damage+slow), `K` WreckingBall (floats at cell center, tune anchor/swing), `c` CrumblingPlatform (**do NOT use in levels — its sprites are outdated; use `T` Trapdoor instead, designer 2026-07-14**), `t` Taret turret, `$` Shopkeeper_NPC, `B` Blompo (`Assets/Prefabs/Blompo.prefab`, added 2026-08-08 — NPCs are loot, see below) (its TMP/UI scripts live in Library/PackageCache — an Assets-only guid scan wrongly flags them "missing").

**Interactive structure markers (2026-07-14):** `=` one-way platform tiles (own tilemap: TilemapCollider2D via CompositeCollider2D + one-way PlatformEffector2D on Ground layer; painted with the thin `_144` lip so they read differently from solid strips) · `G` gate cells (vertical G-runs become one sliding **Gate** — `Assets/Scripts/Gate.cs`, solid Ground-layer collider, slides down + fades on Open, Cainos Gate 01 sprite scaled to height) · `L` Lever (`YeniLeveller/Lever.prefab`; its `OnFlippedOn/Off` UnityEvents are now public) · `A` **Shift Altar** (`Assets/Scripts/ShiftAltar.cs`: IInteractable on the Interactable layer (12), pays `shiftCost` Shift via `player.SpendShift`, free in hub per the umbrella rule, procedural floating TMP cost label, fires public `OnPaid`). **The importer auto-wires each `L` and `A` to its NEAREST `G` gate** (lever On→Open/Off→Close, altar OnPaid→Open) via `UnityEventTools.AddPersistentListener` — rewire in Inspector if a level needs different pairing. Only header directive besides `!backwall` is `!name`. The importer pre-checks for missing scripts before saving and names the culprit object.

**Tile painting reproduces the hand-built visual language** (learned by auditing EfeVrl7's 546 painted tiles, 2026-07-13): an optional "BackWall" backdrop tilemap (**opt-in via `!backwall: on`** — the designer prefers adding backdrop/decoration by hand; when on it must be on the **"Background" sorting LAYER**, NOT Default: ExitDoor's sprite is Default order -1 and gets swallowed by a Default-layer backdrop), plus a "Ground" tilemap (layer 3, TilemapCollider2D, Default sortingOrder 1, z=1). Any 1-tile-thick run (air above AND below, wall-attached or floating) gets the `_112/_113/_114` strip treatment with caps on open ends; the gappy `_186` fill goes in exactly ONE row under a surface, deeper cells get dark `_185` (repeating `_186` looks like a broken colonnade). Frame cells (`#` connected to the grid edge) get role tiles from `Assets/LevelSinasi/biseyler/`: air-above → floor surface `_144`, air-below → ceiling face `_96`, wall faces → inner accent tiles `_188`/`_157` ONLY when backed by a real solid tile (2-thick walls), else the clean outer tiles `_189`/`_156` (the inner tiles have protruding brick nubs + bumpy collision — wrong for 1-thick walls), buried → `_153/_154` top rows, `_156/_189` outer walls, `_186/_185` floor fill. Free-standing `#` platforms: horizontal runs of 2+ get the **platform strip set `Extra_112/_113/_114`** (left cap / middle / right cap — learned from EfeVrl6's interior platforms); lone blocks and 1-wide pillars get chunky `Ground Dirt` block tiles (`#..#..#` = the hand-made stepping-stone style); buried rows of thick platforms get floor fill. NOTE: the edge-strip tiles look like sparse floating crumbs if painted in mid-air, and adjacent Dirt blocks melt into dark blobs — never tile either as strips.

### Sprite-less tiles are the designer's ERASER — do not "fix" them (2026-08-09)

Several tiles in the pack have a **null sprite** (`Ground_13`, `Ground Dirt_31`, `Ground_25`, `Ground Dirt_15` — indices past the end of the sheet). **The designer paints these deliberately to blank a cell**, because it is quicker than switching to the erase tool. Verified by clean-room physics test: `colliderType = Sprite` + null sprite ⇒ no outline to trace ⇒ **draws nothing and collides with nothing**. The technique is safe.

⚠️ **Do not report these as broken tiles.** A scan of the hand-made rooms found 218 such cells (98 in `efeslevel2`, 116 in `efeslevel3`) and they were briefly misdiagnosed as invisible platforms. They are intentional erasures.

⚠️ **But the safety rests entirely on them never getting Grid collision.** A Grid copy of a sprite-less tile is a full-cell collider that renders nothing — an **invisible wall everywhere the designer erased**. Not hypothetical: `Ground_13` was in `MaskTiles` until 2026-08-08 and did exactly that in generated rooms. `TileVariantGenerator` now refuses to build a `Solid` variant of any null-sprite tile, and `LevelTextImporter` already throws if a table tile has no sprite. **Keep both guards.**

⚠️ **Tooling reads an eraser cell as SOLID**, because `Tilemap.GetTile()` returns non-null for it. This contaminated the 8-neighbour mask measurement taken from the hand-made rooms — some configurations counted visually empty cells as solid neighbours, which is part of why the rare corner masks looked erratic. Any future measurement over their tilemaps must skip tiles whose `sprite == null`.

⚠️ **TEN OF THE GROUND TILES ARE BIGGER THAN THEIR CELL — NEVER FORCE GRID COLLISION ON THEM (2026-08-08).** The Cainos ground palette is **not a set of 1×1 blocks**; it is a set of whole pre-drawn **platforms**, each centred on its cell: `Ground_11` **3×1**, `Ground Dirt_0` and `Ground_0` **3×3**, `Ground_1` **2×2**, `Ground Dirt_12` 3×1.6, `Ground Dirt_14` 3×1.3, `Ground Dirt_4` 1.4×2, `Ground_3`/`Ground Dirt_3` 2×1, `Ground Dirt_11` 1.3×1.3.

Grid collision is exactly one cell, so a Grid copy of a 3×1 platform strip **keeps drawing the outer two thirds while deleting their collision** — the player sees platform, steps on it, and drops straight through. That was the "the edges of the mid-air platforms have no colliders" report, and it was self-inflicted: the earlier protruding-brick-nub fix generated Grid `… Solid` variants for *every* painted tile. **The hand-made rooms use `Sprite` collision throughout, which is exactly why their platforms have always felt right — what is drawn is what is solid.**

`TileVariantGenerator` now **skips oversized tiles** when building `Solid` variants (`IsOversized`), so they keep native Sprite collision while cell-sized tiles still get the nub fix. Verified by probing `Physics2D.OverlapPoint` across a platform's drawn width: solid over the full 3-unit art, empty outside. **Before adding any tile to the painting tables, check its sprite size against the cell.**

⚠️ **JUDGE THE MASK TABLE BY SAMPLE COUNT, NEVER BY WINNER SHARE (trimmed 2026-08-08).** `MaskTiles` was measured by taking the tile the designer used most for each 8-neighbour configuration — but some configurations appear only a handful of times across all six hand-made rooms, so their "winner" is a coin flip. And the coin flips are precisely the **outer-corner** configurations: a room has hundreds of buried and wall-face cells and only a few of any given corner. Symptom: mask 193 (a wall's bottom-right outer corner) resolved to `Ground Extra_205` on **2 votes out of 8**, and Extra_205 is a brown interior-looking block — so every generated wall had a brown nub sticking out of that corner. Entries with **n < 10 are now dropped**, falling through to the hand-written, internally consistent `Mask4Tiles`. **Do not trim on winner share:** it is low almost everywhere (mask 255 has n=1231 and its winner takes 12%) because the designer deliberately varies tiles across a mass — that is variety, not uncertainty, and trimming on share would gut the table.

⚠️ **NPCs COUNT AS LOOT when populating a room (designer 2026-08-08).** A Blompo (`B`) or a shopkeeper (`$`) is a place to *spend* what the player picked up, so it pays a room out just as a chest does — and arguably better, since it converts carried gold into something kept. **Chests are the expensive way to reward a room and a pile of them reads as filler**; ~3 is a sensible ceiling even for an Elite. Shift crystals are the exception — the designer considers those genuinely needed, so don't thin them out.

⚠️ **A fitted prefab's COLLIDER is often not centred on its transform (2026-08-08).** `FitAcidToPit`
scaled the pool by its `BoxCollider2D.size` but then positioned the *transform* at the pit centre, as
if the collider sat on the origin. `AcidWater`'s collider carries `offset (0, 1.27)`, so every pool in
every generated room floated a scaled 1.27 units too high — the water's surface sat a full tile above
the floor it was supposed to be sunk into. It imported without a single warning and looked *almost*
right, which is why it survived two rooms. Fixed by backing the scaled offset out of the position.
**Whenever you size or place a prefab from its collider, read `offset` as well as `size`** — and
verify the result by asking for the instance's world bounds, not by eyeballing the transform value.

⚠️ **`T` Trapdoor grounds to the BOTTOM of its cell, like an enemy standing there.** Used as a bridge
across a pit that is what you want one row *above* the pit mouth: place the marker on the standing row
(the row the surrounding floor's occupants use), not in the gap itself, or the planks end up a tile
down inside the hole — under the acid, invisible.

**Entity placement:** most enemies have kinematic physics and do NOT fall, so the importer auto-grounds standing markers (`X m r l M C ^ W T` + the spawn): after instantiating, it measures the instance's combined renderer bounds (ignoring particles/trails, collider fallback) and shifts it so bounds-bottom sits exactly on the cell floor. Floaty pickups (`+ g`) and flyers (`b`) stay at cell center. Decoration (props) stays a manual pass by design. Planned next stages: movement-metrics doc (jump/dash distances in tiles) then batch room drafting.

### Level Validator (2026-08-07) — run this BEFORE importing a level

`Assets/Scripts/Editor/LevelValidator.cs`, menu **Deckshift → Validate Level Text(s)**.

`LevelTextImporter`'s own validation only counts markers (one `S`, an `X`, unknown chars). Every one of the seven Level Design Laws was enforced by prose in a comment header, which demonstrably does not work. This makes them executable: it simulates the real player and flood-fills reachability from the spawn.

**`LevelValidator.Overlay(path)` is the tool to reach for when authoring** — it prints the room with `o` = reachable standing cell, `x` = standable but ORPHANED. It answers "where does the route actually stop?" directly, and it's how the validator itself gets checked.

⚠️ **The movement model constants are read from `PlayerController` + `Player.prefab`, not estimated. If jump/gravity/speed change in the game, change them here or the validator quietly starts lying.**

**Measured from the code 2026-08-07 (tile = 1 world unit), designer-confirmed by playtest:**
- **Jump apex ≈ 4.9 tiles.** Confirms Law #2 ("mandatory rises at 4, 5 is the edge").
- **Airtime ≈ 1.5s** (0.90s up at −12.26, 0.60s down at −26.98 thanks to `fallMultiplier`).
- **Flat jump reach ≈ 12 tiles** — simply `moveSpeed × airtime`. Still about **2× the "flat gaps ≤ 5-6 tiles"** the design laws assume, which is worth knowing when rooms play flat.

⚠️ **`PerformJump`'s horizontal impulse is DEAD CODE — do not model it, and know it's a landmine.** `PerformJump` does `AddForce(moveInput * jumpForce, jumpForce)`, which looks like a running jump should launch at 8 + 11 = 19 u/s. It doesn't: **`isGrounded` is assigned only in `Update()` and nothing clears it on jumping**, so the very next `FixedUpdate` still sees `isGrounded == true`, runs the grounded branch (`rb.linearVelocity = (moveInput * moveSpeed, y)`) and overwrites the horizontal impulse back to 8 about 20ms later. Vertical is untouched, which is why the apex is unaffected. **If anyone ever "fixes" that stale `isGrounded` read, every jump instantly gains a large horizontal boost and every gap in every level becomes trivially clearable.**

(An earlier version of this section claimed a 15-tile reach and a 3× discrepancy, from modelling that impulse as if it survived. It does not.)

**Modelling notes:** the player occupies 1 column × 2 rows. `Solid` (blocks) and `Support` (can land on) are deliberately split — one-way `=` platforms support from above but pass through from below, and treating them as non-support produced a false "exit unreachable" on GenLevel5.

### Tile appearance — what we CAN change (2026-08-07)

Verified, not assumed. The tilemaps render with **`Sprite-Lit-Default` (URP 2D lit)** and the scene has a global `Light2D` at **0.5 intensity**, so:

- **2D lights affect tiles.** Glowing platform edges are achievable with a Light2D, no art needed.
- **Tiles have a `color` field, but every pack tile ships with `TileFlags.LockColor`**, which makes `Tilemap.color` / `SetColor` no-ops on them. `TileVariantGenerator` (menu **Deckshift → Generate Tile Variants**) sidesteps this by writing DUPLICATE `Tile` assets pointing at the same sprite with their own colour — no shader work, no texture edits, no risk to hand-made rooms.
- **The textures are editable** — real 512×512 sheets (`TX Tileset - Dungeon Ground Extra.png`); `readable=False` is just an import setting to flip if pixel edits are ever wanted.

⚠️ **Tint darker than you think and you'll get a black hole.** These render through a 0.5-intensity light, so the scene already halves your value. A 0.42 deep-rock tint measured ~0.21 on screen and the mass read as a pit. Multiply by the light, *then* pick.

⚠️ **THE DEEP-ROCK INTERIOR IS SIGNED OFF. DO NOT "IMPROVE" IT (2026-08-08).** Two changes were made to it and both were reverted the same day at the designer's request: brightening `FlattenSprite` to pull toward the **mean** instead of the 25th percentile, and **jittering** the depth threshold to break up the Chebyshev metric's rectangular contours. Both are measurably more "correct" and the designer rejected both on sight — the interiors read better dark and hard-edged. The low percentile and the hard depth-3 step are deliberate. The jitter was actively harmful besides: a +1 nudge pushed **deep tiles out to depth 2**, one cell from the face, producing dark blocks stuck to wall edges.

⚠️ **DISTINGUISH "THE INTERIOR OF THE MASS" FROM "ONE TILE ON ITS OUTER EDGE".** The designer's complaint that "the corner tiles look really bad" was about a **single mask entry** picking a brown interior-looking tile at wall corners — not about the fill behind it. Reading it as the fill cost a full revert. When feedback points at a tile, find *that tile's* mask before changing anything global.

**Deep interiors are painted, not skipped.** An earlier pass left cells >2 from air unpainted so the backdrop showed through — that was worse, because solid rock then reads as open background and misleads the player, especially when peeking with Ctrl. They now get **darkened copies** of the interior tiles: same art, same collision, recessed value.

⚠️ **`TX Tileset - Dungeon Ground_13` is BROKEN — it has a NULL SPRITE.** The pack's valid range stops at `Ground_12`; `_13` is one past the end. It was the most-used tile in the measured platform-run data, so the importer painted every ledge with nothing and generated rooms genuinely had **invisible mid-air platforms** (30 of 499 cells). Replaced with `Ground_11`. **`LevelTextImporter` now fails the import if any table tile is missing OR has a null sprite** — a resolve-only check passes this happily, which is how it survived.

**First run found:** GenLevel3 is **unfinishable** — its header advertises a "zigzag staircase" that was never drawn into the ASCII, so the only route up is the fan relay, violating Law #1. GenLevel4/5 + ToyboxTest carry banned turrets/one-ways. GenLevel1–4 sit at 14–18% rock density (mostly empty void); the two that read as real rooms, GenLevel5 and GenLevel6, are both 67%.

### Room Pool

`LevelManager.roomPrefabs` holds the pool of room prefabs. **Element 0 must be the hub;** elements 1..n are the run's combat levels. The boss room is NOT in this list — it has its own `bossRoomPrefab` slot.

**Verified pool contents (2026-08-08):** `[0] hub, [1] efeslevel1, [2] efeslevel2, [3] efeslevel3, [4] EfeVrl4, [5] EfeVrl5, [6] EfeVrl6, [7] EfeVrl7, [8] GenLevel7, [9] GenLevel8, [10] GenLevel9` + `bossRoomPrefab = BossRoom`. So the run is **10 combat levels**. All satisfy the room contract (CameraBounds / GirisNoktasi / ExitDoor), and only `hub` has a `HubMarker`.

⚠️ **THE `.txt` IS NO LONGER THE SOURCE OF TRUTH FOR `GenLevel7/8/9` (2026-08-09).** The designer has hand-edited the built prefabs — moved loot, placed a Blompo, erased tiles. **Re-importing any of them from its text file DESTROYS that work**, and also renumbers every fileID so `LevelManager.roomPrefabs` loses its reference. Edit these rooms in the Unity editor, or if a text re-import is genuinely needed, diff the prefab first and re-apply the hand edits afterwards. `GenLevel8` has carried hand-tuning since 2026-08-08; 7 and 9 now do too.

**Tier tags (2026-08-08):** the three importer-built rooms carry `RoomTier` — `GenLevel7` **Fight** (horizontal corridor), `GenLevel8` **Fight** (vertical shaft), `GenLevel9` **Elite** (loop; the pool's first Elite room). The seven originals stay untagged and therefore serve every tier, so eligibility is **7 Skirmish / 9 Fight / 8 Elite**. Verified by driving the real `PickNextRoomPrefab`.

#### Room inventory — relevant to the planned map system (audited 2026-07-18)

**24 prefabs in the project satisfy the FULL room contract, but only 9 are wired into LevelManager.** That means ~15 contract-valid rooms are sitting unused:
- **`Assets/LevelGenerated/`** — `GenLevel1..9`, `TestRoom1`, `ToyboxTest`, `ToyboxTest 1` (12 rooms, importer output). **`GenLevel7` (Fight, horizontal corridor), `GenLevel8` (Fight, vertical shaft) and `GenLevel9` (Elite, loop) are the three built to the corrected movement budget and passing `LevelValidator`** — the earlier six predate it and several fail. All three still need a `RoomTier` component and a slot in `LevelManager.roomPrefabs` before they enter the run.
- **`Assets/LevelSinasi/CainosLeveller/`** — `kuzeymap`, `Room_Easy_01`, `sinasiBigLevel` (3 rooms).
- **Legacy/retired** — `Assets/LevelEfeS/old_levels/` (`-1`, `0`) and `Assets/LevelEfeVrl/Old Levels/EfeVrl2`. (`Old Levels/` also holds the six contract-INCOMPLETE retirees listed below; the folder was consolidated from a stray `Assets/LevelEfeVrl 1/` copy — don't be surprised by the git rename.)

**Why this matters:** the map system's blocker was framed as "we need to build many more levels." The truer statement is **"a dozen contract-valid rooms already exist and need quality/correction passes, not creation from scratch."** That is a much cheaper path to the ~15-30 rooms a map needs.

**Rooms that would BREAK if naively added to the pool** (incomplete contract — verified): `efeslevel4` has CameraBounds + GirisNoktasi but **no ExitDoor** (unfinishable). `EfeVrl1`, `EfeVrl3`, `EfeVrlLevel1..4` (all six now under `Assets/LevelEfeVrl/Old Levels/`), plus `kuzeymap2`, `kuzeymapv1`, `CainosLevel`, are **missing `CameraBounds`** (camera would not clamp). Always re-check the three-part contract before adding a room to `roomPrefabs`. (Note: `CameraBounds.prefab`, `GirisNoktasi.prefab`, `MainMenu`, `GameOverScreen` also match the scan but are shared components/UI, not rooms.)

### Run Map — BUILT AND WORKING END TO END (2026-08-06)

**The whole system is done and verified in play mode: graph, generator, room routing, and the `M` screen.** Run order is driven by the graph, not by a shuffled pool — the section below describes the pre-map order, which survives only as a fallback.

| File | Role |
|---|---|
| `RunMap.cs` | `MapNodeType` / `RechargeType` enums, `MapNode`, `RunMap`. Pure data + queries, no Unity types. `Validate()` and `ToAscii()` live here. |
| `RunMapGenerator.cs` | `RunMapSettings` (Inspector-tunable) + the carving generator. |
| `RunMapManager.cs` | Singleton owning the act and the player's position. **Self-bootstraps** via `RuntimeInitializeOnLoadMethod` — no scene wiring. Also owns the `M` key. |
| `RunMapScreen.cs` | The map UI. Procedural, self-instantiating, Verdigris theme. |
| `MapGlyphs.cs` | Procedural node + recharge symbols. |
| `RoomTier.cs` | Marker on a room prefab root declaring which tier it serves. |

**Where the choice happens:** `RewardManager.FinishReward()` — after the card reward, before the next room exists. It calls `RunMapScreen.OpenForChoice(...)` only when `RunMapManager.NeedsRouteChoice` is true, which is **false if only one branch is available** (a forced screen with a single button is ceremony, not a decision) **and false if the player already planned with `M`** (which is what makes planning worth doing). `M` opens the same screen in planning mode: clicking marks a branch and stays open. In forced mode Escape, `M` and the backdrop all refuse to dismiss it, and clicking commits and continues the run.

⚠️ If `RunMapScreen` can't find a Canvas, `OpenForChoice` **invokes its callback anyway**. A missing Canvas must never strand the run in a room with no way forward.

**Things that will bite you if you forget them:**

- ⚠️ **Recharge rooms are an ATTACHMENT to a node (`MapNode.recharge`), NOT a node.** Modelling them as nodes would make them floors, which the design forbids. `LevelManager` spawns the combat room first, then the recharge room, *without advancing the map* (`pendingRecharge`).
- ⚠️ **Only Fight and Elite may carry a recharge room, never Skirmish.** That is the entire run economy, not a tuning value — `Validate()` re-asserts it so it can't rot.
- ⚠️ **The map never promises a room it cannot spawn.** Recharge types are generated only for the prefab slots assigned on `LevelManager` (`foundryRoomPrefab` / `marketRoomPrefab` / `wellRoomPrefab`). **All three are empty today, so acts currently draw ZERO recharge icons.** Each type starts appearing the moment its prefab is assigned — nothing else to do.
- **Untagged rooms serve every tier.** The 7 existing rooms predate `RoomTier`, so requiring tags would have meant a broken map until a chore was finished. Tagging narrows a room; not tagging costs nothing.
- `ToAscii()`'s edge rows show **direction from the source column**, not lines to scale — a wide fan-out (the hub does this) renders as one `\|/`. Use `Validate()` or the raw `next`/`prev` lists to confirm a specific connection.
- **`RunMapManager` is scene-local, no `DontDestroyOnLoad`** — a map is per-run and must reset on death, exactly like QuestSystem's quests.

**Measured behaviour** (500 seeds, 2000 random routes, default settings — 8 floors, width 5, 4 paths): 0 invalid acts, 0 uniform floors. Per route: **2.34 Skirmish / 2.39 Fight / 1.28 Elite**, **1.44 recharge rooms** (min 0, max 5), and **55% of random routes never pass a Market**. That last number is the one to watch — it is fine if the player can *see* the Market and route to it, and bad if they can't; re-measure it once the map screen exists.

`BreakUniformFloor` exists because the late-floor weights produced all-Elite rows often. A floor where every branch is the same type is a toll, not a choice, which defeats the reason difficulty is the node type at all.

**Traps hit while building the screen — don't re-learn them:**

- ⚠️ **`Image.Type` defaults to `Simple`.** The window outline is a 26px 9-sliced sprite; left at Simple it was stretched across the whole 1040×780 window and rendered as an enormous soft octagon hanging outside the panel. Any FlatUI `Panel`/`Outline` used at panel scale **must** be set to `Image.Type.Sliced` explicitly — the local `AddImage` helper does not do it for you.
- **Text pivots.** A label positioned by offset with a centred pivot places its BOX centre, so a 34px-tall label put its first line back on top of the glyph it was labelling. Pivot to top (or bottom) whenever the offset is meant to clear something.
- **Node labels must be narrower than they look like they need to be** — neighbours on a floor sit about one column apart minus jitter, and 150px labels collided on any floor that filled up. Horizontal jitter is deliberately tighter than vertical for the same reason.
- **`pathCount` must match `width`**, or no route ever starts in the centre column and the act draws as two arcs around an empty middle.
- ⚠️ **Deferred `Destroy` will bite any test that clicks a map button.** `Refresh()` deactivates old nodes before destroying them, but they survive until end of frame, so `GetComponentsInChildren<Button>(true)` still returns the PREVIOUS chart's buttons — whose listeners point at nodes that are no longer reachable, so the click silently does nothing. Filter on `activeInHierarchy`. This produced a convincing false "callback never fired" failure.

### `roomPrefabs` emptied for testing — RESTORED 2026-08-06

Kept as a diagnostic pointer, because the symptom is confusing. Commit `2f236ad` ("h") left `LevelManager.roomPrefabs` holding only `[0] hub` — the designer had emptied it for a test. The map still generated fine, but every combat node failed to spawn, logging *"no combat room available"*, and the player never left the hub.

Restored to the full 8 (hub + `efeslevel1-3` + `EfeVrl4-7`) by resolving the GUIDs recorded in `b2760be`, so the list is byte-identical to what it was rather than rebuilt by filename.

**If the run stops advancing past the hub, check this list first** — an empty or short `roomPrefabs` looks like a map bug and isn't one. It happened again on 2026-08-08 (found as `[hub, <NULL>]`) and was restored, again by resolving the GUIDs from the scene's last commit rather than re-picking by filename.

⚠️ **DELETING AND RE-IMPORTING A PREFAB SILENTLY NULLS EVERY REFERENCE INTO IT.** The `<NULL>` above was a room the designer had slotted for testing. Re-importing a level (`delete the .prefab`, then `Build` again) **keeps the asset GUID** — the `.meta` survives — but **renumbers every fileID inside the prefab**. A scene reference is `{fileID, guid}`, so the guid still resolves while the fileID matches nothing: the link looks valid in YAML and reads as `null` in the Inspector. Before deleting a generated room prefab, check whether anything points at it, and re-assign afterwards. This is why `GenLevel8` is re-tagged via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` rather than a rebuild.

### Run Order — the pre-map order, now a FALLBACK ONLY (reworked 2026-07-02, superseded 2026-08-06)

⚠️ **This is no longer how the run is ordered.** `PickNextRoomPrefab()` routes through the map (above); the logic below now lives in `PickNextRoomPrefabWithoutMap()` and runs only if `RunMapManager.instance` is somehow null. It is kept as a *named, obvious* fallback because a missing manager silently reverting to random rooms would look almost right.

`LevelManager` was changed from an endless-refill pool (which repeated the same level forever) into a **finite, structured run**:

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

All card and enemy numbers derive from the anchor table in **`CardAnchors.md`** (project root, 2026-07-15). Key facts: damage unit = **15** (one Fireball); **player starts with 40 Shift** (Player.prefab overrides the `maxShift = 3` script default — do NOT treat Shift as scarce at base; lowering the pool is the planned ascension difficulty knob); enemy HP is tiered so **fodder ≈ 12 HP dies to one Fireball**, up to Moss Knight 300. Early enemies are built from the Cainos zombie prefabs (recipe in `CardAnchors.md` §6). **Three zombie tiers built 2026-07-16**, importer markers live: **Shambler** `z` (12 HP fodder, melee), **Rotbrute** `Z` (25 HP grunt, 1.15× bigger, harder melee), **Spitter** `s` (18 HP ranged — `ZombieSpitterAI` lobs a projectile on a windup). **Enemy HP retuned 2026-07-16:** Melee 40, Ranged 25, Slime 10, Mimic 30 (untiered), Boss 300.

**Enemy move-speed retune (2026-07-17):** the AIs (`MeleeEnemyAI`/`ZombieSpitterAI`) leave `MonsterController.inputMoveModifier` false, so an enemy's effective ground speed is the max for its `defaultMovement` mode. Final values, all **Walk** mode: **all three zombies = 1.2** (`walkSpeedMax`, deliberately uniform per designer), **MeleeEnemy = 1.4** (buffed a hair above the zombies so it stays the stronger threat), **RangedEnemy = 1.2** (untouched). MeleeEnemy is a prefab **variant** sharing a base with RangedEnemy, so its 1.4 is a variant override and does NOT move RangedEnemy — verify with the effective-value dump (`GetComponentInChildren<MonsterController>()`) if you touch either. Caveat: the Cainos animator has NO speed-scaled playback (only a walk/run blend), so pushing these speeds much higher foot-slides badly — an earlier Run-mode ~3.x pass felt too fast and was reverted. Tune the per-prefab `walkSpeedMax` in the Inspector.

**Spitter projectile — green-goo `SpitGlob` (2026-07-17):** the spitter used to reuse the turret's red bolt `Mermi.prefab` (still the turret's), which read as ugly/placeholder. It now fires `Assets/Prefabs/SpitGlob.prefab` — a dedicated acid-glob whose visual is **procedural** (`Assets/Scripts/SpitGlob.cs`, house pattern: runtime-built goo sprite, squash-stretch wobble, tapering `TrailRenderer` goo streak; no art). SpitGlob sits on the **Projectile layer (8)** — REQUIRED for its trigger to hit the player; if you clone it, keep that layer. Movement/damage still come from the shared global `Projectile` component. NOTE: there are **three** `Projectile` types (global + two Cainos namespaces), so MCP component-add by short name is ambiguous and fails — add it via `execute_code` (`using`-scoped to the global one) or clone an existing prefab.
- **ShieldEnemy has no sprite** → it's unused in levels. Compose one from the Cainos packs (armored humanoid + shield prop) when convenient. The enemy *logic* works; it's purely missing art.
- ~~**Fireball sails over short enemies**~~ **FIXED 2026-07-16.** The Fireball prefab's tiny 0.137 `CircleCollider2D` is now a vertical `CapsuleCollider2D` reaching from wand height down to ~0.30 above the floor (world hitbox F+0.30→F+1.55), so it hits slimes/mimics without detonating on ground tiles. Launch height unchanged; sprite still casts from the wand. See `CardAnchors.md` §7.

### Pattern

- **`EnemyHealth`** base script — handles damage, flash, death, and (since 2026-08-03) **scrap drops**. ⚠️ Before that date this file claimed it "handles drops" and it did not — there was no drop logic of any kind, which is exactly why kills paid nothing. Drops now go through `scrapDropOverride` (−1 = auto-tier from `maxHealth`); the override is the hook for shift-infused elites. **Currently the only callsite that reports KillEnemy/AirKill to QuestSystem.** `Die()` calls `RelicManager.OnEnemyKilled()`, `QuestSystem.ReportEvent(QuestType.KillEnemy, 1)`, and (if airborne) `QuestSystem.ReportEvent(QuestType.AirKill, 1)`. It now also exposes C# events: **`OnDamaged`**, **`OnDamagedAmount(float)`** (carries the hit size — the boss flinches on big hits), and **`OnDied`** (fired inside `Die()` right before the GameObject is destroyed — the boss uses it to hand music back and to spawn its death VFX). **CRITICAL: `Die()` fires `OnDied` and then `Destroy(gameObject)` in the SAME frame**, so an `OnDied` handler must NOT rely on the enemy surviving — anything that needs to outlive the death (VFX, loot) has to run on its own separate object (see `BossDeathVFX`). Non-event death consequences are still direct calls inside `Die()`.
- **AeroBat (BatMan)** — uses Cainos pack visual + custom `AeroBatAI`. Parent has Kinematic Rigidbody2D + Polygon trigger collider. Raycast LOS aimed at player chest (+0.5 Y), shortened by 0.3 to avoid hitting tile at player's feet. State machine: Idle → Preparing → Diving → Returning.
- **MeleeEnemy**, **RangedEnemy** — based on Cainos pack patterns.

**`TakeDamage(float damage, Transform damageSource = null)` does not currently track damage source.** Spike or hazard kills would credit the player's kill counter the same as direct kills. Minor concern; flag if it becomes design-relevant.

### Layer Convention Mismatch (Known Issue)

**Verified against every enemy prefab 2026-07-18** (an earlier version of this file wrongly claimed MeleeEnemy was on Default — it is on Enemy):

- **Default layer (0):** AeroBat, BatMan, ShieldEnemy, Mimic, **Shambler, Rotbrute, Spitter** (all three zombies).
- **Enemy layer (11):** **MeleeEnemy**, RangedEnemy, SlimeEnemy, Taret, PatrolEnemy, MossKnightBoss.

Two consequences, both load-bearing:
1. Many systems check via the `enemyLayer` mask, which **misses every Default-layer enemy** (including all three zombies). The workaround in PlayerController is to use `GetComponentInParent<EnemyHealth>()` instead of relying on layer masks for head-bounce detection.
2. **`groundLayer` (2056) includes layer 11**, so the player can **stand on** MeleeEnemy / RangedEnemy / SlimeEnemy / Taret / PatrolEnemy / MossKnightBoss — but **not** on the zombies, bats, Mimic or ShieldEnemy. That asymmetry is accidental, not designed.

**Be aware of this when adding new enemies — pick a layer and stick with it, or use the EnemyHealth-component approach.** (Note: `PF Knight - Moss` is the raw Cainos prefab at 600 HP and is not the encounter; the real boss is `MossKnightBoss` at 300.)

### Head Bounce (Pogo Boots Relic)

- 8 damage, `defaultJumpForce * 0.7f` upward force, 0.1s camera shake, 0.3s cooldown.
- Gated behind `RelicManager.HasRelic("PogoBoots")`.
- Uses both `OnCollisionEnter2D` and `OnTriggerEnter2D` (AeroBat has trigger collider, others have solid).
- Contact normal check: `contact.normal.y > 0.7`.

**Gravity reversal — HANDLED (verified in code 2026-07-26):** every branch of the head-bounce path now flips on `isGravityReversed` — the falling-direction check (`OnTriggerEnter2D`: `isGravityReversed ? velocity.y > 0.1f : velocity.y < -0.1f`), the position-vs-enemy check (top vs bottom), the collision-normal check (`normal.y < -0.7f` vs `> 0.7f`), and the bounce impulse direction. The old "velocity sign check doesn't account for gravity reversal" gap is closed; head-bouncing works upside-down.

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

### "[RuntimeInitializeOnLoadMethod] runs ONCE PER SESSION, not once per scene" (2026-08-09)

**`RuntimeInitializeLoadType.AfterSceneLoad` says WHEN in the startup sequence the method runs. It does NOT mean "after each scene load".** The name reads exactly like it does, which is why this survived.

Consequence, reported by the designer as *"after restarting from the death screen I can no longer use the map"*: dying is `SampleScene → GameOverScene` and RESTART is `GameOverScene → SampleScene`. Both are scene loads, and a **scene-local self-bootstrapping singleton is destroyed by the first one and never re-created**. So from the player's first death onward, for the rest of the session:

- **`RunMapManager` gone** — `M` did nothing, and `LevelManager` silently fell back to `PickNextRoomPrefabWithoutMap()`, i.e. random room order. Exactly the "looks almost right" failure that class's own header warns about.
- **`ScrapHUD` gone** — no scrap counter.
- `SfxManager` was fine only because it happens to call `DontDestroyOnLoad`.

⚠️ **The irony worth remembering: self-bootstrapping was adopted to stop systems going missing from scenes, and it introduced a new way for systems to go missing.** Managers *placed* in the scene never had this problem — reloading the scene brings them back.

**Fix: `SceneBootstrap.Register(Create)`** — runs the creator now and on every `sceneLoaded`. Any new self-bootstrapping singleton must use it, and its `Create` must be idempotent. Verified across two full death→restart cycles: both managers return, the hub is the first room again, the map regenerates, and `M` opens a freshly drawn chart.

**When touching anything per-run, test the SECOND run, not the first.** A scan for dangling statics found none, so the damage was confined to these two — but the first run is not evidence about the second.

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

### "Setting a shader property that doesn't exist fails SILENTLY" (2026-07-26)

**This is the project's most expensive bug shape: code that looks correct, runs without error, and does nothing.** `material.SetColor("_Color", …)` / `MaterialPropertyBlock.SetFloat(…)` on a shader that lacks that property is a **no-op with no warning**. The gravity-reversal warning flash was "fixed" TWICE this way and stayed invisible for months — first tinting `_Color` (the Cainos **"Alpha Cut"** shader exposes no colour property at all), then switching to `GetComponentsInChildren<SpriteRenderer>()` (the Mage M rig is 16 SkinnedMeshRenderers + ONE SpriteRenderer, the staff — so only the staff flashed).

Known property support (verified by dumping `ShaderUtil.GetPropertyCount`):
| Shader | Has `_Color`? | Has `_Alpha`? |
|---|---|---|
| Cainos `Customizable Pixel Character/Alpha Cut` (most player outfit parts) | ❌ **no colour property** | ✅ |
| Cainos `Customizable Pixel Character/Body` | ❌ (has `_SkinTint`) | ✅ |
| Cainos `Customizable Pixel Character/Hair` | ✅ | ✅ |
| Cainos `Pixel Art Monster - Dungeon/Transparent` (**all enemies**) | ✅ | ✅ |

**Rules:** on the PLAYER rig, tint via **`_Alpha`** — the one handle every Cainos rig shader shares. On ENEMIES, `_Color` is fine (verified across every enemy prefab). When writing any new material-property effect, **dump the shader's property list first** rather than assuming, and prefer `HasProperty` + an explicit fallback over `HasProperty` + silently skipping (a guarded skip still produces "nothing happens", which is the bug).

Also beware the inverse: `BreakableWall.cs` checks `HasProperty("_Color")` when *caching* the original colour but not when *setting* it — an asymmetry worth copying nowhere.

### "First diagnostics can be wrong; always verify"

During the character swap session, Claude Code's first diagnostic incorrectly described `AttackAction` as a Float. The error only surfaced at runtime as a type mismatch. **For Animator parameter types specifically, the YAML `m_Type` integer is the source of truth.**

More generally: this file itself drifts. A 2026-07-26 pass found the entire Relic System section describing a slot redesign as an unbuilt "future direction" when it had already shipped, three "deferred bugs" that were already fixed, and a HUD described as a left-side vertical column when it is a top-centre bar. **Verify against the code before planning from this document** — and when you find drift, fix the doc in the same session.

### "Transform.Find is strict and silent"

The QuestTrackerHUD looks for children named exactly `Title` and `Progress` (case-sensitive). A typo, trailing space, or different capitalization causes Transform.Find to return null, and the defensive code skips text assignment silently. When a tracker, popup, or instantiated UI element appears blank, the first thing to check is child naming inside the prefab.

### "GetComponentInChildren can return null"

`acceptButton.GetComponentInChildren<TextMeshProUGUI>().text = "ACCEPTED"` crashes if the button has no TMP descendant. This caused a silent quest-acceptance failure: the exception fired BEFORE the actual AcceptQuest logic ran, so the system looked like "nothing happened on click." Always null-guard before dereferencing GetComponentInChildren results.

### "Cainos '3D Lit' props sort by Z-DEPTH, not sortingOrder" (2026-07-18)

**A prop drawing on top of the player is almost always a Z-position problem, NOT a sorting-layer/order problem.** The Cainos "Pixel Art Platformer - Dungeon" props (doors, frames, etc.) and the Cainos player character both render with **opaque shaders in render queue 2000** (`Sprite 3D Lit …`, `Customizable Pixel Character/Body`, `.../Alpha Cut`). Opaque geometry sorts by **camera depth (Z distance)** — `SpriteRenderer.sortingOrder` is essentially ignored for them. Two opaque things at the same Z sort ambiguously and one arbitrarily wins.

The fix is **Z position**, not sorting order: push the prop farther from the camera than the player. The camera looks along **+Z** (camera at negative Z), so "behind the player" = **larger Z**. Because the camera is orthographic, changing Z does NOT move the prop on screen — it only changes depth sort. (Setting the door's `sortingOrder` to -1 first did NOTHING — that was a misdiagnosis.) Note: a prop may mix queues (the door's `Door`/`Frame` are opaque 2000, its `Inside`/`Shadow` are transparent 3000); transparent parts do honor sortingOrder and always draw after all opaque.

✅ **SOLVED GENERALLY BY `PlayPlane.cs` (2026-08-08) — you should not need to hand-tune prop Z any more.** Fixing individual props only ever fixes the one somebody noticed, and the designer reported the player and enemies still rendering behind props. Measuring all 11 pool rooms showed why: **there was no play plane at all.** Every room had invented its own depth —

| room | spawn Z | enemies Z | frontmost prop Z |
|---|---|---|---|
| `efeslevel1` | 0.00 | 0.00 | **−0.01** (prop in front of every enemy) |
| `EfeVrl4` | 0.00 | 0.00 | **0.00** (exactly coplanar → arbitrary sort) |
| `EfeVrl7` | 2.00 | 2.00 | **0.00** (props in front of player *and* enemies) |
| `efeslevel3` | 2.56 | 0.00 | 3.01 |
| `hub` | −1.06 | — | −0.61 |

— because `LevelManager` copied the entry point's **full Vector3** onto the player, so the player's depth was whatever that room's `GirisNoktasi` happened to sit at, while enemies sat wherever they were dropped and props ranged from −1.12 to +3.56. Sorting was luck, per room. That "sometimes" is the signature of two opaque things at the *same* Z.

**The rule now: actors live at `PlayPlane.Z` (−2), everything else is behind it.** `PlayPlane.Apply(room)` runs on every spawn — it snaps every `EnemyHealth` onto the plane and pushes any opaque non-actor renderer found at or in front of it behind, moving the prop's top-level ancestor so multi-part props stay together. `LevelManager` now takes only X/Y from the entry point. Verified: all 11 rooms satisfy the invariant (every enemy on the plane, zero props in front), and the fix holds for rooms nobody has authored yet. **Z is free to move** — the camera is orthographic so depth changes cost zero pixels on screen, and Physics2D ignores Z entirely.

The historical per-prop fix below is now redundant but harmless; keep it as the explanation of *why* opaque sprites behave this way.

**The entry-door case is FIXED PROJECT-WIDE (2026-07-22) — at the source, in `Assets/Prefabs/GirisNoktasi.prefab`.** It was never a hub-only bug: `LevelManager` spawns the player with `playerTransform.position = entryPoint.position` (full Vector3, **Z included**), so the player always lands exactly coplanar with the entry door — and a scan found **38 of 39 rooms** with the door on or in front of the spawn plane, because every room nests this one shared prefab. Fix: the `PF Dungeon Props - Door Wood 01` child's local Z is now **0.5**, putting all four door sprites 0.45–0.51 **behind** the spawn plane. All 39 rooms inherited it from the single source change; the hub's earlier per-instance override (and 4 no-op `sortingOrder` overrides) were reverted so the hub tracks the source. **If you add a new room, do not override that door prop's local Z** — inherit it. If you ever place another prop near the entry point, remember the spawn plane is the Z the player occupies.

### "camera.Render() to a RenderTexture can sort DIFFERENTLY than the real game view"

When capturing a frame to inspect it (see Workflow Notes → Visual inspection), a throwaway `Camera.Render()` into a RenderTexture does NOT necessarily match what the URP pipeline actually draws — it gave a *false* "the door is behind the player" image while the real game still showed the door on top. **Trust only the real framebuffer** (`ScreenCapture.CaptureScreenshot(path)`), never a manual `camera.Render()`, when verifying sorting/lighting/pipeline-dependent visuals.

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

### Prefab override auditor (2026-07-22) — run this when something "should work but doesn't"

`Assets/Scripts/Editor/PrefabOverrideAuditor.cs`, menu **Deckshift → Audit Prefab Overrides**.
Scans the active scene + every prefab asset (~2,000 prefab instances, ~9s) and reports **prefab-instance
overrides that have silently diverged from their source prefab** — this project's most recurrent
invisible bug class. Two categories, both deliberately high-signal (it finds ~1 hit in 31,000 overrides):

- **NULLED** — the instance blanks an object reference the source prefab HAS. Almost always a bug,
  and a nasty one: the prefab looks correct, so you debug the code instead. It further distinguishes
  *"reference cleared"* (revert the property) from *"the instance DELETED the object it pointed at"*
  (revert won't help — restore the child or remove the leftover).
- **PINNED** — an override that merely repeats the source's CURRENT value. Harmless today, but the
  instance is frozen and will not follow future prefab edits. Restricted to **our own scripts**:
  Unity's built-ins (especially `RectTransform`) emit value-identical overrides constantly, which
  buried the real findings ~500:1 before the filter.

**Implementation caveat worth preserving:** the NULLED check does NOT read
`PrefabUtility.GetPropertyModifications`. That record can contain **stale entries Unity no longer
applies** — GenLevel3's AcidWater carries an `m_Materials.Array.data[0] = null` record while every
material is in fact assigned, which produced a confident false positive. The auditor instead compares
the **effective instance value** against `PrefabUtility.GetCorrespondingObjectFromSource(...)`. If you
extend this tool, keep that principle: *trust live values, not modification records.*

Verified by regression test: temporarily re-introducing the `warningSoundClip` null override made the
auditor flag it immediately. Note that restoring a value by **assigning** it creates a PINNED override —
always fix these with `PrefabUtility.RevertPropertyOverride`, not by re-typing the value.

### Visual inspection via MCP screenshots (2026-07-18)

Claude Code CAN see the running game — this is the fix for "I can't judge how it looks." The reliable recipe (via `execute_code`):
1. Enter Play mode (`manage_editor play`) so levels/entities actually spawn; edit mode is sparse (rooms instantiate at runtime).
2. `ScreenCapture.CaptureScreenshot("<abs path>")` — **async**, captures the REAL framebuffer (full URP render + all Screen-Space-Overlay UI/HUD) after the next frame renders.
3. In a LATER tool call (a frame has passed), `Read` the PNG. Reading it in the SAME call fails — the file isn't written yet.
4. Stop Play mode (`manage_editor stop`) to leave the editor clean.

Gotchas learned the hard way:
- `ScreenCapture.CaptureScreenshotAsTexture()` returns null/invalid from `execute_code` (it must run at end-of-frame, which `execute_code` can't hit). Use the async file method.
- A manual `Camera.Render()` into a RenderTexture is synchronous and handy, but **can sort differently than the real pipeline** — do NOT trust it for sorting/lighting checks (see Common Pitfalls). It's fine only for a rough world grab.
- To zoom on something (e.g. the spawn), move the REAL `Camera.main` onto the target and shrink `orthographicSize` **after disabling `CameraFollow.enabled`** (it re-clamps every LateUpdate), then use the async framebuffer capture. Play-mode changes revert on stop, so no restore needed.
- `execute_code` safety checks block `System.IO.File.Delete` and `AssetDatabase.DeleteAsset` (pass `safety_checks:false` when a delete is truly intended); `using` directives are illegal in its method body (fully-qualify types); and there are **three `Projectile` types** so component-add by short name is ambiguous (see Enemy System).

Use this liberally to verify visual changes, diagnose "it looks wrong" reports, and fact-check the docs against reality — it caught a wrong sorting fix this session before it shipped.

---

## Known Issues / Deferred Work

### Architecture (planned, highest priority)

- ~~CardActionExecutor conflict-flag enforcement~~ — **DONE (2026-07-06).** The ExecuteAction() extraction, all per-effect flag registration (incl. ReverseGravity via `SetManualFlag`), AND enforcement in `TryExecute` (Blocked on flag overlap) are complete. The card-effect-conflict bug class is resolved. Only remaining nuance: the Echo Chamber double-cast no-ops on stateful cards (see Card System → Known interaction) — flagged, not urgent.
- ~~CameraPeek rebuild~~ — **done**; rebuilt without Cinemachine (see Camera System).
- **Manager dependency graph** — undocumented. Long-term docs task.
- ~~QuestSystem DontDestroyOnLoad inconsistency~~ — **resolved 2026-06-10**: removed; QuestSystem is scene-local like every other manager, and quests are per-run by design. Quest meta-progression, if ever wanted, should go through the save system (PlayerPrefs, like AchievementManager), not DontDestroyOnLoad.

### Future: Slot-Constrained Relic Redesign (MAJOR DESIGN DIRECTION)

✅ **THE MECHANICAL REDESIGN IS DONE (corrected 2026-07-26).** This section spent months describing a "future direction" that had in fact already shipped. What actually exists now is documented under **Relic System** above: 5 slots, rarity-based sell values, `TryGrantRelic` + the forced full-slot swap screen, a manage panel, and hover tooltips. **Do not re-plan or re-build any of that.**

**What genuinely remains is BALANCE, not code:**
- **Rebalance the 18 relics for a slot economy.** They were authored as small always-on Slay-the-Spire bonuses (+5 HP on kill, +2 Shift on kill). In a 5-slot loadout where every pick costs you another relic, small passive trickles are the wrong shape — slot-constrained systems want **bigger, more interactive, more build-defining** effects that change how you play, not just numbers that tick up. This is the real outstanding work and it is a **design pass, not an engineering one**.
- **Economy tuning** — sell refunds are currently flat by rarity (150/90/50/25) and untuned against a 45-50 min run and the actual rate relics are offered.
- **Possibly** distinguish acquisition sources (shop vs. pack vs. voucher).

**Why this fits the game's DNA:** Deckshift's core philosophy is "Movement is a Resource" — resources matter. Slot-constrained relics extend that principle: a curated pool the player manages, not a pile that grows passively.

⚠️ **The old "don't invest in relic UX / don't add relics" freeze is LIFTED** — it was guarding against a rework that has now happened. Tooltips and the manage/swap UI exist. Adding relics is fine and in fact *needed* (a 5-slot system wants a deep pool to choose from); just author them at slot-worthy power, not as another +2 trickle.

**Approach for the balance pass:** paper design first, code second.

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
- **Phase card wall-stuck:** ⚠️ **MITIGATED, not fully solved (audited 2026-07-26).** `PhaseRoutine` implements the planned "prevent expiration inside collider": after the base duration it EXTENDS Phase up to 1 extra second while `IsCollidingWithGround()` is true (giving the player time to move clear), then, if still embedded, nudges the player 0.5 units along the gravity axis (`ejectDir` handles reversal). Common cases are covered. Remaining edge cases: a player who deliberately stops deep inside a 2-thick wall won't clear it in 0.5 units, and the nudge is vertical-only (no horizontal escape). Harden with a nearest-safe-position search if it recurs.
- ~~**Comet Dive identity loss**~~ — **RESOLVED (verified 2026-07-26).** Comet Dive was redesigned into an AoE **dive-blast** (`StartCometDive`/`LandCometDive`: fast downward slam → `Physics2D.OverlapCircleAll` damage at `cometRadius`/`cometDamage`, with a `CometDiveVFX` telegraph while falling). It is no longer the single-target head-bounce; the two are distinct.
- ~~**Head bounce + gravity reversal**~~ — **RESOLVED (verified 2026-07-26).** All head-bounce branches now flip on `isGravityReversed` (see Head Bounce section). Head-bouncing works upside-down.
- **Duplicate ExitDoor possible in some room prefabs:** defensive guards now in place but the scene-side duplicate (if any) hasn't been cleaned up.
- **AnimationEventReceiver may resurrect on prefab reimport.** It is now fully REMOVED from the Mage M Animator child (was previously just disabled). If OnFootstep NullRefs reappear in the console, a pack reimport probably restored it — remove it again. (The "'OnFootstep' has no receiver!" *warning* spam is absorbed by `PlayerAnimEventSink` on that same GameObject, now serialized in Player.prefab; see Visual Model Internals.)
- ~~**Gravity reversal warning flash may be invisible**~~ — **RESOLVED (screenshot-verified 2026-07-26).** `WarningFlashRoutine` now strobes `_Alpha` across all 16 SkinnedMeshRenderers (whole-body blink) + red-tints the staff. The prior versions no-op'd (`_Color` unsupported by the Alpha Cut shader) or flashed only the staff. See Gravity Reversal System.

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

### Resolved this session (Player prefab audit, 2026-07-16)

(Kept for short-term reference; can be deleted once stale.)
- ✅ **Scene→prefab tuning drift eliminated.** The scene Player carried 12 uncommitted PlayerController overrides (moveSpeed 8 vs prefab's stale 5, jump 11 vs 10, run pose, real jump SFX, aura VFX, dash tint). All applied into `Player.prefab`; the prefab is now the source of truth. The only scene overrides left are root position/rotation + name (correct). **Rule going forward: tune the PREFAB (or apply overrides after tuning in scene), never leave player tuning scene-only.**
- ✅ Removed three leftover Cainos bone colliders (Rig Spine1/Spine2 capsules, Rig Head circle) — solid, animation-driven, attached to the player's Rigidbody2D. The root capsule is now the only solid player collider.
- ✅ Removed the magic staff's Kinematic Rigidbody2D + trigger PolygonCollider2D (Cainos leftovers; its `Weapon` script is a passive visual helper and stays).
- ✅ Restored `PlayerAnimEventSink` + `footstepClips` (3 Walk mp3s) — this time saved into the prefab, not scene-only (the old wiring had never been committed and was silently lost, breaking footstep SFX and re-triggering "no receiver" spam).
- ✅ Removed duplicate `CameraPeek` from the Player root (lives on Main Camera only).
- ✅ Assigned `warningSoundClip` (breaker-switch SFX, designer may swap) — the gravity-reversal warning had become fully silent.
- ✅ AudioSource `playOnAwake` disabled; prefab root transform reset to identity; 17 stale skeleton-receiver overrides cleaned from the scene instance.

### Content (TODO)

- Scale to 60+ cards (currently **16 assets in `Assets/Cards/`, ~14 genuinely playable** — `Stagger` is the fail-state card, `AnaKartVeritabanı` is the database asset). **This is the single biggest content gap and it gates both the map system and card enhancements.**
- Glass archetype: cards exist in theory, not implemented.
- Expand Vampiric archetype.
- Three-act structure: Act 1 prototype exists; Acts 2-3 not started.
- ~~**Run map system**~~ — **BUILT 2026-08-06, working end to end.** See "Run Map — BUILT AND WORKING END TO END" under Level System for the implementation and its traps. What remains is CONTENT and TUNING, not engineering: the three recharge room prefabs (Foundry / Market / Well) don't exist, so no recharge rooms appear yet; rooms are untagged so every room still serves every tier; and the shift-infused / buffed-enemy half of Elite tiers is not built. The settled design, kept for reference:
  - **Shape: a Slay-the-Spire branching graph, whole act visible**, so the player plans a route rather than picking one door at a time. **Opened with the `M` key** — meaning it's also viewable in the hub, for quest planning.
  - **Difficulty IS the node type, not a second axis on top of it.** Three combat nodes — **Skirmish / Fight / Elite** — ascending cost and reward. Layering easy/med/hard *onto* Fight/Shop/Event would give ~15 icon combinations and an unreadable map; one node = one icon = one promise.
  - **Per-tier content rules (designer-specified):**
    - **Skirmish** — simple layouts, low-HP enemies, thin loot. At most 1 chest. **No shop, no Blompo, no NPCs at all.** Gold and Shift crystals scaled to how much the layout drains.
    - **Fight** — harder layout, mid-tier enemies with some fodder. **At least 1 chest.** Shop appears sometimes; Blompo rarely (shop more common than Blompo).
    - **Elite** — genuinely uncomfortable to pick. Hardest layouts. Some enemies carry **more HP than the same enemy in a Fight room**, plus **shift-infused enemies** (faster, hit harder, drop Shift on death).
  - **The governing law: a room's loot scales to the Shift it costs to cross.** Drainy layout ⇒ bigger payout. Self-balancing; write new rooms to it.
  - **Two axes, two sources:** platforming difficulty is **authored into the room prefab** (geometry can't change at runtime without violating Level Design Law #1); combat difficulty is a **runtime spawn table**. Cheapest extra lever: author *optional* enemy/hazard groups in a prefab and have the tier switch them on.
  - **No Shift cost on map paths** (decided against, for now). It isn't needed: **the danger is the cost.** Skirmish routes are cheap to survive but never resupply; Elite routes are expensive but are the only path to recharge rooms. That loop is the economy.
  - **Recharge rooms** — extra rooms hanging off a route, **not counted as floors**, and **only ever reachable from Fight/Elite nodes, never Skirmish** (that restriction IS the economy above). **Each is specialised, never a do-everything room** — one room that fixes every problem is never a decision. Design them by *which player problem they solve*: a Foundry (scrap → repair/salvage, Blompo), a Market (shop), a Well (Shift + healing). **The map must show which one is on which branch before the player commits**, or it's a coin flip instead of a choice.
  - ⚠️ **Two things that must be visible, not silent:** (1) if an enemy is buffed, **it must LOOK different** — a Shambler that quietly has 20 HP instead of 12 reads to the player as "my Fireball is broken", and corrodes the `CardAnchors.md` anchor that fodder dies to one Fireball. (2) The single most sensitive number in the system is how much Shift a shift-infused enemy drops: too generous and Elite is always correct, too stingy and it's never taken. **Target: an Elite room should be net-negative Shift for an average player and net-positive only for a good one.** Keep it a single tunable value, not baked across prefabs.
  - **Dependency status:** the old "BLOCKED on level count" framing is softer than it looked. Shop/Blompo/quest board are **NPCs placed in rooms**, not dedicated room prefabs, so those node types are near-free. ~15 contract-valid rooms already exist unused (see Room Pool) and need correction passes, not authoring from scratch. Still, tiers are baked into layout, so each room serves ONE tier — roughly 4 Skirmish / 4 Fight / 3 Elite are needed for one repeat-free act.

- **Quest banking — designed 2026-08-03, not built.** Quest rewards should stop paying out instantly and instead **accumulate**, to be collected at a quest board **at the start of the next act** (post-boss). Quests are taken at run start, so they act as *route-shaping objectives* — "kill 3 elites" pushes you onto dangerous paths, "collect 500 gold" into exploration detours. The existing run loop already does this shape (`LevelManager` goes hub → levels → boss → back to hub, and the hub already has the board), so the structural work is small. **The board does NOT need its own map node yet** — only four quest assets exist (one pays zero), which is too thin to carry a node; put it inside the Market or Well for now. When the map exists, show it *while* the player picks quests, so quest selection isn't a blind bet.
- **Card enhancements via "Blompo" (SkillManager repurpose) — designed 2026-07-18, NOT started.** Pivot the existing global skill passives into **per-card enhancements** (e.g. +charges, infinite charges, free-to-play, generates Shift, Retain-on-Recall, scaling-on-discard, Bond). Open decisions flagged in that discussion: (1) the **stacking guardrail** — `does-not-cost-shift` + `gain-shift-when-played` + `infinite-charges` on one card is an unbounded Shift engine and must be rule-blocked; (2) **Bond** (auto-play a linked card) collides with the ConflictFlags system exactly like Echo Chamber's double-cast; (3) enhancements should be **filtered by card type** (a damage buff is meaningless on Portal); (4) decide whether an enhancement is permanent and what happens when the card exhausts.
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
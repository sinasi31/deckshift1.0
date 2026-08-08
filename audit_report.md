# Deckshift — Full Codebase Audit
**Date:** 2026-06-10 · **Scope:** all 105 scripts in `Assets/Scripts/` (read in full), CLAUDE.md, relic/quest assets, build settings, package manifest, prefab naming. Read-only audit — no code was changed.

**How to read severity:**
- **Critical** — can break a run or corrupt the game permanently; fix before adding content.
- **High** — real bug or ship-blocker that players will hit.
- **Medium** — degrades feel/performance or will bite when content scales.
- **Low** — debt, polish, cleanup.

**Blast radius:** "isolated" = one file, safe to fix alone; "cross-system" = touches multiple systems, needs care.

---

> ## ⏱️ STATUS UPDATE — 2026-07-02 (read this first)
>
> **This is a historical snapshot from 2026-06-10. The findings below are NOT auto-maintained.** They record the codebase as it was on the audit date. Treat every finding as **still open unless it carries a 【RESOLVED】 tag** — do not assume anything here was fixed just because time passed.
>
> **Confirmed resolved since the audit:**
> - **1.4 (QuestSystem survives scene changes with dead UI refs)** — ✅ **RESOLVED 2026-06-10.** `DontDestroyOnLoad` was removed from QuestSystem; it's now scene-local like every other manager and quests reset per run (see CLAUDE.md rule #3). The recommended fix (removal) was taken.
> - **§4.4 (CLAUDE.md stale in five places)** — ✅ **ADDRESSED.** CLAUDE.md gained a "Resolved bugs (verified 2026-06-10)" section correcting the shield-leak / CameraPeek / spike-knockback / `CameraBounds`-naming / fall-damage entries, and was refreshed again 2026-07-02 (Audio System, run order, Relic HUD, footstep sink). The doc-drift those items describe is corrected.
> - The items §4.4 already self-marked "ALREADY FIXED" at audit time (shield-block leak, CameraPeek rebuild, spike knockback, fall-damage removal) remain fixed — re-confirmed by reading the current `EnemyHealth.cs`.
>
> **Explicitly NOT confirmed fixed (still treat as open):** the Critical/High items 1.1 (R-key ownership), 1.2 (Phase death wall-stuck), 1.3 (Adrenaline death slow-mo), 1.5 (Turret double-fire), 1.6 (ESC-in-shop), 1.7 (AchievementManager null-guards), 3.5 (F12 wipes saves). Several hot-path `Debug.Log`s (2.2) and the `.material` flash (2.5) are also still present in `EnemyHealth.cs` as of this update. The RECOMMENDED FIX ORDER below stands.
>
> **Context (not audit findings):** since this audit the project also grew new systems — the Act 1 Moss Knight boss (moveset, awaken cinematic, ability SFX, boss health bar, death VFX + real loot drops), a `SfxManager`-centred audio pattern, a finite hub→levels→boss run order, and procedural chest/relic VFX. None of that is covered below; see CLAUDE.md and `BossDesign_MossKnight.md`.

---

## 1. CORRECTNESS RISKS

### 1.1 — CRITICAL — The R key is handled by THREE scripts, and whether Recall costs Shift is decided by luck
- **Where:** `PlayerController.HandleCardInput()` (line ~327), `DeckManager.Update()`, `RelicManager.Update()`
- **What happens:** When you press R, three scripts all react in the same frame:
  1. `PlayerController` calls `DeckManager.ReloadHand()` directly — a **free** hand refresh that skips the Recall cost entirely.
  2. `DeckManager.Update()` calls `TryRecall()` — the **paid** version that spends Shift and escalates the cost.
  3. `RelicManager.Update()` dumps a debug list of owned relics to the console.
- **Why it matters:** Whichever of the first two runs first wins (the loser sees `isReloading == true` and bails). The project has **no custom Script Execution Order** (verified in the .meta files), so Unity decides arbitrarily. In plain terms: **Recall is either free or paid depending on an invisible coin flip Unity made when it imported your scripts.** If it's currently "paid" on your machine, a reimport or build could silently flip it to "free" and gut the resource economy.
- **Blast radius:** Isolated — delete the R handling from PlayerController and RelicManager, let DeckManager own the key. One small commit.

### 1.2 — CRITICAL — Dying during Phase leaves "walk through walls" switched on forever
- **Where:** `PlayerController.PhaseRoutine()` (lines ~740–783)
- **What happens:** Phase turns off collisions between the Player layer and the Ground/Enemy layers via `Physics2D.IgnoreLayerCollision`, then turns them back on when the effect ends. But that "turn back on" only runs if the coroutine survives to the end. If the player dies mid-Phase, the scene loads `GameOverScene` and the coroutine is killed before cleanup. **`IgnoreLayerCollision` is a global engine setting that survives scene loads** — so after restarting, the player permanently falls through every floor until the game is fully closed.
- **The same shape of bug:** the gravity scale Phase cached is also never restored on early death, but that one at least dies with the player object.
- **Blast radius:** Isolated — wrap the cleanup in `finally` and/or restore the layer matrix in `OnDisable`/`OnDestroy` on PlayerController. One commit.

### 1.3 — CRITICAL — Dying while Adrenaline slow-motion is active leaves the whole game running at 40% speed
- **Where:** `PlayerController.AdrenalineSlowMoRoutine()` + `PlayerHealth.Die()` / `WaitAndReload()`
- **What happens:** Adrenaline sets `Time.timeScale = 0.4` and `Time.fixedDeltaTime = 0.008`, restoring both after 3 real-time seconds. If the player dies in that window, the scene loads `GameOverScene` and the restore never runs. `Time.timeScale` is global and survives scene loads. The Game Over screen, and the entire next run after "Restart", play in slow motion. Nothing in `GameManager.Awake()` or `GameOverUI` resets it (only `PauseMenu.LoadMenu()` does, and that's the menu path, not the death path).
- **Blast radius:** Isolated — reset `timeScale`/`fixedDeltaTime` in `PlayerHealth.Die()` (or defensively in `GameManager.Awake()`). One commit.

### 1.4 — HIGH — QuestSystem survives scene changes but its UI references don't  ·  【RESOLVED 2026-06-10 — `DontDestroyOnLoad` removed from QuestSystem; now scene-local, quests reset per run (CLAUDE.md rule #3)】
- **Where:** `QuestSystem.Awake()` (`DontDestroyOnLoad`), plus the death flow `PlayerHealth.Die() → GameOverScene → GameOverUI.RestartGame() → SampleScene`
- **What happens:** QuestSystem is the one manager with `DontDestroyOnLoad`. Its serialized fields (`overlayPanel`, `container`, `paperPrefab` target) live in SampleScene's Canvas. After the first death-and-restart cycle: the original QuestSystem survives, the freshly-loaded scene's QuestSystem destroys itself as a "duplicate," and the survivor is now pointing at **destroyed UI objects**. Opening the quest board after your first death will throw errors or silently do nothing. Bonus side effect: `activeQuests` (and completed quests' permanent rewards like max-Shift increases) carry over between runs, which is probably not intended for a roguelike.
- **Why it matters:** This is the exact failure mode CLAUDE.md already flags as "inconsistent, pending review" — the audit confirms it's not just inconsistent, it's actively broken across the death loop that already exists in the game today.
- **Blast radius:** Isolated decision, cross-system consequences — either remove `DontDestroyOnLoad` (consistent with every other manager, quests reset per run) or rebuild UI references on scene load. Recommend removal.

### 1.5 — HIGH — Turret fires two bullet streams because its coroutine is started twice
- **Where:** `Turret.Start()` and `Turret.OnEnable()`
- **What happens:** `OnEnable` runs before `Start` on scene load, and **both** call `StartCoroutine(FireRoutine())`. Every turret runs two overlapping fire loops — double the intended fire rate. The `OnDisable`/`OnEnable` pair was added to handle being toggled off, but the `Start` call was never removed.
- **Blast radius:** Isolated — delete the `StartCoroutine` call in `Start()`. One-line fix.

### 1.6 — HIGH — Pressing Escape inside the Shop triggers the Pause Menu at the same time
- **Where:** `ShopManager.Update()` and `PauseMenu.Update()` both react to `KeyCode.Escape` with no awareness of each other
- **What happens:** ESC while the shop is open runs both handlers in one frame: PauseMenu opens its panel and requests pause; ShopManager closes the shop and **releases** pause and sets the state back to `Playing`. Net result (order-dependent): the pause menu is visible on screen, but the game underneath is unpaused and running, and the pause-depth counter is out of sync. The same conflict exists for the Slot Machine and Quest Board panels (they don't handle ESC, so the pause menu just opens on top of them).
- **Blast radius:** Cross-system but small — pick one owner for ESC (e.g., PauseMenu checks "is any full-screen panel open?" before opening; panels register themselves). One focused commit.

### 1.7 — HIGH — Exiting a room crashes the run if AchievementManager isn't in the scene
- **Where:** `ExitDoor.PerformExit()` calls `AchievementManager.instance.OnRoomClearedFlawlessly()` with **no null check**; `RewardManager.ShowRewardScreen()` calls `AchievementManager.instance.GetAvailableCardPool()` the same way. Also `AchievementManager.CompleteChallenge()` logs `challenge.unlockableCard.cardName` without checking the card exists.
- **Why it matters:** CLAUDE.md's own hard-won lesson is "the system exists in code but the component isn't in the scene." If that ever happens to AchievementManager, the flawless-room path throws an exception **before** the reward screen opens, soft-locking the run at the exit door. Most managers in this codebase get null-guarded; these call sites were missed.
- **Blast radius:** Isolated — add guards at three call sites.

### 1.8 — MEDIUM — Gravity-reversal expiry can strand or corrupt other effects (the documented "conflict class," current shape)
- **Where:** `PlayerController.PhaseRoutine()` × `GravityReversalRoutine()`; `UseAdrenaline()` double-plays
- **Concrete current failure modes (all reachable in the hub):**
  - Play **Floor is Lava while Phasing**: `StartGravityReversal` caches `originalGravityScale` from the current rigidbody — which Phase has set to **0**. When the reversal ends it "restores" gravity to 0. The player floats forever.
  - **Reversal expires during a Phase**: reversal restores positive gravity, then Phase ends and restores the *negative* gravity it cached — player now has inverted gravity while `isGravityReversed` is false, so ground checks and jumps all point the wrong way.
  - Play **Adrenaline twice quickly** (low HP branch): the second play captures the already-boosted `moveSpeed` as "original," so the boost becomes permanent.
- **Status note:** this is the known bug class CLAUDE.md says not to patch per-card. The executor infrastructure to fix it properly now exists (see Section 5) — what's missing is the *enforcement* step.
- **Blast radius:** Cross-system — belongs to the CardActionExecutor conflict-gating work, not piecemeal patches.

### 1.9 — MEDIUM — The "better jump" gravity tweak keeps running during Phase
- **Where:** `PlayerController.Update()` lines ~291–299
- **What happens:** The fall-multiplier / low-jump-cut block is not inside the `if (!isPhasing)` guard. During Phase (which sets gravity to 0 and gives free 8-direction movement), any downward velocity still gets the 2.5× fall multiplier added every frame — so phasing downward accelerates unnaturally and fights the player's stick input. Also in this block: the low-jump cut checks `KeyCode.Space` directly while jumping uses the `"Jump"` button — if jump is ever rebound, short-hopping breaks silently.
- **Blast radius:** Isolated — move the block inside the `!isPhasing` branch; swap the hardcoded Space for `Input.GetButton("Jump")`.

### 1.10 — MEDIUM — HitStop unconditionally restores time to full speed
- **Where:** `HitStop.Wait()` sets `Time.timeScale = 1.0f`
- **What happens:** Every enemy hit triggers a HitStop. If one fires during Adrenaline slow-mo, the restore stomps the 0.4 timescale and silently cancels the slow-mo effect early. (It can't conflict with the pause counter in normal play today, but it's the same "restore to a constant instead of to what was there" pattern that caused the conflict-class bugs.)
- **Blast radius:** Isolated — cache and restore the prior timescale, or route through a shared owner.

### 1.11 — MEDIUM — Quest reward "+10 Shift" permanently raises maximum Shift
- **Where:** `QuestSystem.GiveReward()` — `RewardType.ShiftCharge` calls `player.IncreaseMaxShift(rewardAmount)` then `ResetShiftToMax()`
- **What happens:** The "Hit a Clip" quest (reward 10) raises max Shift from 3 to **13 for the rest of the run** — and because QuestSystem survives scene loads (1.4), potentially beyond. If the design intent was "refill / temporary charges," this is wrong; if it's intentional, it's dramatically stronger than it reads. Flagging for a design decision, not auto-fix.
- **Blast radius:** Isolated once the intent is decided.

### 1.12 — LOW — Smaller correctness notes
- **Overlapping i-frames shorten each other** (`PlayerHealth.GrantInvincibility`): two dashes in quick succession — the first one ending sets `isInvincible = false` while the second is still supposed to be active.
- **Echo Chamber doubles Stagger**: `DeckManager.PlayCard` re-executes any card on the 50% proc, including the Stagger card — one play can add 2 toward the 3-stagger death counter. It also re-fires Portal placement with the `keepCardInHand` result discarded.
- **Respawning mid-Comet-Dive**: `FallAndRespawn` doesn't reset `currentState`, so the player respawns still "diving"; the first ground touch after respawn triggers the comet impact (AoE damage + shake) at the spawn point.
- **`RangedEnemyAI`** uses `pm.Facing` without a null check (`GetComponent<PixelMonster>()` result) — one missing component on a prefab variant = exception every frame.
- **`LevelManager.SpawnNextRoom`** calls `Camera.main.GetComponent<CameraFollow>()` and **`GoldPickup.PlaySound`** uses `Camera.main.transform` with no null guard.
- **`DeckManager.Update`** reads `GameManager.instance.currentState` unguarded — hard exception every frame if GameManager is missing from a scene.
- **Stale `PauseMenu.GameIsPaused` static**: survives scene reloads, so after dying while paused the first ESC press in the new run calls `Resume()` on a menu that isn't open.

---

## 2. PERFORMANCE / GAME-FEEL

### 2.1 — HIGH — Standing in lava or a laser hammers the full damage-feedback pipeline every frame
- **Where:** `HazardZone.OnTriggerStay2D` / `LaserBeam.OnTriggerStay2D` → `PlayerHealth.TakeDamage` → `OnDamaged` event
- **What happens:** Damage-per-second is implemented as ~60 tiny `TakeDamage` calls per second. Each one plays the hurt sound, fires the `InjuredFront` animator trigger, writes a `Debug.Log` (string allocation), triggers a **0.2s camera shake**, and runs `RelicManager.OnPlayerTakeDamage()` — which, with Spiked Carapace owned, does a physics `OverlapCircleAll` **every frame**. The result is continuous max camera shake, a machine-gun hurt sound, GC pressure, and console spam whenever the player grazes a hazard.
- **Blast radius:** Small cross-system — add a short feedback cooldown inside `PlayerHealth.TakeDamage` (damage still applies every frame; sound/shake/trigger/log fire at most every ~0.25s). One commit, testable by standing in lava.

### 2.2 — MEDIUM — Debug.Log calls in hot gameplay paths
- **Where (worst offenders):**
  - `PlayerController.OnTriggerEnter2D` and `OnCollisionEnter2D` — interpolated-string head-bounce logs that run on **every enemy contact** while Pogo Boots is owned (even when the bounce doesn't happen).
  - `PlayerHealth.TakeDamage` and `EnemyHealth.TakeDamage` — log on every hit (combined with 2.1, every frame in hazards).
  - `EnemyHealth.Die`, `LevelManager.SpawnNextRoom`, `DeckManager`, `QuestSystem.ReportEvent` — moderate frequency.
- **Why it matters:** Each log allocates strings (GC spikes → stutter) and `Debug.Log` itself is expensive even in builds unless stripped. These are leftover diagnostics, not intentional telemetry.
- **Blast radius:** Isolated sweep — delete or wrap in `#if UNITY_EDITOR`.

### 2.3 — MEDIUM — `Camera.main` called twice per frame in CameraFollow
- **Where:** `CameraFollow.LateUpdate()` — `Camera.main.orthographicSize` and `Camera.main.aspect` every frame
- **Why it matters:** `Camera.main` is a tag-based lookup; CLAUDE.md's own pitfall list says "cache it in Awake." The camera's own script is the one place this rule is violated per-frame. (`EnemyHealth.Die` also does a `FindFirstObjectByType<PlayerController>` per kill, and `QuestSystem.GiveReward` another — same family, lower frequency.)
- **Blast radius:** Isolated — cache the Camera reference (it's on the same GameObject).

### 2.4 — MEDIUM — Per-frame string allocations in HUD scripts
- **Where:** `ChargeDisplayUI.Update`, `HealthUI.Update`, `DeckViewUI.Update`, `ShopManager.Update` (gold text while shop open)
- **What happens:** These rebuild their display strings every frame regardless of whether the value changed — steady GC garbage that contributes to periodic hitches. The codebase already has the right pattern available (GoldUI and RecallUI are event-driven and only update on change).
- **Blast radius:** Isolated per file — cache last value, update text only on change (or subscribe to events like GoldUI does).

### 2.5 — MEDIUM — `renderer.material` used for flashes (instantiates materials, breaks batching)
- **Where:** `EnemyHealth` (damage flash + stun tint), `PlayerController` (gravity-reversal warning flash, line ~191 and `SetPlayerFlashColor`)
- **What happens:** Accessing `.material` clones the material per renderer at runtime (memory churn + broken batching). The codebase already contains the correct pattern — `BreakableWall.FlashRoutine` and `PlayerController.PhaseVisualRoutine` both use `MaterialPropertyBlock`. EnemyHealth and the warning flash predate it.
- **Extra wrinkle:** `PlayerController.Awake` grabs `GetComponentInChildren<SkinnedMeshRenderer>(true)` — with `true`, it finds the **disabled skeleton fallback** and clones its material at startup; the warning flash then writes to a renderer nobody can see (this is the known "invisible warning flash" issue, plus a pointless material instantiation).
- **Blast radius:** Isolated per file. The player-flash fix should target the SpriteRenderers of the new visual model (already on the deferred list in CLAUDE.md).

### 2.6 — MEDIUM — Jump feel: no input buffering, no coyote time, and the gravity tweak is frame-rate dependent
- **Where:** `PlayerController.Update()` / `HandleJumpInput()`
- **What happens:**
  - Jump is checked with `GetButtonDown` against `isGrounded` *in the same frame* — pressing jump 1 frame before landing does nothing (no buffer), and stepping off a ledge gives 0 frames of grace (no coyote time). For a precision platformer where jumps cost a resource, eaten inputs read as "the game stole my Shift."
  - The fall-multiplier/low-jump block modifies `rb.linearVelocity` in `Update` using `Time.deltaTime` — physics-affecting math outside `FixedUpdate`, so jump arcs differ subtly with frame rate (the rest of the movement correctly lives in FixedUpdate).
- **Blast radius:** Isolated to PlayerController — a buffered-jump timestamp + coyote timer is a contained change; moving the gravity tweak to FixedUpdate is a second small commit. Both directly testable in the hub.

### 2.7 — LOW — Instantiate/Destroy churn (acceptable now, watch as content scales)
- `HandUI.UpdateHandDisplay` destroys and re-instantiates every card UI (plus ghost copies) on every hand change; `QuestSystem.GenerateQuests` and `DeckViewUI.OpenView` do the same for their panels. Fine at 4 cards / 3 quests; will become reward-screen hitching at 60+ cards. Pooling is *not* worth doing yet — just noting the future hotspot.
- `DamagePopup` and fireball/explosion VFX are instantiate-per-hit with no pooling — same verdict.
- `CardUI.Update` runs a lerp for every card every frame even when idle — harmless at hand size 4.
- `PendulumMotion` rotates a (presumably collider-bearing) transform without a kinematic Rigidbody2D — Unity recalculates the collider each frame; add a kinematic Rigidbody2D if axes/hazards use it.

---

## 3. ARCHITECTURE-RULE VIOLATIONS (vs. CLAUDE.md Critical Rules)

### 3.1 — MEDIUM — Unpinned serialized enums: `QuestType`, `RewardType`, `Rarity`
- **Where:** `QuestData.cs` (QuestType, RewardType), `GameEnums.cs` (Rarity, serialized via `RelicData.rarity`)
- **What happens:** `CardActionType` is correctly pinned with explicit numbers (with retired values documented — exemplary). But QuestType, RewardType, and Rarity have no explicit values and **are serialized into .asset files as integers**. Inserting a new value anywhere but the end silently re-maps every existing quest and relic asset (e.g., every "Gold" reward becomes "ShiftCharge").
- **Blast radius:** Isolated — pin the three enums with explicit `= 0, 1, 2…` now, while the current order is known-good. Zero behavior change.

### 3.2 — MEDIUM — `DontDestroyOnLoad` exists in TWO places, not one
- **Where:** `QuestSystem.Awake()` (the documented exception) and **`MusicManager.Awake()`** (undocumented).
- **What happens:** CLAUDE.md rule #3 says QuestSystem is the only DDOL manager. MusicManager also has it. For MusicManager it's arguably *correct* (uninterrupted music across scenes is the point, and it has no scene-bound references), but it's an undocumented rule violation either way. Decide: bless it in CLAUDE.md, or remove it.
- **Blast radius:** Documentation fix or one-line removal.

### 3.3 — LOW — Cinemachine: package still installed, dead `using` directives remain
- **Where:** `Packages/manifest.json` (`com.unity.cinemachine: 3.1.5`), `PlayerController.cs` line 4, `LevelManager.cs` line 1 (`using Unity.Cinemachine;` — neither file uses any Cinemachine type)
- **What happens:** The "no Cinemachine" policy was executed in scenes/code but the package and two using-directives linger. Notably, **CameraPeek no longer references Cinemachine at all** — it's been rebuilt (see Section 4 / doc-staleness). Nothing is broken; the leftovers just invite confusion and keep dead compile surface alive.
- **Blast radius:** Isolated — remove the two `using` lines; removing the package is optional (verify the 2D Pixel Perfect package's optional Cinemachine integration doesn't complain).

### 3.4 — COMPLIANT — checks that passed
- **IDamageable rule:** all enemy-damage call sites go through `IDamageable` (VampiricBite, CometDive, Stagger, Fireball, Projectile, Spiked Carapace reflect, head-bounce via `EnemyHealth` which implements it). Player damage uses the separate `PlayerController.TakeDamage → PlayerHealth` path consistently. No bypasses found.
- **Pause counter rule:** all UI panels use `RequestPause`/`ReleasePause`; the only direct `Time.timeScale` writers are the three documented exceptions (HitStop, AdrenalineSlowMo, PauseMenu.LoadMenu). *(But see 1.6/1.10 for how the exceptions interact badly.)*
- **Player root scale rule:** nothing writes `transform.localScale` on the player root except `LaunchFromCannon` restoring it to the cached original — fine. Facing goes through `isFacingRight`/`ApplyVisualFacing` everywhere.
- **Hub umbrella rule:** all ten consumption sites listed in CLAUDE.md are correctly gated with the `IsCurrentRoomHub()` pattern. One note: `FallAndRespawn` no longer applies fall damage *at all* (see Section 4) so its hub gate is moot.
- **Relic ID strings:** all `HasRelic("...")` literals match the actual `relicID` fields in the seven .asset files (verified byte-for-byte: VampireTooth, KineticCapacitor, SpikedCarapace, PogoBoots, LavaBoots). No mismatches.

### 3.5 — HIGH (ship risk) — F12 wipes ALL save data and is not editor-only
- **Where:** `AchievementManager.Update()` — `PlayerPrefs.DeleteAll()` + scene reload on F12, with no `#if UNITY_EDITOR` guard (contrast with `DebugTools.cs`, which is correctly editor-gated).
- **Why it matters:** In a shipped build, any player pressing F12 (a common screenshot key in other launchers!) silently deletes challenge unlocks and settings.
- **Blast radius:** Isolated — wrap in `#if UNITY_EDITOR`.

---

## 4. LEGACY DEBT

### 4.1 — Dead code
- **`PlayerController`:** `ghostPrefab`, `ghostTimer`, `ghostDelay`, `adrenalineSpeedMult` (Adrenaline ghost-trail system — never spawns anything; `GhostTrail.Init` has **zero callers** project-wide, and `GhostTrail` itself expects the old single-SpriteRenderer player rig). `isPeeking` is read in `Update` but **nothing ever sets it** — the rebuilt CameraPeek doesn't touch it, so the freeze-while-peeking branch is unreachable. `isAdrenalineActive` is written and never read. `phaseVisuals` is typed `SkinnedMeshRenderer[]` — on the new sprite rig only the disabled skeleton qualifies, so the Phase pulse is likely invisible (same family as the warning-flash issue).
- **`HandUI.AnimateCardFromHand(int)`** — empty method, kept callers-zero.
- **`SkillRewardManager`** offers a pool containing only `InfinitySeal`; descriptions for EchoChamber/SpectralWings/Overclock exist but are unreachable through the selection UI (the skills themselves still work if unlocked another way). `SkillType.KineticDiscount` is checked in two places but appears in no pool either.
- **Legacy scenes** `Hub.unity` and `GameScene.unity` are still enabled in Build Settings, and `ExitDoor.isSceneLoader` + `MainMenuController.PlayGame` (loads `buildIndex + 1` = Hub.unity from the menu!) still reference the old scene flow. Worth verifying which scene the main menu actually lands in.

### 4.2 — Duplicate logic implemented two (or three) ways
- **Interaction:** three competing patterns — (a) `IInteractable` + `PlayerController.CheckInteraction` (Lever, Chest, SimpleInteract — the modern one), (b) self-polling `Update` + `Input.GetKeyDown(E)` (ExitDoor, Shopkeeper, WorldSlotMachine), (c) trigger-touch auto-activate (AncientBook, Cannon). The self-polling ones each duplicate the prompt show/hide code and don't respect `interactionRange`/pause state.
- **Projectiles:** `Projectile.cs` (modern, IDamageable, friendly-fire-aware) vs `ArrowDamage.cs` (legacy: tag checks, reads facing from `transform.localScale.x` — which violates the facing rule's spirit and breaks if an arrow is mirrored) vs `Fireball.cs` (its own third variant). Candidates for consolidation when convenient, not urgent.
- **Reward-tier rolling:** `Chest.PickRandomRelic` intentionally mirrors `SlotMachineUI.CheckRewards` (the comment says so) — fine for now, will drift; the planned Dice Broker rework is the natural moment to extract one shared roller.
- **Singleton Awake patterns:** three variants coexist — full (`if null… else Destroy`: GameManager, DeckManager, etc.), no-destroy (`if null instance = this`: SkillRewardManager, SlotMachineUI, ShopManager), and unconditional overwrite (`instance = this`: HandUIDrawer, CameraPeek). The no-destroy/overwrite ones silently misbehave with accidental duplicates.

### 4.3 — Misleading comments (flagged only where they now state something false)
- `PlayerController.cs:15` — `visualModel` tooltip says "drag the PF Skeleton object here" (Turkish). The skeleton was retired; it's the Pixel Character now. Actively misleading for Inspector work.
- `PlayerController.UpdateAnimations` — comments say the new pack uses `"MovingBlend"` and `"SpeedVertical"`, but the code (correctly) writes `MoveBlendX` / `VelocityY`. The comments describe parameters that don't exist.
- `GameEnums.cs` — `KineticDiscount// Kill = Sonraki Kart Bedava` ("kill = next card free") describes **Overclock's** effect, not KineticDiscount's (which is a Shift-cost discount per `DeckManager`/`TryPlacePortal`).
- `DeckManager.startingDeck` header `[Header("Deste Ayarlarý")]` and friends are mojibake-corrupted Turkish (encoding damage) in several files — cosmetic, but these comments are unreadable to anyone.

### 4.4 — CLAUDE.md is stale in five places (doc-only, but each invites a wrong "fix" later)
1. **Shield-block damage leak: ALREADY FIXED.** `EnemyHealth.TakeDamage` now checks `shield.IsBlocking()` and returns **before** deducting health. The doc still lists it as an open "REAL BUG."
2. **CameraPeek: ALREADY REBUILT.** It no longer references Cinemachine; it computes a mouse-direction `peekOffset` (unscaled-time smoothed) that `CameraFollow.LateUpdate` consumes — exactly the planned CameraShake-style design. The doc calls it "BROKEN, slated for rebuild." (Whether the component is present+enabled in SampleScene still needs an in-editor check.)
3. **Spike knockback: ALREADY FIXED.** `Spike.cs` does velocity reflection off `transform.up` with a minimum-force floor. The doc lists "always sends right-up" as an open bug.
4. **Camera zone naming:** the doc says the child "must be named exactly `LevelBounds`" and that looking for `CameraBounds` was the old silent failure. The code looks for **`CameraBounds`**, and all five room prefabs nest a `CameraBounds.prefab` (verified by GUID) — code and assets agree; the doc has it backwards. Anyone "fixing" the code to match the doc would break every room's camera.
5. **Fall damage: ALREADY REMOVED.** `FallAndRespawn` teleports and fires an event; no `TakeDamage(fallDamage)` exists. The doc lists hub-gated fall damage and a plan to remove it.

---

## 5. SUSTAINED-EFFECT SYSTEM STATE (CardActionExecutor conflict flags)

**Overall:** the extraction CLAUDE.md calls "TOP architectural priority" is **done**. `PlayerController.ExecuteAction` is a one-line delegate to `CardActionExecutor.TryExecute`; all 12 actions live in `Assets/Scripts/CardActions/Actions/` as `CardAction` subclasses. The conflict system is **half built: flags are declared and tracked, but nothing ever checks them** — `TryExecute`'s own comment says "Overlapping flags are tracked but never block execution."

**Per-effect conversion status:**

| Effect | Status | Mechanism |
|---|---|---|
| **Dash** | ✅ Fully converted | `IsCoroutine = true`; flags `PlayerVelocity \| Invincibility` held live by `ManagedCoroutine` for the i-frame window, cleared in `finally`. |
| **Phase** | ✅ Fully converted | `IsCoroutine = true`; flags `GravityScale \| LayerCollisionMatrix \| PlayerVelocity` held through base duration + wall-stuck extension, cleared in `finally`. |
| **Adrenaline** | ✅ Converted (manual-flag pattern) | Instant action; `UseAdrenaline`'s two sub-coroutines call `SetManualFlag(TimeScale \| MoveSpeed, true/false)` at start/end. Two caveats: (a) both branches set **both** flags even though each modifies only one; (b) `SetManualFlag` is not reference-counted — two overlapping Adrenalines clear the flags when the *first* ends, while the second is still active. |
| **Fireball** | ✅ Converted (bonus) | Managed coroutine holding `AnimatorAttackState`. |
| **ReverseGravity** | ⚠️ **NOT converted — flags are dead** | Declares `ModifiedState = GravityScale \| VisualTransform`, but `IsCoroutine = false`, and instant actions' flags are **never OR-ed into `activeFlags`** (only `ManagedCoroutine` does that). `StartGravityReversal`/`GravityReversalRoutine` also never call `SetManualFlag`. So while Floor-is-Lava is active, `ActiveFlags` shows nothing. |

**What ReverseGravity currently does:** `ReverseGravityAction.Execute` calls `player.StartGravityReversal()`, which stops any in-flight reversal coroutine and starts `GravityReversalRoutine`: first activation flips `isGravityReversed`, caches and negates `rb.gravityScale`, lerps the visual 180° over 0.15s, waits 4.35s; replays just restart the 4.5s timer without re-caching. At t=4.5s it plays the warning sound and (invisible — see 2.5) flash; at t=5.0s it snaps gravity back, un-flips, and lerps the visual home. The stop-and-restart replay logic is sound *in isolation*; the cache-and-restore of `gravityScale` is exactly what collides with Phase (1.8).

**What remains for the ReverseGravity conversion (and the system overall):**
1. Register its flags: `SetManualFlag(GravityScale | VisualTransform, true)` at routine start, `false` at clean end — being careful with the stop-and-restart path (the old coroutine is killed without cleanup, so flags must be set-once / cleared-only-at-true-end, not toggled per coroutine instance).
2. Same lifecycle care for early death (coroutine killed by scene load leaves a stale manual flag — mirror of 1.2/1.3).
3. **Build the enforcement half:** make `TryExecute` consult `ActiveFlags` against the incoming action's `ModifiedState` and decide (block with feedback, or queue) on overlap. This is the step that actually kills the conflict bug class. Until it exists, the flags are bookkeeping.
4. Optional correctness niceties: refcount `SetManualFlag`; have instant actions with durations (CometDive's `PlayerVelocity` persists until landing; Jump/Stagger declare flags that never register) either drop their declared flags or get real lifecycles, so `ActiveFlags` doesn't lie in either direction.

---

## RECOMMENDED FIX ORDER
Small, commit-sized chunks; each independently testable in the hub sandbox (or a 1-room run where noted). Ordered by severity × risk-of-fix (safest-first within tiers).

**Chunk 1 — R-key ownership** *(Critical 1.1, ~3 line deletions)*
Remove the R handling from `PlayerController.HandleCardInput` and the debug R from `RelicManager.Update`; `DeckManager.TryRecall` becomes the only R handler. **Hub test:** press R repeatedly — hand refreshes; leave hub into a room — R now costs Shift and the cost escalates.

**Chunk 2 — Phase cleanup safety net** *(Critical 1.2, one file)*
Wrap `PhaseRoutine`'s restore steps so they run even on early termination (`try/finally` around the body, plus restore layer collisions + gravity in `PlayerController.OnDisable`). **Hub test:** play Phase, then deliberately die mid-phase (debug damage), restart — verify the player stands on floors.

**Chunk 3 — TimeScale reset on death** *(Critical 1.3, one file)*
In `PlayerHealth.Die()` (before `WaitAndReload`), reset `Time.timeScale = 1` / `Time.fixedDeltaTime = 0.02`. Optionally also defensively in `GameManager.Awake`. **Test:** trigger Adrenaline slow-mo (HP > 50%), die during it, confirm Game Over and restart run at full speed.

**Chunk 4 — Turret single coroutine** *(High 1.5, one line)*
Delete `StartCoroutine(FireRoutine())` from `Turret.Start()` (note: also move the `health`/`playerTransform` lookups to `Awake` so the OnEnable-started routine has them). **Test:** drop a turret room, count shots per cycle.

**Chunk 5 — Hazard feedback cooldown + hot-path log sweep** *(High 2.1 + Medium 2.2)*
Add a ~0.25s feedback cooldown inside `PlayerHealth.TakeDamage` (damage applies every call; sound/animator/shake/log are gated). Delete or editor-gate the head-bounce, TakeDamage, and Die `Debug.Log`s. **Hub-adjacent test:** stand in a lava zone — health drains smoothly, one hurt cue per ~quarter second, no console spam, no permanent shake.

**Chunk 6 — F12 + null-guard batch** *(High 3.5 + High 1.7 + Low 1.12 guards)*
Editor-gate `PlayerPrefs.DeleteAll`; null-guard `AchievementManager.instance` in ExitDoor and RewardManager, `Camera.main` in LevelManager/GoldPickup, `GameManager.instance` in DeckManager.Update, `pm` in RangedEnemyAI. Pure-guard commit, no behavior change. **Test:** exit a room normally; temporarily disable AchievementManager and exit again — reward screen still opens.

**Chunk 7 — ESC ownership** *(High 1.6)*
Give PauseMenu a "is another panel open?" check (simplest honest signal that already exists: `HandUIDrawer.instance.isLocked`, which every full-screen panel already sets) and let ShopManager keep consuming ESC for itself. **Test:** open shop → ESC closes shop only; ESC again opens pause; same for slot machine and quest board.

**Chunk 8 — Enum pinning** *(Medium 3.1, zero behavior change)*
Pin `QuestType`, `RewardType`, `Rarity` with explicit values matching current order. **Test:** open each quest/relic asset in the Inspector and confirm values unchanged.

**Chunk 9 — QuestSystem DDOL decision** *(High 1.4 — needs a design call first)*
Recommended: remove `DontDestroyOnLoad` from QuestSystem (matches every other manager; quests reset per run, which fits a roguelike). Same commit: decide MusicManager (3.2) — keep DDOL but document it in CLAUDE.md. **Test:** accept a quest, die, restart — quest board opens cleanly and shows fresh quests.

**Chunk 10 — Better-jump containment + jump-feel pass** *(Medium 1.9 + 2.6)*
Two commits: (a) move the fall-multiplier block inside `!isPhasing` and replace `KeyCode.Space` with the Jump button; (b) add jump buffering (~0.1s) and coyote time (~0.08s) in `HandleJumpInput`. **Hub test:** phase movement no longer accelerates downward; jump pressed just before landing now fires; walking off a ledge still allows a grounded jump for a few frames (and doesn't double-charge Shift).

**Chunk 11 — ReverseGravity flag registration + conflict enforcement** *(Medium 1.8 + Section 5 — the big one, do it after the stabilizers above)*
Three commits: (a) `SetManualFlag` for ReverseGravity with restart-safe lifecycle; (b) refcount `SetManualFlag` and split Adrenaline's branch flags; (c) make `TryExecute` block (with a "card fizzle" cue) when `ModifiedState & ActiveFlags != 0`. **Hub test:** spam Floor is Lava + Phase + Adrenaline in every order — no floating, no inverted-gravity-while-normal-state, no permanent speed boost; each conflicting play visibly fizzles instead.

**Chunk 12 — Material flash modernization** *(Medium 2.5)*
Port EnemyHealth's flash/stun tint to `MaterialPropertyBlock` (copy BreakableWall's pattern); rebuild the player warning flash against the sprite rig's SpriteRenderers (also resolves the known invisible-warning-flash issue and the dead `phaseVisuals` array). **Hub test:** hit a dummy enemy — flash still shows; trigger gravity-reversal expiry — warning is now visible.

**Chunk 13 — Dead code + misleading comment sweep** *(Low 4.1/4.3 + 3.3)*
Remove ghost-trail fields + GhostTrail.cs, `isPeeking`/`isAdrenalineActive`, empty `AnimateCardFromHand`, the two `using Unity.Cinemachine;` lines; fix the four misleading comments. **Test:** project compiles, hub plays normally.

**Chunk 14 — CLAUDE.md refresh** *(doc-only, 4.4 + outcomes of chunks above)*
Update the five stale entries (shield leak fixed, CameraPeek rebuilt, spike knockback fixed, `CameraBounds` is the real name, fall damage removed) and record the decisions from chunks 9 and 11. Cheap insurance against a future session "fixing" working code.

*Deliberately not scheduled:* HUD string-caching (2.4), Camera.main caching (2.3), interaction-pattern consolidation (4.2), and pooling (2.7) are all safe, isolated quality work — fold them into whatever chunk touches the same file, or batch them when convenient. The CardTemplate rebuild stays blocked on art per CLAUDE.md.

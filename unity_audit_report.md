# Unity Editor-Side Audit — Deckshift

> ## ⚠️ HISTORICAL SNAPSHOT — 2026-06-10. DO NOT PLAN FROM THIS FILE.
>
> A dated point-in-time report, **not** a live issue list. The scene has changed substantially
> since: the quest board overlay, both settings panels, the pause menu and the slot machine are all
> **deleted**, and most screens are now procedural objects that don't exist in the scene file at
> edit time. Player prefab findings are also stale — the collider set was cleaned up on 2026-07-16
> and again on 2026-08-11.
>
> **`CLAUDE.md` is the current source of truth**, and `Deckshift → Audit Prefab Overrides` is the
> live tool for the override class of bug this report was chasing.

**Date:** 2026-06-10
**Scope:** Editor data only — active scene, prefabs, asset import settings, project settings. Script *code* quality is covered by `audit_report.md` and is not repeated here.
**Method:** Read-only inspection via Unity MCP (scene walks, serialized-property scans, import-setting queries) plus direct reads of `ProjectSettings/` YAML and git history. **Nothing in the project was created, modified, or deleted. Play mode was never entered.**

Tool notes (things that didn't work first try, all worked around read-only):
- The Unity console was empty at audit time, so the known `CameraBoundsController` missing-script warning could not be re-observed live (its source was found on disk instead — see Finding P1).
- Scene serialized-reference listings were capped per pass to keep responses manageable; the LevelEfeS/LevelEfeVrl room prefabs were sampled in one pass and LevelSinasi/Prefabs completed in a second. Coverage of *required-looking* fields (healthbars, AI, UI wiring) is complete; exhaustive listing of every optional null VFX/audio field is not.

---

> ## ⏱️ STATUS UPDATE — 2026-07-02 (read this first)
>
> **Historical snapshot from 2026-06-10, editor data as it was then.** Not auto-maintained — treat every finding as **still open unless it carries a 【…】 tag**. Most of these are Inspector/asset-data items that can only be re-verified inside Unity, which this update did not do.
>
> **Behavioural change relevant to a finding:**
> - **Exec #2 / P2 (run repeats efeslevel1 forever)** — 【PARTLY SUPERSEDED 2026-07-02】 `LevelManager` was reworked in *code* from endless-refill into a **finite run: hub → each pool level once (random, no repeats) → boss room → loop to hub**, with a new separate **`bossRoomPrefab`** slot. So the "efeslevel1 over and over" *behaviour* is gone. **BUT** the underlying data gap this finding is really about — `roomPrefabs` only containing hub + efeslevel1, leaving efeslevel2/3/4 unused — is a **scene-data** issue not touched by the code change; **Chunk 2 (populate the Room Prefabs list in the Inspector) still applies** unless it's since been done. This update could not read the current scene list to confirm.
>
> **New since this audit (context, not findings):** the boss room is now the intended **run finale** (`LevelManager.bossRoomPrefab`), and the Act 1 Moss Knight boss got a full build-out (see `BossDesign_MossKnight.md`). None of the other findings here (build-list Hub row, 85 MB WAV import, gravity-warning clip unassigned, SettingsMenu triplication, enemy-healthbar wiring, dead content, tag/layer tidy) have been confirmed fixed — assume open.

---

## Executive Summary — the five things that matter most

1. **(Critical) The Build Settings contain a scene that no longer exists.** `Assets/Scenes/Hub.unity` was deleted from disk but is still enabled at build index 1. The main menu's Play button loads "build index + 1" — in the editor that's the missing scene (Play does nothing/errors); in a real build all the indices silently shift, so "SampleScene = build index 2" (which CLAUDE.md and this project assume) is no longer true in builds.
2. **(Critical) The run only has two rooms.** `LevelManager.roomPrefabs` contains exactly `hub.prefab` and `efeslevel1.prefab`. Every run is: hub, then efeslevel1 repeated forever. `efeslevel2/3/4` exist, have content, and are referenced by nothing.
3. **(High) An 85 MB WAV file is set to "Decompress On Load".** `mainmenusound.wav` will be fully decompressed into memory the moment it loads. This is the single largest file in the project (by far) and the import setting is the worst possible one for it.
4. **(High) The gravity-reversal warning is now completely silent AND invisible.** CLAUDE.md believes the audio cue still plays — but `PlayerController.warningSoundClip` is **None** in the scene and in `Player.prefab`. Combined with the already-known invisible flash (SkinnedMeshRenderer removed), the player gets zero warning before gravity snaps back.
5. **(Medium) A whole layer of legacy/duplicate content is silently rotting:** a duplicated folder (`Assets/LevelEfeVrl 1/`), a stray scene (`mainmenu 1.unity`), a byte-identical duplicated GameOverScreen prefab, an `old player.prefab`, and an `AeroBat.prefab` whose AI script was deleted in a refactor and never re-attached.

---

## 1. Active Scene (SampleScene) — deep pass

### What's healthy ✅

- **No missing scripts anywhere in the scene hierarchy.** (The famous missing-script warning comes from a *prefab*, not the scene — see P1.)
- **No suspicious transforms** — nothing parked at extreme positions, no extreme scales.
- **Exactly one enabled Camera, one EventSystem, one active root Canvas.** (A second "root" canvas reading, `Canvas/QuestCanvas`, is just an inactive nested canvas — harmless.)
- **Every manager exists exactly once.** No duplicate singletons that would self-destruct at runtime. Layer 12 is confirmed to be "Interactable" (an open question in CLAUDE.md — answered: yes, the QuestBoard layer setup is correct).
- **Player root scale is (1,1,1)** as the hard rule requires.

### S1. CinemachineCamera — dead object, confirmed (Low)

- **Path:** scene root → `CinemachineCamera` (inactive)
- **What it is:** A full Cinemachine virtual-camera rig (`CinemachineCamera`, `CinemachineFollow`, `CinemachineBasicMultiChannelPerlin`) plus a **second copy of CameraShake**. All components are enabled, but the GameObject itself is inactive, so none of it runs.
- **Consequence:** None at runtime today. The risk is the trap it sets: if anyone ever re-activates it (one accidental checkbox), there would be two CameraShake singletons fighting and a Cinemachine camera fighting `CameraFollow`. It is the scene-side half of the pending Cinemachine cleanup CLAUDE.md mentions.
- **Severity:** Low (dormant), but cheap to remove.
- **Fix type:** Scene change — deleting a GameObject. *Not performed.* One-line Claude Code prompt: "Delete the inactive CinemachineCamera root GameObject from SampleScene."

### S2. Three SettingsMenu components where there should be one (Medium)

- **Paths:** `Canvas/SettingsPanel` (the real one), plus **duplicate SettingsMenu components on `Canvas/SettingsPanel/Slider` and `Canvas/SettingsPanel/w`** (note the junk GameObject name "w"). The same triplication exists in the source prefab `Assets/LevelSinasi/SettingsPanel.prefab`, so this is a prefab-level mistake mirrored into the scene.
- **What's wrong:** The two extra components have all their references (volume slider, toggle, panel) set to None. They look like accidental "Add Component" slips during UI work.
- **Consequence:** Mostly inert today, but any code that does `FindObjectOfType<SettingsMenu>()` or hooks events can grab the *wrong, unwired* copy — the classic "settings randomly don't apply" bug waiting to happen.
- **Severity:** Medium.
- **Fix type:** Inspector step — open `Assets/LevelSinasi/SettingsPanel.prefab`, select the `Slider` child, right-click the SettingsMenu component header → Remove Component; same on the `w` child (and consider renaming/deleting `w` itself). The scene instance should then update automatically; verify the scene copy too.

### S3. Gravity-reversal warning sound is unassigned (High)

- **Path:** `Player` → PlayerController → `warningSoundClip` = **None** (also None in `Assets/Prefabs/Player.prefab`).
- **What's wrong:** CLAUDE.md explicitly states "the expiration audio cue still plays, so the warning is still audible." It is not. The visual flash already silently no-ops (known issue: SkinnedMeshRenderer path), and with the clip unassigned the audio path no-ops too.
- **Consequence:** When Floor is Lava is about to expire, the player gets **no warning at all** — they can be standing on the ceiling and just fall with zero telegraph. This directly undermines a designed 0.5s warning window.
- **Severity:** High (it's a designed mechanic that's fully dark; also corrects a wrong belief in CLAUDE.md).
- **Fix type:** Inspector step — select the `Player` object in SampleScene (and apply to `Assets/Prefabs/Player.prefab`), find PlayerController → "Warning Sound Clip", drag in any short alarm/beep AudioClip from the project. Also note `staggerEffect` is None on the same component (likely optional VFX — verify with a Stagger play).

### S4. ChargeDisplayUI is permanently inert (Medium — verify intent)

- **Path:** `Managers/UIManager` → ChargeDisplayUI component.
- **What's wrong:** Both `playerController` and `shiftTextElement` are None. The script null-guards both and never self-wires, so it silently does nothing, forever.
- **Consequence:** If the Shift counter on screen works, something *else* is drawing it and this component is dead weight. If the Shift counter is missing/stale anywhere, this is why.
- **Severity:** Medium (either a dead component to delete, or a broken HUD element).
- **Fix type:** First an Inspector check: run the game, confirm the Shift number updates. If it does → Claude Code prompt to remove the dead component. If it doesn't → Inspector step: drag the Player object into "Player Controller" and the Shift TMP text into "Shift Text Element".

### S5. Tutorial panel references are None (Medium — verify)

- **Paths:** `Canvas/TutorialPanel` → TutorialMenu: `pageNumberText`, `nextButton`, `prevButton` all None (same in source prefab `Assets/LevelSinasi/TutorialPanel.prefab`).
- **Consequence:** If the tutorial is meant to have multiple pages, page navigation cannot work and may throw errors when opened from the pause/settings menus. If it's a single static image right now, this is harmless-but-untidy.
- **Severity:** Medium if multi-page is expected, Low otherwise.
- **Fix type:** Inspector step — wire the Next/Prev buttons and page text into the TutorialMenu component, in the prefab first.

### S6. Shop item rows missing optional-looking pieces (Low)

- **Paths:** all 10 `ShopItem_Prefab` instances under `Canvas/ShopPanel/...` (and the source prefab `Assets/Prefabs/ShopItem_Prefab.prefab`): `cardStatsPanel`, `shiftText`, `buySound` are None. Reward screen cards (`Canvas/RewardScreen_New/.../Reward_Card_1..3`) have `selectionFrame` None.
- **Consequence:** Shop items won't show card stats / shift cost and purchases are silent; reward cards won't show a selection highlight. These read like "UI styling pass removed them" rather than breakage, but the buy experience is poorer for it.
- **Severity:** Low (cosmetic/feedback polish), flagged so it's a decision rather than an accident.
- **Fix type:** Inspector steps on the prefab if you want them back.

### S7. Things the scene tells us about CLAUDE.md (informational)

- **There is no `EffectManager` script in the project.** The GameObject `Managers/EffectManager` hosts only `HitStop`. CLAUDE.md lists "EffectManager — VFX spawning helper" as a manager; that's aspirational/stale. Low — fix the doc, or rename the GameObject to HitStop to stop confusing future sessions.
- **MusicManager is not in SampleScene — by design, but with a catch.** It lives in `MainMenu.unity` with `DontDestroyOnLoad` and is supposed to survive into gameplay. Two consequences: (a) pressing Play directly in SampleScene in the editor = no music, expected; (b) **because the menu→game flow is broken (Finding B1), in the editor the music manager currently never reaches gameplay at all.**
- `Managers/SlotMachineManager` is a GameObject with a RectTransform + `SlotMachineUI` sitting outside any Canvas — odd but functioning; slated for replacement by Dice Broker anyway. No action.
- `DebugManager` (DebugTools, F1 = free relic) is active in the scene. Fine for development; remember it before any public build.
- `QuestBoard`'s `SimpleInteract.prompt` is None — already on CLAUDE.md's deferred list, confirmed still true.

---

## 2. Prefabs — medium pass

740 prefabs total; 117 non-third-party prefabs scanned (Cainos / TextMesh Pro excluded).

### P1. Missing scripts — three prefabs, two distinct dead scripts (High for AeroBat, Low for BiteVFX)

Identified by extracting the broken GUIDs from the prefab files and matching them against git history:

| Prefab | Missing script | What it was |
|---|---|---|
| `Assets/Prefabs/AeroBat.prefab` (root) | `AeroBat.cs` | The bat's **AI brain**, deleted in commit `9861f4f` when it was replaced by `AerobatAI.cs` — the prefab was never given the new script |
| `Assets/LevelEfeVrl/EfeVrl3.prefab` (3 × AeroBat children) | `AeroBat.cs` | Same — three stale copies of the old bat embedded in a legacy level |
| `Assets/Prefabs/BiteVFX.prefab` (root) | `DestroyEffect.cs` | A self-cleanup script, deleted in commit `e898123` |

- **AeroBat.prefab consequence:** if this prefab is ever placed in a level, the bat has health but **no brain** — it hangs in the air doing nothing, and logs a missing-script warning. Today it's referenced by nothing (the live bats are the `BatMan` objects inside level prefabs, which use `AerobatAI` correctly), so it's a landmine, not an active bug. It also has `healthBarPrefab` and `damagePopupPrefab` unassigned. **Severity: High as a trap / Medium today.** Fix: either delete the prefab (it's orphaned) or re-attach `AerobatAI` + healthbar wiring if you want a placeable bat prefab. Scene/prefab change — not performed.
- **EfeVrl3.prefab:** this is the source of the missing-script console warning at load **if** EfeVrl3 is ever loaded; it's currently unreferenced legacy. Clean up together with the legacy-level decision (Chunk F below).
- **BiteVFX consequence:** `PlayerController` already does `Destroy(vfx, 1.0f)` after spawning it, so **nothing leaks** — the cost is one console warning per Vampire Bite and whatever visual behavior DestroyEffect used to add. **Severity: Low.** Fix: remove the dead component from the prefab (Inspector: select prefab → remove the "Missing (Mono Script)" entry), since cleanup is handled by code.

### P2. The room pool — only 2 of the ~5 levels are actually in the game (Critical)  ·  【PARTLY SUPERSEDED 2026-07-02 — LevelManager code now runs a finite hub→levels→boss run (no more endless efeslevel1); but populating `roomPrefabs` with efeslevel2/3/4 in the Inspector (below) is a scene-data step that still applies unless already done】

`LevelManager.roomPrefabs` in SampleScene contains:

| Index | Prefab |
|---|---|
| 0 | `Assets/LevelEfeS/hub.prefab` |
| 1 | `Assets/LevelEfeS/efeslevel1.prefab` |

With the documented first-room logic (hub forced first, then stripped from the pool), **every run after the hub is efeslevel1, over and over**. `efeslevel2.prefab`, `efeslevel3.prefab`, `efeslevel4.prefab` are finished-looking rooms referenced by *nothing* in the project.

- **Consequence:** runs have no variety; four-fifths of the hand-crafted level content is invisible to players.
- **Severity:** Critical if unintentional, trivial to fix. (If this is a deliberate test configuration, downgrade to "note".)
- **Fix type:** Inspector step — select `Managers/LevelManager` in SampleScene, expand "Room Prefabs", set Size to 5, drag `efeslevel2`, `efeslevel3`, `efeslevel4` from `Assets/LevelEfeS/` into the new slots (keep hub at element 0). Save the scene.

### P3. Enemy healthbar wiring is incomplete at the prefab level (Medium)

CLAUDE.md records healthbars as "wired and assigned to all six enemy prefabs". The editor data disagrees for three of them:

- `Assets/Prefabs/ShieldEnemy.prefab` — `healthBarPrefab` **and** `damagePopupPrefab` None
- `Assets/Prefabs/PatrolEnemy.prefab` — `healthBarPrefab` None (also `pixelMonster` None — its animation driver; verify it self-finds)
- `Assets/Prefabs/AeroBat.prefab` — both None (legacy, see P1)
- Plus every PatrolEnemy instance embedded in the legacy EfeVrl / old_levels rooms, and the ShieldEnemy inside EfeVrl3.

The enemies actually used by the current 2-room pool (MeleeEnemy, RangedEnemy, BatMan, Slime, Mimic in hub/efeslevel1) all have healthbars assigned — which is why the game *looks* fully wired today.

- **Consequence:** the moment ShieldEnemy or PatrolEnemy is placed into a live room, it silently has no healthbar and no damage numbers.
- **Severity:** Medium (dormant, will definitely bite during content expansion).
- **Fix type:** Inspector step — open each prefab, drag `Assets/Prefabs/UI/EnemyHealthBar.prefab` into EnemyHealth → "Health Bar Prefab" (and the damage popup prefab where the field exists). Update CLAUDE.md's claim afterwards.

### P4. Unreferenced prefabs — 48 candidates for dead content (Low–Medium)

Reference scan covered every scene, prefab, ScriptableObject, animator controller, animation clip, and material in the project. Caveat: references made purely from code at runtime would not show up — but none of these sit in a `Resources/` folder, so code can't load them by path either. Highlights (full list available on request):

- **`Assets/LevelEfeVrl 1/`** — entire folder is an accidental duplicate of `LevelEfeVrl` (its `EfeVrlLevel2.prefab` is byte-identical to the original; the other three differ only slightly). Classic drag-copy slip.
- **`Assets/GameObject (1).prefab`** — junk at the Assets root.
- **`Assets/Prefabs/old player.prefab`** — the pre-swap player, with ~23 unassigned references of its own. Pure legacy.
- **`Assets/JaceAether/GameOverScreen.prefab`** — byte-identical duplicate of `Assets/LevelEfeVrl/GameOverScreen.prefab` (501 KB each).
- **`Assets/Prefabs/GoldCoin.prefab`** — unreferenced, yet gold pickups presumably work; whatever drops gold uses something else. Worth one minute of verification before deleting.
- **`Assets/Prefabs/AchievementManager.prefab`** — the scene uses a plain GameObject instead; prefab orphaned.
- All of `Assets/LevelEfeS/old_levels/`, `old_prefabs/`, all `LevelEfeVrl*` rooms, all `LevelSinasi` rooms (kuzeymap, sinasiBigLevel, Room_Easy_01, CainosLevel, Level1Sinas), `NewTilePrefabs/`, `Lazer`, `MainPlatform`, `MainWall`, `PaperTemplate`, `Lever`, plus `efeslevel2-4` (see P2 — these three should be *re-referenced*, not deleted).
- **Severity:** Low individually; Medium collectively (confusion, accidental edits to dead content, repo weight).
- **Fix type:** Deletion is a project change — *not performed*. Recommend deleting via a single dedicated git commit so it's trivially reversible.

### P5. Minor prefab notes (Low)

- `Assets/LevelSinasi/CainosLevel.prefab` has a `Switch` with `target` None (a switch wired to nothing) and `Room_Easy_01.prefab` has a `Door` with all three sprite references None — both rooms are unreferenced legacy, so these only matter if the rooms come back.
- `ExitDoor.interactionPopup`, `Chest.prompt/openSound`, `UpdraftFan.windParticles`, `BreakableWall.breakVFX/breakSound`, `PixelMonster.fx/dieFxPrefab` are None in many places — all null-guarded optional polish fields per the code patterns; listed here once so they're a conscious choice, not findings.

---

## 3. Assets — summary pass

### A1. Ten largest files

| Size | Asset |
|---|---|
| **85.34 MB** | `Assets/LevelEfeVrl/Sprites/mainmenusound.wav` (an audio file in a Sprites folder) |
| 11.41 MB | `Assets/Cainos/.../Ghost - Attack STEPPED.anim` (third-party) |
| 9.88 MB | `Assets/Art/bg1.png` |
| 8.11 MB | `Assets/Art/Gemini_Generated_Image_qigmomqigmomqigm.png` |
| 7.92 MB | `Assets/Cainos/.../SC Demo Scene.unity` (third-party demo) |
| 4.18 MB | `Assets/Cainos/.../Bat - Die Land STEPPED.anim` |
| 4.06 MB | `Assets/Cainos/.../Bat - Wing Flap STEPPED.anim` |
| 3.76 MB | `Assets/Settings/Lit2DSceneTemplate.scenetemplate` |
| 2.93 MB | `Assets/Cainos/.../SC Demo.unity` (third-party demo) |
| 2.17 MB | `Assets/LevelEfeVrl/EfeVrl6.prefab` |

### A2. mainmenusound.wav — wrong import settings on the biggest file in the project (High)

- 85 MB WAV, import Load Type = **Decompress On Load**.
- **Consequence:** the entire track is decompressed into RAM when the menu loads — roughly 100+ MB of memory and a noticeable load hitch for one music track. Music should stream.
- **Fix type:** Inspector step — select the file, in Import Settings set **Load Type → Streaming** (leave Vorbis compression), Apply. Optionally also move it out of `Sprites/` and consider exporting a smaller OGG master. `Assets/LevelEfeVrl/Sprites/levelsound.mp3` (1.33 MB) has the same Decompress On Load setting — same one-click fix, Low priority.

### A3. Pixel-art sprite import hygiene (Medium)

- **Every one of the 214 project (non-Cainos) sprites is at the Unity-default 100 pixels-per-unit, and every one of them uses Bilinear filtering.** The level art convention (Cainos) is 32 PPU with Point filtering. The affected files are almost all AI-generated animation frames under `Assets/animations/` (`_MConverter…`, `GoblinAnim/ezgif-frame-…`).
- **Consequence:** Bilinear filtering makes pixel art blurry — these sprites will look soft/smeared next to the crisp Cainos art. The PPU mismatch means inconsistent world-scale whenever these are dropped into levels.
- **Severity:** Medium (pure visual quality; no crashes).
- **Fix type:** Inspector step, bulk: select all textures in `Assets/animations/` → Import Settings → Filter Mode = **Point (no filter)**, and standardize Pixels Per Unit (decide 32 vs 100 once, per how they're currently scaled in prefabs — changing PPU rescales anything already placed, so test after).
- Compression: 35 textures are Uncompressed, all tiny Cainos character textures — fine as-is. No texture has an oversized (≥4096) max resolution. `bg1.png`/`Gemini_…png` are heavy on disk but import-capped at 2048 — cosmetic repo weight only.

### A4. Duplicated assets (byte-identical) (Low)

1. `Assets/LevelEfeVrl/GameOverScreen.prefab` ⇔ `Assets/JaceAether/GameOverScreen.prefab` (501 KB)
2. `Assets/LevelEfeVrl 1/EfeVrlLevel2.prefab` ⇔ `Assets/LevelEfeVrl/EfeVrlLevel2.prefab` (148 KB; symptom of the duplicated folder, P4)
3. `Assets/LevelEfeVrl/Sprites/Slide 16_9 - 5.png` ⇔ `Assets/Art/Slide_16_9_-_5.png` (1.4 MB)

Keep one of each; covered by cleanup Chunk F.

---

## 4. Settings — quick pass

### B1. Build scene list — a deleted scene is still enabled (Critical, pairs with the Play-button issue)

Editor build list (all five **enabled**):

| Index | Scene | On disk? |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | ✅ |
| 1 | `Assets/Scenes/Hub.unity` | ❌ **file deleted, entry remains** |
| 2 | `Assets/Scenes/SampleScene.unity` | ✅ (the active scene — as expected) |
| 3 | `Assets/Scenes/GameOverScene.unity` | ✅ |
| 4 | `Assets/Scenes/GameScene.unity` | ✅ but legacy/inactive per CLAUDE.md |

Two compounding problems:

- `MainMenuController.PlayGame()` loads **buildIndex + 1**. In the editor, from MainMenu (0) that's the *missing* Hub entry → the Play button fails. In a built game, Unity drops the missing scene, every index shifts down by one, and Play happens to land on SampleScene — by accident, while silently breaking the "SampleScene is build index 2" assumption that CLAUDE.md documents.
- `GameScene.unity` (legacy) ships in every build for no reason.

- **Severity:** Critical (broken editor flow + index drift between editor and build — the nastiest kind of "works on my machine").
- **Fix type:** Inspector step — File → Build Profiles (Build Settings): remove the dead `Scenes/Hub` row, and **uncheck** `GameScene`. Afterward indices are MainMenu 0, SampleScene 1, GameOverScene 2 — at that point also decide whether PlayGame's "buildIndex + 1" is the loading scheme you want long-term (loading by name is sturdier; that part is a code change, not performed). Note `GameOverUI`/`PauseMenu`/`PlayerHealth` already load scenes **by name** ("SampleScene", "MainMenu", "GameOverScene"), so they're immune to index shifts.
- Related stray files: `Assets/Scenes/mainmenu 1.unity` (duplicate scene, not in build — junk) and `MasterLevel.unity` (legacy, not in build — fine to keep or cull).

### B2. Tags and layers (Low)

- **Tags:** `DeathZone` (used: 17 prefab objects + PlayerController code ✅), `New tag` — **unused junk**, clearly an accidental "Add Tag". Remove via any object's Tag dropdown → Remove Tag, or Project Settings → Tags and Layers.
- **Layers:** Defined: Ground(3), Water(4), Player(7), Projectile(8), Hazard(9), PlayerProjectile(10), Enemy(11), Interactable(12), Density(13).
  - **Unused on any object:** Water(4), Hazard(9), Density(13) — no prefab GameObject uses them and no script references them by name. `HazardZone` works by tag ("Player"), not by the Hazard layer; the collision-matrix row carved out for Hazard (see B3) protects nothing. Note there is also a *sorting* layer named "Density" — the duplication of the name across two different systems invites confusion.
  - **Consequence:** none today; minor confusion cost. Leave or prune in Project Settings → Tags and Layers (only after confirming nothing planned uses them).

### B3. Physics2D collision matrix — clean (✅ informational)

Only three pairs are disabled, and all three read as deliberate:
- Player ↔ PlayerProjectile (your fireballs don't hit you)
- Projectile ↔ Projectile (bullets pass through each other)
- Projectile ↔ Hazard (enemy shots ignore hazards — currently moot since nothing is on the Hazard layer)

Nothing looks unintentional. Gravity is the default (0, −9.81); `PlayerController` flips its own gravity scale, never the global value — consistent with the architecture rule.

### B4. Quality / VSync (✅ informational, one watch-item)

- Current level: **Ultra** with **VSync on** — sensible for a 2D pixel game (no tearing, frame pacing tied to monitor). All six default quality tiers still exist; for a 2D URP game they differ in almost nothing that matters to you (shadows/AA/LOD are 3D features). No action needed now; before Steam release consider collapsing to one or two tiers so the settings menu can't expose meaningless options.
- The Main Camera is plain orthographic (size 6) with **no Pixel Perfect Camera component** — optional URP 2D polish for crisper pixel rendering; listed as an idea, not a problem.

---

## Prioritized Fix List — small, independently testable chunks

Each chunk is self-contained and verifiable on its own. Inspector chunks are things you can do yourself; the rest are one-prompt Claude Code jobs.

**Chunk 1 — Build list repair (Critical, Inspector, ~2 min)**
Remove the dead `Hub` row from Build Settings; uncheck `GameScene`.
*Test:* press Play in `MainMenu.unity`, click Play button → should land in SampleScene (in the editor).

**Chunk 2 — Room pool (Critical, Inspector, ~2 min)**
Add `efeslevel2/3/4` to `Managers/LevelManager → Room Prefabs` in SampleScene; save scene.
*Test:* play through 4–5 rooms; you should see different layouts, hub never repeats.

**Chunk 3 — Menu music import (High, Inspector, ~1 min)**
`mainmenusound.wav` → Load Type: Streaming. Same for `levelsound.mp3`.
*Test:* main menu music still plays; editor memory profiler (or just load feel) improves.

**Chunk 4 — Gravity warning clip (High, Inspector, ~2 min)**
Assign an AudioClip to `warningSoundClip` on the Player (scene + `Player.prefab`).
*Test:* play Floor is Lava in the hub; at ~4.5 s you hear the warning before gravity restores. (The *visual* flash fix remains a known code task — SpriteRenderer flash path.)

**Chunk 5 — SettingsMenu de-duplication (Medium, Inspector, ~5 min)**
Remove the two stray SettingsMenu components (children `Slider` and `w`) in `SettingsPanel.prefab`; confirm the scene instance follows.
*Test:* open Settings in-game; volume slider and enemy-numbers toggle still work.

**Chunk 6 — Enemy healthbar wiring (Medium, Inspector, ~5 min)**
Assign `EnemyHealthBar.prefab` (and damage popup where present) on `ShieldEnemy.prefab` and `PatrolEnemy.prefab`; check `pixelMonster` on PatrolEnemy while there. Update CLAUDE.md's "all six wired" note.
*Test:* drop a ShieldEnemy into the hub temporarily (or wait until one appears in a room) and confirm the bar renders.

**Chunk 7 — Pixel-art import hygiene (Medium, Inspector, ~10 min)**
Bulk-set Point filtering on `Assets/animations/`; pick a single PPU convention and apply.
*Test:* visual check of any goblin/animation sprite in-scene — edges crisp, sizes unchanged (or intentionally corrected).

**Chunk 8 — Dead content sweep (Medium, Claude Code + git, one commit)**
Delete: `Assets/LevelEfeVrl 1/` (folder), `Assets/Scenes/mainmenu 1.unity`, `Assets/GameObject (1).prefab`, `Assets/Prefabs/old player.prefab`, one of the two GameOverScreen prefabs (keep the referenced one: `LevelEfeVrl/GameOverScreen.prefab`), one of the duplicate Slide PNGs, and — after a one-minute check that nothing plans to use them — `Assets/Prefabs/AeroBat.prefab` + `Assets/Prefabs/GoldCoin.prefab`. Decide the fate of `old_levels/`, `LevelSinasi` rooms, and `EfeVrl*` rooms separately (they're level design history; archive branch is a fine answer). Remove the inactive `CinemachineCamera` from SampleScene and the dead missing-script component from `BiteVFX.prefab` in the same pass.
*Test:* project compiles, SampleScene plays, no new console errors, git diff shows only deletions.

**Chunk 9 — Tag/layer tidy-up (Low, Inspector, ~1 min)**
Remove `New tag`. Optionally prune Water/Hazard/Density layers after confirming no near-term plans for them.
*Test:* console clean, gameplay unaffected.

**Chunk 10 — Decisions, not fixes (when convenient)**
ChargeDisplayUI (wire it or delete it — S4), tutorial panel wiring (S5), shop item stats/sound (S6), Pixel Perfect Camera experiment (B4), CLAUDE.md corrections (EffectManager doesn't exist; warning audio claim; healthbar claim; SampleScene build index after Chunk 1 changes it to 1).

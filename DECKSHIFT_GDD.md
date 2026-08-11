# DECKSHIFT — Game Design Document

*A 2D pixel-art roguelike deckbuilder platformer where movement itself is the resource you spend.*

**Version:** 1.1 (living document) · **Last compiled:** 2026-08-11
**Engine:** Unity 6.0+ (URP, 2D Renderer) · **Platform:** PC (Steam) · **Team:** Solo developer, designer-first

> **Status legend used throughout this document**
> `[BUILT]` — implemented and playable today · `[PROTOTYPE]` — exists in rough/greybox form · `[PLANNED]` — designed, not yet built · `[TBD]` — open design question
>
> This GDD is written as an external-facing pitch and vision document that also carries full internal design detail. Where the two goals meet, honesty wins: aspirational scope is labelled as such.

---

## 1. The Pitch

**Deckshift** is a roguelike deckbuilder built on top of a precision 2D platformer — and it fuses the two genres at the resource layer, not just the theme layer.

In most deckbuilders you spend *energy* to play cards. In most platformers you jump for free. Deckshift asks one question that reframes both: **what if jumping cost you something?**

Every jump spends **Shift** — a resource that does *not* regenerate over time and CARRIES OVER from room to room for the whole run. Your cards — attacks, dashes, utility, movement tech — also cost Shift and carry limited charges. So every room becomes a spatial puzzle *and* a hand-management puzzle at the same time: reach the exit, kill what needs killing, and don't run your movement dry doing it.

- **Genre:** Roguelike deckbuilder × precision platformer
- **Session length:** A full run targets **45–50 minutes** `[PLANNED target]`
- **Comparables (for positioning, not imitation):** *Slay the Spire* (deck construction, run structure) × *Celeste / Dead Cells* (2D movement feel) × *Balatro* (the resource-as-decision philosophy behind the slot-constrained relic loadout)
- **Hook in one line:** *"Movement is a resource. Spend it well or die stranded."*

---

## 2. Design Pillars

Everything in Deckshift is measured against three pillars. If a feature doesn't serve one of them, it's cut.

### Pillar 1 — Movement is a Resource
Shift is the spine of the game. Because jumping and cards draw from the same non-regenerating pool, the player is *constantly* trading traversal against combat. Running out of Shift isn't a soft failure — it triggers the **Stagger** death spiral (§5.4). Every system in the game is designed to press on this tension: hazards that force movement, bosses that tax your platforms, relics that pay out in Shift.

### Pillar 2 — Every Choice Is a Resource Choice
Cards have finite charges. Relics occupy 5 finite slots. Recall costs escalate. Gold, scrap, and Shift are all scarce. The player is never given a free lunch; they are given a **curated pool they must manage**. The Balatro-style slotted loadout (§6.3) exists specifically to extend Pillar 1's logic to permanent upgrades.

### Pillar 3 — Feel First
A deckbuilder can be spreadsheet-dry. A platformer cannot afford to be. Deckshift invests heavily in game feel — hitstop, camera shake, screen flashes, procedural VFX, slow-motion punctuation, animated UI juice — so that spending a card *feels* as good as it reads. The house style is **art-free procedural VFX** built in code, which keeps a solo, low-budget project visually rich without commissioning art.

---

## 3. Player Fantasy & Setting (Light Lore)

> *Narrative is intentionally light at this stage. The world is a backdrop that gives the mechanics flavour and the acts identity; a full plot is an open design track (§12).* `[TBD]`

The player is a **wizard** — the canonical protagonist, a sprite-based mage (Cainos Customizable Pixel Character). The character was recently swapped from a skeleton rig to the wizard identity; the skeleton survives in the project as a future enemy.

The run descends (or ascends) through three themed **districts**, each an act with its own hazards, enemies, and boss pool:

| Act | District | Theme / Flavour | Status |
|-----|----------|-----------------|--------|
| **Act 1** | **Oxidation District** | Rust, verdigris, corrosion, acid. A decaying industrial zone. | `[PROTOTYPE]` — playable slice |
| **Act 2** | **Vapor Stratum** | (Theme TBD — atmospheric/gaseous implied by name) | `[PLANNED]` |
| **Act 3** | **Final Forge** | (Theme TBD — heat/industry/climax implied) | `[PLANNED]` |

The wizard's toolkit (fireball, phasing, gravity manipulation, vampiric bite) is expressed *through the deck*, so the character's "spellbook" and the deckbuilding are one and the same fiction — a natural marriage that keeps narrative overhead low while the mechanics carry the identity.

---

## 4. Core Gameplay Loop

```
   ┌─────────────────────────────────────────────────────────────┐
   │  HUB (sandbox, no consumption) — test cards, take contracts   │
   └───────────────────────────┬─────────────────────────────────┘
                               ▼
        ┌──────────────────────────────────────────┐
        │  RUN MAP — pick your branch                │
        │  • Skirmish / Fight / Elite                │
        │  • Recharge rooms hang off Fight & Elite   │
        └───────────────┬───────────────────────────┘
                        ▼
        ┌──────────────────────────────────────────┐
        │  COMBAT ROOM  (Shift does NOT refill)      │
        │  • Traverse the platforming space          │
        │  • Fight enemies / avoid hazards            │
        │  • Spend Shift on jumps + cards             │
        │  • Manage hand: play, Recall, watch charges │
        │  • Kills drop scrap; piles hold gold        │
        └───────────────┬───────────────────────────┘
                        ▼
     (repeat: the map decides the route, one floor at a time)
                        ▼
        ┌──────────────────────────────────────────┐
        │  BOSS ROOM (run finale)                    │
        └───────────────┬───────────────────────────┘
                        ▼
                  (loop back to HUB)
```

**Run structure `[BUILT]`:** A run is **Hub → a routed series of levels → Boss → loop to Hub**. The hub is always the first room. The route is chosen on the **run map** (§6.7); the old "every level once in random order" shuffle survives only as a fallback if the map manager is missing.

**Interstitial economy stops:** a **Shop** and **Blompo** placed as NPCs in rooms, a **Scrap Forge**, and the **quest board** in the hub. *(Gambling has been removed — see §6.6.)*

---

## 5. Core Systems

### 5.1 Shift — The Movement Resource `[BUILT]`

- ⚠️ **Shift CARRIES OVER between rooms and does not regenerate on its own.** It is a **run-long** resource, not a per-room budget — spending it now means having less for the rest of the run. This persistence is the whole identity of the game; describing Shift as refilling each room gets the design exactly backwards.
- **Jumping costs Shift.** Traversal is a spend.
- **Cards cost Shift** (each card has a `shiftCost`).
- **Placing a portal's second endpoint costs Shift.**
- **Shift Crystals** are collectible pickups that grant Shift (dropped by the boss, the crusher trap, and placed in levels).
- Other sources are deliberately scarce: a few relics, and quest payouts that raise your **maximum** permanently.
- The player starts a run with **40 Shift**. Lowering that starting pool is the planned ascension difficulty knob — it makes every Shift cost in the game bite harder without rebalancing a single card.

Shift is the single most important number on screen. The entire difficulty curve is a Shift-scarcity curve.

### 5.2 The Deck & Cards `[BUILT]`

**Data model:**
- **`CardData`** (ScriptableObject) — the card template (action type, max charges, Shift cost, art).
- **`RuntimeCard`** — the live instance, tracking current charges and infinite status.
- **`CardActionType`** — the dispatch enum that maps a card to its behaviour.

**Card actions** are modular: each is a `CardAction` subclass registered in a dictionary (no giant switch statement). Adding a card is a data + small-code task. This architecture is clean and extensible.

**Charges & exhaust:** Cards have limited **charges**. When a card's charges deplete, it moves to the **exhaust pile** and must be recovered via **scrap** `[BUILT concept]`. This makes even your reliable tools a spendable resource — reinforcing Pillar 2.

**The four piles (`DeckManager`):**
| Pile | Role |
|------|------|
| **Draw** | Cards waiting to be drawn into the hand |
| **Hand** | Currently playable cards |
| **Discard** | Played/discarded cards, reshuffled into draw |
| **Exhaust** | Depleted cards, recovered only via scrap |

A **Deck View** popup `[BUILT]` lets the player inspect any pile (or the full combined deck) at any time — a procedurally built, framed, scrollable card grid.

### 5.3 Recall `[BUILT]`

**Recall (R key)** is the player's manual hand-refresh: it redraws the hand for a **Shift cost that escalates with each use within a level**. Recall is the pressure valve — you *can* dig for the card you need, but every dig taxes the resource you're already short on.

### 5.4 Stagger — Buying Shift With Blood `[BUILT — redesigned 2026-08-09]`

A **Stagger card** appears in the hand the moment Shift hits **0** — that alone, nothing else. Playing it pays **+2 Shift** and charges **HP: 8, then 16, 24, 32, 40…**, escalating for the whole run with no cap and no per-room reset.

It is **no longer a three-strikes death sentence**. The run ends when the next bill is bigger than your health bar, which means Stagger is a pump you *choose* to reach for rather than a counter ticking toward a loss. Three rules make it work: it can never be discarded (Recall would otherwise be the dodge), it enters no pile (it is conjured on empty and evaporates when spent — it is not a card you own), and it can never be offered as a reward. It remains the mechanical teeth behind Pillar 1.

### 5.5 Movement & Platformer Tech `[BUILT]`

Beyond the baseline run/jump, the deck grants movement and utility spells. Current card kit (~10 cards; **60+ is the content-complete target** `[PLANNED]`):

| Card | Effect | Notes |
|------|--------|-------|
| **Dash** | Burst horizontal movement | Grants brief invincibility during the dash |
| **Phase** | Pass through geometry temporarily | Ending inside a wall now ejects you to the nearest position that actually fits |
| **Adrenaline** | Speed + slow-motion buff | Duration buff |
| **Fireball** | Ranged projectile attack | Cast animation, timed projectile release (the wizard's signature) |
| **Floor is Lava** (Reverse Gravity) | Flips the player's gravity for 5s | Warning cue before expiry |
| **Glass Wail** | Shockwave that stuns enemies | Part of the planned **Glass archetype** |
| **Vampiric Bite** | Bite attack with lifesteal feel | Part of the **Vampiric archetype** |
| **Comet Dive** | Downward dive **AoE blast** | Redesigned — no longer overlaps head-bounce |
| **Portal** | Two-point teleport | Second placement costs Shift |
| **Jump** (baseline) | Core traversal | Costs Shift |

**Wall sliding** exists but is **not a base ability** — it is a relic (*Gecko Gloves*). The slide is free; the wall jump costs Shift like any other jump, because free vertical movement would contradict Pillar 1.

**Planned archetypes `[PLANNED]`:** **Glass** (high-risk/high-reward, cards exist in theory only) and an expanded **Vampiric** line. Archetypes are how the 60-card content push stays coherent rather than becoming a grab-bag.

### 5.6 Hub / Sandbox Mode `[BUILT]`

The hub is a **consequence-free sandbox** — the always-first room of every run. Its governing principle: **no resource is consumed and no permanent state changes in the hub.** Jumps are free, cards don't burn charges or Shift, Recall is free, Stagger can't trigger. Crucially, **the UI still reacts normally** (Shift counter, hand, Recall all visible and animated), so the hub doubles as a **tutorial space** where the player learns the interface without risk. The quest board lives here.

---

## 6. Progression & Economy

### 6.1 Currencies `[BUILT]`
| Currency | Source | Sink |
|----------|--------|------|
| **Shift** | Crystals, relics, quest payouts | Jumps, cards, portals, Recall |
| **Gold** | Piles placed in levels (exploration) | Shop — buys NEW power |
| **Scrap** | Enemy kills, plus a rebate when a card exhausts | Scrap Forge — buys SUSTAIN (charges, salvaging exhausted cards) |

**The gold/scrap split is load-bearing and must never blur.** Given one wallet, players buy the shiny relic over repairing a card every time, and the exhaust problem stays unsolved. Scrap also exists to make combat *pay*: before it, killing an enemy returned literally nothing, so skipping every fight was optimal in a game built around a deck of attack cards.

### 6.2 Card Acquisition `[BUILT]`
Cards come from **chests, the shop, and quest payouts**. The old per-level "3 cards, choose 1" reward screen has been **removed** — the route choice that followed it moved to the run map.

### 6.3 Relics `[BUILT — slot-constrained]`
A **Balatro-style slotted loadout**: **5 slots**, 19 relics. Acquiring one while full raises a forced sell-or-decline decision, so a loadout is curated rather than accumulated. Selling refunds by rarity and returns the relic to the offer pool. Stat relics are always recomputed from base values, so selling reverses exactly.

> **⚠ What remains is BALANCE, not code (§7.1).** The relics were authored as small always-on bonuses, which is the wrong shape for a 5-slot economy where every pick costs you another relic.

### 6.4 Quests & Oaths `[BUILT]`
A **quest board** in the hub, and a **live tracker HUD** (top-right) made of the same pinned paper.
- **Contracts:** 8 authored, 7 offered. Three are ordinary objectives (kill counts, a flawless room); four are **oaths** — streak contracts asking you to give something up: no cards for three rooms, no Recall, a Shift budget per room, no Stagger for four rooms.
- **Oaths are streaks, not tallies.** Breaking one resets it to zero, but the next room starts clean, so a contract can never dead-end. The tracker shows a break *live*, in the room you're standing in.
- **Reward types:** Gold, Shift charge, Heal, Card, Scrap, MaxHealth — all wired. The rule is *quests pay in things the shop doesn't sell*.
- Reward delivery is immediate; banking payouts to a board at the start of the next act is still `[PLANNED]`.

### 6.5 Skills `[BUILT — system present, slated for repurposing]`
A **skill tree / skill selection** layer exists (`SkillManager`, `SkillRewardManager`). The plan is to repurpose these global passives into **per-card enhancements** granted by Blompo `[PLANNED]`.

### 6.6 Shop `[BUILT]`
Buy cards, relics and services for gold from a **shopkeeper who talks back** — barks split by event (browsing a card, a relic, being too poor, buying, leaving), typed out a character at a time, with small body language.

**Gambling has been removed.** The slot machine is deleted from the project entirely; the Dice Broker (§7.4) is now a from-scratch build rather than a reskin.

### 6.7 The Run Map `[BUILT]`
A branching, whole-act graph opened with **`M`**, in the Slay-the-Spire shape: you plan a route rather than picking one door at a time.
- **Difficulty IS the node type** — Skirmish / Fight / Elite, ascending cost and reward. One node, one icon, one promise.
- **Recharge rooms** (Foundry / Market / Well) hang off a route as attachments rather than floors, and are **only ever reachable from Fight or Elite nodes** — that restriction *is* the run economy. `[PLANNED]`: the three room prefabs don't exist yet, so no recharge rooms appear.
- The governing law for authoring: **a room's loot scales to the Shift it costs to cross.**

---

## 7. Major Planned Directions

### 7.1 Relic Rebalance for the Slot Economy `[PLANNED — highest design priority]`
The slotted system itself is **BUILT** (§6.3). What remains is a **design pass, not an engineering one**:
- Relics were authored as small always-on Slay-the-Spire trickles (+5 HP on kill, +2 Shift on kill). Under scarcity those are the wrong shape — a 5-slot loadout wants **bigger, more interactive, build-defining** effects that change how you play.
- **More relics**, authored at slot-worthy power, so the choice has depth.
- Economy tuning: sell refunds are flat by rarity and untuned against a 45–50 min run.

**Why it matters to the pitch:** the slotted loadout is what extends "Movement is a Resource" into *"everything is a curated resource you manage."* Building it was half the job; making each pick feel like a real decision is the other half.

### 7.2 Content Scale-Up `[PLANNED — the project's real bottleneck]`
- **60+ cards** (from ~14 playable), organized by archetype (Glass, Vampiric, movement, utility).
- **More quests.** The board is built to offer more contracts than you can carry — with 7 it can't yet.
- **More rooms.** The run map is mediocre at ~10 and sings at ~30; ~15 contract-valid rooms already exist unused and need correction passes rather than authoring from scratch.
- **Three-act structure** fully built (Acts 2 & 3).
- **3 bosses per act**, randomly selected from a pool (Act 1's Moss Knight is the first).

> **This gates the two biggest planned systems.** Both the run map and card enhancements are multipliers on content that doesn't exist yet. When choosing between "build another system" and "author more cards/levels/quests", the honest answer is usually the latter.

### 7.3 Chunk-Based Levels `[PLANNED]`
Move from hand-crafted levels to a **chunk/module-based level system** for replayable, roguelike-appropriate variety.

### 7.4 The Dice Broker `[PLANNED]`
A grimy, characterful gambling NPC. The slot machine it was meant to replace is now deleted, so this is a from-scratch build. Rolls the result **in code first, then plays a sprite-sheet dice animation that lands on the correct face** (no physics dependency). Voice/banter potential.

### 7.5 Proper Scene Flow `[PLANNED]`
Real hub→run→hub scene transitions (currently the hub is faked as room 0 of a single scene). Requires a manager-lifetime review.

---

## 8. Boss Design — The Moss Knight (Act 1) `[PROTOTYPE — playable]`

The **Moss Knight** is the Oxidation District finale and the reference design for all future bosses. Full detail lives in `BossDesign_MossKnight.md`; summary here.

**Identity:** A verdigris-corroded knight (Cainos Pixel Art Monster). Green = oxidation, fits the district. He is **immune to acid** (a corrosion creature) — so floor acid threatens only the player.

**Health / structure:** **~300 HP, single-phase.** The multi-phase / 600 HP plan was **cut** — this is a first-act boss most players meet under-geared, so it stays fair and readable rather than over-complicated.

**Moveset** (all built from the rig's single Attack animation, reused as different gestures):
| Attack | Built from | Effect |
|--------|-----------|--------|
| **Acid Cleave** | Attack anim | Melee swing, damage synced to the animation's contact frame |
| **Charge** | Run | Dashes *through* the player (knockback, not bulldoze) |
| **Leap Slam** | Jump | Parabolic leap; on land spawns a green acid shockwave + AoE |
| **Lob** | Attack anim (as throw) | Arcs an acid blob onto the player's spot → lingering acid puddle that slows; if the player camps on a platform, lobs a **live Slime enemy** onto the perch instead |

**Encounter beats `[BUILT]`:**
- **Dormant start** — the boss is a statue until the player steps onto the arena platform (a trigger starts the fight).
- **Awaken cinematic** — camera pans to the boss, ground pounds, a coiling wind-up, then a **roar** (hitstop + screen flash + shockwave + gel eruption) that drops the boss health bar and boss music on the beat.
- **Boss health bar** — a big, top-center, verdigris-themed bar with hit-flash, low-HP danger pulse, and a dramatic fill-up intro.
- **Death celebration** — hitstop, camera focus, gold screen-flash, brief slow-motion, shockwave rings + light beam, then **real collectible loot**: spinning gold coins + tumbling Shift crystals that arc, bounce, and settle (or hover if the boss dies mid-air).
- **Full audio pass** — roar, pound, cleave, charge, leap, slam, lob, hurt, death, all designer-assignable and frame-synced where it matters.

**Card-free damage path `[BUILT]`:** A **Crusher Trap** (lever-operated press) in the arena deals 80 damage to the boss (20 to the player). This guarantees a win condition even if the player runs out of damage cards — the bait→lever→slam loop is a skill tax. Crushing the boss also drops Shift crystals (economy relief).

**Still open for a *complete* encounter:** the acid arena layout (static flank pools + safe platforms) and an optional post-kill card/relic reward screen (loot is the payoff for now).

---

## 9. Enemies & Hazards `[BUILT]`

**Enemy roster** (all with procedural health bars that appear on first damage):
- **AeroBat** — diving flyer with line-of-sight state machine
- **MeleeEnemy**, **RangedEnemy** — Cainos-pattern grunts
- **ShieldEnemy** — blocks damage while shielding
- **Turret**, **PatrolEnemy** — stationary/patrolling threats
- **SlimeEnemy** — also used as the boss's lobbed add
- **Mimic** — disguised threat

**Hazards:**
- **Acid / Lava zones** (`HazardZone`) — damage over time; acid also **slows the player** (a reusable slow system that composes cleanly with speed buffs). **Lava Boots** relic grants full immunity.
- **Spikes** — reflect the player's velocity for correct directional knockback (floor/wall/ceiling aware).
- **Crusher Trap** — the boss-arena press (also a general hazard primitive).

**Head-bounce** (Pogo Boots relic): jump off enemy heads for damage + bounce.

---

## 10. Game Feel & Presentation

### 10.1 Art Direction `[BUILT]`
- **Pixel art**, built on **Cainos asset packs** (character, monsters, environment, water). Solo-budget constraint: **no commissioned art** — work within existing packs and pixel conventions.
- **Procedural VFX is the house style.** Effects are generated in code (sprites baked at runtime, cached) — shockwaves, auras, bite fangs, chest bursts, boss death, reward-screen atmosphere, UI juice. This keeps the game visually rich and *consistent* without an art budget, and every effect is Inspector-tunable.
- **Rarity colour language** is unified across the game (rarity separates on hue, luminance AND glyph, so it survives greyscale and colour-blindness) — chest bursts, relic sockets and Blompo offers all speak it, so the player reads drop quality instantly.

### 10.1b Screen Design — One Ideology, Never One Skin `[BUILT]`

Every full-screen panel is procedural (no prefabs, no art files) and shares an ideology — flat cut plates, restraint, directional light, a subtle particle drift, one meaningful accent. **But no two screens share a skin.** Each gets its own material, and *the material says what the place does*:

| Screen | What it is | The inversion |
|---|---|---|
| **Scrap Forge** | a workbench | warm charcoal, fire from **below**, embers rising, rivets, scuffed |
| **Blompo** | a creature granting a blessing | cold indigo, light from **above**, motes settling, pristine |
| **Relic bar** | what you're **carrying** | near-colourless — a permanent overlay must not compete with the game |
| **Run map** | a chart you **read**, not a place | flat and unlit; motion lives in the information |
| **Pause** | the **moment** you stopped | no window plate at all; motes hang dead still, dragging frozen streaks |
| **Settings** | the **machine's** control panel | light emitted by the content; deliberately outside the fiction |
| **Quest board** | **contracts** you promise to do | dark board, **pale paper** pinned to it — the value structure inverts |

The hue budget is nearly spent, which is the point: **value structure, light direction, motion vocabulary and surface treatment separate these screens at least as much as colour does.** The relic bar proves a theme can carry no hue at all.

### 10.2 Game-Feel Toolkit `[BUILT]`
- **Hitstop** — brief freeze-frames on impact.
- **Camera Shake** — custom (no Cinemachine), pushes past bounds for punch.
- **Camera Peek** — hold Left Ctrl to look ahead toward the mouse.
- **Slow-motion** — used as punctuation (Adrenaline, boss death).
- **UI juice everywhere** — quest slips that sway on their pins and take a wax seal when accepted, cards that flip to their back on hover, interact prompts with a beveled keycap, relic icons that pop in.

### 10.3 Camera `[BUILT]`
Custom `CameraFollow` with per-level `LevelBounds` zones and hysteresis zone transitions. Cinemachine was removed early (confiner issues with multi-shape rooms); the custom system is the standard.

### 10.4 Audio `[BUILT]`
Central `SfxManager` (2D and positional entry points, global volume aware) + `MusicManager` (including boss music). Footsteps and boss abilities are frame-synced via animation events.

---

## 11. Technical Overview

*(For internal reference; abbreviated for the external reader.)*

- **Engine:** Unity 6.0+, URP 2D. Single active scene (`SampleScene`).
- **Architecture:** Manager-singleton pattern (13+ managers: GameManager, DeckManager, LevelManager, RewardManager, RelicManager, QuestSystem, ShopManager, SkillManager, etc.). Known trade-offs (cyclic deps, flat global state) — load-bearing, not up for restructure.
- **Player:** `PlayerController` (~1,200 lines) handles movement, card execution, gravity reversal, VFX, audio, health/gold/shift, death/respawn. Card logic is extracted into modular `CardAction` classes.
- **Centralized pause counter** (`RequestPause`/`ReleasePause`) instead of raw `Time.timeScale`.
- **Known technical debt:** card-effect conflict-flag enforcement (overlapping state cards can corrupt player state — gated by Shift cost in normal play, reachable in the free-spend hub); enemy layer-convention mismatch; a few Cinemachine-era dead references pending cleanup.

Full engineering context, pitfalls, and conventions live in `CLAUDE.md`.

---

## 12. Open Design Questions `[TBD]`

These are unresolved and deliberately flagged rather than papered over:
1. **Narrative depth** — is there a plot, or does Deckshift stay mechanics-first with light district flavour? Currently the latter.
2. **Act 2 / Act 3 themes** — Vapor Stratum and Final Forge need concrete hazard/enemy/boss identities.
3. **Relic power level** — the slotted system is built; what shape should a slot-worthy relic take? (paper design first)
4. **Quest carry cap** — the board can offer more contracts than you can hold, but the cap is still 3 of 3. Should holding drop to 1–2 so accepting is a real choice?
5. **Skill tree scope** — how the skill track interlocks with cards and relics long-term.
6. **Difficulty / meta-progression** — is there any between-run persistence, or is it pure roguelike?
7. **Monetization / pricing** — Steam price point and launch scope. `[TBD]`

---

## 13. Development Roadmap (Priority Order)

*Reordered 2026-08-11. Items 1, 4 and much of 9 from the previous list are now **done** — the slotted relic system, conflict-flag enforcement, and the quest board rebuild (randomised offer, carry cap, card/scrap/HP rewards, oaths).*

1. **Content push — the real bottleneck.** Cards toward 60+, more quests than you can carry, more rooms. Everything else is a multiplier on this.
2. **Relic rebalance for the slot economy** (§7.1) — paper design first.
3. **The three recharge rooms** (Foundry / Market / Well). Until they exist the run map draws no recharge icons and scrap has nowhere to be spent mid-run.
4. **Place Scrap Forges in combat rooms** — the only forge in the game is in the hub, visited once, before you have any scrap or any damaged cards.
5. **Complete the Act 1 boss arena** (acid pools + platforms + reward hook).
6. **Card enhancements via Blompo** — repurpose the skill passives into per-card upgrades.
7. **Chunk-based level system** for real roguelike variety.
8. **Acts 2 & 3** (Vapor Stratum, Final Forge) — themes, enemies, boss pools.
9. **Proper scene flow** (hub → run → hub) and manager-lifetime pass.
10. **Dice Broker** — now a from-scratch build; the slot machine is deleted.
11. **Tutorial / How To Play screen** — the last screen still using the legacy scene panel.

---

## 14. One-Paragraph Summary (for the busy reader)

**Deckshift** is a PC roguelike deckbuilder platformer whose defining twist is that **jumping costs the same resource as your cards.** That single rule — *Movement is a Resource* — turns every room into a simultaneous platforming and hand-management puzzle, and it propagates into every system: finite card charges, escalating Recall costs, a Stagger death spiral when you run dry, and a planned slot-constrained relic overhaul that makes even permanent upgrades a resource you must curate. Wrapped in a procedurally-juiced pixel-art presentation built on a solo budget, Deckshift aims to be the deckbuilder for players who love how it *feels* to move.

---

*Sources: `CLAUDE.md` (architecture, current through 2026-08-11), `BossDesign_MossKnight.md`, and the project's design memory. Implementation status tags reflect the state of the codebase as documented; verify against the live project before treating any `[BUILT]` claim as final.*

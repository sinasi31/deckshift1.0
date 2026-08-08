# DECKSHIFT — Game Design Document

*A 2D pixel-art roguelike deckbuilder platformer where movement itself is the resource you spend.*

**Version:** 1.0 (living document) · **Last compiled:** 2026-07-02
**Engine:** Unity 6.0+ (URP, 2D Renderer) · **Platform:** PC (Steam) · **Team:** Solo developer, designer-first

> **Status legend used throughout this document**
> `[BUILT]` — implemented and playable today · `[PROTOTYPE]` — exists in rough/greybox form · `[PLANNED]` — designed, not yet built · `[TBD]` — open design question
>
> This GDD is written as an external-facing pitch and vision document that also carries full internal design detail. Where the two goals meet, honesty wins: aspirational scope is labelled as such.

---

## 1. The Pitch

**Deckshift** is a roguelike deckbuilder built on top of a precision 2D platformer — and it fuses the two genres at the resource layer, not just the theme layer.

In most deckbuilders you spend *energy* to play cards. In most platformers you jump for free. Deckshift asks one question that reframes both: **what if jumping cost you something?**

Every jump spends **Shift** — a resource that does *not* regenerate over time and refills only when you enter a new room. Your cards — attacks, dashes, utility, movement tech — also cost Shift and carry limited charges. So every room becomes a spatial puzzle *and* a hand-management puzzle at the same time: reach the exit, kill what needs killing, and don't run your movement dry doing it.

- **Genre:** Roguelike deckbuilder × precision platformer
- **Session length:** A full run targets **45–50 minutes** `[PLANNED target]`
- **Comparables (for positioning, not imitation):** *Slay the Spire* (deck construction, run structure) × *Celeste / Dead Cells* (2D movement feel) × *Balatro* (the resource-as-decision philosophy behind the planned relic redesign)
- **Hook in one line:** *"Movement is a resource. Spend it well or die stranded."*

---

## 2. Design Pillars

Everything in Deckshift is measured against three pillars. If a feature doesn't serve one of them, it's cut.

### Pillar 1 — Movement is a Resource
Shift is the spine of the game. Because jumping and cards draw from the same non-regenerating pool, the player is *constantly* trading traversal against combat. Running out of Shift isn't a soft failure — it triggers the **Stagger** death spiral (§5.4). Every system in the game is designed to press on this tension: hazards that force movement, bosses that tax your platforms, relics that pay out in Shift.

### Pillar 2 — Every Choice Is a Resource Choice
Cards have finite charges. Relics (post-redesign) will occupy finite slots. Recall costs escalate. Gold, scrap, and Shift are all scarce. The player is never given a free lunch; they are given a **curated pool they must manage**. The planned Balatro-style relic overhaul (§7) exists specifically to extend Pillar 1's logic to permanent upgrades.

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
   │  HUB (sandbox, no consumption) — test cards, read quest board │
   └───────────────────────────┬─────────────────────────────────┘
                               ▼
        ┌──────────────────────────────────────────┐
        │  COMBAT ROOM  (Shift refills on entry)     │
        │  • Traverse the platforming space          │
        │  • Fight enemies / avoid hazards            │
        │  • Spend Shift on jumps + cards             │
        │  • Manage hand: play, Recall, watch charges │
        └───────────────┬───────────────────────────┘
                        ▼
        ┌──────────────────────────────────────────┐
        │  EXIT DOOR → REWARD SCREEN                  │
        │  • Choose 1 of 3 cards to add to deck      │
        │  • One offered card carries a +1 Shift bonus│
        └───────────────┬───────────────────────────┘
                        ▼
     (repeat: each combat level once, random order, no repeats)
                        ▼
        ┌──────────────────────────────────────────┐
        │  BOSS ROOM (run finale)                    │
        └───────────────┬───────────────────────────┘
                        ▼
                  (loop back to HUB)
```

**Run structure `[BUILT]`:** A run is **Hub → each combat level once, in random order → Boss → loop to Hub**. The hub is always the first room. The boss room is the finale, reached only after every pool level is cleared. This is a finite, structured run (the old endless-refill loop that repeated levels forever was removed).

**Interstitial economy stops `[BUILT / PARTIAL]`:** Between and within the run the player encounters a **Shop**, a **gambling NPC** (currently a Slot Machine, planned to become the **Dice Broker** — §7.4), a **quest board** (hub), and **card / skill reward screens**.

---

## 5. Core Systems

### 5.1 Shift — The Movement Resource `[BUILT]`

- **Non-regenerating per room.** Shift refills when the player enters a new room, then only depletes.
- **Jumping costs Shift.** Traversal is a spend.
- **Cards cost Shift** (each card has a `shiftCost`).
- **Placing a portal's second endpoint costs Shift.**
- **Shift Crystals** are collectible pickups that grant Shift (dropped by the boss, the crusher trap, and as rewards).
- The end-of-level reward screen always offers one card carrying a **+1 Shift bonus**, surfaced with a dedicated badge so the player can weigh the bonus against the card itself.

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

A **Deck View** popup `[BUILT]` lets the player inspect any pile (or the full combined deck) at any time — a procedurally built, framed, scrollable card grid, reachable even from the reward screen.

### 5.3 Recall `[BUILT]`

**Recall (R key)** is the player's manual hand-refresh: it redraws the hand for a **Shift cost that escalates with each use within a level**. Recall is the pressure valve — you *can* dig for the card you need, but every dig taxes the resource you're already short on.

### 5.4 Stagger — The Fail-State Spiral `[BUILT]`

When the player has **0 Shift AND no playable cards**, a **Stagger card** is automatically forced into the hand. **Three Stagger plays in a single run = death.** Stagger converts "I mismanaged my movement" into a concrete, escalating consequence rather than a soft stall. It is the mechanical teeth behind Pillar 1.

### 5.5 Movement & Platformer Tech `[BUILT]`

Beyond the baseline run/jump, the deck grants movement and utility spells. Current card kit (~10 cards; **60+ is the content-complete target** `[PLANNED]`):

| Card | Effect | Notes |
|------|--------|-------|
| **Dash** | Burst horizontal movement | Grants brief invincibility during the dash |
| **Phase** | Pass through geometry temporarily | Known edge case: can stick if it ends inside a wall |
| **Adrenaline** | Speed + slow-motion buff | Duration buff |
| **Fireball** | Ranged projectile attack | Cast animation, timed projectile release (the wizard's signature) |
| **Floor is Lava** (Reverse Gravity) | Flips the player's gravity for 5s | Warning cue before expiry |
| **Glass Wail** | Shockwave that stuns enemies | Part of the planned **Glass archetype** |
| **Vampiric Bite** | Bite attack with lifesteal feel | Part of the **Vampiric archetype** |
| **Comet Dive** | Downward dive attack | Currently overlaps head-bounce — flagged for redesign |
| **Portal** | Two-point teleport | Second placement costs Shift |
| **Jump** (baseline) | Core traversal | Costs Shift |

**Planned archetypes `[PLANNED]`:** **Glass** (high-risk/high-reward, cards exist in theory only) and an expanded **Vampiric** line. Archetypes are how the 60-card content push stays coherent rather than becoming a grab-bag.

### 5.6 Hub / Sandbox Mode `[BUILT]`

The hub is a **consequence-free sandbox** — the always-first room of every run. Its governing principle: **no resource is consumed and no permanent state changes in the hub.** Jumps are free, cards don't burn charges or Shift, Recall is free, Stagger can't trigger. Crucially, **the UI still reacts normally** (Shift counter, hand, Recall all visible and animated), so the hub doubles as a **tutorial space** where the player learns the interface without risk. The quest board lives here.

---

## 6. Progression & Economy

### 6.1 Currencies
| Currency | Source | Sink |
|----------|--------|------|
| **Shift** | Room entry, crystals, reward bonus | Jumps, cards, portals, Recall |
| **Gold** `[BUILT]` | Enemy drops, boss, quests | Shop, gambling |
| **Scrap** `[BUILT concept]` | (recovery resource) | Recover exhausted cards |

### 6.2 Card Rewards `[BUILT]`
After each level, a **reward screen** offers **3 cards, choose 1**, with one card flagged for a **+1 Shift bonus**. The screen is fully juiced (atmosphere, staggered "dealt" reveal, selection burst) and the deck stays inspectable via a View Deck button.

### 6.3 Relics `[BUILT — pending major redesign]`
Currently a **Slay-the-Spire-style additive system**: unlimited relics, each a small passive bonus, no slot limits. Functional relics today:
- **Vampire Tooth** — kills heal HP
- **Kinetic** — kills grant +2 Shift
- **Spiked Carapace** — taking damage reflects to nearby enemies
- **Pogo Boots** — head-bounce on enemies
- **Lava Boots** — immunity to hazard zones (acid/lava)

> **⚠ Major planned direction — Slot-Constrained Relics (§7.1).** The additive system is slated for a Balatro-style overhaul. New relic content and relic UX are deliberately frozen until then.

### 6.4 Quests `[BUILT]`
A **quest board** in the hub offers up to 3 quests; a **live tracker HUD** (top-right) shows progress with animated rows and completion celebrations.
- **Quest types:** `KillEnemy` and `AirKill` fully wired; `GoldAccumulate`, `NoDamageRoom`, `UseCardCount` defined but not yet firing `[PLANNED]`.
- **Reward types:** Gold, Shift charge, Heal (all wired); card rewards `[PLANNED]`.
- Reward delivery is currently immediate; deferring to level-end is planned.

### 6.5 Skills `[BUILT — system present]`
A **skill tree / skill selection** layer exists (`SkillManager`, `SkillRewardManager`, a skill reward screen). This is a secondary progression track alongside cards and relics.

### 6.6 Shop & Gambling `[BUILT — gambling to be re-themed]`
- **Shop** — buy cards/relics for gold.
- **Slot Machine** — pay gold for a random relic. **Planned replacement: the Dice Broker** (§7.4), a character-driven NPC with the same payout but far more personality.

---

## 7. Major Planned Directions

### 7.1 Slot-Constrained Relic Redesign `[PLANNED — highest design priority]`
Replace the additive relic model with a **Balatro-style slotted system**:
- **Fixed relic slots** (starting ~5, tunable).
- **To acquire when full, you must sell an existing relic** — every acquisition becomes a real trade-off.
- Relics get **bigger, more interactive effects** (small passive bonuses don't shine under scarcity).
- **15–25 new relics** to make slot decisions meaningful; economy tuning for the 45–50 min run.

**Why it matters to the pitch:** this is the design move that makes Deckshift's identity *complete* — it extends "Movement is a Resource" to *"everything is a curated resource you manage."* It's the connective tissue between the platformer core and the deckbuilder metagame.

### 7.2 Content Scale-Up `[PLANNED]`
- **60+ cards** (from ~10), organized by archetype (Glass, Vampiric, movement, utility).
- **Three-act structure** fully built (Acts 2 & 3).
- **3 bosses per act**, randomly selected from a pool (Act 1's Moss Knight is the first).

### 7.3 Chunk-Based Levels `[PLANNED]`
Move from hand-crafted levels to a **chunk/module-based level system** for replayable, roguelike-appropriate variety.

### 7.4 The Dice Broker `[PLANNED]`
A grimy, characterful gambling NPC replacing the slot machine. Rolls the result **in code first, then plays a sprite-sheet dice animation that lands on the correct face** (no physics dependency). Voice/banter potential.

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
- **Rarity colour language** is unified across the game (Common = pale, Rare = blue, Epic = purple, Legendary = gold) — chest bursts, relic HUD chips, and reward screens all speak it, so the player reads drop quality instantly.

### 10.2 Game-Feel Toolkit `[BUILT]`
- **Hitstop** — brief freeze-frames on impact.
- **Camera Shake** — custom (no Cinemachine), pushes past bounds for punch.
- **Camera Peek** — hold Left Ctrl to look ahead toward the mouse.
- **Slow-motion** — used as punctuation (Adrenaline, boss death).
- **UI juice everywhere** — animated reward screens, quest board pop-ins with an "ACCEPTED" stamp, quest tracker completion celebrations, interact prompts with a beveled keycap, relic icons that pop in.

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
3. **Relic redesign specifics** — final slot count, sell-refund %, offer frequency (paper design first).
4. **Scrap economy** — full definition of scrap acquisition and exhaust-recovery costs.
5. **Skill tree scope** — how the skill track interlocks with cards and relics long-term.
6. **Difficulty / meta-progression** — is there any between-run persistence, or is it pure roguelike?
7. **Monetization / pricing** — Steam price point and launch scope. `[TBD]`

---

## 13. Development Roadmap (Priority Order)

1. **Slot-constrained relic redesign** (paper → code) — the identity-completing system.
2. **Card content push** toward 60+, with archetypes fully realized (Glass, Vampiric).
3. **Complete the Act 1 boss arena** (acid pools + platforms + reward hook).
4. **Card-effect conflict-flag enforcement** (the top engineering fix).
5. **Chunk-based level system** for real roguelike variety.
6. **Acts 2 & 3** (Vapor Stratum, Final Forge) — themes, enemies, boss pools.
7. **Dice Broker** replaces the slot machine.
8. **Proper scene flow** (hub → run → hub) and manager-lifetime pass.
9. **Quest system expansion** (wire remaining types, card rewards, randomization, 3-quest cap).

---

## 14. One-Paragraph Summary (for the busy reader)

**Deckshift** is a PC roguelike deckbuilder platformer whose defining twist is that **jumping costs the same resource as your cards.** That single rule — *Movement is a Resource* — turns every room into a simultaneous platforming and hand-management puzzle, and it propagates into every system: finite card charges, escalating Recall costs, a Stagger death spiral when you run dry, and a planned slot-constrained relic overhaul that makes even permanent upgrades a resource you must curate. Wrapped in a procedurally-juiced pixel-art presentation built on a solo budget, Deckshift aims to be the deckbuilder for players who love how it *feels* to move.

---

*Sources: `CLAUDE.md` (architecture, current through 2026-07-02), `BossDesign_MossKnight.md`, and the project's design memory. Implementation status tags reflect the state of the codebase as documented; verify against the live project before treating any `[BUILT]` claim as final.*

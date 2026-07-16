# Deckshift — Card & Enemy Number Anchors

The baseline exchange rates every card and enemy number is *derived* from, so nothing is a
random guess. This is the yardstick doc (like `RelicRedesign.md`). Approve/adjust the anchors
here first; then per-card numbers fall out of them.

Status: **v1 draft, 2026-07-15.** Numbers grounded in the current game, not invented.

---

## 1. The two economies (read this first)

Cards are limited on **two independent axes**, and they mean different things:

- **Charges (uses per room)** — the PRIMARY limiter on attacks. Fireball gets 6, a finisher
  gets 1. This is where most of a card's power is bought.
- **Shift cost (0/1/2)** — a SECONDARY, light tax. The player starts each run with **40 Shift**
  (prefab value; the script default of 3 is not used) and Shift is the run-long movement
  resource (~1 per air-jump, carries between rooms, refilled by crystals/relics). At base
  difficulty 40 is abundant, so a 1–2 Shift card cost is minor friction, **not** a hard limiter.
  Most pure attacks cost **0**; Shift cost is reserved for movement cards and premium utility
  (Phase/Portal = 2).
  - **Ascension knob (later):** lowering the starting Shift pool as difficulty climbs makes every
    Shift-cost card progressively more painful *for free* — no per-card rebalance needed. Design
    the base numbers for a 40 pool and let ascension squeeze it.

---

## 2. Reference constants (fixed)

| Thing | Value |
|---|---|
| Player HP | 100 |
| Player Shift (base) | 40 |
| **Damage unit `U`** | **15** (= one Fireball: 0 Shift, 6 charges, Common, single-target ranged) |
| Fodder enemy | ~12 HP → dies to one Fireball |

---

## 3. Anchor rates

**Card power budget ≈ `U × ChargeMult × ShapeMult × RarityMult` (+ a small Shift premium).**
The card's total effect — damage + heal-value + condition-value — should land near that budget.

| Axis | Multiplier |
|---|---|
| **Charges** (per-use power) | 6:×1.0 · 5:×1.1 · 4:×1.25 · 3:×1.45 · 2:×1.8 · 1 (single-use):×2.6 |
| **Shape** | ranged ×1.0 · melee ×1.3 · AoE ×1.8 |
| **Rarity** | Common ×1.0 · Rare ×1.3 · Epic ×1.6 · Legendary = unique/rule-bending |
| **Shift premium** | light at base: ~+3–5 dmg-value per Shift, or a small rider. Grows with ascension. |

**Value conversions:**
- **Heal gained:** 1 HP ≈ 1 dmg-value (≈ 0.7 when it's a rider on an attack — situational).
- **HP spent (Reckless cost):** 1 HP ≈ **0.7** dmg-value. Spending HP is *cheap* at full health
  and *lethal* at low — that asymmetry is the Reckless gamble, and it's deliberate.
- **Conditions:** 1s single-target stun ≈ 4 · 50% slow for 2s ≈ 6 (×1.8 if AoE).

---

## 4. Worked examples (validates the method on existing cards)

| Card | Budget calc | Effect | Verdict |
|---|---|---|---|
| **Fireball** | 15 ×1.0 ×1.0 ×1.0 = **15** | 15 dmg | on the nose ✓ |
| **Vampiric Bite** | 15 ×1.1(5) ×1.3(melee) = **21** +1 Shift | 20 dmg + 10 heal | slightly hot (fan favourite — leave, or trim heal to 8) |
| **Comet Dive** | 15 ×1.25(4) ×1.8(AoE) = **34** | 40 AoE, needs airborne setup | fits (setup = discount) ✓ |

**Fixes it flags for our 3 new cards (placeholders):**
- **Freefall Blade** (1 Shift, 3 ch, arc) — budget ≈ 15 ×1.45 ×1.3 ≈ **28**. Current 12 / 24-falling
  is under. → propose **15 grounded / 30 falling**, or drop to 0 Shift as a cheap momentum poke.
- **Dead Weight** — +3 Shift is now **weak** (3 of 40) for the cost of a whole hand slot all room.
  → reconsider payoff: **+8–10 Shift**, or a different reward (gold / a free card next room).
  Designer originally said +3 under the (wrong) assumption Shift was scarce — worth revisiting.
- **Glass Parry** (25 riposte + negate a hit + refund charge on success) ≈ on curve for a skill card ✓.

---

## 5. Enemy HP tiers (anchored to Fireball = 15)

| Tier | HP | Fireballs to kill | Members |
|---|---|---|---|
| **Fodder** | 12 | **1 (one-shot)** | Zombie Shambler (new), Slime (10, keep) |
| **Grunt** | 25 | 2 | Zombie Rotbrute (later), Ranged → bump 20→25 |
| **Soldier** | 40 | 3 (or 1 Comet Dive) | Melee → bump 30→40, Mimic ~35, Shield ~40 |
| **Elite** | 70 | ~5 | future |
| **Mini-boss** | 140 | ~10 | future |
| **Boss** | 300 | — | Moss Knight (keep) |

Note: Whetstone relic (+5 first hit) lets a first Fireball reach 20, one-shotting up to Grunt-lite —
intended synergy, not a problem.

---

## 6. Zombie early-enemies (build order: Shambler first)

Cainos `PF Zombie - A/B/C/D` are ART prefabs with Cainos' own AI — building a game enemy means a
new prefab: zombie sprite/animator + the game's `EnemyHealth` + a game AI.
- **Shambler** — 12 HP, slow walk, contact damage (reuse MeleeEnemy's AI, slowed). Core one-shot
  fodder, travels in packs. New importer marker `z`. **← build this first.**
- **Rotbrute** (later) — 25 HP, bigger/slower, harder contact hit. Grunt-tier variety.
- **Spitter** (later) — weak ranged, reuse RangedEnemy AI. Keep early enemies simple for now.

---

## 7. TODOs surfaced here

- **Fireball hitbox:** collider is a 0.137 circle at wand height → sails over slimes/mimics. Fix =
  bigger collider **+** lower launch height, tuned so the hitbox bottom sits between floor and
  enemy chest (can't go full-tall or it explodes on the floor). Needs a playtest tune.
- **ShieldEnemy has no sprite** → unused in levels. Compose one from the Cainos packs (armored
  humanoid + a shield prop) later. Purely art; the enemy logic works.
- **Retune existing enemy HP** to the tiers above when we implement (Melee 30→40, Ranged 20→25).
- **Fix the 3 new cards' placeholder numbers** per §4 once anchors are approved.

---

## 8. Open decisions (designer)

Anchors §2–3 are approved-for-now (2026-07-15). Still open: Dead Weight's real payoff (§4);
whether to trim Bite's heal; exact Freefall numbers. Everything else derives from the table.

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

**Fixes it flagged for our 3 new cards — APPLIED 2026-07-16:**
- **Freefall Blade** (1 Shift, 3 ch, arc) — budget ≈ 15 ×1.45 ×1.3 ≈ **28**. Was 12 / 24-falling (under).
  → **DONE: actionValue 12→15** (15 grounded / **30 falling**, on budget). Kept 1 Shift as the small
  premium. Open feel-test: if the Shift cost fights the momentum "flow," drop it to 0 (the doc's
  alternative) — that's a playtest call, not a balance one.
- **Dead Weight** — +3 Shift was **weak** (3 of 40) for the cost of a whole hand slot all room.
  → **DONE: actionValue 3→8** (+8 Shift held-to-room-end). Picked the floor of the +8–10 range
  because Dead Weight is a *Basic* — modest-but-real, easy to bump to +10 if it feels flat in play.
- **Glass Parry** (25 riposte + negate a hit + refund charge on success) ≈ on curve for a skill card
  → **left at 25** ✓.

---

## 5. Enemy HP tiers (anchored to Fireball = 15)

| Tier | HP | Fireballs to kill | Members |
|---|---|---|---|
| **Fodder** | 12 | **1 (one-shot)** | Zombie Shambler `z` (12), Slime (10), Spitter `s` (18, ranged glass) |
| **Grunt** | 25 | 2 | Zombie Rotbrute `Z` (25), Ranged (25) |
| **Soldier** | 40 | 3 (or 1 Comet Dive) | Melee (40), Mimic (30), Shield ~40 (no sprite) |
| **Elite** | 70 | ~5 | future |
| **Mini-boss** | 140 | ~10 | future |
| **Boss** | 300 | — | Moss Knight (keep) |

Note: Whetstone relic (+5 first hit) lets a first Fireball reach 20, one-shotting up to Grunt-lite —
intended synergy, not a problem.

---

## 6. Zombie early-enemies (build order: Shambler first)

Cainos `PF Zombie - A/B/C/D` are ART prefabs with Cainos' own AI — building a game enemy means a
new prefab: zombie sprite/animator + the game's `EnemyHealth` + a game AI. **All three BUILT
2026-07-16** (`Assets/YeniLeveller/`), capsule colliders baked in, importer markers live:
- **Shambler** `z` — 12 HP, slow walk (0.45), contact damage 8 (MeleeEnemyAI). Core one-shot fodder,
  travels in packs. PF Zombie - A skin.
- **Rotbrute** `Z` — 25 HP grunt, 1.15× bigger, slower walk (0.38) + heavier (mass 5), harder
  contact hit (damage 14, cooldown 2.2, knockback 6, MeleeEnemyAI). PF Zombie - B skin.
- **Spitter** `s` — 18 HP weak ranged. New **`ZombieSpitterAI`** (approach → stop at range 8 →
  spit on a 2.8s cadence): the zombie rig has no ranged animation, so the AI reuses the melee
  gesture and spawns the projectile itself on a 0.35s windup (mirrored-by-facing origin, no
  firepoint child). Fires the existing turret bolt `Mermi.prefab` (8 dmg) — a **placeholder look**;
  a green goo reskin is an easy follow-up. PF Zombie - C skin.

**Reusable recipe (any Cainos monster → game enemy):** copy `PF <Monster>`, remove
`MonsterInputMouseAndKeyboard`, add `EnemyHealth` (wire healthBar+damagePopup, stunSkinnedRenderers
→ the SkinnedMeshRenderer) + a game AI that drives `MonsterController.inputMove/inputAttack`, swap
the source `BoxCollider2D` → a vertical `CapsuleCollider2D` (box snags on tile seams), re-skin via
the two `m_Materials.Array.data[0]` modifications on the nested FBX instance.

---

## 7. TODOs surfaced here

- ~~**Fireball hitbox**~~ **DONE 2026-07-16.** The 0.137 circle at wand height became a vertical
  `CapsuleCollider2D` "curtain" (local size 0.309×0.622, offset y −0.168; prefab scale 2.008× →
  world hitbox **F+0.30 to F+1.55**). Keeps its top at wand height (still hits tall enemies) but
  reaches down through slime (top F+0.88) / mimic (top F+1.0) bodies, with 0.30 clearance above the
  floor so it doesn't detonate on ground tiles. Launch height unchanged (sprite still casts from the
  wand). Known cosmetic: the explosion VFX spawns at the fireball's center (wand height), so a hit
  on a low slime pops slightly above it — lower the spawn in `PerformFireball` if that reads badly.
- **ShieldEnemy has no sprite** → unused in levels. Compose one from the Cainos packs (armored
  humanoid + a shield prop) later. Purely art; the enemy logic works.
- ~~**Retune existing enemy HP**~~ **DONE 2026-07-16.** Melee 30→40, Ranged 20→25. Slime 10 (fodder,
  kept), Shambler 12 (fodder), Boss 300 (kept). Mimic left at 30 — sits between grunt (25) and
  soldier (40); designer's call whether to snap it to a tier.
- ~~**Fix the 3 new cards' placeholder numbers**~~ **DONE 2026-07-16** — see §4.

---

## 8. Open decisions (designer)

Anchors §2–3 are approved-for-now (2026-07-15). Dead Weight (+8) and Freefall (15/30) resolved
2026-07-16 (§4). Still open: whether to trim Vampiric Bite's heal (20+10 runs slightly hot);
whether Mimic should snap to a clean tier; the Freefall 1-Shift-vs-0 feel-test.

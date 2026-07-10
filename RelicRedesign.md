# Relic Redesign — Slot-Constrained Loadout

Paper design for replacing Deckshift's additive (Slay-the-Spire-style) relic system
with a **Balatro-style slot-constrained loadout**. Started 2026-07-09. This is the
anchor doc — keep it in sync as the build progresses across sessions.

Companion context: see `CLAUDE.md` → "Relic System" and "Future: Slot-Constrained
Relic Redesign". Memory: [[project-deckshift]].

---

## Why

Deckshift's core philosophy is **"Movement is a Resource."** The old relic system
contradicted that: relics were a pile that only ever grew, every one a small passive
bonus, no decisions after acquiring. The redesign makes relics a **curated resource
pool the player manages** — every new relic is a real "what do I give up?" decision.

---

## Locked decisions (v1)

Agreed with the designer 2026-07-09:

1. **5 slots, fixed.** No slot expansion in v1 (may revisit — start simple).
2. **Loadout bar lives in a top row.** Always on screen: filled slots + empty frames
   + an `N/5` count. Reads as a loadout, not a growing column.
3. **Full-slot acquisition = a Swap Screen.** When a relic is granted and all 5 slots
   are full, the game pauses and shows the incoming relic vs. the current loadout;
   the player **sells one to take it, or leaves it.**
4. **Selling refunds a fixed amount by rarity** (tunable):
   - Common **25** · Rare **50** · Epic **90** · Legendary **150** gold.
5. **Manage panel opens from both a bar click and a hotkey.**
6. **Rarity colour language is unchanged** (Legendary gold / Epic purple / Rare blue /
   Common grey — the same palette already used by `RelicIcon` and the chest burst).

### The no-refund economics rule

Selling must never feel like a punishment for a purchase. The Swap Screen **decides
before anything is finalized**:

- **Paid source (shop):** clicking buy while full opens the Swap Screen *first*. Take
  it → pay for the new relic AND pocket the sell gold. Leave it → **no charge at all.**
- **Free source (chest, boss, slot machine):** Swap Screen; leaving it simply declines
  the relic at no cost.

There is never a "I paid and lost it" moment.

---

## Three UI surfaces

### 1. Loadout bar (always on screen)
- Top row, part of `GameplayHUD` (so it hides behind full-screen panels like today).
- Renders exactly **5 slot cells**: filled cells reuse the existing procedural
  `RelicIcon` chip; empty cells show a dim rounded frame.
- An `N/5` count label.
- **Hover tooltip:** name + description + sell value.
- **Clickable** → opens the Manage panel. A hotkey opens it too.

### 2. Manage panel (calm, paused screen)
- Opened any time from the bar/hotkey. Pauses via `GameManager.RequestPause`, hides
  `GameplayHUD`, locks the hand drawer (`HandUIDrawer.SetLocked(true)`) — the standard
  full-screen-panel contract.
- Shows all 5 slots large with full descriptions. Click a relic → **Sell** (shows its
  gold value) → relic becomes gold, slot frees.

### 3. Swap Screen (acquire-when-full decision)
- Triggered by a grant while full. Shows the incoming relic + the loadout as
  click-to-sell targets. "Take it" (after selecting one to sell) / "Leave it".
- Wired into every grant path (shop, chest, boss death, slot machine).

The Manage panel and Swap Screen share a clickable-loadout widget; build it once.

---

## Data model changes

`RelicManager` (owned relics stay a `List<RelicData>`; list index = slot index):
- `public const int MaxSlots = 5;`
- `public bool IsFull => ownedRelics.Count >= MaxSlots;`
- `public int SellValueFor(RelicData)` — switch on rarity (numbers above).
- `public void SellRelic(RelicData)` — remove, credit gold, fire `OnRelicRemoved`.
- `public event Action<RelicData> OnRelicRemoved;` (HUD/panels subscribe alongside
  the existing `OnRelicAdded`).

`RelicData` unchanged for v1 (relicID / relicName / description / relicArt / rarity).
Sell value is derived from rarity, not stored per-asset.

---

## Grant-path integration (Stage 3)

Current grant paths call `RelicManager.AddRelic(relic)` directly:
`ShopItemUI`, `SlotMachineUI`, `DebugTools` (F1). Boss death loot drops real pickups,
not relics (no relic grant there yet).

Stage 3 routes grants through a single **`TryGrantRelic(relic, context)`** entry point:
- Not full → add as today.
- Full → open the Swap Screen; the `context` carries the "paid" info so the shop can
  honour the no-refund rule (charge only on "Take it").

---

## Staged build plan

- **Stage 1 — Foundation + loadout bar.** Data model (slots, sell values, `SellRelic`,
  `OnRelicRemoved`) + reworked top-row HUD with empty slots, `N/5` count, hover tooltip.
  *(Bar click / hotkey wired in Stage 2 once the panel exists.)*
- **Stage 2 — Manage panel.** Inspect + sell. Wire bar-click + hotkey to open it.
- **Stage 3 — Swap Screen** + route all grant paths through `TryGrantRelic`.
- **Stage 4 (later) — Content.** Bigger, more interactive relics + rebalance the
  current 5 (small passives don't shine in a slot system). Target ~15–25 relics so
  slot decisions matter.

---

## Stage 4 content — agreed relic slate (2026-07-09)

Designed with the user. **Corrections learned:** every jump costs exactly **1 Shift**; **Shift carries
between rooms and never resets** (a few wasted early jumps hurt later); enemies **don't drop gold** yet
(scrap system planned). These invalidated some first-draft ideas.

**Build (functional standalone) — proposed numbers, tunable:**
- **Reclaimer's Clamp** (Rare) — first card exhausted each room returns to hand with 1 charge.
- **Flux Regulator** (Common) — first card each room is free (0 Shift). Reuses `DeckManager.isNextCardFree`.
- **Overclocked Recall** (Epic) — Recall costs 0 Shift but each Recall deals 5 self-damage.
- **Meteor Greaves** (Epic) — landing from a fall ≥~4u makes a shockwave (radius ~3.5), damage scales with
  fall height (~3/unit, cap ~45) + knockback.
- **Executioner's Seal** (Epic) — hitting a **non-boss** enemy at ≤20% max HP instakills it (bosses immune).
- **Midas Recoil** (Rare) — +1 damage per 25 gold held, on all player damage (needs a central outgoing-damage hook).
- **Phoenix Cog** (Legendary) — once/run, lethal damage leaves you at 1 HP + screen-clear blast (~60).

**Commons batch (agreed, all but one):** Flux Regulator (above) · **Pocket Battery** (start each room +1 Shift)
· **Scrap Magnet** (pickups pulled from farther) · **Whetstone** (first hit on each enemy +5) ·
**Reinforced Plating** (+15 max HP). **Kinetic Primer** (idle→free jump) — user: too strong, redesign later.

**Needs the sell UI (Stage 2) before it's meaningful:**
- **Glass Heart** (Epic) — +100% damage, −50% max HP. A punishing relic is only fair if sellable.
- **Foundry Rights** (Epic) — each relic sold → +1 max Shift this run (start +1; user flagged it strong).
  Literally can't fire until selling exists.

**Still designing (numbers/feel):** Deadweight Governor (ground-drain vs kill payout — +3/kill too low) ·
Overpressure Regulator (Shift regen + halved max — spicy) · Flywheel Core (airborne kill → +1 Shift — cheap, defined).

**Shelved (revisit when systems exist):** Surge Cell (dead — Shift already carries) · Vulture's Instinct
(needs scrap drops) · Rust Catalyst (needs supporting cards + status system) · Gecko Grip (no wall-cling yet
— maybe the relic that GRANTS it) · Hollow Socket (design pass).

**Note on rarity curve:** a slot system still needs Commons — they're the *cycle fuel* you slot early and
**sell** later to fund Epics. Don't make everything build-defining.

## Deferred / open

- Slot **expansion** (buy a 6th/7th slot) — out of scope for v1.
- Relic **reordering** (drag) — nice-to-have; not required for the decision loop.
- Existing relics keep their current effects until Stage 4 rebalance.
- Sell numbers are first-pass; tune once the run economy is felt end to end.

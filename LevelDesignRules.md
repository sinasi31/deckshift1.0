# Level Design Rules — measured, not guessed

Written 2026-07-22 after the designer's verdict on the generated levels: *"they're empty and
huge, there's no rhythm, the jump distances are wrong, and they look messy."*

Everything in this file is a **measurement**, either of the player's real physics or of the
nine hand-built rooms the designer actually likes. Nothing here is taste. The tool that
produced these numbers is `Tools/LevelLab` — rerun it instead of trusting this file if the
player gets retuned or new rooms get hand-built.

---

## 1. What the player can actually do

Full table: `MovementMetrics.md` (regenerate with `dotnet run --project Tools/LevelLab -- metrics`).

| Move | Trivial | Standard | Tight | Max |
|---|---:|---:|---:|---:|
| **Rise** onto a ledge | 2 | 3 | 4 | **4.8** |
| **Flat gap** at the same height | 5 | 8 | 10 | **11.9** |
| Drop 4 and cross | — | 9 | 12 | **13.6** |
| Walk off a ledge (no jump), drop 4 | — | — | — | **4.4** |
| Dash | — | — | — | **4.2** |

Body: 0.51 wide x 1.68 tall. **Walkable corridors need 2 tiles of clear height.**

### The asymmetry that was getting levels wrong

Vertically the player is **tight**: a rise of 4 is already 83% of the maximum, so rise-4
ladders are precision work, not filler. Horizontally the player is a **cannon**: a running
jump clears ~12 tiles, so the "flat gaps ≤ 5-6 tiles" rule the old level laws prescribed is
under half of what a jump covers — those gaps cannot be felt at all.

Building tall shafts out of rise-4 ledges and padding the room with 6-tile gaps produces
exactly the reported complaint: climbs that feel fiddly, horizontal stretches that feel empty.

**Use rise 3 as the default step and rise 4 only when you mean it. Use gaps of 8-10 when you
want a jump to register at all; anything under 5 is just floor with a hole in it.**

### One live bug this uncovered

`PlayerController.PerformJump` (line 736) adds a horizontal impulse of `moveInput * jumpForce`
on top of the vertical one. It never does anything: `isGrounded` is computed in `Update`
(line 339) but movement is applied in `FixedUpdate` (line 610), so the first physics step after
a jump still sees `isGrounded == true`, takes the grounded branch, and pins `vx` back to
`moveSpeed`. Jump distance today is "run speed for the whole arc" — 11.9 tiles. If that
impulse is ever fixed, jumps reach **14.8** tiles and every level in the game needs re-checking.
Leave it alone unless the designer wants the change; just don't design around the dead code.

---

## 2. What a Deckshift room looks like

Measured across the seven hand-built **combat** rooms: `efeslevel1-4`, `EfeVrl4-6`.
`hub` and `BossRoom` are deliberately **excluded** — a sandbox and a boss arena are not what a
generated combat room should imitate, and including them was quietly widening three bands
(the hub alone set the upper bound on `open %` and `longest ledge run`).

One of the seven, `EfeVrl5`, is a sprawling two-chamber room that sits far outside the other
six on nearly every metric. So the bands come in two tiers:

- **Limit** — the full envelope of all seven. Outside this is a **failure**.
- **Typical** — where six of the seven live. Outside this is a **warning** worth a second look.

| Metric | Limit | Typical | Why |
|---|---|---|---|
| Width | 44-56 | 44-48 | only the sprawling room is 56 wide |
| Height | 22-30 | 22-27 | |
| Area | 950-1700 | 950-1300 | bigger reads as a slog |
| Open % | 44-69 | 44-55 | above = an empty hall, below = a carved maze |
| Footholds per 100 open cells | 8.5-23 | 11-23 | how much there is to stand on |
| **1-tile ledges %** | **54-85** | **54-85** | **the signature — see below** |
| Longest ledge run | ≤ 38 | ≤ 38 | long flat floors are dead walking time |
| Biggest single void | ≤ 635 | ≤ 130 | one big empty rectangle = nothing to do in it |
| Deep-void % | ≤ 41 | ≤ 33 | open space more than 8 tiles above any floor |

Calibration check: all seven hand-built combat rooms pass every limit (EfeVrl5 raises seven
warnings, as it should). All eight old generated rooms fail between 2 and 8 of them.

### The stepping-stone signature

The single strongest difference between the hand-built rooms and the generated ones:

- Hand-built: **55-83%** of all standable ledges are exactly **one tile wide**.
- Generated: **5-38%**.

A hand-built room is a *climbing gym* — a compact chamber whose open volume is peppered with
small, irregular, single-block footholds, so the player is almost always within one jump of
something. A generated room was a *carved corridor system* — wide strips, long walks, big
voids between them.

Look at what `efeslevel3` actually is (extracted from its tilemap, `Tools/LevelLab/extracted/`):

```
###.............#...#....................###
###.............###.#............#.......###
###......#......###.......#####.##..#....###
###.###...............###...........#....###
###...##......#####...#....#...#...##....###
###....#........#.###..........#...##....###
```

Sparse, irregular, one and two-tile blocks at varied heights. **That is the target texture.**

---

## 2b. What actually goes IN the room — the object census

Geometry was only half the gap. Reading the prefab *instances* placed inside the six
hand-built rooms (`Tools/LevelLab -- objects`) shows where their character really comes from:

| Room | Objects placed | Decoration | Gameplay |
|---|---:|---:|---:|
| efeslevel1 | 107 | 87 | 20 |
| efeslevel2 | 144 | 124 | 20 |
| efeslevel3 | 82 | 62 | 20 |
| EfeVrl4 | 88 | 67 | 21 |
| EfeVrl5 | 133 | 107 | 26 |
| EfeVrl6 | 76 | 55 | 21 |

**Two numbers to internalise: gameplay objects are near-constant at ~20 per room, and
decoration is 55-124 per room — 70-86% of everything in the level.**

### The gameplay budget (~20 per room, remarkably consistent)

| Thing | Per room |
|---|---:|
| ShiftCrystal | ~6 |
| Gold | ~4 |
| Enemies (Melee / Ranged / BatMan / Mimic / Slime / Taret) | ~4-5 |
| Spikes (`spikers`, `newspike`) | ~1-2 |
| Chest | 0-1 |
| ExitDoor + GirisNoktasi + CameraBounds | 1 each (structural) |

Rare specials, roughly one per several rooms: `Blompo`, `Shopkeeper_NPC`, `WreckingBall`,
`BreakableWall_Bookshelf`.

### The decoration budget (~68 per room) — this is what generated rooms are missing entirely

Across the six rooms, ~410 decoration objects break down as:

| Kind | Share | Per room | Examples (all from the Cainos Dungeon Props pack) |
|---|---:|---:|---|
| Small floor clutter | 44% | ~30 | bottles, books, book groups, pots, bags, baskets, kettles, fry pans, candlesticks, candelabra, coin piles, bones, brick debris |
| Large floor furniture | 23% | ~15 | tables, beds, chairs, bookshelves, cabinets, coffins, statues, stoves, pillars, beams, barrels, iron doors |
| Wall decoration | 24% | ~17 | banners, paintings, windows, wall altars, prison bars, wall breaks |
| Ceiling hangings | 6% | ~4 | chandeliers, lamps, hangers |
| Wall dirt texture | 3% | ~2 | `PF Dungeon Wall Dirt - 01..11` |

And the variety is real, not repetition: **68 distinct kinds** of small clutter and **39** of
furniture across those rooms. The most-repeated single prop appears 19 times.

**A generated room ships with ZERO of these.** The importer paints tiles and places gameplay
markers; `CLAUDE.md` records decoration as "a manual pass by design". That is the direct cause
of the "visually messy / bare" verdict — the geometry is being judged naked, against rooms
that are wearing ~68 props.

Until the importer can dress a room, a generated level is **not finished** when it validates —
it is finished when it has been through a decoration pass.

## 3. Why every generated level failed

Running the validator over the eight generated levels:

| Level | Bands missed | Exit reachable by jumping alone? |
|---|---|---|
| GenLevel1 | 5 | yes |
| GenLevel2 | 5 | yes |
| GenLevel3 | 5 | yes |
| GenLevel4 | 8 | yes |
| GenLevel5 | 4 | **NO** |
| GenLevel6 | 5 | yes |
| GenLevel7 | 2 | yes |
| GenLevel8 | 3 | **NO** |

**All eight** miss `open %` and `1-tile ledges %` — the two texture metrics. And **two of
eight cannot be finished without cards**, which violates LEVEL DESIGN LAW 1 in `CLAUDE.md`.
The shape of that failure: the room reads as ~95% reachable from the *best* starting point but
far less from the actual spawn — a one-way level where you drop into a shaft that has nothing
to climb back out on.

---

## 4. The workflow — mandatory

**Never hand the designer a level that has not been through the validator.**

```
dotnet run --project Tools/LevelLab -- check Assets/LevelTexts/YourLevel.txt --map
```

It reports:
1. whether the exit is reachable from the spawn using **jumps and walking only**,
2. any pickup / enemy / shop marker sitting on ground the player can never reach,
3. every style band that is out of range,
4. with `--map`, the room drawn with `,` for reachable floor and `:` for stranded floor.

Exit code 0 means clean. Fix and re-run until it is clean, *then* show the designer.

### Limits of the tool — do not over-trust it

- **Wall jumping is not modelled.** The player has one (`wallJumpForce` 10x15), so the
  validator is conservative: it may call a shaft unclimbable when a wall-jump chain would do
  it. Treat a reachability FAIL as "prove me wrong", not as certain truth.
- The reachability numbers move when the movement model improves. They already did once: an
  early version only simulated maximum-length jumps (button held to the end), which made
  precise short hops onto one-tile ledges look impossible and wrongly failed four generated
  rooms and half the hand-built ones. `Check.LaunchFamily()` now samples a spread of
  button-release timings. **If a result looks wrong, suspect the model before the level.**
- Extracted hand-built rooms contain **only tilemap collision**. Elevators, moving platforms,
  fans and prop platforms are separate GameObjects and are missing from those grids, so the
  hand-built reachability percentages read lower than they play.
- Enemy / pickup **rhythm** (how often something happens along the path) is not measured yet.
  That is the obvious next metric to add.

---

## 5. Checklist when drafting a room

0. A room is **not done when it validates** — it is done when it has ~20 gameplay objects AND
   a decoration pass of roughly 68 props (see 2b). Geometry alone always reads as bare.
1. Start from a **44-56 x 21-30** frame. Never bigger. If the idea needs more, it is two rooms.
2. Carve one connected chamber system, aiming for **~50% open**.
3. Fill the open volume with **single-tile footholds**, irregularly, mostly rise 2-3 apart.
   Aim for more than half of all ledges being one tile wide.
4. Keep the mandatory path completable on **jump + move only** (LAW 1). Every drop needs a
   way back up that does not need a card.
5. Spend the horizontal budget: a jump that matters is **8-10 tiles**, not 5.
6. Put the exit far from the spawn (LAW 7), then run the validator and believe it.

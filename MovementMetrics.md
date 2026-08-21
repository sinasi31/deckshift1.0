# Deckshift — Measured Movement Metrics

All values in TILES (1 tile = 1 world unit). Feet-relative: `dy` is the target
surface height relative to the launch surface, `dx` the furthest that surface can
sit horizontally and still be landed on.

Source constants: gravity -9.81 x scale 1.25, moveSpeed 8, jumpForce 11, fallMultiplier 2.5, lowJump 2, air lerp 0.07/step.

## Running jump (hold jump + direction) — THE DESIGN BASELINE

- Apex: **4.82** tiles
- Highest ledge landable: **4.82** tiles

| target dy | max dx |
|---:|---:|
| +4 | 9.1 |
| +3 | 10.1 |
| +2 | 10.8 |
| +1 | 11.4 |
| 0 | 11.9 |
| -1 | 12.4 |
| -2 | 12.8 |
| -3 | 13.2 |
| -4 | 13.6 |
| -5 | 13.9 |
| -6 | 14.3 |
| -7 | 14.6 |
| -8 | 14.9 |
| -9 | 15.2 |
| -10 | 15.5 |
| -11 | 15.8 |
| -12 | 16.0 |
| -13 | 16.3 |
| -14 | 16.6 |
| -15 | 16.8 |
| -16 | 17.0 |
| -17 | 17.3 |
| -18 | 17.5 |
| -19 | 17.7 |
| -20 | 18.0 |

## Straight-up jump (no direction held)

- Apex: **4.82** tiles
- Highest ledge landable: **0.00** tiles

| target dy | max dx |
|---:|---:|
| +4 | 0.0 |
| +3 | 0.0 |
| +2 | 0.0 |
| +1 | 0.0 |
| 0 | 0.0 |
| -1 | 0.0 |
| -2 | 0.0 |
| -3 | 0.0 |
| -4 | 0.0 |
| -5 | 0.0 |
| -6 | 0.0 |
| -7 | 0.0 |
| -8 | 0.0 |
| -9 | 0.0 |
| -10 | 0.0 |
| -11 | 0.0 |
| -12 | 0.0 |
| -13 | 0.0 |
| -14 | 0.0 |
| -15 | 0.0 |
| -16 | 0.0 |
| -17 | 0.0 |
| -18 | 0.0 |
| -19 | 0.0 |
| -20 | 0.0 |

## Tapped jump (released early)

- Apex: **2.63** tiles
- Highest ledge landable: **2.63** tiles

| target dy | max dx |
|---:|---:|
| +2 | 5.6 |
| +1 | 6.7 |
| 0 | 7.4 |
| -1 | 8.1 |
| -2 | 8.6 |
| -3 | 9.1 |
| -4 | 9.5 |
| -5 | 9.9 |
| -6 | 10.3 |
| -7 | 10.7 |
| -8 | 11.0 |
| -9 | 11.3 |
| -10 | 11.7 |
| -11 | 12.0 |
| -12 | 12.2 |
| -13 | 12.5 |
| -14 | 12.8 |
| -15 | 13.1 |
| -16 | 13.3 |
| -17 | 13.6 |
| -18 | 13.8 |
| -19 | 14.0 |
| -20 | 14.3 |

## Walk off a ledge (no jump)

- Apex: **0.00** tiles

| target dy | max dx |
|---:|---:|
| 0 | 0.0 |
| -1 | 2.2 |
| -2 | 3.1 |
| -3 | 3.8 |
| -4 | 4.4 |
| -5 | 4.9 |
| -6 | 5.3 |
| -7 | 5.8 |
| -8 | 6.2 |
| -9 | 6.5 |
| -10 | 6.9 |
| -11 | 7.2 |
| -12 | 7.6 |
| -13 | 7.9 |
| -14 | 8.2 |
| -15 | 8.4 |
| -16 | 8.7 |
| -17 | 9.0 |
| -18 | 9.2 |
| -19 | 9.5 |
| -20 | 9.7 |

## Running jump IF the dead horizontal impulse worked (bug ref)

- Apex: **4.82** tiles
- Highest ledge landable: **4.82** tiles

| target dy | max dx |
|---:|---:|
| +4 | 12.0 |
| +3 | 12.9 |
| +2 | 13.7 |
| +1 | 14.3 |
| 0 | 14.8 |
| -1 | 15.3 |
| -2 | 15.7 |
| -3 | 16.1 |
| -4 | 16.5 |
| -5 | 16.9 |
| -6 | 17.2 |
| -7 | 17.5 |
| -8 | 17.8 |
| -9 | 18.1 |
| -10 | 18.4 |
| -11 | 18.7 |
| -12 | 19.0 |
| -13 | 19.2 |
| -14 | 19.5 |
| -15 | 19.7 |
| -16 | 20.0 |
| -17 | 20.2 |
| -18 | 20.4 |
| -19 | 20.7 |
| -20 | 20.9 |

## Dash

- Flat dash distance: **4.2** tiles (26 u/s for 0.16s), plus a short momentum tail.

## Body

- Collider: 0.51 wide x 1.68 tall => a 1-tile-wide gap is passable, a 1-tile-tall crawl is NOT.
- Walkable corridors need **2 tiles** of clear height, 3 to feel roomy.

## Design bands (use these, not gut feel)

| Move | Trivial | Standard | Tight | Max possible |
|---|---:|---:|---:|---:|
| Rise (climb onto a ledge) | 1.9 | 3.1 | 4.3 | **4.8** |
| Flat gap (same height) | 4.8 | 7.7 | 10.7 | **11.9** |

Rounded for authoring: **rise 2 / 3 / 4 / 4.8 max**, **gap 5 / 8 / 10 / 11.9 max**.

> The asymmetry is the thing to internalise: vertically the player is *tight*
> (a rise of 4 is already 83% of maximum), horizontally the player is a *cannon*
> (the 5-6 tile gaps the old level laws prescribe are under half of what a jump
> clears). Building tall shafts out of rise-4 ledges and then padding the room
> with 6-tile gaps produces exactly the complaint: climbs that feel fiddly and
> horizontal stretches that feel empty.

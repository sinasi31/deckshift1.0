# Boss Design — The Moss Knight (Act 1, Oxidation District)

**Status:** Paper design v1 (2026-06-30). No code yet. Decisions below are locked unless revisited.

Green is *oxidation* too — copper/bronze oxidize to green verdigris. The Oxidation District's
guardian fused with that corrosion: a patient, acid-bleeding knight.

---

## 1. Toolkit (all assets already owned — no new art)

- **Boss visual/anims:** `PF Knight - Moss` (`Cainos/Pixel Art Monster - Dungeon/`). Anim clips:
  Idle, Walk, **Run**, **Attack**, **Jump Prepare → Air Up → Air Down → Land**, Injured Front/Back, Die.
- **Adds:** `PF Slime - Green` (has its own Attack anim).
- **Arena hazard:** `PF Pixel Water - Acid` + `HazardZone.cs` (damages player on contact; LavaBoots relic already interacts).
- **Card-free damage:** `CrusherTrap.cs` + a `Lever` (already built and in the boss room).
- **Arena:** `LevelSinasi/BossRoom.prefab`.
- **Reuse:** `EnemyHealth` (boss + slimes), enemy healthbar system, `CameraShake`, our procedural VFX (`ShockwaveVFX` recolored green for slam splashes).

## 2. Core combat loop

Boss is **always damageable** (no armor windows). The challenge is *surviving and creating damage*, via two avenues:

- **Cards** — Fireball/Bite/Comet Dive etc. (may run dry → that's why the crusher exists).
- **Crusher Trap** — lever-activated press, **80 dmg** to the boss, but **20 to the player** if caught, ~6s rearm. Bait the boss under it, flip the lever, punish. Positioning = damage.

So the boss must be **baitable**: it approaches/chases the player predictably enough to be led under the press.

## 3. Moveset (3 attacks, all from existing anims)

| Attack | Built from | Telegraph | Effect |
|--------|-----------|-----------|--------|
| **Acid Cleave** | Attack clip | brief wind-up | close-range melee arc; small acid splash at strike point |
| **Leap Slam** | Jump Prepare → Air → Land | crouch + target marker | leaps toward player, slams on landing → green acid shockwave (ShockwaveVFX) you must jump/dash over; spreads an acid pool |
| **Charge** | Run | wind-up + flash | dashes across the arena; dodge with dash/phase/jump |
| **Lob (Slime / Acid Blob)** | Attack clip as a throw | overhead throw gesture | arcs a payload **up onto the platforms** — either a Green Slime (an add) or an acid blob that splashes a short-lived acid patch. Makes the platforms unsafe so they aren't a free refuge. |

## 4. Signature threats (the "depth")

**① Acid & the Shift tax (spatial).** **The boss is IMMUNE to acid** (it's a creature of corrosion) — this is the key that makes floor-acid work: it only threatens the player, so the boss can wade through it freely and it never blocks his melee/charge/crusher-bait game.
- **P1–P2:** acid sits in the **two flank pits** (far left/right of the floor); the **center stays dry** (melee duel + crusher kill-zone). Falling off a side platform into a flank pool punishes sloppiness.
- **P3 "Meltdown":** the acid **rises and creeps inward** across the floor, shrinking the dry ground until the player is pushed **up onto the platforms** — leaving a small dry-ish island around the crusher so the bait stays *possible but risky*. The boss roams the acid untouched.

**② Slimes + the unsafe platforms.** Once acid pushes the player up, the **platforms must not be a free refuge**, so the boss contests them from the ground:
- **Slimes lobbed onto platforms (P2+).** The knight reuses its **Attack anim as a throw**, hurling `Green Slime` adds **up onto the platforms** where they crowd the player and can drop back down. Pure adds — they do **NOT** heal the boss.
- **Acid Lob projectile.** Same throw gesture, different payload: an **arcing acid blob** that splashes a short-lived acid patch on the platform the player is camping. Since the Knight has no ranged anim, the slime-lob and acid-lob **share the one throw gesture** (economical + readable).

Net effect: **floor** (melee, charge, rising acid) and **platforms** (slimes, acid lobs) are both hostile — constant repositioning between two bad options *is* the Shift tax.

## 5. Phases — CUT (decision 2026-07-01)

**No phase system.** This is a first-act boss most players reach under-geared, so it's deliberately kept simple/readable: all attacks are available from the start, no HP-gated escalation, no rising-acid climax. The full moveset (Cleave / Charge / Leap Slam / Lob[acid|slime]) just runs by range + cooldown the whole fight. The 3-phase plan below is kept only as a historical record in case a harder remix is ever wanted.

- ~~**P1 "Duelist"**~~ / ~~**P2 "Bloom"**~~ / ~~**P3 "Verdant Meltdown"**~~ — shelved.

## 6. Starting numbers (all to tune)

- **Boss HP:** **~300** (revised down 2026-07-01 — players reach this boss under-geared; the crusher's 80-dmg hits are then a big assist, with cards/attacks doing the rest). Fair, non-punishing damage. Was ~600 in the original phased plan.
- **Contact/Cleave dmg:** ~15; **Leap Slam:** ~20 + acid; **Charge:** ~18.
- **Acid pool dmg:** reuse HazardZone's current value.
- **Slimes:** spawn 1–2 at a time in P2, up to 3 concurrent in P3; low HP (~30), small contact dmg.
- **Crusher:** unchanged (80 boss / 20 player / 6s rearm).

## 7. Kit synergies (why cards matter)

- **Platform Create** — build safe ground as acid rises (power play).
- **Floor is Lava (Reverse Gravity)** — cling to the ceiling above rising acid.
- **Glass Wail** — stun all slimes at once.
- **Dash / Phase** — i-frame the charge and slams; cross acid.
- **Comet Dive** — burst the boss during its Land recovery.
- **Adrenaline** — slow-mo a tight slam, or heal when low.
- **Shift economy** — every dodge-jump spends Shift; Recall refreshes; running dry risks Stagger death. The arena taxes the resource the run is built on.

## 8. Implementation map (for build sessions)

- `BossController` (new) — state machine: Idle/Pursue → choose attack (Cleave/Slam/Charge) by range + cooldowns; phase transitions on HP thresholds; slime spawning; P3 acid-rise trigger.
- Boss uses `EnemyHealth` for HP/damage/death (so cards + crusher both work for free via `TakeDamage`).
- **Boss acid-immunity:** when the acid hazard is built, it must skip the boss (flag/tag the boss so `HazardZone` ignores it). The boss also passes through the player physically (already done — ignores solid collision so a charge doesn't bulldoze the player).
- Slimes: `EnemyHealth` + a simple chase AI (reuse `MeleeEnemy` pattern). Spawned by the **Lob** attack, which arcs them onto a platform; the acid-blob variant shares the lob and splashes a temp acid patch.
- Acid rise (P3): move/scale the `PF Pixel Water - Acid` surface up, or enable stacked HazardZones. Flank pools in P1–P2; floods inward in P3.
- Boss healthbar: ✔ done. `BossHealthBar.cs` + `Assets/Prefabs/UI/BossHealthBar.prefab` — big screen-anchored bar (top-center) with the boss name + a delayed "damage chunk" drain, built procedurally like EnemyHealthBar. Spawned by the boss in Start (assign to `MossKnightBoss.bossHealthBarPrefab`); polls `EnemyHealth.CurrentHealth`; removes itself when the boss dies. Clear the boss's `EnemyHealth.healthBarPrefab` slot so the small floating bar doesn't also show.
- Telegraphs: procedural markers/flashes (reuse our VFX approach).

## 9. Arena layout (BossRoom.prefab — ~58 wide × 23 tall, 1 tile ≈ 1 unit)

Current state: tilemap geometry + the Crusher (PressHead/Chain/ChainAnchor) + a Lever exist. Acid, platforms still to place.

```
══════════════════ ceiling ══════════════════
                  ▟▟ crusher head ▙▙
                  (over CENTER kill-zone)

   [plat]                              [plat]      high platforms (~+4–6):
            [plat]            [plat]               safe perches in P3 +
                    [plat]                         ranged-poke / Comet spots
 ~~~acid~~~   ===dry center===   ~~~acid~~~        pools FLANK; center dry
 ████████████████ floor ████████████████           so the boss can be baited
        ⊏ LEVER ⊐  (side shelf, away from kill-zone)
```

- **Damage loop:** lure Knight onto dry center → run to the side **Lever** → slam (80) → reposition. Lever placement away from the kill-zone is the skill tax (can't camp both).
- **~5 platforms** at mid-height for P3 survival + ranged angles.
- **Flank acid pools** static in P1–P2; **rise & merge in P3** over ~8s to flood the floor, leaving platforms + a small central island safe.

## 9b. Resolved decisions (this design pass)

- **Slime spawn:** Knight spits them (Attack anim as a lob). ✔
- **Crusher:** fully baitable; boss never avoids it. ✔
- **Acid:** static pools → rising tide flood in P3. ✔
- **Numbers (starting):** HP ~600; Cleave 15 / Slam 20+acid / Charge 18 / contact 10; ~90–120s fight. ✔ (playtest-tune)
- **Reward:** route into `RewardManager` card-pick + bonus scrap; guaranteed relic for Act-1 bosses is a later add. ✔

**Remaining (tune in-engine, not blocking the build):** exact HP/damage, lever vs. kill-zone spacing, platform count/positions, acid-rise rate + max height, slime spit cadence.

## 10. Build order (greybox roadmap)

1. ✔ `BossController` greybox: Moss Knight with EnemyHealth + healthbar, pursues player, **Acid Cleave**. Cards + crusher damage it.
2. ✔ **Charge** (run-across dash, passes through player) and **Leap Slam** (parabolic leap → green acid shockwave + AoE).
3. ✔ **Lob**: `AcidBlobProjectile.cs` (+ prefab) is an arcing carrier. On the floor it bursts into a lingering acid puddle (reuses `HazardZone`, so LavaBoots protects); when the player is camped **above** (platform), it carries a **Slime add** (`SlimeEnemy.prefab` in `Assets/YeniLeveller/`, already wired with SlimeAI + EnemyHealth) and drops it onto the perch instead. Boss prioritizes the slime-lob when the player is above, acid-pokes at floor-range otherwise.
4. Phase transitions (gate Charge/Lob/slime by HP, ramp cadence) + boss death anim + RewardManager hook.
5. **Acid system**: static flank pools → P3 rising tide (the slam + lob puddles foreshadow it).
6. Arena/layout tuning, boss healthbar UI, reward, balance pass.

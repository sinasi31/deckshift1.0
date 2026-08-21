# Deckshift — Audio Inventory & Licence Record

Created 2026-08-20 during the audio reorganisation. **Two jobs: say where every sound came
from (which a Steam release makes a legal question), and say which sounds are still missing.**

---

## 1. Where audio lives now

All game audio is under `Assets/Audio/`. It used to be split across three places, including
**24 audio files inside `Assets/LevelEfeVrl/Sprites/`** — a sprites folder.

```
Assets/Audio/
  Music/            the one music track
  SFX/
    Player/         footsteps, jump, dash, death, hurt, adrenaline
    Cards/          fireball, phase, portal, comet dive, platform, glass wail, bite
    Enemies/        melee, ranged, slime, and the boss's set
    Pickups/        gold, shift crystal, chest, purchase
    World/          crusher, lever
    UI/             card play, menu, level start
    _Unused/        kept but referenced by nothing — see §4
```

⚠️ **Files were moved with `AssetDatabase.MoveAsset`, which preserves the GUID**, so every
reference in every prefab and scene survived untouched. Verified after the move: all 13 of
`Player.prefab`'s assigned clips still resolve, and the 3-clip footstep array is intact.
**If you ever move audio again, do it inside Unity, never in Explorer** — moving the `.wav`
without its `.meta` orphans every reference in the project.

⚠️ **`Assets/ProcSfxPreview/` is NOT game audio.** Those nine `.wav`s are audition renders
written by the ProcSfx bake tool so procedural clips can be listened to outside play mode.
Nothing references them and nothing should. Left where the tool writes them.

---

## 2. Provenance — ⚠️ UNVERIFIED, NEEDS THE DESIGNER

**I inferred these from the original filenames. They are guesses and must be confirmed before
release.** Anything not confirmed CC0 or bought is a risk.

| Original filename | Inferred source | Licence | Status |
|---|---|---|---|
| `freesound_community-breaker-switch-45684` | Freesound | CC0 or CC-BY — **check the sound page** | ❓ |
| `freesound_community-short-success-…-6346` | Freesound | CC0 or CC-BY — **check the sound page** | ❓ |
| `dragon-studio-simple-whoosh-382724` | Pixabay | Pixabay licence (commercial OK) | ❓ |
| `Dark_fantasy_player__#4-1782900520715` | AI generator (prompt+ID naming) | depends on tier | ❓ |
| `Huge_dark_fantasy_bo_#1-1782940241745` | AI generator | depends on tier | ❓ |
| `video_game_huge_armo_#…` ×4 | AI generator | depends on tier | ❓ |
| `Huge_stone_crusher_t_#2-…` | AI generator | depends on tier | ❓ |
| `A_sharp,_quick_air_s_#4-…` | AI generator | depends on tier | ❓ |
| `Heavy_punch_impact_o_#1-…` | AI generator | depends on tier | ❓ |
| `card-sounds-35956`, `zoom-sound-effect-125029` | Pixabay-style | ❓ | ❓ |
| Everything else (`Fireball`, `dash2`, `Phase`, `ZIPLA AMK DECKSHOFT`, …) | unknown / designer-supplied | ❓ | ❓ |

**The AI-generated ones matter most.** Most services allow commercial use on paid tiers and
not on free ones. Confirm which tier produced these and write the answer here.

---

## 3. ⚠️ THE SHOPPING LIST — what is silent right now

Found by scanning every `AudioClip` field on every prefab: **103 empty slots.** Collapsed to
the distinct sounds actually missing, on the prefabs that ship:

| Sound needed | Slot | Affects |
|---|---|---|
| Zombie melee swing | `MeleeEnemyAI.attackSound` on **Shambler** and **Rotbrute** | ~27 enemies, the largest group in the game |
| Spit / retch | `ZombieSpitterAI.spitSound` on **Spitter** | 7 enemies |
| Altar payment accepted | `ShiftAltar.paySound` | every altar |
| Altar refuses (can't afford) | `ShiftAltar.refuseSound` | every altar |
| Wall/bookshelf shattering | `BreakableWall.breakSound` | every breakable |
| Boss death | `MossKnightBoss.deathSound` | the run's finale has no death sound |
| Boss ground pound | `MossKnightBoss.poundSound` | |
| Boss leap | `MossKnightBoss.leapSound` | |
| Glass Parry | `PlayerController.glassParrySound` | a card |
| Freefall Blade | `PlayerController.freefallBladeSound` | a card |

**Eleven sounds.** That is the whole gap, and it is a much smaller job than "the audio needs
work" suggested.

⚠️ `PF Knight - Moss` also lists nine empty slots — that is the **raw Cainos prefab**, not the
encounter (`MossKnightBoss` is). Ignore it.

⚠️ `Assets/Audio/SFX/Cards/Portal.mp3` exists but **the `Portal` component has no AudioClip
field at all**, so there is nowhere to assign it. Needs a code hook before it can be used.

**Already fixed during this pass:** `SlimeAI.attackSound` was empty on every slime *while
`SlimeAttack.wav` sat in the project unreferenced* — the right file had been downloaded and
never wired. Now assigned on the `SlimeEnemy` source prefab, which fills all 14.

---

## 4. `_Unused/` — kept, not deleted

Nothing references these. Kept rather than deleted because several are plausible candidates
for the shopping list above, and deleting someone's sourced audio is not mine to do.

`Boss_Armor_4_unused` · `Dark_fantasy_player_4` · `PlayerDeath_alt` (the player uses
`Player/Death.mp3` instead) · `stone_crusher_alt` · `VampiricBite_v1` (superseded by v2) ·
`whoosh` · `zoom` · `unknown_yogurt`

---

## 5. Sourcing notes

- **Sonniss GDC Game Audio Bundle** — free, annual, professionally recorded, royalty-free, no
  attribution. Tens of GB. Best single source for a solo dev and most people don't know it exists.
- **Humble Bundle audio bundles** — a few times a year, large libraries for ~$25.
- **AI generation** — already in use here. Good for one-off specifics. **Record the tier and terms.**
- **Freesound** — free, mixed licences. Every file needs its licence noted in §2.

⚠️ **What actually separates good game SFX from bad is LAYERING and VARIATION, not fidelity.**
One sample rarely works — a gate slam is a low thud plus a wood creak plus an iron rattle plus a
tail. And 3–4 variants with randomised pitch beats one perfect sample every time. The project
already does variation correctly in exactly one place: the three footstep clips.

⚠️ **`ProcSfx.cs` should be read as a SOUND DESIGN BRIEF, not deleted.** Its families are
separated by physics — magic = harmonic bell partials, metal = inharmonic bar modes, stone =
noise + sub, paper = no pitched component at all, Halt = defined by being *choked* rather than
faded. That is a better articulation of an audio identity than most indie games ever write down.
When auditioning a real sample, the question is already written for you.

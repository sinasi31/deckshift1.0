---
name: deckshift-ui
description: Deckshift's UI design system — how to pick a screen's material and what to invert, linear-colour-space calibration, uGUI layout traps, pause/HUD wiring, and a pre-delivery checklist. Use when building, restyling, reviewing or debugging ANY screen, panel, HUD element, card face, world-space marker or UI VFX in this project.
---

# Deckshift UI

Everything here was paid for with a real mistake in this codebase. Rules without
reasons get "improved" back into bugs — so every rule carries its why. Do not
strip them.

**Read §1 before choosing a look. Read §2–3 before writing pixels. Run §7 before
calling a screen done.**

---

## 1. The house style — highest priority

### The failure mode has a name

The first FlatUI pass delivered the literal brief ("soothing, simple,
understandable, but also cool") as flat slate-blue panels, uniform rounded
corners, neutral greys, one accent. The designer's verdict: **"it screams AI."**

That was correct. It was the house style of every dev dashboard, and it had no
*place* in it. **Simple and generic are not the same thing.** Competent, safe,
professional flat design is the failure state, not the goal.

What fixed it was pointing every choice at the world:

- **Warm charcoal, not slate-blue** — Act 1 is the *Oxidation District*. Rust,
  not brushed steel. This single palette shift did most of the work.
- **Chamfered corners, not rounded** — cut plate reads as a made object; a
  uniform corner radius reads as a web card. Biggest silhouette cue.
- **Directional light** — a lit top lip plus ember glow rising off the *bottom*
  edge (firelight under the bench). Uneven light = physical object in a place.
- **Rivets and faint scuffs** — fasteners, not jewels. Imperfection is what
  kills the generated feel.
- **Rules score across and fade at the ends**, never edge-to-edge like a CSS
  border.

### ⛔ SUPERSEDED 2026-08-20 — SALVAGE REPLACES EVERYTHING IN THIS SUBSECTION

**Do not build a new screen from the table below.** It is kept only so you can
read a screen that has not been migrated yet, and so nobody re-derives the rule
it encodes. That rule was:

> Every screen gets its own material. Screens share the ideology and **never the
> same skin**. Pick a material and invert something.

It did exactly what it says, and what it says is *make the screens look unlike
each other* — nine invented materials (smoked glass, brass, frost…) and a hue
budget that ran out. **Every settings screen built under it was rejected, and
the rule, not the execution, is why.** Designer, 2026-08-20:

> "i want a settings menu, a pause menu, a blompo UI/VFX, the shop, the forge,
> the map, and every other UI asset … to feel like they would have been in the
> cainos packs. i want consistency in the visuals overall, not seperated to
> menus and the actual gameplay, but everything."

**The replacement is `Salvage.cs` — read it before any UI work.** Its thesis, and
the thing both the old rule and the obvious fix get wrong:

⚠️ **CONSISTENCY LIVES IN THE TREATMENT, NOT THE SUBSTRATE.** One substrate
everywhere is *not* the answer — that reads as monotony, and this project has the
receipt: **Vigil** was stone alcoves with real dungeon art and a torch per alcove,
and it was rejected **twice**. But look at the Cainos dungeon pack itself: crates,
pots, bottles, banners, chains, skeletons, candles, fireplaces — wildly different
materials, reading as one world. Not because it is all stone. Because everything
in it obeys the same handful of laws.

So: screens may be made of anything the dungeon contains; they may not disagree
about these five.

| | law |
|---|---|
| **1 · Scale** | `Salvage.Scale` = **2.4107** — 14 world units over a 1080 canvas at 32 PPU. UI art is the exact size the same art is in the game. `Salvage.SpritePPU` enforces it, so no screen has to remember. Deliberately non-integer: the *world* already displays at this scale, so 2× or 3× would make UI pixels visibly a different size from world pixels. |
| **2 · Light** | Warm, from the **upper left**, always. The old system had Iron lit from below, Arcane from above, Halt edges-inward and Bulletin from the left — four screens, four suns. |
| **3 · Colour** | **Sampled from the pack PNGs, never chosen.** `SalvageArtBaker` → `SalvageArt` ramps. Measured dungeon stone is **`#444548` cool-neutral**; the old palette reasoned "warm charcoal, rust not brushed steel" from the *district's name* and was simply wrong against the art. The warmth in this game comes from wood (`#401D13`) and torchlight, never from the walls. |
| **4 · Accent** | **Exactly two in the whole game.** `Salvage.Torch` (lit / present) and `Salvage.Shift` (energised / live — the altar orb's exact cyan, the same colour that seals the gate). **The hue budget stops existing; no screen ever spends a colour again.** `Salvage.Wound` red is a *warning*, not an accent, and is the only permitted third. |
| **5 · Wear** | Used **and repaired** — not pristine, not derelict. The world's repair currency is literally called scrap. |

**Variety then comes from WHAT THE OBJECT IS**, which is a property of the
screen's purpose rather than a colour someone picked: a hung sheet, a notice
board, a workbench, a banner, paper pinned across a grate.

⚠️ **THE ONE FREE RESOURCE NOBODY WAS USING:** `Assets/Cainos/Pixel Art Icon
Pack - RPG` holds **107 icons, 89 of them referenced nowhere in the project** —
Heart, Gear, Scroll, Map, Chest, three Keys, Coins, Rune Stone, Book, Lantern,
gems, ingots. Same artist, same 32 PPU, same palette. Reach for these before
drawing another procedural sigil.

**Migration status:** `PauseScreen` is Salvage (Dust Sheet). Everything else is
still on the table below and reads as the old system until converted.

---

<details>
<summary>The superseded per-screen material table (reference only)</summary>

Screens share the *ideology* — flat procedural plates, restraint, directional
light, a subtle particle drift, one meaningful accent — and **never the same
skin**. The material should say what the place DOES.

| | **Iron** (ScrapForge) | **Arcane** (Blompo) | **Loadout** (relics) | **Halt** (pause) | **Apparatus** (settings) | **Bulletin** (quests) | **Cartograph** (run map) | **Marquee** (character select) |
|---|---|---|---|---|---|---|---|---|
| What it is | a workbench | a blessing granted | what you're carrying | the moment you stopped | the machine's own panel | contracts you promise | a folded map you just opened | the billing before you go on |
| Palette | warm charcoal | cold indigo | near-**colourless** | cold blue-black | smoked glass + arc-cyan | dark wood + **pale paper** | **tan paper** + oxblood | near-black + **the CHARACTER's colour** |
| Light | fire from **below** | descends from **above** | none | from **edges inward** | **emitted by the content** | rakes in from the **left** | even, with aged corners | one key light on the hero |
| Particles | embers **rising** | motes **settling** | none | **suspended**, shivering | none — one scan sweep | none — the content sways | none — paper doesn't move | **speed streaks** tearing past |
| Corner marks | rivets | four-point stars | none | none | calibration crosshairs | brass tacks | **compass rose** | none — raked livery bars |
| Surface | scuffed | pristine | plain sockets | **crazed** | unblemished glass | **perforated** | **stained, foxed, folded** | none — no plate at all |

The Marketplace (`ShopScreenUI`) keeps its own: warm wood, striped awning,
lamplight.

⚠️ **A MATERIAL ALONE IS NOT ENOUGH — the map proved it twice.** It was given flat
slate, then acid-etched copper, both carefully lit, and both were rejected as
still reading like a diagram. What fixed it was making it a **DOCUMENT**: paper
instead of a panel (the sheet IS the window, torn edge, no frame), fold creases,
**dashed** trails instead of solid lines, and the player's own progress drawn
over the print in red pen by a visibly different hand. When a screen depicts a
THING that exists in the world, ask what that thing has been *through*, not just
what it is made of.

</details>

### The inversions are the point

⚠️ **Under Salvage the inversions still matter, but they may no longer spend a
HUE.** Light direction is fixed and the palette is sampled, so what separates two
screens is the OBJECT, its silhouette and its motion. Worked example: the pause
screen is the only **soft** thing in the game — everything else coming is rigid
(planks, a board, an anvil, a banner) — and it is the only screen that lets you
see the world behind it, and the only one that leaves by being **pulled away**
instead of faded. Three separations, no colour spent.

Warm/cold. Below/above. Rising/falling. Worn/pristine. Still/moving.
Inside/outside the fiction.

**When adding a screen, pick a material and invert something. Do not retint
Iron.**

⚠️ **The strongest available inversion is VALUE, not hue.** Every screen except
Bulletin is a dark plate with light text. Bulletin is a dark board with **pale
paper pinned to it** — its `TextBright` is nearly black, and bright and dark have
swapped places. That one structural choice makes it unmistakable while claiming
almost no colour. **Reach for this before reaching for another hue.**

⚠️ **MARQUEE (character select) is the other proof, and the cheapest inversion
yet: THE THEME OWNS NO ACCENT.** Every other screen has one fixed accent that
identifies a PLACE. Marquee is about an IDENTITY, so it takes the *character's*
colour and the whole frame cross-fades when the selection moves — colour there is
the **selection signal**, not the theme signature. It costs nothing from the
budget below, and it makes the choice feel consequential before a word is read.

⚠️ **AND A SCREEN REJECTED TWICE HAS A MEANING PROBLEM, NOT A DRESSING PROBLEM.**
Marquee replaced **Vigil**, a cold hall of stone alcoves where the roster stood
dormant and a travelling lamp woke only the chosen one. Vigil's second pass gave
it real dungeon art, a torch per alcove and a diegetic flame — better dressing,
same rejection. The fault was that its whole vocabulary was **DORMANCY** on the
one screen in the game that is pure hype: the last beat before the run starts.
**Ask what a screen is SAYING before you improve how it looks.**

⚠️ **The hue budget is nearly spent.** Claimed: orange (Iron), violet (Arcane),
no-hue (Loadout), **tan paper + oxblood (map — Cartograph)**, warm wood/amber
(shop), frost blue (Halt), arc-cyan (Apparatus), deep wax red (Bulletin), and
**no fixed hue (Marquee** — but its ROSTER palette spends jade, magenta, gold and
ice). Roughly yellow remains for a *place*. After that, **stop reaching for a
colour and invert a different axis** — light direction, motion vocabulary,
surface treatment and value structure separate these screens at least as much as
hue does. Loadout and Marquee both prove a theme can carry no fixed hue at all.

⚠️ **The map is CARTOGRAPH — paper, not metal.** An earlier verdigris-and-copper
etched-plate version was built and REJECTED: a material is not enough, because a
map reads as a map by being a DOCUMENT (torn deckle edge, fold creases, dashed
routes, progress annotated in red pen over printed brown ink). If a doc still
describes the map as verdigris, it is stale.

### Motion has a vocabulary too, and it must not contradict itself

Blompo's blessing animation was originally a hammer-and-anvil forging: strikes,
sparks, screen shake. Once his screen went arcane that fought everything on the
panel — he grants a charm, he isn't a blacksmith. The rebuild inverted it:

> forging → strikes, impacts, gravity, sparks flying **out**, the window rattling
> binding → orbit, convergence, weightlessness, motes drawn **in**, nothing hit

**When a beat feels weak, check whether it contradicts the sequence's own
vocabulary before reaching for more particles.** The settle beat was called bland
because it *expanded* while everything else converged; pressing a seal inward
fixed it.

⚠️ **AN EFFECT MUST COME FROM WHAT THE THING MEANS, NOT FROM A SHAPE.** Standing
instruction from the designer (2026-08-20), after a chalk ring that circled the
exit door on first sight was cut as "too basic … i don't think it's even a good
idea to have them at all", with: **be more creative with animations and effects.**

The reflex to avoid is the *generic reveal* — a ring that expands, an outline that
pulses, a glow that pops in, a shape drawn around the thing you want noticed. They
are interchangeable, they carry no information, and they would fit any game.

The test that replaced it: **what does this object already MEAN in this game, and
what does the thing that acts on it mean?** Worked example from the same day — the
gate. It was animated as a realistic medieval door (groan, strain, brown dust,
1.6s) while the `ShiftAltar` was firing a glowing **cyan orb of Shift** across the
room that burst on it. Cause and effect were in two different genres. The rebuild
made the gate **sealed with Shift**: a cyan hairline breathing in the join, which
flares and shatters when the orb lands, in the altar's exact colour. Same event,
but now it says something — *this is locked, Shift is what locks it, and Shift is
what just broke it* — and it doubles as gameplay information that sends the player
looking for the switch.

**Ask what is causing the effect and answer in that thing's own language.** If the
effect would work equally well on a chest, a door and a menu button, it is the
generic reveal wearing a costume.

### A permanent overlay must not compete with the game behind it

Loadout is the quietest theme by weight because the relic bar sits over gameplay
forever and the relic art is colourful pixel work. **Do not add a hue to the
relic bar.** Same reason the quest tracker slips are a third of the board's sway,
with no grain, fold or perforation.

### A screen with a person in it needs the person to talk back

The shop's brief was "make the player feel like they are talking to a person who
is trying to sell them stuff." Barks split by EVENT (greet / browse-card /
browse-relic / too-poor / bought / farewell), fired from hover, purchase,
refusal. **Affordability outranks item type** — being told you can't afford it is
more useful than a joke about what it does. Speech **types out**; a line that
snaps in whole reads as a label changing. Body language stays tiny (a portrait
that lurches pulls focus off the prices). **No line repeats back-to-back** —
with pools this small, plain randomness repeats constantly and that's what makes
barks feel canned.

### Sound is part of the screen

Four families, separated by physics, not by taste:

- **Magic** — harmonic (bell partials 1, 2, 3, 4, 5.1)
- **Metal** — inharmonic (bar modes 1, 2.76, 5.40, 8.93)
- **Paper** — no pitched component at all
- **Halt** — defined by ENVELOPE: the only sound that gets **choked**, a damper
  clamping the ring away in 180ms. A sound that fades says "ending"; a sound cut
  short says "held".

Keep them distinct so a blessing and a scrap pickup are never confusable.

---

## 2. Calibration — you cannot compute a colour here, you must measure it

⚠️ **The project renders in LINEAR colour space.** A small alpha of a bright
saturated colour over a dark panel lands **much** higher in sRGB than the
arithmetic says. Measured: a selection plate at alpha **0.065** arc-cyan came out
near **0.36 sRGB** and filled the whole row with a teal slab. It had to drop to
0.03.

⚠️ **World sprites also render through a 0.5-intensity global `Light2D`.** The
scene halves your value before you see it. A 0.42 deep-rock tint measured ~0.21
on screen and the mass read as a pit. **Multiply by the light, then pick.**

Consequences, all load-bearing:

- **Pick every subtle alpha by screenshot, never by reasoning about the value.**
- **The brighter and more saturated the colour, the worse the gap.** Halt's frost
  blue at 0.07 is a restrained plate; Apparatus's cyan at 0.065 was a slab.
  **Do not copy an alpha across themes.**
- **Atmosphere wants roughly half the alpha you first reach for.** Embers at
  0.085/140px were an orange wash owning the bottom third; ~0.05 over 120px is
  firelight. Scuffs at 0.045 read as *rendering glitches*; 0.022 reads as wear.
- **Hairlines need to be brighter than theory says.** The quest board's header
  rule was invisible at `T.Border` and had to move to `EdgeLight`.
- **Rules are 2px, not 1.** Cards render at ~0.8 scale in the hand, so a 1px rule
  is 0.8 device pixels and visibility is subpixel luck. Two identical rules were
  drawn by identical code and only one appeared.
- **A watermark is an OUTLINE, small and faint.** A filled diamond at 56% card
  width and 0.055 alpha measured `#231E12` against a `#0D0D0D` ground — three
  times the ground's value — and read as an olive blob the text sat on. A
  watermark has to survive being ignored.
- **A dark dot cannot mark anything on a dark surface.** The quest board's pin
  holes read because of a pale crescent on the side away from the lamp, drawn
  OVER a wider dark smudge. Rim-only reads as dust; dark-only is invisible.

### ⚠️ A LIGHT GROUND INVERTS EVERY RULE ABOVE

Everything above assumes a dark plate. `Cartograph` is paper, and on paper the
same instincts are wrong in the opposite direction. Measured while building it:

- **A subtle bright mark has almost no headroom.** The fold highlight had to drop
  from 0.20 to **0.055** — at 0.20 the sheet came out with three glowing lines
  ruled across it, reading as laser guides rather than creases. On a dark plate a
  low bright alpha blooms; on a light one it just becomes a drawn line.
- **A subtle DARK mark washes out instead of reading as subtle.** The compass was
  completely invisible as a large 0.115 watermark. It needed to be **smaller and
  roughly three times stronger** to register at all. This is the exact inverse of
  "atmosphere wants half the alpha you reach for".
- **The masking trick flips too, and gets better.** On a dark screen, hiding a
  line behind a label needs a visible plate. On paper the mask can be the GROUND
  ITSELF — a paper-coloured blob is invisible except for what it hides.

The rule underneath all three: **contrast against the ground is what matters, not
the alpha.** Never carry a value across from a dark theme to a light one.

### Generating a Salvage surface — five traps, all paid for on the pause screen

⚠️ **EVERY CAINOS SPRITE HAS A 1PX DARK OUTLINE, AND IT IS A DIFFERENT MATERIAL
FROM THE THING IT OUTLINES.** Sampling a sprite rect swallows it. `Cloth 08` is
grey linen (`#97918A`) inside a solid brown (`#563B25`) border on all four sides,
so the first linen ramp had a brown bottom third and every shadowed part of the
sheet came out blotched. `SalvageArtBaker` insets **2px** (the corners are
stepped, so the outline is two pixels thick diagonally). If a baked ramp ever
looks contaminated, this is why.

⚠️ **A RAMP CARRIES THE MATERIAL; A MULTIPLIER CARRIES THE FORM.** Do not shade
by walking the ramp. Measured, linen spans `#8A8179`..`#A29B91` — about **22
luminance levels out of 255** — so driving folds, key light and drape through
`Sample()` produced a sheet as flat as poured concrete. `Sample()` picks *which*
linen; a `shade` float decides how lit it is.

⚠️ **SMOOTH GRADIENTS READ AS SHEET METAL.** Carrying the form in wide soft
gradients made cloth look like brushed steel. Matte surfaces need a fine crumple
broken into the shade itself so the surface never resolves into a clean gradient,
plus a few **hard** creases — the sharp lines are what the eye reads as fabric.

⚠️ **A RADIAL FALLOFF STRETCHED TO A MENU ROW BECOMES A STREAK, NOT A BAND.**
A 64×64 radial blob at 600×62 rendered as a horizontal smear with a hot core and
read as a lens flare lying across the menu. Any soft shape that will be stretched
to a very different aspect must fall off on each axis **independently**, with a
flat plateau (`SalvageSurfaces.Edge`).

⚠️ **A DASHED RECTANGLE ENCLOSING NOTHING IS MARCHING ANTS.** The stitched patch
was a perfect rect whose interior was 8% brighter than the sheet, so all that was
visible was its dashed border — it read as a UI selection box left on screen.
Wear has to be a visibly **different** piece of material, with a boundary that
wobbles and stitches spaced irregularly.

⚠️ **AND WEAR GOES WHERE THE LAYOUT IS EMPTY AT EVERY CONTENT LENGTH.** The mend
sits bottom-left because the menu bottoms out around v 0.67 and the stat column
around v 0.70. A stain behind a column of numbers reads as a rendering fault.

### Rarity must separate on three channels at once

The first palette was amber / violet / azure / cool-slate and the tiers were
indistinguishable — three sat in the blue-violet quadrant at near-identical
**luminance**, so the only cue was a ~40° hue step. Invisible on a small sigil,
and gone entirely for a colour-blind player. `FlatUI.RarityColor` now separates
on:

1. **Hue** spread right around the wheel (neutral → **green** → violet → amber)
2. **Luminance** strictly ascending (0.42 → 0.56 → 0.66 → 0.82) so a better item
   is literally brighter and the order survives greyscale
3. **Saturation** climbing from near-zero

Plus **shape**: `FlatUI.RaritySigil` progresses bare ring → ring + 4 rays → ring
+ 6 rays + inner ring → the full `ArcaneSigil`. Shape reads faster than hue and
survives greyscale, colour-blindness and a 40px icon.

**Common is deliberately muted** — at a lighter slate it rendered near-white and
made the *weakest* offer the brightest thing on screen.

On the relic bar, rarity is a **solid coloured strip** along the socket bottom,
not a tinted hairline: at 52px over moving gameplay a hairline is not reliably
readable. **Only Epic/Legendary animate** — that's what makes a Legendary catch
your eye in a row of five.

⚠️ **A STATUS COLOUR MUST BE MEASURED AGAINST THE SURFACE IT APPEARS ON, not
chosen for its meaning.** Two digits on the canonical card frame were invisible
for exactly this reason, and both would have shipped: the Shift cost was
`(0.307, 0.304, 0.934)` on a crystal sampling `(0.377, 0.398, 0.920)` — the same
blue — and the *last charge* warning was painted `Color.red` on a medallion that
**is a red ball**. Red means danger, and it is the one colour that cannot say so
there. Sample the artwork, then pick.

⚠️ **Card rarity is the ART's job, not the UI's.** Card art bakes rarity in as
colour (dark grey Common, light grey Uncommon, yellow Rare, purple Epic; no
Legendary cards). **Never invent a second rarity colour system on a card** — two
colour codes that disagree is worse than one. The blessing mark is therefore
**one fixed teal on every blessing**, and blessing hierarchy moved to a channel
the art doesn't use: only Epic/Legendary blessings pulse.

---

## 3. Composition

- **Detail placed exactly on another element disappears.** `ArcaneSeal`'s four
  diamond glyphs sat at the inner ring's radius and merged into it invisibly;
  they now punctuate the outer ring on the diagonals, clear of the twelve ticks.
- **Keep wear out of content columns.** The first scuff pass ran a streak through
  the title. Grain belongs in bands the layout leaves **empty at any content
  count** — one placed between the hint and the LEAVE button instantly read as a
  divider rule nobody asked for.
- ⚠️ **A glow that doesn't reach its container's edge must fade on that axis
  too, or it draws its own border.** The bottom glow reused `VerticalFade` (Y
  falloff only) inset 14px from the sides, and its hard left/right ends produced
  a visible seam down both edges. That's what `BottomGlow()` exists for.
- **An emblem needs STRUCTURE or it reads as a lens flare.** A four-point sparkle
  behind a big soft glow looks cheap. `ArcaneSigil` works because of a containing
  ring, rays of two lengths, and ticks outside the ring — plus a much tighter,
  dimmer glow, since the haze was doing most of the damage.
- **Small icons inside dense text don't work.** A 17px scrap shard beside each
  cost read as a smudge fused to the first digit. The accent colour alone
  carries it.
- **Empty states must collapse.** `LayoutSections` lays out top-down and resizes
  the window to its content, so an empty section shrinks to one explanatory line.
  The fixed-height version had two large voids and looked broken — and that state
  is *common* (early in a run nothing is damaged or exhausted).
- **Show the numbers a decision depends on.** Blompo's card picker listed a bare
  charge count — no Shift cost, no maximum — so you permanently altered a card
  without seeing what it cost or how much life it had.
- **Labels must be narrower than they look like they need.** Map nodes sit about
  one column apart minus jitter; 150px labels collided on any full floor.
- **Text pivots.** A label positioned by offset with a centred pivot places its
  BOX centre, so a 34px-tall label put its first line back over the glyph it was
  labelling. Pivot to top or bottom whenever the offset is meant to clear
  something.
- **The rotation pivot carries the metaphor.** Quest slips rotate about a point
  under their TACK (`SLIP_PIVOT_Y = 0.94`), which is the entire reason the sway
  reads as paper hanging from a pin rather than a card wobbling in space. One
  line.
- **Stillness can be the selection signal.** Hovering a quest slip *stops* its
  sway and lifts it — it becomes the only motionless thing on the board, which is
  clearer than any highlight. (Safe from flip-flop because the slip grows on
  hover, keeping the cursor inside it.)
- **Put the decoration where the content isn't.** The wax seal goes **top-right**
  of a slip: the bottom holds the payout block and progress bar, and a 100px blob
  covers both. The top corner is the only region empty at every content length.

---

## 4. uGUI mechanics — the traps

- ⚠️ **NEVER SCALE UI CONTAINERS — RESIZE THEM.** Changing Scale cascades to
  children and fights Layout Groups, producing wildly wrong sizes. The honest fix
  is always Width/Height, sometimes anchor/pivot. Leave Scale at (1,1,1).
- ⚠️ **`Image.Type` defaults to `Simple`.** A 26px 9-sliced outline left at
  Simple was stretched across a 1040×780 window and rendered as an enormous soft
  octagon hanging outside the panel. Any FlatUI `Panel`/`Outline` at panel scale
  **must** be set to `Image.Type.Sliced` explicitly.
- ⚠️ **Every CanvasScaler is `ScaleWithScreenSize`, ref 1920×1080,
  `matchWidthOrHeight = 1` (HEIGHT). Do not change the match value.** The camera
  is height-anchored (`orthographicSize = 7` ⇒ 14 world units tall at every
  aspect), so matching width makes the UI do the opposite of the camera — at 21:9
  the canvas became 810 logical px tall instead of 1080 and clipped 170px off the
  run map.
- **An element at a screen EDGE must be ANCHORED to that edge.** A centre-anchored
  element at a large offset drifts as canvas width varies. `RecallButton` was
  anchored to centre at x = −859.2 and got cut in half on a 1728-wide canvas.
- **Two fit strategies, not interchangeable.** *Resize* the window only when its
  content reflows (RunMapScreen's chart is anchored to the window corners).
  *Uniform scale, never above 1* when content sits at fixed offsets from centre
  (Blompo, Settings, Shop) — resizing those overlaps their own columns.
- ⚠️ **Two objects that track each other by position must agree on their ANCHORS
  first.** Copying `anchoredPosition` is not enough: a slip anchored TOP and its
  shadow anchored CENTRE shared a position measured from origins 300px apart, and
  every shadow rendered as a free-floating black rectangle mid-screen.
- ⚠️ **`anchoredPosition` places the PIVOT.** With a pivot at the pin in the
  top-left corner, positioning at x = 0 hangs 90% of the strip to the right of
  the anchor. Back the offset out explicitly.
- ⚠️ **A Layout Group can never own rotated or custom-pivoted children.** A
  `VerticalLayoutGroup` relaid every quest slip *and every slip's shadow* as
  separate list items, spacing rows at 176 instead of 94. Build such rows into a
  dedicated child layer with no layout components, and disable any inherited
  group in `Start()`.
- ⚠️ **UI children are NOT clipped, so FX geometry is bounded by the WINDOW, not
  the stage.** A 520px ring scattered runes outside the panel onto the backdrop.
  Check available room in both axes; squash in Y if the stage is off-centre.
- ⚠️ **`preserveAspect` MEANS THE ART IS NOT THE HOST.** Anything stamped at a
  fraction of the host rect lands off the artwork the moment the sprite's aspect
  differs from the box. The card set has two generations — 1024×1536 (0.667) and
  118×200 (0.590) — so the newer art letterboxes to **88.5%** of the host width
  and every medallion number drifted outward off its socket. Measure against the
  DRAWN size (`CardFace.DrawnArtSize`), never the host.
- ⚠️ **TO CENTRE SOMETHING ON A SHAPE, MEASURE THE RENDERED FRAME — not the
  source art.** Scanning the sprite for strongly-coloured pixels finds a
  saturated disc fine, but a shape that tapers to dark, desaturated tips (a
  diamond, a gem, a flame) loses its ends to the colour test, and the "centre"
  comes out shifted. The card's Shift digit ended up 14.5px low on a 900px card
  — 8% of the medallion's height — from exactly this. Render it, capture, and
  measure the SHAPE and the THING YOU ARE CENTRING in the same image; that
  answers "is it on it?" directly instead of inferring it through a chain of
  rect maths.
- ⚠️ **Use a ROW-WIDTH PROFILE, not a bounding box or a centroid.** Circles and
  diamonds both reach their widest row exactly at their vertical centre, so the
  peak row is the answer — and unlike a bbox it is not inflated by a rim, and
  unlike a centroid it is not dragged by interior highlights. On one ball the
  three methods gave x = 814 (bbox), 811.1 (centroid) and 811 (profile mode);
  the profile was the symmetric, correct one. Read a capture back with
  `File.ReadAllBytes` + `Texture2D.LoadImage` to sample pixels.
- ⚠️ **Know when the residual is the GLYPH and stop.** After correcting, ~2px on
  a 900px card remained — that is each digit's own bearing (worst case 0.63px
  vertical at hand size, measured from the font asset's glyph metrics), it
  differs per digit, and "fixing" it over-fits to whichever number you tested.
- ⚠️ **A TWO-DIGIT NUMBER IS ~1.93× THE WIDTH OF ONE DIGIT.** Measured in the
  display face at 100pt: widest digit `0` = 58.2px, `10` = 110.8, `99` = 112.2,
  `100` = 176.0, `∞` = 70.9. Any socket, badge or medallion drawn for one digit
  will be overflowed by two — and it will not be noticed until one value in the
  whole game reaches 10. **Fit the string, don't trust the authored size.**
- ⚠️ **Fit it DETERMINISTICALLY, not with `enableAutoSizing`.** Auto-size settles
  over several frames (§4 above), so a label rebuilt every refresh can render at
  the wrong size on the frame that matters. Scale from a measured glyph-width
  constant instead and it is correct immediately.
- **Textures need `FilterMode.Bilinear`** — Point aliases chamfer edges badly.
- **Get the SDF right.** Rounded box is `inside + outside − radius`; the chamfer
  is that box distance `max`'d with a normalised diagonal half-plane. Naive
  versions pinch the outline at corners.
- **All FlatUI shapes are WHITE and tinted via `Image.color`**, so one cached
  sprite serves every panel.
- ⚠️ **TMP auto-size does not settle within one frame** — you cannot batch-measure
  it. Setting `text` + `ForceMeshUpdate` in a loop gives sticky, wrong numbers
  (one pass reported `textBounds.size.y` of −4294967000). Measure ONE string per
  frame, read `textInfo.lineInfo[]` ascender/descender rather than `textBounds`,
  or drive a real card through `Setup` and read it the following frame.
- **The auto-size CEILING is the design; the floor is a safety net.** Do not
  widen the ceiling to give short strings bigger text — one card rendering at
  twice the size of a wordy one reads as broken, not as emphasis. Shorten the
  copy instead.
- **`UIEmberField` must use `Time.unscaledDeltaTime`** (every screen it belongs
  on pauses the game) **and re-read the parent rect every frame** (window heights
  are dynamic; a bounds snapshot leaves embers outside a collapsed panel).

---

## 5. Wiring — a screen that doesn't integrate is broken

> ### ⚠️ Start here: extend `GameScreen`, and set text through `UIType`
>
> **`GameScreen` (`Assets/Scripts/GameScreen.cs`) already implements most of this section.**
> A new screen calls `AcquireDisplay()` from its Show and `ReleaseDisplay()` from its Hide, and
> gets the pause, game-state, HUD-hide and drawer-lock handover correct by construction — plus
> `FindRootCanvas()`, both aspect-fit modes, the one-frame Escape memory (`UIHeldPauseLastFrame`)
> and an unscaled-time `FadeGroup`. It does **not** own activation, because `PauseScreen`'s root
> must stay active to catch its own Escape; screens keep their own Show/Hide.
>
> **`UIType` decides the face.** `UIType.Apply(text, role)` for the display face — titles,
> headings, buttons, stat labels, numbers. `UIType.ApplyProse(text, role)` for **real sentences
> only**; the display face has essentially no lowercase and renders prose as a wall of capitals.
> Sizes are roles (Hero/Title/Heading/Label/Body/Caption), never magic numbers, and prose is
> auto-compensated ×1.18 for Pixie's smaller cap height — **never hand-tune a size to compensate.**
>
> ⚠️ **Judge any type decision on a screen with SENTENCES in it.** The pause screen looks like the
> obvious test as the densest screen and is nearly useless for it — 30 labels and numbers, almost
> no prose. The quest board decided the current split.
>
> ⚠️ **A thin face on a LIGHT ground needs darker ink than the number suggests** — §2's calibration
> rule applies to type too. The quest slip's body ink had to go 0.189 → 0.141 against paper at 0.75
> when it moved to the prose face, because thinner strokes cover less area at the same value.
>
> Existing screens migrate when already being touched — **do not retrofit them all at once.**
> `QuestBoardScreen` is the worked example for both.

- **Pause through the counter, never `Time.timeScale` directly.**
  `GameManager.instance.RequestPause()` / `ReleasePause()`. Documented exceptions:
  `HitStop`, Adrenaline slow-mo, and hard resets before a scene transition.
- **`GameManager.IsUIPaused` is the single honest "is another screen up?" test.**
  Every modal routes through `RequestPause`, so one property covers all of them
  and cannot fall behind when a screen is added. **Prefer it over a hand-kept
  list of `SomeScreen.IsOpen` flags** — that pattern has rotted twice here.
- ⚠️ **And it needs a ONE-FRAME MEMORY, not just a live read.** Script execution
  order is undefined, so on the frame a shop closes on Escape it may release its
  pause *before* the pause screen's `Update` runs — opening the pause screen
  instantly behind the screen just dismissed. Refuse to open if any UI held the
  pause on the **previous** frame.
- **Hide `GameplayHUD` when a full-screen panel opens**, and call
  `HandUIDrawer.instance.SetLocked(true)` / `(false)`. The drawer's Image has
  `raycastTarget` on to detect hover, so it absorbs clicks in its rect until
  locked.
- **Self-bootstrapping singletons must register with `SceneBootstrap.Register`**,
  and `Create` must be idempotent. `[RuntimeInitializeOnLoadMethod]` runs **once
  per session, not once per scene** — a scene-local self-bootstrapped manager is
  destroyed by the first scene load and never returns.
- **A screen's root GameObject stays ACTIVE; only its `Content` child toggles.**
  `Update` has to run to catch the key that *opens* it, and a deactivated
  GameObject gets no `Update`. When a sub-panel borrows the display, drop the
  CanvasGroup's alpha rather than deactivating anything.
- ⚠️ **Anything a screen creates OUTSIDE its own hierarchy will NOT be hidden with
  it.** The character select's live rigs are world objects 3000 units out, so
  hiding the screen left one camera per character rendering a 420×614 target
  every frame for the rest of the session. Switch them off explicitly in `Hide`.
- ⚠️ **A static `instance` and a scene object can DISAGREE, so adopt-then-verify.**
  Anything that clears statics without destroying scene objects — an editor domain
  reload is the everyday one — leaves the field null while the old screen sits in
  the Canvas still running. Building on top of that stacks screens (measured:
  **three screens, six character plots**). But re-finding it is only half the fix:
  a domain reload also **resets every non-serialized field**, so the adopted
  component comes back with its collections EMPTY while its built children survive.
  That renders the old hierarchy while driving none of it. **Check the screen is
  actually built before trusting it, and rebuild if not.**
- **Restore only what you hid.** Turning every child back on is not the inverse
  of hiding them — some children are supposed to be off. Record the visible set
  when hiding and restore exactly that. (One card flip resurrected three
  deliberately-disabled children and came back wearing a grey overlay reading
  "New Text".)
- **Read the set of children on each change, never cache it in `Awake`.** Other
  systems parent things onto UI afterwards; an `Awake` snapshot left a bonus
  badge rendering mirrored as "+1 TFIHS".
- ⚠️ **A SETTING MUST DO SOMETHING.** Never add a row without a consumer — a
  slider that moves and changes nothing is worse than an absent feature, because
  the player stops trusting the ones that work. Name the consumer in a comment.
- **Scale a global effect at its ONE chokepoint**, not at its 23 call sites, so a
  later addition cannot forget to respect the setting. A zero setting must
  **return before touching state** — a zero-length freeze that still sets
  `timeScale = 0` for a frame is a visible hitch.
- **Re-read values from source on every refresh** rather than mirroring them in
  widget state — rows affect each other, and RESET changes everything at once.
- **Keyboard navigation must skip disabled rows**, and a click anywhere on a
  slider track should jump the value there (grabbing a 3px handle is miserable).
- **One shared hint line** describing the selected row beats N permanent captions
  burying the controls.
- **Destructive entries are two-step**: first activation arms and relabels,
  second commits, and moving away or ~4s of silence disarms.

---

## 6. Verification — this project's #1 bug shape is the silent no-op

**Code that looks correct, runs without error, and does nothing.** The gravity
warning flash was "fixed" twice this way and stayed invisible for months.

- ⚠️ **Setting a shader property that doesn't exist fails SILENTLY.** Dump the
  property list before writing any material effect. On the PLAYER rig tint via
  **`_Alpha`** (the only handle every Cainos rig shader shares — "Alpha Cut"
  exposes no colour property at all); on ENEMIES `_Color` is fine. Prefer
  `HasProperty` + an explicit fallback over `HasProperty` + silently skipping — a
  guarded skip still produces "nothing happens", which *is* the bug.
- ⚠️ **A UI raycast test must let a FRAME PASS after building the UI.**
  `GraphicRaycaster` skips any graphic whose `Graphic.depth == -1`, and `depth`
  is only assigned on a render pass — so a UI built inside one tool call is
  invisible to `EventSystem.RaycastAll` in that same call. Open the screen in one
  call, raycast in the NEXT. This produced five convincing false MISSes.
- ⚠️ **POINTER BEHAVIOUR CANNOT BE VERIFIED BY CALLING `OnPointerEnter`
  YOURSELF.** Invoking the handler never produces the *exit* that breaks things,
  so every test passes while real hovering is unusable. Verify **geometrically**:
  build a `PointerEventData` at the cursor position and run
  `EventSystem.current.RaycastAll` at each animation angle.
- ⚠️ **Deferred `Destroy` survives to end of frame.**
  `GetComponentsInChildren<Button>(true)` still returns the previous chart's
  buttons, whose listeners point at unreachable nodes — a convincing false
  "callback never fired". Filter on `activeInHierarchy`, or check in the NEXT
  tool call.
- **Screenshot recipe:** enter Play mode → `ScreenCapture.CaptureScreenshot(abs
  path)` → `Read` the PNG in a **later** tool call (it's async) → stop Play mode.
  `CaptureScreenshotAsTexture()` returns null from `execute_code`.
- ⚠️ **`Texture2D.ReadPixels` does NOT read the game framebuffer from
  `execute_code` either** — it returned a uniform flat grey for a screen that was
  demonstrably on display. The async file capture is the ONLY trustworthy route.
  To sample exact pixel values, capture to a PNG and load that back as a texture.
- ⚠️ **NEVER WRITE A MEASURED CLAIM INTO A COMMENT YOU HAVE NOT MEASURED.** A
  header in `RunMapScreen` asserted that a layout change cut edge crossings from
  ~9 per act to under 1. Measured afterwards over 300 generated acts: crossings
  were **zero before and zero after** — the change did something else entirely
  (sideways travel per edge, 214px → 86px). A confident false number in a comment
  is worse than no comment, because the next person plans around it.
- ⚠️ **Verify a fix against the FAILURE, not the mechanism.** A soft radial was
  added behind labels to hide lines crossing them, and it looked like a fix — but
  a radial is an ELLIPSE, so it was nearly transparent at exactly the left and
  right ends where the lines actually cross. Test the thing the user complained
  about, not the thing you built.
- ⚠️ **Trust only the real framebuffer.** A manual `camera.Render()` into a
  RenderTexture can sort differently from the URP pipeline and has produced a
  false "this is fixed" image.
- **When a field mysteriously stops working, check for a scene-instance prefab
  override before touching the prefab or the code.** Run **Deckshift → Audit
  Prefab Overrides**. Fix findings with
  `PrefabUtility.RevertPropertyOverride`, never by re-typing the value (that
  creates a new PINNED override).
- **Verify at more than one aspect.** 4:3 (1440×1080), 16:10, 16:9, 21:9
  (2560×1080). Every change should be a no-op at 1920×1080.

---

## 7. Pre-delivery checklist

Run this before saying a screen is done.

**Identity**
- [ ] Does it have a MATERIAL, and does that material say what the place does?
- [ ] What did I invert relative to the nearest existing screen? (name it)
- [ ] Did I reach for a new hue when a value/light/motion inversion would do?
- [ ] Would a stranger call this generic? If unsure, it is.

**Calibration** *(by screenshot, not arithmetic)*
- [ ] Every subtle alpha eyeballed on screen, not computed
- [ ] No alpha copied across themes
- [ ] Hairlines and rules actually visible (2px, brighter than theory)
- [ ] Rarity separates on hue + luminance + saturation + shape
- [ ] Nothing dark placed on a dark surface without a pale rim

**Composition**
- [ ] Empty state collapses rather than leaving voids
- [ ] Every number a decision depends on is on screen
- [ ] Wear/grain only in bands empty at any content count
- [ ] No detail sitting exactly on another element
- [ ] Glows fade on both axes if inset from the edge

**Mechanics**
- [ ] All Scale values (1,1,1); sizing done with Width/Height
- [ ] Every 9-sliced Image explicitly `Image.Type.Sliced`
- [ ] Edge-hugging elements anchored to that edge
- [ ] Paired objects share anchors, not just positions
- [ ] Window fits a narrow aspect (resize *or* uniform scale — the right one)
- [ ] FX geometry inside the window bounds

**Wiring**
- [ ] Pause via `RequestPause`/`ReleasePause`
- [ ] `GameplayHUD` hidden, `HandUIDrawer.SetLocked(true)`
- [ ] Escape checked against `IsUIPaused` with a one-frame memory
- [ ] All motion on `unscaledDeltaTime`
- [ ] Any new setting has a named consumer
- [ ] Root stays active; only `Content` toggles

**Verification**
- [ ] Screenshotted in Play mode from the real framebuffer
- [ ] Pointer behaviour tested geometrically, one frame after building
- [ ] Checked at 4:3, 16:9 and 21:9; no-op at 1920×1080
- [ ] Any material property confirmed to exist on that shader

---

## 8. The screens that exist — reference

Moved here from CLAUDE.md 2026-08-20, where it was costing ~10k tokens on every session
including ones that never touched a pixel. §1–7 above are the *method*; this part is the
*catalogue* — what is already built, what each screen is made of, and the traps each one
paid for. Skim for the screen you are about to touch.


### Canvas Hierarchy

SampleScene's main Canvas contains:
- **`GameplayHUD`** — contains all in-game HUD elements (gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer trigger zone, **RelicHUD**, **QuestTracker**). Toggle with `SetActive(false)` to hide HUD during full-screen UI.
- Various menu panels (ShopUI, TutorialPanel, etc.) as direct children of Canvas. **Procedural screens (`PauseScreen`, `RunMapScreen`, `ScrapForgeScreen`, `BlompoScreen`, `QuestBoardScreen`, `SettingsScreen`…) create themselves under this Canvas at runtime and are NOT in the scene file** — do not go looking for them in the hierarchy at edit time. (`QuestBoardOverlay` and both `SettingsPanel`s were deleted; only `TutorialPanel` remains as a scene-placed panel.)

**When adding new full-screen UI panels**, hide GameplayHUD when they open by adding a `[SerializeField] GameObject gameplayHUD;` reference and toggling SetActive. ShopManager and QuestBoardScreen already follow this pattern.

### `FlatUI.cs` — the new UI direction (2026-08-03)

**The designer has disliked the ornate stone-and-gold chrome "since the beginning."** `FlatUI.cs` is the replacement, prototyped on the Scrap Forge screen.

**It took two passes, and the first one's failure is the useful part.** Pass 1 delivered the literal brief ("soothing, simple, understandable, but also cool") as flat slate-blue panels, uniform rounded corners, neutral greys, one accent. The designer's verdict: **"it screams AI."** That was right — it was the house style of every dev dashboard, and crucially it had no *place* in it. Simple and generic are not the same thing.

**Pass 2 keeps the restraint but points every choice at the world: a sheet of iron on a workbench, lit by the forge.**
- **Warm charcoal, not slate-blue.** Act 1 is the *Oxidation District* — rust, not brushed steel. This single palette shift did most of the work.
- **Chamfered corners, not rounded.** Cut plate reads as a made object; a uniform corner radius reads as a web card. Biggest silhouette cue.
- **Directional light.** A lit top lip plus an ember glow rising off the *bottom* edge (firelight under the bench), instead of a uniform glowing border. Uneven light = physical object in a place.
- **Rivets and faint scuffs.** Small, dark, functional — fasteners, not jewels. Imperfection is what kills the "generated" feel.
- **Rules score across and fade at the ends** rather than running edge to edge like a CSS border.
- **The only two colours on screen are the game's own two resources:** charges in Shift-blue, costs in scrap-orange.

API: `Panel(chamfer)` / `Outline(chamfer, thickness)` (9-sliced chamfered plates), `Rivet()`, `FadedRule()`, `SoftGlow()`, `BottomGlow()`, `VerticalFade()`, `EmberDot()`, `Pixel()`. **All shapes are WHITE and tinted via `Image.color`**, so one cached sprite serves every panel. Shared palette at the bottom of the file.

**`UIEmberField.cs`** — drifting embers for a panel background (`UIEmberField.Attach(rect, count, colour)`); builds and animates its own Image dots, no particle system. Two things that would break it: it must use **`Time.unscaledDeltaTime`** (every screen it belongs on pauses the game, so scaled time freezes the embers solid), and it must **re-read the parent rect every frame** (the forge window's height is dynamic, so a bounds snapshot would leave embers outside a collapsed panel).

Lessons already paid for, don't re-learn them:
- **Get the SDF right.** Rounded box is `inside + outside - radius`; the chamfer is that box distance `max`'d with a normalised diagonal half-plane. Naive versions pinch the outline at corners.
- Textures need `FilterMode.Bilinear` — Point aliases the chamfer edges badly.
- **Hairlines need to be brighter than theory says**, or they don't register on a dark surface.
- **Atmosphere effects want roughly half the alpha you first reach for.** The ember at 0.085/140px was an orange wash owning the bottom third; ~0.05 over 120px is firelight. Scuffs at 0.045 read as *rendering glitches*; 0.022 reads as wear.
- ⚠️ **A glow that doesn't reach its container's edge must fade on that axis too, or it draws its own border.** The bottom glow originally reused `VerticalFade` (which only falls off in Y) inset 14px from the window sides — the sprite's hard left/right ends produced a visible vertical seam down BOTH edges of the panel. That's what `BottomGlow()` exists for: falloff in both axes.
- **Keep wear out of content columns.** The first scuff pass ran a streak straight through the title. They belong in margins that are empty at any content count.
- **Small icons inside dense text don't work.** A 17px scrap shard beside each cost read as a smudge fused to the first digit; the accent colour alone carries it.
- **An emblem needs STRUCTURE, or it reads as a lens flare.** Blompo's offer marks were a plain four-point sparkle behind a big soft glow and looked cheap. `ArcaneSigil` fixed it with a containing ring, rays of two lengths, and ticks outside the ring — plus a much tighter, dimmer glow, since the haze was doing most of the damage.
- **Detail placed exactly on another element disappears.** `ArcaneSeal`'s four diamond glyphs originally sat at the inner ring's radius and merged into it invisibly; they now punctuate the outer ring on the diagonals, clear of the twelve ticks.
- **Show the numbers a decision depends on.** Blompo's card-pick step listed only a bare charge count — no Shift cost, no maximum — so you chose which card to permanently alter without seeing what it cost or how much life it had. Chips now carry labelled SHIFT / CHARGES stats, and `StampChip` refreshes *both* on the bind frame because several blessings visibly change them.
- **Empty states must collapse.** `LayoutSections` lays the screen out top-down and resizes the window to its content, so an empty section shrinks to one explanatory line. The fixed-height version had two large voids and looked broken — and that state is *common*, since early in a run nothing is damaged or exhausted.

### ⚠️ DESIGNER NOTE: the menu screens don't feel like the WORLD yet (2026-08-17)

> **ANSWERED 2026-08-20 by SALVAGE (§1).** This note called it three days before the cause was
> found, and it named the two screens — pause and settings — that then failed twice more before the
> RULE, rather than either screen, turned out to be the problem. **`PauseScreen` is converted; every
> other screen here is still the thing this note is complaining about.** Keep the note until the
> migration is done; it is the standing brief for the rest of it.

**Standing feedback, not a bug.** On accepting Marquee the designer said it is
"a better screen, doesn't really fit the theme of the game and the world", and named **the pause
menu and the settings menu** as feeling the same way — "they are fine for now, I would like to
change them in the future for sure".

Why this is worth recording rather than fixing on the spot: **the three named screens are the three
that depict no place in the game.** Iron is a workbench you stand at, Bulletin is a board in the hub,
Cartograph is a document you carry, the Marketplace is a stall with a person in it — all of them
borrow their material from something the player has actually seen. Halt, Apparatus and Marquee are
abstractions (a moment, a control panel, a billing), so each had to invent its material from nothing,
and inventing is exactly where "competent but generic" creeps back in — the failure the FlatUI pass
was created to kill.

**Do not start a redesign of these unprompted**, and do not treat their themes as settled either.
When it is picked up, the lead to follow is the one the run map already proved: **a material is not
enough; ask what the thing has been THROUGH.** The map stopped reading as a diagram only when it
became a document that had been folded, carried and scribbled on.

### Themes — same ideology, never the same skin (2026-08-03)

**Screens must NOT all look alike.** Designer's rule: share the ideology (flat procedural plates, restraint, directional light, a subtle particle drift, one meaningful accent), but each place gets its own material, and **the material should say what the place DOES**.

`FlatUI.Theme` is the mechanism — a colour set (`Surface`, `Border`, `EdgeLight`, `Accent`, text ramp) picked per screen:

| | **Iron** (`ScrapForgeScreen`) | **Arcane** (`BlompoScreen`) | **Loadout** (`RelicHUD`, `RelicIcon`, `RelicTooltip`) | **Halt** (`PauseScreen`) | **Apparatus** (`SettingsScreen`) | **Bulletin** (`QuestBoardScreen`) |
|---|---|---|---|---|---|---|
| What it is | a workbench you repair cards at | a mythic creature granting a blessing | what you're **carrying** | the **moment** you stopped | the **machine's own control panel** | a board of **contracts** you promise to do |
| Palette | warm charcoal (rust district) | cold indigo | near-**colourless** | cold blue-black (frost) | smoked glass + arc-**cyan** | dark wood + **pale paper** + wax red |
| Light | fire from **below** | descends from **above** | none — it's not a place | from the **edges inward** | **emitted by the content itself** | **rakes in from the LEFT** |
| Particles | embers **rising**, fast | motes **settling**, slow, twinkling | none | **suspended**, shivering in place | none — one **scan sweep** instead | none — **the content itself sways** |
| Corner marks | **rivets** (fasteners) | **four-point stars** (light) | none | none — it has no corners | **calibration crosshairs** | **brass tacks**, on the content not the frame |
| Surface | scuffed and worn | pristine | plain, recessed sockets | **crazed** (hairline fractures) | unblemished glass | **perforated** (old pin holes) |

**The inversions are the point.** Warm/cold, below/above, rising/falling, worn/clean, still/moving, and — with Apparatus — inside/outside the fiction. When adding a screen, pick a material and invert something — **do not just retint Iron**.

⚠️ **Bulletin proves the strongest available inversion is VALUE, not hue.** Every other screen is a dark plate with light text on it; the quest board is a dark board with **pale paper pinned to it**, so its text ramp is INK (`TextBright` is nearly black) and the bright/dark areas have swapped places. That single structural choice makes it unmistakable at a glance while claiming almost no colour. Its wear is also the only wear in the game that says something about the **world** (other people took contracts here) rather than about the object. **Reach for this before reaching for another hue.**

### `ExitMarker` — chalk on the wall, pointing at the way out (2026-08-20)

**`Assets/Scripts/ExitMarker.cs`.** The generated rooms are ~2.5× the area of the hand-made ones and
Level Design Law 7 deliberately puts the exit in a different region from the spawn, so the exit is
usually off screen with nothing saying which way. The designer asked for "an arrow pointing towards it
so the player knows where the level ends".

**The material is CHALK ON STONE** — a wayfinding mark somebody scratched on the wall. It is drawn
with `Parchment`'s pen (the same hand that annotates the run map) but with **the ground inverted**:
Cartograph is dark ink on pale paper, this is pale chalk on dark rock.

⚠️ **That inversion is a VALUE one, not a hue one, and it is why this costs nothing from the nearly
spent hue budget.** It claims no colour at all — which is also the correct weight for something
sitting over gameplay permanently, the same reason the relic bar is near-colourless.

⚠️ **NOT RED, even though the map's annotations are.** On paper oxblood reads as *pen*; over gameplay
red is already **damage** (health bar, damage numbers, hurt flashes), so a red arrow at the screen edge
reads as "you are being hurt". Same lesson as the card's last-charge warning that could not be red
because it sat on a red medallion: **pick a status colour against what it will appear on and mean.**

**It shows an arrow riding the inset frame while the door is off screen, and nothing at all once the
door is in view** — once you can see the archway there is nothing left to say.

⚠️ **IT ALSO SETTLES BACK, AND FAR FURTHER THAN FEELS RIGHT.** After **3s** in a room the arrow eases
from alpha 0.92 down to **0.15** over 1.6s and stays there; a new room restarts the clock. The first
pass at 5s / 0.42 was corrected by the designer to *"way more transparent, almost kind of invisible"*
and *"5 seconds might be a little bit too long"*. **Verified on real dungeon art, not against black**
— at 0.15 it is present if you look for it and completely ignorable if you are not, which is the
target. The nudge motion is doing most of the work of keeping it findable at that value, so do not
remove the motion to "tidy" the settled state.

**This is the general shape for any persistent guidance mark: loud while it is teaching, nearly gone
once it is only confirming.** The instinct is to settle at something still comfortably readable;
the designer's correction says go lower than that.

⚠️ **A CHALK RING THAT CIRCLED THE DOOR ON FIRST SIGHT WAS BUILT AND CUT** (designer, 2026-08-20):
*"too basic … i don't think it's even a good idea to have them at all"*, delivered with a general
instruction to **be more creative with animations and effects**. Do not re-propose it. The lesson
generalises past this one mark: **a shape that simply appears around a thing is the most obvious
effect available**, and reaching for it is a failure of imagination rather than a design. Expanding
rings, pulsing outlines and pop-in glows are all the same reflex. If something must say "look here",
the answer has to come from what the game already MEANS — the way the gate's seal became Shift-cyan
because Shift is literally what opens it — not from a shape drawn around it.

- ⚠️ Bootstraps through **`SceneBootstrap.Register`**, never a bare `RuntimeInitializeOnLoadMethod`.
- Parented under **`GameplayHUD`**, so it inherits the HUD auto-hide for free.
- ⚠️ **The on/off-screen test uses `WorldToViewportPoint`, NOT `screenPoint / Screen.width`.**
  `Screen` reports the Game View *window* rather than the render target for at least a frame after a
  resolution change — measured **2269×334 while the canvas was correctly 1440×1080** — so dividing by
  it can be a whole aspect ratio out.
- **Hysteresis** on that test (0.10 to notice, 0.02 to lose), or it flickers while the player walks
  along the boundary, which is exactly where they spend their time.
- The exit is **re-found whenever the cached one dies with its room** — derived, never pushed at it, so
  a room spawned by any path is picked up with no wiring in `LevelManager`.
- The one piece of motion is a slow nudge **along the pointing direction**, not a pulse: drift in the
  direction of travel says "that way", a pulse only says "look at me".
- Arrow geometry is **fractions of `ArrowLen`**, so resizing keeps the barbs on the head.

Verified by screenshot at 4:3, 16:9 and 21:9. **Still open:** no `GameSettings` toggle — if it should
be switchable off, that is a row in `SettingsScreen` plus a consumer in `LateUpdate`.

### Cartograph — the run map, rebuilt on paper (2026-08-14)

⚠️ **THE MAP TOOK THREE ATTEMPTS AND THE TWO FAILURES ARE THE LESSON.** It was a flat slate panel, then an acid-etched copper plate. Both were given a MATERIAL, both were carefully lit, and the designer rejected both as still reading like a diagram. **A material is not enough.** A map feels like a map because it is a **DOCUMENT** — printed, folded, carried, then scribbled on. Four things carry that, and stripping any one slides it back to a node graph:

1. **PAPER, NOT A PANEL.** The sheet IS the window — no frame, and its edge is a torn deckle rather than a chamfer. Every other screen is a plate you look AT; this is an object you're holding.
2. **FOLDS.** Two vertical creases and one horizontal. The cheapest possible signal the thing was in a pocket a second ago.
3. **DASHED TRAILS.** A solid line between two points is a graph edge; a dashed line is a ROUTE. Biggest change to the read after the paper itself.
4. **PROGRESS IS ANNOTATION.** The chart is printed in brown ink; where you've been and what you may take next is marked over it in **red pen**. Printed trails are mechanically tiled and neat; the player's are individual strokes with per-stroke wobble — **two different hands, deliberately**. Every state is signalled by that fiction with no colour key.

`Parchment.cs` holds the procedural paper, grain, ink strokes, hand-drawn rings and compass rose. It claims **tan/paper + oxblood** and gives back verdigris.

⚠️ **LIGHT GROUND INVERTS THE CALIBRATION RULES.** Everything in §2 of the `deckshift-ui` skill assumes a dark plate. On paper:
- The fold **highlight** had to drop 0.20 → **0.055**. A bright line has almost no headroom above bright paper, so any visible value instantly reads as a drawn rule — the sheet came out with three glowing lines across it.
- The compass was **invisible** as a large 0.115 watermark. A dark mark on a light ground **washes out** rather than reading as subtle. It needed to be *smaller and three times stronger*.
- The player's pen needed a **shorter stroke period and more overlap** than felt right: a trail between adjacent floors is only ~60px after trimming, so at the printed spacing the player's own route came out fainter than the chart it overlays.

⚠️ **NODE LAYOUT IS A FIXED COLUMN LATTICE. DO NOT REINTRODUCE BARYCENTRIC RELAXATION.** It was tried and reverted the same day. Pulling nodes toward their neighbours' mean X does straighten the trails — measured, sideways travel per edge falls 214px → 86px — but it computes a **different spread for every row**, so a floor with three nodes shares no column with a floor that has five. The designer read the result instantly as *"the nodes are off, they are not where they are meant to be"*. A grid you can scan beats trails that lean less. Edge crossings are **zero either way** (measured over 300 acts), so nothing is lost.

**Marquee — the character select (rebuilt 2026-08-17).** The billing before you go on: one character
owns the frame, the rest of the roster stands back in the dark, the name is printed across the top at
poster size, and everything tears past in that character's colour. ⚠️ **Its inversion is that the
theme claims NO ACCENT OF ITS OWN — it takes the character's, and the whole frame cross-fades when
the selection moves.** Every other screen has one fixed accent identifying a PLACE; this screen is
about an IDENTITY, so colour here is the *selection signal* rather than the theme signature. Its
motion vocabulary is the second inversion: everything else in the game is restrained and settled, and
this one never rests. **It replaced *Vigil*, which the designer rejected twice — see Characters for
what Vigil got wrong and for the rebuild's traps. Do not rebuild Vigil.**

⚠️ **The hue budget is nearly spent.** Claimed: orange (Iron), violet (Arcane), no-hue (Loadout), **tan paper + oxblood (map — Cartograph)**, warm wood/amber (shop), frost blue (Halt), arc-cyan (Apparatus), deep wax red (Bulletin), and **no fixed hue at all (Marquee**, which borrows the character's — jade / magenta / gold / ice are spent on the ROSTER, not on the screen). Roughly magenta and yellow remain for a *place*, but note Marquee is already using magenta for a character. **Cartograph and Bulletin are the two light-ground themes** and stay separable because Bulletin is small pale slips on a DARK board — its dominant field is dark, where the map's whole field is paper. When those run out, **stop reaching for a new colour and invert a different axis instead** — light direction, motion vocabulary, surface treatment and now value structure separate these screens at least as much as hue does, and Loadout and Marquee both prove a theme can carry no fixed hue at all.

**The Marketplace (`ShopScreenUI`) keeps its own material** — warm wood, striped canvas awning, lamplight — and was already bespoke rather than old chrome. What it needed wasn't a reskin but a PERSON; see "The keeper talks back" below.

**Loadout inverts a different axis: it's the only theme where the chrome is NOT the subject.** The other two dress a place, so the material carries the character. The relic bar dresses your inventory, sits over gameplay permanently, and the relic art is colourful pixel work — so the sockets are deliberately near-colourless and the theme is the quietest by weight. **Do not add a hue to the relic bar.** A permanent HUD element cannot compete with the game behind it the way a modal panel can.

`UIEmberField.Settings` carries the motion half (`Settings.Embers` / `Settings.Motes`): rise speed (negative = falling), lateral spread, size, life, sway, twinkle.

⚠️ **RARITY MUST SEPARATE ON MORE THAN HUE (reworked 2026-08-09).** The first palette was amber / violet / azure / cool-slate and the designer could not tell the tiers apart at a glance. Three of the four sat in the blue-violet quadrant with near-identical **luminance**, so the only cue was a ~40° hue step — invisible on a small sigil over a dark panel, and gone entirely for a colour-blind player. `FlatUI.RarityColor` now separates on **three channels at once**: hue spread right around the wheel (neutral → **green** → violet → amber; green is the biggest possible jump from both violet and amber), strictly ascending luminance (0.42 → 0.56 → 0.66 → 0.82, so a better blessing is literally brighter and the order survives greyscale), and saturation climbing from near-zero. Common stays the dimmest, for the reason already established below.

⚠️ **Rarity also has its own GLYPH now — `FlatUI.RaritySigil(rarity)`.** Every Blompo offer used one shared sigil, so colour carried the tier alone. Shape is read faster than hue and survives greyscale, colour-blindness and a 40px icon, so the marks progress **bare ring** (Common) → **ring + 4 axial rays** (Rare) → **ring + 6 rays + inner ring** (Epic) → **the full ornate `ArcaneSigil`** (Legendary). Legendary deliberately reuses the established emblem so the lesser tiers read as reduced versions of it rather than unrelated symbols.

Rarity note: the old chrome carried rarity as a gem set in gold. Without that frame **colour has to carry rarity alone**, so `FlatUI.RarityColor` is brighter and more separated than jewel tones, and Blompo tints the sigil, border, name and label together — four quiet signals instead of one loud jewel. **Common is deliberately muted**: at a lighter slate it rendered near-white and made the *weakest* offer the brightest thing on screen.

**On the relic bar, rarity is a coloured STRIP along the bottom of each socket**, plus a muted tint on the socket outline and (Epic/Legendary only) a slow glow pulse. The strip is the load-bearing signal: at 52px over moving gameplay a tinted hairline is not reliably readable, but a solid bar is legible at a glance. The tooltip repeats the rarity in its border and name, confirming what the strip meant. **Only the two rarities worth noticing animate** — that's what makes a Legendary catch your eye in a row of five.

**Blompo's blessing animation (`BlompoForgeFX`) was rebuilt to match (2026-08-03).** It used to be a hammer-and-anvil forging: three blows, sparks, screen shake. Once his screen went arcane, a smithy sequence fought everything else on the panel — he grants a charm, he isn't a blacksmith. The motion vocabulary is inverted the same way the palette was:

> forging → strikes, impacts, gravity, sparks flying **out**, the window rattling
> binding → orbit, convergence, weightlessness, motes drawn **in**, nothing ever hit

Four beats: GATHER (rune ring forms, motes stream in) → DRAW (ring contracts, everything accelerates) → BIND (`onSet` fires here) → SETTLE, where an `ArcaneSeal` contracts **into** the card and snuffs out. Two procedural sounds accompany it (`ProcSfx.ArcaneGather`, `ArcaneBind`).

The settle originally used an *expanding* ring, which the designer called bland — and re-reading it, that was the one beat in the sequence pushing **outward** while everything else converged. Pressing a seal inward finishes the idea the rest of the animation sets up. **When a beat feels weak, check whether it contradicts the sequence's own vocabulary before reaching for more particles.**

⚠️ **UI children are NOT clipped, so FX geometry is bounded by the WINDOW, not the stage.** A first pass used a 520px ring radius and scattered runes across the whole screen, outside the panel, onto the backdrop. The stage sits 60px below centre in a 762-tall window, so there is only ~321px of room downward — anything that must travel further does so on an ellipse squashed in Y (`VERT_SQUASH`). Check this whenever you add UI FX.

**Sound design note:** magic is **harmonic** (bell/chime partials 1,2,3,4,5.1), metal is **inharmonic** (bar modes 1,2.76,5.40,8.93 — see `ProcSfx.ScrapPickup`). The **gate** family (2026-08-19) is the only one that is deliberately TWO materials at once — bar modes layered over stone grit, because a portcullis is iron running in a stone slot. That ratio choice is the whole difference between "charm" and "clank"; keep the two families distinct so a blessing and a scrap pickup are never confusable.

### The keeper talks back (`ShopScreenUI`, 2026-08-03)

The designer's brief for the shop was **"make the player feel like they are talking to a person who is trying to sell them stuff."** The stall already looked like a stall; what was missing was a shopkeeper.

- **He has a face.** `Shopkeeper.ResolvePortrait()` returns an assignable `portrait` sprite, falling back to the shopkeeper's own world sprite — so a placed stall gets a face with zero wiring. ⚠️ The fallback grabs the whole stall prop, not a head; **assign `portrait` for a proper close-up.**
- **He reacts to what you do.** Barks used to be one array with a single line picked at open — decoration that never changed. They're now split by EVENT (`Greetings` / `BrowseCard` / `BrowseRelic` / `BrowseService` / `TooPoor` / `Bought` / `AlreadySold` / `Farewells`) and fired from hover, purchase, refusal and the Leave button. **Affordability outranks item type** on hover: being told you can't afford it is more useful than a joke about what it does, and it's what a real trader would say to you eyeing something out of your league.
- **Speech is typed out a character at a time.** A line that snaps in whole reads as a label changing; typed, it reads as *said*.
- **Small body language** — `Mood.Lean` on browse, `Nod` on a sale, `Slump` on a refusal, plus a constant idle bob. Deliberately tiny: a portrait that lurches around pulls focus off the prices, which is what the player is there to read.
- **No line repeats back-to-back** (`lastLine`), because with pools this small plain randomness repeats constantly and repetition is what makes barks feel canned.
- Lamplit **dust** drifts through the stall (`UIEmberField.Settings.Dust` — warm, very slow, no twinkle). A shop is a place with air in it; stillness is what made the panel feel like a menu.

⚠️ `ShopScreenUI` already had an `Update()`. The keeper's idle bob is a `TickKeeperIdle()` called from it, **not a second `Update`** — and it skips while a mood coroutine owns the transform, or the two fight over `anchoredPosition`.

**Status: converted —** `ScrapForgeScreen`, `ScrapHUD`, `BlompoScreen`, `RelicHUD`, `RelicIcon`, `RelicTooltip`, `RelicManagePanel`, `RelicSwapScreen`, `ResourceBarUI`/`ResourcePanelHUD`, `ShopScreenUI`, `CardUI`, `PauseScreen`, `SettingsScreen`. **The pass is complete.** (`PixelUI` remains and is fine as-is — the shop uses it for grain/frames.)

### The pause screen (`PauseScreen.cs`, rebuilt from scratch 2026-08-09)

Escape. **The old `PauseMenu` + `PauseMenuPanel` + `MenuManager` are DELETED** at the designer's word — do not resurrect a scene-placed pause panel. (For the record, the old one also had a wiring bug nobody had noticed: its `settingsPanel` field pointed at **TutorialPanel**, so the Settings button opened the how-to-play text, and `CloseSettings` then closed a different object than the one it had opened.)

**It is the only screen with NO window plate, and that is structural, not decorative.** Every other screen is a place you walked to inside the world, so each is a panel sitting on top of the game. Pause is not somewhere you go — it is the world being stopped — so it takes the whole frame. That choice separates it from every other screen before a single colour is picked.

#### ⚠️ RE-SKINNED 2026-08-20 — it is now **DUST SHEET**, the first Salvage screen

A sheet of canvas thrown over the frozen world, hung from a rope on wooden pegs. **The Halt theme, the frost edges, the hairline fractures and the suspended mote field are GONE** — do not restore them; they belonged to the superseded per-screen-material system. What survives from the old screen is everything below this box: the structural no-plate choice, the status readout, the two-step destructive entries, and every wiring rule.

Why cloth, and why it is the screen that proves Salvage:

- **It is the only SOFT screen in the game.** Everything else coming is rigid — planks, a notice board, an anvil, a banner, paper on a grate. That one structural difference separates it with **no colour spent**, which is what the old rule kept failing to do.
- **A sheet does not replace the room, it hangs in front of it.** The backdrop is deliberately **not opaque** (alpha 0.78) and the frozen game stays dimly visible past the sheet's edges. No other screen in the game shows you the world behind it.
- **It leaves by being PULLED AWAY, not faded.** A dissolve says the screen was an image laid over the game; whipping the cloth up off the rope says the game was behind it the whole time.

⚠️ **THE PIVOT IS THE ROPE, and the content is parented to the sheet** so the text swings with the cloth it is printed on. Same lesson as the quest board's tack pivot: the rotation pivot carries the metaphor. Content that stayed level while the sheet swung would read as a texture behind a window.

⚠️ **The pause is released on the FIRST frame of the yank**, not at the end — so the game is already running for the ~0.2s the cloth takes to clear. That is the point, not a compromise. Two consequences that are easy to miss: `CloseAnim` must drop `blocksRaycasts` immediately (or the player's first click after resuming is eaten by a sheet halfway off screen), and `Open` must **re-arm** the group (or a screen opened during a yank comes up looking perfect and ignoring every click). `CloseAnim` also checks `isOpen` before deactivating, since Escape-mashing can re-open mid-flight.

⚠️ **The sheet is 1400 wide because 4:3 is 1440.** Canvases match on HEIGHT, so width is what flexes and width is what breaks screens. A sheet wider than 1440 loses its hanging edges at 4:3 — and those edges are most of what makes it read as an object rather than a background.

**Selection is marked in CHALK** — a chevron plus an underline, in `Salvage.Chalk`, the exact colour and stroke sprite the world's exit marker uses. Two earlier attempts *lit* the row instead (an accent plate, then a "rubbed brighter" patch of cloth) and both failed: see §2's radial-falloff trap for why the rubbed version read as a lens flare.

The old screen's signature was a **suspended** mote field saying "time is held". Its replacement says something more physical: **dust knocked off the sheet when it dropped**, falling and thinning to almost nothing over a few seconds, so the screen calms down instead of fidgeting for as long as you leave it open.

⚠️ **The root GameObject stays ACTIVE; only its `Content` child toggles.** `Update` has to run to catch the Escape that *opens* the screen, and a deactivated GameObject gets no `Update`. Same reason `SetContentVisible` (used while a sub-panel borrows the display) drops the CanvasGroup's alpha rather than deactivating anything.

**It doubles as the run's status readout** — floor, HP, Shift, gold, scrap, relics, deck, exhausted, recall cost, and the next Stagger price (red once it exceeds current HP, mirroring the card's own rule). Several of those numbers are visible **nowhere else in the game**, and it is the one screen that can afford to show everything at once. That is what makes it worth its space; four buttons on a dark rectangle is not.

Destructive entries (**ABANDON RUN**, **QUIT**) are two-step: the first activation arms and relabels, the second commits, and moving the selection away or 4s of silence disarms. Sitting one keypress below RESUME, they need it.

**Settings and How To Play still open the OLD panels** (`SettingsPanel` / `TutorialPanel` under the Canvas). `PauseScreen` hides its own furniture, keeps its pause held, and **polls the panel's `activeSelf`** to know when it closed — both panels dismiss via their own buttons, so this needed no rewiring of either. They are next to be rebuilt; this handover exists so the pause rebuild wasn't blocked on theirs.

### Settings — `GameSettings.cs` + `SettingsScreen.cs` (rebuilt 2026-08-09)

**`GameSettings` is THE single source of truth for every player setting**, PlayerPrefs-backed, loaded through `SceneBootstrap` so it re-applies on every scene load. `SettingsMenu.cs` and both `SettingsPanel` objects (SampleScene *and* MainMenu) plus `Assets/LevelSinasi/SettingsPanel.prefab` are **DELETED**.

⚠️ **THE MAIN MENU AND THE PAUSE MENU NOW OPEN THE SAME SCREEN.** There used to be two settings panels, one per scene; with two copies every new setting has to be added twice and they drift apart the first time one is missed. `MainMenuController.OpenSettings()` calls `SettingsScreen.Open()` and its `settingsPanel` field is gone.

⚠️ **A SETTING MUST DO SOMETHING.** Never add a row without a consumer — a slider that moves and changes nothing is worse than an absent feature, because the player then stops trusting the ones that work. Every property in `GameSettings` names its consumer in a comment. The eleven live settings and where they land:

| Setting | Consumer |
|---|---|
| Master / Music / SFX volume | `AudioListener.volume`, `MusicManager.SetVolume`, `SfxManager.SetVolume` |
| **Screen Shake** | `CameraShake.Shake` scales intensity; 0 refuses the call outright |
| **Freeze Frames** | `HitStop.Stop` scales duration; **0 must return BEFORE touching `timeScale`**, or a zero-length freeze still sets it to 0 for a frame — a visible hitch |
| Damage Numbers | `EnemyHealth`'s popup spawn |
| **Enemy Health Bars** | `EnemyHealthBar` — switches its whole Canvas |
| Card Aim Preview | `CardAimIndicator.LateUpdate` |
| Display Mode / VSync / Frame Cap | `Screen.fullScreenMode`, `QualitySettings.vSyncCount`, `Application.targetFrameRate` |

**Screen Shake and Freeze Frames are scaled at the ONE chokepoint each**, not at the 23 and 8 call sites — so a shake added later cannot forget to respect the setting.

`ApplyDisplayMode` is deliberately `#if !UNITY_EDITOR`: `Screen.fullScreenMode` in the editor resizes the actual **editor window**, which is alarming and has to be undone by hand.

Screen details worth keeping: the value is re-read from `GameSettings` on every `RefreshAll` rather than mirrored in widget state (rows affect each other — VSync greys out Frame Cap — and RESET changes all eleven at once); keyboard navigation **skips disabled rows** so it never parks on a control that ignores input; a slider click anywhere on the track jumps the value there (grabbing a 3px handle would be miserable); and there is **one shared hint line** describing the selected row rather than eleven permanent captions burying the controls.

Three procedural sounds in `ProcSfx`: `PauseHalt`, `PauseRelease`, `PauseTick`. They are a **fourth sound family**, defined by their ENVELOPE rather than their spectrum (magic = harmonic bell partials, metal = inharmonic bar modes, stone = noise + sub). The halt is the only sound in the game that gets **choked** — a damper clamps the ring away over 180ms instead of letting it decay. A sound that fades out says "ending"; a sound cut short says "held". Release is its inverse and is allowed to run out naturally.

### `GameScreen.cs` (2026-08-16) — the shared screen contract

**Every new full-screen panel should extend `GameScreen`.** It owns taking over the display and
handing it back: pause, game state, HUD hide, hand-drawer lock, the one-frame Escape memory, both
aspect-fit modes, and finding the right Canvas.

⚠️ **It is deliberately NOT a lifecycle that owns activation.** Screens genuinely differ there —
`PauseScreen`'s root must stay ACTIVE so its `Update` can catch the Escape that *opens* it, while
every other screen deactivates its own GameObject. Screens keep their own Show/Hide and call
`AcquireDisplay()` / `ReleaseDisplay()` from inside it. A base class that insisted on `SetActive`
would have to be fought by the one screen that matters most.

**Why it exists:** those twelve lines were copy-pasted *identically* into ten screens — same fields,
same order, same guards. Three details are load-bearing and none are obvious, so every new screen was
one forgotten line from a bug that only appears when screens open on top of each other:

- **`hudWasActive` is RECORDED, not assumed.** A screen opened over another (a chest's relic swap,
  Blompo from the forge) must restore the HUD to what it *was*, not switch it on.
- **The drawer lock is GATED on `hudWasActive`**, or an inner screen unlocks a drawer the outer
  screen still needs locked.
- **`prevState` is SAVED, not hardcoded to `Playing`.**

`AcquireDisplay`/`ReleaseDisplay` are **idempotent** (a double Show can't stack two pauses), and
`OnDestroy` releases — a screen destroyed while open would otherwise leave the game paused forever
with no HUD.

⚠️ **The two aspect-fit modes are NOT interchangeable.** `FitWindowToCanvas` RESIZES and is only safe
when content is anchored to the window's corners with insets (the run map's chart). `FitScaleFor`
returns a uniform scale and is required when content sits at fixed offsets from the window centre
(Blompo, Settings, the shop) — *resizing* those overlaps their own columns.

`UIHeldPauseLastFrame` + `TickUIPauseMemory()` generalise the guard that used to live only in
`PauseScreen`: any screen opening on a keypress must check it, because script execution order is
undefined and the screen closing this frame may release its pause before yours runs.

⚠️ **Do NOT retrofit every screen at once.** New screens use it immediately; existing ones migrate
when already being touched. **`QuestBoardScreen` is the migrated worked example.** Verified after
migrating: open/close balanced, no pause leak over three cycles, double-open and double-close safe,
and — the load-bearing case — opening the board *on top of* the relic panel and closing it leaves the
HUD hidden and the outer screen's pause intact.

### The UI sound family (2026-08-16) — defined by PITCH MOTION, not by material

Six sounds in `ProcSfx`: `UIMove` / `UIConfirm` / `UICancel` / `UIRefuse` / `UIOpen` / `UIClose`,
fired from `GameScreen`.

⚠️ **This family's rule is a different KIND of rule from the others.** Every existing family is
defined by a MATERIAL — magic by harmonic bell partials, metal by inharmonic bar modes, stone by
noise + sub, paper by having no pitched component at all, the pause pair by a choked envelope. **A UI
sound has no material**: it is not a thing in the world, it is the interface. So this family is
defined by **pitch motion** instead — all six share one voice (literally the same `WoodTap` call) and
differ only in which way the pitch moves. That is what makes them a learnable *language*, and it is
the right mechanism because these are the only sounds in the game that must be told apart **from each
other**; a world sound only has to be distinguishable from other materials.

⚠️ **The voice is soft struck WOOD**, deliberately claiming the one material the world does not use
(metal = forge, glass/bell = magic, stone = rooms, paper = quest board). A clean synth blip would
sound like it came from a different game.

⚠️ **CANCEL AND REFUSE ARE NOT THE SAME SOUND.** Cancel is the player choosing to back out —
consonant, no fault implied. Refuse is the *game* saying no, and is the only dissonant sound in the
family. Refuse must also not read as damage: it means "you can't", not "you got hurt".

⚠️ **Open and Close are the same three notes inverted**, not two unrelated sounds — the pairing is
what says the thing that arrived is the thing that left.

⚠️ **A screen with a BESPOKE open sound must override `PlaysDefaultOpenCloseSound` to false**, or it
plays two. The quest board's paper rustle and the pause screen's halt/release are signatures and beat
the generic pair; the generic pair exists for screens that would otherwise be silent.

**Audition without Play mode:** **Deckshift → Bake UI SFX Previews** writes the six to
`Assets/ProcSfxPreview/*.wav` (throwaway folder). Verified by measurement rather than ear —
Move is the quietest (peak 0.038 vs 0.072–0.088), Confirm's 930Hz overtakes its 620Hz while Cancel's
585Hz overtakes its 780Hz, Open/Close are 520→780 and 780→520, and Refuse carries both 600Hz and
636Hz simultaneously (a beating minor second, not a melody).

### Typography — `UIType.cs` (2026-08-16), two stated faces and a size scale

**`UIType` is the single source of truth for what the UI is set in.** Before it, the font was decided
by **census**: `FlatUI.UIFont()` counted every `TMP_Text` in the scene and returned the most common
one. That is an emergent property, not a decision — a full `FindObjectsByType` per call, capable of
answering differently in MainMenu than in SampleScene, and **any screen that forgot to call it fell
silently out of the system** (the character select shipped in Liberation Sans exactly that way).

**The split (designer-chosen 2026-08-16, from screenshots):**

| | face | takes |
|---|---|---|
| **Display** | `CCBattleScarred` | titles, headings, menu items, buttons, stat labels, numbers — the game's voice |
| **Prose** | `Pixie` | running sentences ONLY — contract text, card rules, barks, trait blurbs |

⚠️ **CCBattleScarred has essentially no lowercase**, so used for prose it renders every sentence as
capitals. That is fine for labels and terrible for paragraphs.

⚠️ **JUDGE A TYPE DECISION ON A SCREEN WITH SENTENCES IN IT.** The obvious candidate — the pause
screen, "the densest screen" — turned out to barely discriminate: it is 30 labels and numbers with
almost no prose, and the all-display version looks *best* there. The quest board decided it, because
a contract reads `CLEAR 4 ROOMS IN A ROW WITHOUT PLAYING STAGGER.` in the display face and
`Clear 4 rooms in a row without playing Stagger.` in the prose face — and the Bulletin theme's whole
conceit is that a person wrote these and pinned them up.

⚠️ **Prose size is auto-compensated (`ProseScale` 1.18).** Pixie has a smaller cap height, so at equal
nominal pt it renders visibly smaller. `UIType.SizeFor(role, prose: true)` applies it — **never
hand-tune a size to compensate**, or the two faces drift apart again.

⚠️ **A THIN FACE ON A LIGHT GROUND NEEDS DARKER INK THAN THE NUMBER SUGGESTS.** The quest slip's body
colour was chosen for the heavy display face; Pixie's strokes cover far less area, so the same value
read washed out. Measured on the slip: paper luminance 0.75, title ink 0.109, body ink **0.189** —
nearly twice as light as the title while carrying the sentence you actually have to read. Pulled to
0.141. Same family as the linear-colour-space rule: **measure the pixels, don't compute them.**

⚠️ **`Assets/Resources/UIType.asset` carries the two font references** because neither font lives in a
`Resources/` folder (Pixie ships inside the Cainos pack, CCBattleScarred sits in `LevelEfeVrl/
Sprites/`), and moving either risks a pack reimport undoing it. Rebuilt by **Deckshift → Rebuild UI
Type**, same pattern as `RelicCatalogue`. If the asset goes missing, `UIType` **falls back to the old
census** rather than breaking — degrading to today's look, not to Liberation Sans.

**Migration policy: do NOT retrofit every screen at once.** `FlatUI.UIFont()` now delegates to
`UIType.Display()` and returns exactly what the census was already resolving to, so wiring it in was
a visual no-op across all 18 screens that call it. New screens use `UIType` immediately; existing ones
move their prose to `UIType.Prose()` when they are already being touched. **`QuestBoardScreen` is the
one migrated so far** — use it as the worked example.

### Cards: rarity colour is the ART's job, not the UI's (designer 2026-08-06)

**Card rarity is telegraphed in the card ARTWORK, in colour: dark grey Common, light grey Uncommon, yellow Rare, purple Epic. There are no Legendary cards.** The incoming art has this baked in, so **UI code must not invent a second rarity colour system on a card** — two colour codes on one object that disagree is worse than one.

This is a live constraint, not a preference: `CardUI`'s blessing mark originally tinted itself by the *blessing's* rarity via `FlatUI.RarityColor`. That's a different axis, but no player would read it as one — and it contradicted the art (calling Rare azure where the art calls it yellow). It is now **one fixed teal on every blessing**, chosen to sit outside the grey/grey/yellow/purple palette and pushed green of Shift-blue so it can't read as a cost either. Blessing hierarchy moved to a channel the art doesn't use: **only Epic/Legendary blessings pulse.**

### Hovering a card TURNS IT OVER (`CardBack.cs` + `CardHoverFlip.cs`, 2026-08-09)

⚠️ **`CardHoverFlip` IS THE ONE IMPLEMENTATION — never hand-roll a second.** The hand (`CardUI`), the Scrap Forge's repair chips and Blompo's card picker all attach it. It exists as a component because the mechanism has three non-obvious requirements that have each already caused a shipped bug: the back must be **pre-rotated 180°** or it renders mirrored; the hit target must **counter-rotate** or the card flaps edge-on under the cursor; and showing the front must **restore only what it hid** or deliberately-inactive children get resurrected. `CardBack.BindStandard(card)` fills the normal SHIFT/CHARGES footer (CardUI overrides it only for Stagger), so every screen reads identically.

⚠️ **`CardHoverFlip.Attach` takes a GEOMETRY SOURCE.** Pass `cardArtImage` for a hand card — its root is rewritten to 200×100 by the hand's layout group. Pass nothing for the forge and Blompo, whose chips are built at the size the player sees; `CardBack.MatchTo` detects "the source is my parent" and fills it.


The old hover was a flat grey rectangle laid over the card, the art faded to 12% behind it, and a **140×50** text box that every real description overflowed. It read as a tooltip that had landed on the card. The designer asked for something nicer and suggested the card's back — so the card now flips.

**The flip is free.** Screen Space Overlay is an orthographic projection, so rotating the card on Y renders as a horizontal squash to nothing and back out — exactly what turning a card over looks like, for one `Quaternion` per frame. No perspective canvas, no shader. Unscaled time throughout (the reward screen and deck view both hold `timeScale` at 0). Faces swap at the halfway point, where the card is edge-on.

⚠️ **THE HOVER IS DETECTED BY A COUNTER-ROTATING CHILD, NOT BY THE CARD.** A rotating card's raycast rect narrows exactly as its picture does, so halfway through the flip the pointer is inside nothing, `OnPointerExit` fires, the card turns back, widens, `OnPointerEnter` fires — and it sits edge-on flapping, a vertical sliver under the cursor. That is the shipped-broken state the designer reported. `CardUI.hoverTarget` is an invisible, full-card-size child that cancels the root's turn each frame, holding a stable axis-aligned rect for the whole animation; pointer events bubble from it to `CardUI` and clicks bubble to the root's `Button`. Its centre sits on the root's rotation axis, which is what makes the cancellation exact. It must never be disabled by `SetFrontVisible`.

⚠️ **POINTER BEHAVIOUR CANNOT BE VERIFIED BY CALLING `OnPointerEnter` YOURSELF.** That is precisely how this shipped: invoking the handler directly never produces the *exit* that breaks it, so every test passed while real hovering was unusable. Verify geometrically instead — build a `PointerEventData` at the cursor's would-be position and run `EventSystem.current.RaycastAll` at each flip angle. Measured, with the counter-rotation the card is HIT at all of 0/22.5/…/180°; without it, MISS from 90° onward (past 90° the graphics are also back-face-culled, so it can't be re-entered at all).

⚠️ **`CardBack` is pre-rotated 180° on Y.** Past 90° every child of the rotating root renders MIRRORED, text included; the pre-rotation cancels it exactly when the back is the face you're looking at.

⚠️ **The back is SIZED OFF `cardArtImage`, never off the card root.** The root carries a `LayoutElement` inside the hand's layout group, which overwrites its RectTransform at runtime — it measures **200×100**, not the 200×300 the prefab shows. Stretching to it produced a back a third of the card's height over its bottom edge. Same reason the blessing mark anchors to the art. The back still *parents* to the root (that's what turns it) and copies the art's geometry instead.

⚠️ **The front is "every child that isn't the back", re-read on each face change — never a list cached in `Awake`.** Other systems parent things onto a card afterwards: `RewardScreenFX` hangs a "+1 SHIFT" bonus badge on the offered card, and an `Awake` snapshot left it showing straight through the flip, rendered mirrored as "+1 TFIHS".

⚠️ **AND THE FLIP ONLY RE-SHOWS WHAT IT ITSELF HID.** Turning every child back on is *not* the inverse of hiding them — three of `CardUI_Template`'s children are supposed to be off. `Image` and `ShiftCostContainer` ship disabled in the prefab (dead leftovers) and `Awake` retires the legacy `Hover_Panel`, so one flip out and back **resurrected all three** and the card came back wearing a grey overlay reading "New Text". `SetFrontVisible` records what was actually visible when it hid the face and restores exactly that set.

**It is NOT dressed in FlatUI's iron.** FlatUI is the material for *screens*, and each screen picks a material and inverts something. A card back is not a screen — it belongs to the deck, whose fronts are painted gold-on-near-black. Re-skinning it as a charcoal workbench plate would make the card visibly stop being a card halfway through its own flip. It borrows FlatUI's *shapes* (they're just white sprites) and none of its palette.

**Sizing the description text (2026-08-09).** The card is only ~160×240 screen px, which is small for a paragraph, so two things carry it:
- **A flip zoom of 1.2×, plus a 40px LIFT.** Hand cards sit 200px apart and are 160px wide, so 1.2× (=192px) is the largest zoom that cannot overlap a neighbour — measured, not guessed. It composes with the selection bump rather than replacing it, and it lerps on **unscaled** time because `Time.deltaTime` is 0 on every screen that pauses, which would have left reward cards flipping without ever growing.
  ⚠️ **The zoom is useless without the lift.** The hand sits on the screen's bottom edge and a card's art already overhangs it — measured, the card bottom is **6px below the screen at rest**, and because the zoom grows about the root's pivot that becomes **22px** at 1.2×. The Shift/charges row lives in the lowest 12% of the back, so it was exactly the part that got cut off. 40px clears it with ~18px to spare. If the zoom or the drawer's resting position ever changes, re-measure the back's bottom corner against y=0.
- ⚠️ **The body's auto-size CEILING is the design; the floor is a safety net.** The first pass capped it at 13pt while the box was two-thirds empty — nothing was constraining the text except the cap. 14 of 15 cards now settle at exactly **21pt**, so they look identical; the longest steps to 19. **Do not widen the ceiling to give short cards bigger text** — a one-line card rendering at twice the size of a wordy one reads as broken, not as emphasis. If a card can't reach 21, shorten the card's text (Glass Parry was trimmed from 173 to 142 chars for exactly this reason). The floor is 12 for the rare **blessed** long card, which carries two extra lines on an already-full face; at a 16pt floor three blessings clipped straight out of the box.

⚠️ **TMP auto-size does not settle within one frame, so you cannot batch-measure it.** Setting `text` and calling `ForceMeshUpdate` in a loop gives sticky, wrong numbers — one pass reported 36pt with `textBounds.size.y` of −4294967000, another reported 12pt for a string that really renders at 21. Measure ONE string per frame, read line metrics (`textInfo.lineInfo[0].ascender − lineInfo[last].descender`) rather than `textBounds`, or better, drive a real card through `Setup` and read it on the following frame.

Two calibration lessons, both re-learned the hard way:
- ⚠️ **Rules are 2px, not 1.** Cards render at ~0.8 scale in the hand, so a 1px rule is 0.8 device pixels and visibility comes down to subpixel luck. Both rules were drawn by identical code and only the lower one appeared — measured at `#9D8541`, full strength, while the upper sampled as bare card.
- ⚠️ **The watermark is an OUTLINE, small and faint.** First pass was a filled diamond at 56% of the card width and 0.055 alpha: it measured `#231E12` against a `#0D0D0D` ground — three times the ground's value — and read as an olive blob the body text sat on. A watermark has to survive being ignored.

**The deck view does not flip.** `DeckViewUI` sets `ui.enabled = false` after `Setup` (so `CardUI.Update` stops resetting the scale it needs for grid cells), which also stops `Update` and pointer events. That's unchanged behaviour — the deck view never had hover text — but it's the obvious follow-up if browsing your deck should read descriptions too.

### Card descriptions are written for a player, not a spec (2026-08-09)

Rewritten across all 15 cards: lead with the verb, state the number, one or two short sentences, no restating the cost (the card face and the back's footer both show it). Two were also **factually wrong** and are fixed — Comet Dive said 20 damage when `cometDamage` is **40** (radius 5), and Dash never mentioned that it grants **i-frames**, which is most of why you'd play it.

### Every screen draws the REAL card face — `CardFace.cs` (2026-08-09)

**All three non-hand screens use it: the Scrap Forge, Blompo and the shop.** The shop was the worst of them — it drew the card into a **68×68 square icon**, letterboxing a 2:3 card down to ~45×68, then re-printed the name and `N SHIFT  N CHARGES` underneath. Its card tiles are now card-shaped (`TILE_H / CardFace.ASPECT` wide; relics and services keep the square shelf tile) and carry no grain plate or PixelUI frame — the card has its own painted border, and a second frame around it read as a card inside a card. ⚠️ The price plaque is lifted clear of the card's **name plate** (bottom ~10% of the face); at the normal height it covered the title, leaving a row of unlabelled pictures.

The Scrap Forge and Blompo used to build their own card chips: a FlatUI plate with `cardArt` squeezed into a **square** box, which letterboxed the whole 2:3 painted face down small enough that its own medallions were unreadable — which is exactly why those screens re-printed the name, SHIFT and CHARGES as separate text underneath. Both now draw the card at its true aspect via `CardFace.Build`, and the duplicate readouts are gone. There was never a design reason for the divergence; it was history.

⚠️ **THE MEDALLION NUMBERS ARE NOT PAINTED INTO THE ART.** The art carries the empty gold circles; the digits are TMP fields in `CardUI_Template`. Any screen that draws `cardData.cardArt` on its own gets a card with two **blank sockets**. `CardFace` stamps them at fractions measured off the prefab (`Cost_Text` at (69.5, 121.4), `Uses_Text` at (-65.4, 126.4) in a 200×300 rect), so there is one place to fix if the art is re-cut.

⚠️ **THE SET CURRENTLY HAS TWO ART STYLES AND THEY FIGHT.** The older cards socket their medallions in dark gold circles; **Dead Weight, Freefall Blade, Glass Parry and Shuriken** are newer art with a red ball and a **blue crystal**, and no painted name. Consequences already hit: a blue Shift digit on a blue crystal was *invisible* at ~10px (fixed with a 4-way dark **keyline**, not a one-sided drop shadow — that leaves most of the glyph edge unlit), and those three had `nameIsPaintedIntoArt` wrongly set true in the bulk pass, so they rendered a blank name plate **in the hand as well**.

### ⚠️ THE FREEFALL BLADE FRAME IS THE CANONICAL CARD FRAME (designer, 2026-08-17)

**All new card art uses it**, and the layout is fixed for every card: the **red ball** (charges, left), the **blue crystal** (Shift cost, right), an **empty name plate** (drawn in code — see below), and on cards that deal damage a **heart container**. `CardFace.Gem` is therefore the layout to tune and trust; **`CardFace.Classic` is legacy** and exists only until the 14 old cards are re-cut. When they are, delete `Classic` and the chooser with it.

⚠️ **The heart container is NOT BUILT — it is the designer's stated plan, not a request.** Do not invent a different mechanism for "does this card deal damage" in the meantime. When it lands, the machinery already exists: `CardUI.RefreshCardFace` draws a number into a heart for **Stagger** today (the `HEART_*` fraction constants), which is the same problem in the same place.

⚠️ ~~Both styles put cost right / charges left, so the positions do hold.~~ **THAT WAS WRONG AND IS NOW FIXED (2026-08-17).** The two generations put their medallions **0.045 of a card width apart**, and on the gem cards the charge number sat off the LEFT EDGE of the red ball entirely:

| | charges | cost | sprite |
|---|---|---|---|
| **gem (canonical)** | **(0.2188, 0.8796)** | **(0.8330, 0.8767)** | `freefallblade_0`, 118×200, aspect **0.590** |
| classic (legacy) | (0.173, 0.921) | (0.848, 0.905) | `fireball_0`, 1024×1536, aspect **0.667** |

⚠️ **MEASURE ON THE RENDERED CARD, NOT ON THE SPRITE.** The first pass scanned the sprite for strongly-coloured pixels. That is fine for the ball (a saturated disc, and its value was confirmed correct to 0.3px) and **wrong for the crystal**: a diamond tapers to dark, desaturated tips, the strict colour test missed the top one, and the resulting "centre" put the Shift digit **14.5px low on a 900px card — about 8% of the crystal's height.** That is what the designer reported as the numbers not being centred. Rendering the real card and measuring the medallion **and** the digit ink in the SAME image removes every mapping assumption at once — it answers "is the number on the medallion?" directly instead of inferring it.

⚠️ **The tool that settled it: a ROW-WIDTH PROFILE, not a bounding box or a centroid.** A circle and a diamond both reach their widest row exactly at their vertical centre, so the peak row *is* the answer, and it is immune to the rim, highlights and facets that drag a centroid or inflate a bbox. On the ball the three methods disagreed — bbox said x=814, centroid said 811.1, and the mode of the row midpoints said 811 with a symmetric profile, which is the truth. Capture with `ScreenCapture.CaptureScreenshot`, then read the PNG back with `File.ReadAllBytes` + `Texture2D.LoadImage` to sample it.

⚠️ **A residual of ~2px on a 900px card is the GLYPH, not the placement, and must not be "corrected".** Both medallions now land within 1.5px, and the leftover is each digit's own bearing — measured from the font asset, the worst digit is 0.63px vertical and 0.19px horizontal at hand size. It also differs per digit, so tuning it against one number over-fits.

⚠️ **`CardFace` is the single source for every screen INCLUDING the hand.** `CardUI.Setup` calls `CardFace.PlaceMedallion` on its two prefab labels rather than trusting their authored positions, so the hand and the forge cannot drift apart.

⚠️ **The generation is told apart by SPRITE ASPECT, and that is a STOPGAP.** Aspect is at least a property of the art FILE rather than of gameplay data, but it is still a proxy — a new card cut at 0.667 would silently take the legacy positions.

### ⚠️ Two digits were invisible against the medallions they sat on (2026-08-17)

Both are the same mistake and both were on the **canonical** frame, so both would have shipped:

- **Shift cost: blue on a blue crystal.** Sampled off the sprite, the crystal averages **(0.377, 0.398, 0.920)** and the digit was **(0.307, 0.304, 0.934)** — the same colour. The designer reported it as blending into the background, and it did, exactly. Now pale ice **(0.90, 0.95, 1.00)**: keeps the Shift-blue identity the whole game uses for this resource, at luminance ~0.93 against the crystal's ~0.43.
- **Last-charge warning: red on a red ball.** `currentUses == 1` painted the number `Color.red`, and the canonical charge medallion *is* a red ball — the warning was invisible exactly when it mattered most. Now amber **(1.00, 0.82, 0.25)**, which still reads inside the legacy frame's dark gold ring.

**The general rule: a status colour must be measured against the SURFACE it appears on, not chosen for its meaning.** Red means danger, and it is the one colour that cannot say so on a red ball.

⚠️ **The cost also grew 30 → 34.** Colour was the reported fault, but the cost was also the *smaller* of the two numbers while sitting on the *larger* medallion — a single digit filled ~40% of the crystal's width. Recolouring fixed legibility without fixing presence, and the cost is the number a player checks most often ("can I afford this?").

⚠️ **The four-copy keyline is GONE — there is now ONE shared outlined material.** Every number used to be drawn five times (the digit plus four offset black copies) because the digits sit on saturated artwork. It worked, but **the hand never had it** — its labels are prefab objects, not built by `CardFace` — so the same card read differently in your hand than in the forge. `CardFace.ApplyNumberOutline` puts a real SDF outline on both. It must be `fontSharedMaterial` and it must be ONE cached material: writing `outlineWidth` on a `TMP_Text` auto-instances a material **per label**, which breaks batching and leaks one material per card drawn. Same look everywhere, one draw call, 8 fewer TMP objects per card.

⚠️ **Max number width is PER MEDALLION** (`USES_MAX_W` 0.165 / `COST_MAX_W` 0.130), because the sockets are not the same size: the ball is 0.357 of the card wide, the crystal only 0.219 — and the crystal is a diamond, so a number near its full width runs into the tapering facets. One shared budget either wasted the ball or overran the gem.

⚠️ **`preserveAspect` MEANS THE ART IS NOT THE HOST — measure against the DRAWN art.** The gem sprite is 0.590 where the card box is 0.667, so it letterboxes to **88.5%** of the host width with bars either side, and every number stamped at a fraction of the HOST lands outside the artwork it belongs to. This is half of the misplacement above, and it is the same letterbox `CardUI` already maps Stagger's heart and name plate through. `CardFace.DrawnArtSize` is the shared helper.

⚠️ **A TWO-DIGIT CHARGE COUNT IS 1.93× THE WIDTH OF ONE DIGIT, AND THE SOCKETS ARE DRAWN FOR ONE.** Measured in the display face at 100pt: widest digit `0` = 58.2px, `10` = 110.8, `99` = 112.2, `100` = 176.0, `∞` = 70.9. **Shuriken is the only card in the set with `maxUses` 10**, so it was the one that showed it — the designer reported the charges as "weird and bad over 10", and on both styles the number simply spilled off its medallion. `CardFace.FitNumberSize` shrinks any number to `NUMBER_MAX_W` (0.135 of the drawn card width, which is inside both sockets — verified on both styles at 1/9/10/99).

⚠️ **Deterministic scaling, NOT `enableAutoSizing`.** TMP auto-size settles over several frames and is documented in this file as unreliable to measure; these labels are rebuilt on every hand refresh. The scale comes from a measured glyph-width constant instead, so it is correct on the frame it is set.

⚠️ **Verified as a NO-OP on the 14 classic cards**: a single-digit classic card still resolves to font size 38.0 at exactly (-65.40, 126.40) — byte-identical to the authored prefab values.

⚠️ **`CardUI_Template` is NOT scale-corrupted** — measured 2026-08-09: root scale (1,1,1), 200×300, `ShiftCostContainer` scale (1,1,1) and inactive. The "non-uniform (0.119, 0.568, 0.92)" warning below refers to an older prefab and does not apply to the card the game actually uses.

### Card name plates are drawn in CODE from now on (designer 2026-08-09)

**New card art must ship with an EMPTY name plate.** `CardUI` types `cardName` into it. This decouples a card's name from its texture — renaming a card stops being a repaint — and it is why **`CardData.nameIsPaintedIntoArt` defaults to `false`**.

⚠️ **The 14 pre-2026-08-09 cards have their titles painted in and all set that flag**, so nothing about them changed. **Clear it on each card as its art is replaced.** Getting it backwards is visible instantly: set-when-blank leaves an empty plate, clear-when-painted prints the name on top of itself.

Plate geometry (`PLATE_CY/W/H` in `CardUI`) was measured on Stagger's art but is expressed as fractions of the **sprite rect**, and the legacy 1024×1536 cards put their plate within ~1% of the same place — so one set of constants serves both layouts, letterboxing included. Re-measure only if new art moves the plate. Colour is the set's title gold, matching the painted plates.

### `CardUI` — the blessing mark (2026-08-06)

`CardUI`'s only procedural chrome was the blessing badge; the card frame, cost medallions, rarity tag and name plate are all **painted into the card art sprite**, so "converting CardUI" meant converting that one mark. Three things were wrong with it and all three are fixed:

- **It wasn't on the card.** It was anchored to the card ROOT, whose RectTransform is a **200×100 stub** — while `cardArtImage` is the real 200×300 card face. The mark floated off the card's right *edge* at mid-height. It is now parented to `cardArtImage.rectTransform`, the only honest geometry on the prefab.
- ⚠️ **`cardArtImage`'s sprite is the WHOLE CARD FACE** (1024×1536), not the inner picture — frame, medallions and name plate included. Measured on the real cards, the inner picture occupies roughly **10%–80% of the card height**, so a naive small inset lands the mark inside the painted *name plate*, on top of the card's title. `MARK_INSET_Y = 62` (of 300) clears it.
- **The look** was a jewel in an ornate gold ring — the chrome this pass exists to remove, and its bright gold setting drowned the gem so different rarities read identically. It is now Blompo's own `ArcaneSigil` glowing over a soft dark halo: light *inscribed on* the card rather than an object stuck to it, tying the mark to the screen that grants it. The dark halo (not a frame) is what keeps it legible over busy artwork.

The mark deliberately does **not** say which of the seven blessings it is — the hover text names it. Seven legible glyphs at ~24 screen px is a bespoke-art job, not a procedural one.

Verified in play mode across the hand and the deck view: blessed cards mark, unblessed cards build no mark at all.

### Resolution independence (2026-08-09) — the game is NOT 1920x1080-only

The project was believed to be locked to 1920x1080. It never was: `defaultIsNativeResolution` is **on**, so a build launches at the player's native resolution and the 1920x1080 in ProjectSettings is only the *windowed fallback* size. What was actually wrong was three settings.

⚠️ **EVERY CanvasScaler IS `ScaleWithScreenSize`, ref 1920x1080, `matchWidthOrHeight = 1` (HEIGHT). Do not change the match value.**

**Match HEIGHT because the camera is height-anchored.** `Camera.main.orthographicSize = 7` means the view is exactly **14 world units tall at every aspect**, with the width flexing (`halfW = orthoSize * aspect`, which `CameraFollow` already computes correctly). Matching *width* made the UI do the opposite of the camera: on a 21:9 display the canvas became only **810** logical px tall instead of 1080, which clipped 170px off the run map (980 tall) and 130px off settings (940 tall). With match=height the canvas is always 1080 tall and its width is `1080 * aspect` — 1440 at 4:3, 1728 at 16:10, 1920 at 16:9, 2560 at 21:9.

Also fixed: **MainMenu and GameOverScene were `ConstantPixelSize`**, so their UI did not scale at all (measured: at 2560x1440 the menu rendered at its authored pixel size and looked shrunken). And `resizableWindow` was off, so windowed mode could not be dragged.

⚠️ **AN ACTIVE BUILD PROFILE OVERRIDES ProjectSettings, AND `PlayerSettings.*` WRITES TO THE PROFILE.** Setting `PlayerSettings.resizableWindow = true` changed `Assets/Settings/Build Profiles/New Windows Profile.asset` and left `ProjectSettings/ProjectSettings.asset` still reading `resizableWindow: 0`. Both are now set. **When changing a player setting, check which of the two actually moved** — a value set only in the profile silently reverts for any build made without it, and reading `PlayerSettings.x` back gives you the profile's value, so it looks correct either way.

⚠️ **A UI element that sits at a screen EDGE must be anchored to that edge.** With the canvas width now varying, a centre-anchored element at a large offset drifts. Audited every `GameplayHUD` child; exactly one was wrong — **`RecallButton`** was anchored to centre `(0.5, 0.5)` at `x = -859.2`, which put it 5px from the left edge on a 1728-wide canvas and cut it in half. Re-anchored to `(0, 0.5)` at `x = 100.8`, which is the identical position at 1920 and correct everywhere else. Everything else was already edge-anchored.

**Oversized windows now fit themselves**, and the two mechanisms are NOT interchangeable:
- **`RunMapScreen.FitWindowToCanvas()` RESIZES** the window (1560x980, the widest in the game). Its chart lives in `area`, anchored to the window corners with insets, so it genuinely reflows into a smaller box.
- **`BlompoScreen` (1600) and `SettingsScreen` (1240) SCALE** uniformly instead, via `FitScale()`, never above 1. Their content sits at fixed offsets from the window centre, so *resizing* them would overlap their own columns — shrinking is only safe as a uniform scale. `ShopScreenUI` already did this.

**Verified by screenshot at 4:3 (1440x1080), 16:10 (1920x1200), 16:9 (1920x1080, 2560x1440) and 21:9 (2560x1080):** zero visible graphics off-screen in SampleScene or GameOverScene at any of them, and every change is a **no-op at 1920x1080** (canvas scaleFactor 1, RecallButton on the same pixels, both windows at full size).

**Camera vs room width — measured, no action needed up to 21:9.** A room's CameraBounds zone must be at least `14 * aspect` wide or the clamp inverts. Need is 24.9 at 16:9 and **33.2 at 21:9**; the pool's rooms are 42.8–68 wide, so all clear it. Only `EfeVrl5`'s narrow sub-zone (25.9) inverts at 21:9, and its art still covers the overshoot, so nothing is visibly wrong. **32:9 super-ultrawide needs 49.8 and most rooms fail it** — that's the line to draw.

Known cosmetic nit at 21:9: `GameOverScene`'s background art doesn't reach the edges, leaving plain grey strips. Scene art, not UI.

### Never Scale UI Containers — Resize Them

When a UI element needs to be bigger or smaller, **change Width and Height in the RectTransform, not Scale.** Scaling a UI container cascades to children and fights with Layout Groups, producing wildly incorrect sizes (twice during the last session we hit this — once with the RelicHUD container scaled 5.44× on Y, once nearly happened with the QuestBoardOverlay). The honest fix is always Width/Height, sometimes anchor/pivot. Leave Scale at (1, 1, 1) on UI elements.

### HandUIDrawer

The hand drawer at the bottom of the screen auto-slides up on hover and down when idle.

**Critical raycast behavior:** The drawer's `Image` component has `raycastTarget` enabled to detect hover (`IPointerEnterHandler`). This means it absorbs clicks in its rect. The `SetLocked(bool)` method:

- Sets `isLocked` (stops slide animation)
- Sets `isHovered = false`
- **Toggles `raycastTarget` on the Image component** so the drawer stops absorbing clicks when locked.

**When opening any full-screen UI panel, call `HandUIDrawer.instance.SetLocked(true)`** and `SetLocked(false)` when closing. ShopManager, QuestBoardScreen and DeckViewUI already do this.

---


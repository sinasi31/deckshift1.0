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

### Every screen gets its own material

Screens share the *ideology* — flat procedural plates, restraint, directional
light, a subtle particle drift, one meaningful accent — and **never the same
skin**. The material should say what the place DOES.

| | **Iron** (ScrapForge) | **Arcane** (Blompo) | **Loadout** (relics) | **Halt** (pause) | **Apparatus** (settings) | **Bulletin** (quests) | **Cartograph** (run map) | **Vigil** (character select) |
|---|---|---|---|---|---|---|---|---|
| What it is | a workbench | a blessing granted | what you're carrying | the moment you stopped | the machine's own panel | contracts you promise | a folded map you just opened | who you are about to be |
| Palette | warm charcoal | cold indigo | near-**colourless** | cold blue-black | smoked glass + arc-cyan | dark wood + **pale paper** | **tan paper** + oxblood | near-black + one **warm lamp** |
| Light | fire from **below** | descends from **above** | none | from **edges inward** | **emitted by the content** | rakes in from the **left** | even, with aged corners | **travels the row** (the inversion) |
| Particles | embers **rising** | motes **settling** | none | **suspended**, shivering | none — one scan sweep | none — the content sways | none — paper doesn't move | none — the chosen rig **breathes** |
| Corner marks | rivets | four-point stars | none | none | calibration crosshairs | brass tacks | **compass rose** | none — alcove arches |
| Surface | scuffed | pristine | plain sockets | **crazed** | unblemished glass | **perforated** | **stained, foxed, folded** | cold **stone** |

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

### The inversions are the point

Warm/cold. Below/above. Rising/falling. Worn/pristine. Still/moving.
Inside/outside the fiction.

**When adding a screen, pick a material and invert something. Do not retint
Iron.**

⚠️ **The strongest available inversion is VALUE, not hue.** Every screen except
Bulletin is a dark plate with light text. Bulletin is a dark board with **pale
paper pinned to it** — its `TextBright` is nearly black, and bright and dark have
swapped places. That one structural choice makes it unmistakable while claiming
almost no colour. **Reach for this before reaching for another hue.**

⚠️ **VIGIL (character select) is the other proof, and the cheapest inversion yet:
LIGHT DIRECTION.** A dark hall of alcoves where the roster stands dormant and one
travelling lamp wakes only the chosen one. Every other screen is an evenly lit
surface marking its selection with COLOUR; this one is dark and marks it by
*lighting* it — so it claims **no hue at all**, and gets motion as a free second
signal (unselected rigs are `animator.speed = 0`).

⚠️ **The hue budget is nearly spent.** Claimed: orange (Iron), violet (Arcane),
no-hue (Loadout), **tan paper + oxblood (map — Cartograph)**, warm wood/amber
(shop), frost blue (Halt), arc-cyan (Apparatus), deep wax red (Bulletin), and
no-hue again (**Vigil**). Roughly magenta and yellow remain. After that, **stop
reaching for a colour and invert a different axis** — light direction, motion
vocabulary, surface treatment and value structure separate these screens at least
as much as hue does. Loadout and Vigil both prove a theme can carry no hue at all.

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

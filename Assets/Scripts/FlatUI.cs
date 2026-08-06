using UnityEngine;

// Deckshift's procedural UI kit — the alternative to RelicUISprites' ornate stone-and-gold chrome.
//
// THE BRIEF (designer, 2026-08-03): the first pass at this was "soothing, simple" but read as
// generic — flat slate-blue panels, uniform rounded corners, one accent. That is the house style of
// every dev dashboard on earth and has no PLACE in it. It needed to feel like Deckshift's world
// without going back to ornament.
//
// THE ANSWER: a sheet of iron on a workbench, lit by the forge. Same restraint, but every choice
// now points at Act 1's Oxidation District:
//   · WARM charcoal, not slate-blue. The district is rust and corrosion, not brushed steel. This
//     single palette shift does most of the work.
//   · CHAMFERED corners, not rounded. Cut plate reads as a made object; a uniform radius reads as
//     a web card. This is the biggest silhouette cue.
//   · Light on the TOP LIP only, plus an EMBER GLOW rising from the bottom edge — the forge fire
//     below the bench. Uneven, directional light reads as a physical thing in a place; a uniform
//     glowing border reads as a UI widget.
//   · RIVETS and faint SCUFFS. Small, dark, functional — fasteners, not jewels. Imperfection is
//     what kills the "generated" feel, and it costs almost nothing.
//   · Rules score across and FADE AT THE ENDS instead of running edge to edge like a CSS border.
//
// Everything here is a WHITE shape meant to be tinted by Image.color, so one cached sprite serves
// every panel in any colour, and all of it is 9-sliced where it needs to stretch.
public static class FlatUI
{
    private static Sprite plateLarge, plateSmall, outlineLarge, outlineSmall;
    private static Sprite softGlow, verticalFade, bottomGlow, fadedRule, rivet, pixel;
    private static Sprite emberDot, fourPointStar, arcaneSigil, arcaneSeal;

    // Solid chamfered plate. chamfer 10 = windows, 5 = cards and buttons.
    public static Sprite Panel(int chamfer = 10)
    {
        if (chamfer <= 6)
        {
            if (plateSmall == null) plateSmall = BuildPlate(5, 0);
            return plateSmall;
        }
        if (plateLarge == null) plateLarge = BuildPlate(10, 0);
        return plateLarge;
    }

    // Chamfered OUTLINE only, stacked over a Panel so the edge tints independently of the fill.
    public static Sprite Outline(int chamfer = 10, int thickness = 2)
    {
        if (chamfer <= 6)
        {
            if (outlineSmall == null) outlineSmall = BuildPlate(5, Mathf.Max(1, thickness));
            return outlineSmall;
        }
        if (outlineLarge == null) outlineLarge = BuildPlate(10, Mathf.Max(1, thickness));
        return outlineLarge;
    }

    // A small domed fastener: dark body, lit along its top edge so it reads as raised metal.
    public static Sprite Rivet()
    {
        if (rivet != null) return rivet;

        const int S = 16;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                // Light from above-left: brighten the upper edge, darken the lower.
                float lit = Mathf.Clamp01(0.5f + (dy * 0.55f - dx * 0.25f));
                float v = Mathf.Lerp(0.35f, 1f, lit);
                float a = Mathf.Clamp01((1f - d) * 6f);   // soft 1px rim
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
        tex.Apply();
        rivet = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return rivet;
    }

    // A scored rule: solid in the middle, fading out at both ends. Reads as a scratch across metal
    // rather than a border drawn to the edges of a box.
    public static Sprite FadedRule()
    {
        if (fadedRule != null) return fadedRule;

        const int W = 128;
        Texture2D tex = NewTex(W, 1);
        for (int x = 0; x < W; x++)
        {
            float t = (float)x / (W - 1);
            // Fade over the outer ~22% at each end.
            float a = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.22f);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        fadedRule = Sprite.Create(tex, new Rect(0, 0, W, 1), new Vector2(0.5f, 0.5f), 100f);
        return fadedRule;
    }

    // Radial falloff, used behind a selected card so the highlight bleeds softly outward.
    public static Sprite SoftGlow()
    {
        if (softGlow != null) return softGlow;

        const int S = 64;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        softGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return softGlow;
    }

    // Forge-fire wash: strongest along the BOTTOM edge, fading upward AND toward the left/right
    // ends.
    //
    // The horizontal falloff is the whole point. The first version reused VerticalFade, which only
    // falls off in Y — inset from the panel sides, it hard-cut vertically and left a visible seam
    // down both edges of the window. Any glow that doesn't reach its container's edge must fade on
    // that axis too, or it draws its own border.
    public static Sprite BottomGlow()
    {
        if (bottomGlow != null) return bottomGlow;

        const int W = 96, H = 48;
        Texture2D tex = NewTex(W, H);
        for (int y = 0; y < H; y++)
        {
            float v = 1f - (float)y / (H - 1);       // 1 at the bottom row
            float vy = v * v;                         // squared: hugs the edge, no hard stop
            for (int x = 0; x < W; x++)
            {
                float t = (float)x / (W - 1);
                float hx = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.28f);
                hx = hx * hx * (3f - 2f * hx);        // smoothstep the ends
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, vy * hx));
            }
        }
        tex.Apply();
        bottomGlow = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return bottomGlow;
    }

    // Opaque at the top fading to nothing downward. Used for the top-lip sheen.
    public static Sprite VerticalFade()
    {
        if (verticalFade != null) return verticalFade;

        const int H = 64;
        Texture2D tex = NewTex(1, H);
        for (int y = 0; y < H; y++)
        {
            float t = (float)y / (H - 1);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
        }
        tex.Apply();
        verticalFade = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);
        return verticalFade;
    }

    // A four-point sparkle. Blompo's answer to the Forge's rivet: where the workbench is held
    // together by fasteners, the mythic panel is pinned by points of light.
    public static Sprite FourPointStar()
    {
        if (fourPointStar != null) return fourPointStar;

        const int S = 32;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);

                // Two crossed tapered spikes, plus a bright core where they meet.
                float horiz = Mathf.Clamp01(1f - ax) * Mathf.Clamp01(1f - ay / 0.16f);
                float vert = Mathf.Clamp01(1f - ay) * Mathf.Clamp01(1f - ax / 0.16f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - d * 3.4f);

                float a = Mathf.Clamp01(Mathf.Max(horiz, vert) * Mathf.Max(horiz, vert) + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        fourPointStar = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return fourPointStar;
    }

    // A single floating ember: a hot core with a soft halo. Deliberately not a hard dot — at the
    // 2-4px these are drawn at, a hard-edged square reads as a dead pixel rather than a spark.
    public static Sprite EmberDot()
    {
        if (emberDot != null) return emberDot;

        const int S = 16;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float halo = Mathf.Clamp01(1f - d);
                float core = Mathf.Clamp01(1f - d * 2.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(halo * halo * 0.55f + core)));
            }
        tex.Apply();
        emberDot = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return emberDot;
    }

    // An arcane MARK — the emblem above each of Blompo's offers.
    //
    // Replaced a plain four-point sparkle, which read as a lens flare rather than as a symbol
    // somebody drew. The difference is structure: a containing ring, rays of two different
    // lengths, and tick marks outside the ring all say "this was inscribed". A soft blob with
    // four points says "bloom effect".
    public static Sprite ArcaneSigil()
    {
        if (arcaneSigil != null) return arcaneSigil;

        const int S = 192;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }
                float ang = Mathf.Atan2(dy, dx);

                // Containing ring.
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.70f) / 0.030f);

                // Eight rays: long ones on the axes, shorter on the diagonals.
                float axis = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 26f) * Falloff(d, 0.66f);
                float diag = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 44f) * Falloff(d, 0.42f);

                // Four ticks just outside the ring, on the diagonals.
                float tickBand = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.075f);
                float tick = tickBand * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 90f);

                float core = Mathf.Pow(Mathf.Clamp01(1f - d * 7f), 2f);

                float a = Mathf.Clamp01(ring * 0.85f + axis + diag * 0.8f + tick * 0.9f + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        arcaneSigil = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return arcaneSigil;
    }

    // A binding circle: two concentric rings, radial ticks between them, and four diamond glyphs
    // on the cardinals. Used for the moment a blessing is pressed into a card.
    public static Sprite ArcaneSeal()
    {
        if (arcaneSeal != null) return arcaneSeal;

        const int S = 256;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }
                float ang = Mathf.Atan2(dy, dx);

                float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.016f);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) / 0.030f);
                float faint = Mathf.Clamp01(1f - Mathf.Abs(d - 0.32f) / 0.012f);

                // Twelve radial ticks spanning the gap between the two main rings.
                float gap = (d > 0.66f && d < 0.90f) ? 1f : 0f;
                float ticks = gap * Mathf.Pow(Mathf.Abs(Mathf.Cos(6f * ang)), 60f);

                // Four diamond glyphs punctuating the OUTER ring, on the diagonals.
                //
                // They were originally on the inner ring at the cardinals — at exactly that
                // radius they merged into the ring itself and disappeared. On the outer ring they
                // read, and the diagonals keep them clear of the twelve ticks (which land on
                // multiples of 30 degrees).
                float glyph = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float ga = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                    float gx = dx - Mathf.Cos(ga) * 0.94f;
                    float gy = dy - Mathf.Sin(ga) * 0.94f;
                    float diamond = Mathf.Abs(gx) + Mathf.Abs(gy);
                    glyph = Mathf.Max(glyph, Mathf.Clamp01(1f - diamond / 0.105f));
                }

                float a = Mathf.Clamp01(outer * 0.9f + inner + faint * 0.45f + ticks * 0.8f + glyph);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        arcaneSeal = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return arcaneSeal;
    }

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // Ray brightness: full near the centre, tapering to nothing at `reach`.
    private static float Falloff(float d, float reach)
    {
        return Mathf.Pow(Mathf.Clamp01(1f - d / reach), 1.4f);
    }

    // 1x1 white — flat fills and hard edges.
    public static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        Texture2D tex = NewTex(1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return pixel;
    }

    // Builds a chamfered rectangle. thickness 0 = filled, >0 = hollow outline of that thickness.
    private static Sprite BuildPlate(int chamfer, int thickness)
    {
        int pad = chamfer + 3;
        int S = pad * 2 + 2;

        Texture2D tex = NewTex(S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = ChamferDistance(x + 0.5f, y + 0.5f, S, chamfer);

                float outer = Mathf.Clamp01(0.5f - d);
                float a = outer;
                if (thickness > 0)
                {
                    float inner = Mathf.Clamp01(0.5f - (d + thickness));
                    a = Mathf.Clamp01(outer - inner);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(pad, pad, pad, pad));
    }

    // Signed distance to a rectangle with its corners cut off at 45°: the box distance, then
    // intersected (max) with a diagonal half-plane that slices each corner.
    private static float ChamferDistance(float px, float py, int size, int chamfer)
    {
        float half = size * 0.5f;
        float ax = Mathf.Abs(px - half);
        float ay = Mathf.Abs(py - half);

        // Box SDF.
        float qx = ax - half, qy = ay - half;
        float box = Mathf.Min(Mathf.Max(qx, qy), 0f) +
                    Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));

        // Diagonal cut: |x| + |y| <= (half + half - chamfer), normalised to a true distance.
        float diag = (ax + ay - (half * 2f - chamfer)) * 0.70710678f;

        return Mathf.Max(box, diag);
    }

    // Renders a 9-sliced FlatUI sprite's border at an exact on-screen thickness, whatever the
    // sprite was baked at. Needed wherever a plate is small — a HUD bar is ~26px tall, and the
    // sprite's native 8px border would eat two thirds of it.
    public static void ApplySliceThickness(UnityEngine.UI.Image img, float pixels)
    {
        if (img == null || img.sprite == null || pixels <= 0f) return;
        float border = img.sprite.border.x;      // uniform on every shape here
        img.pixelsPerUnitMultiplier = border > 0f ? Mathf.Max(0.01f, border / pixels) : 1f;
    }

    private static Texture2D NewTex(int w, int h = -1)
    {
        if (h < 0) h = w;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;   // Point would alias the chamfer edges
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ---- palette ------------------------------------------------------------------------------
    // Warm charcoal iron, not slate-blue. Act 1 is the Oxidation District — everything here should
    // sit on the rust side of neutral.

    public static readonly Color Backdrop = new Color(0.020f, 0.017f, 0.015f, 0.92f);
    public static readonly Color Surface = new Color(0.086f, 0.076f, 0.068f, 0.99f);
    public static readonly Color SurfaceRaised = new Color(0.133f, 0.117f, 0.104f, 1f);
    public static readonly Color Border = new Color(0.278f, 0.243f, 0.204f, 1f);
    public static readonly Color BorderSoft = new Color(0.239f, 0.208f, 0.176f, 1f);
    // The lit top lip of the plate — brighter than the border, applied to the top edge only.
    public static readonly Color EdgeLight = new Color(0.420f, 0.369f, 0.310f, 1f);
    // Forge fire under the bench, washing up from the bottom edge.
    public static readonly Color Ember = new Color(0.85f, 0.42f, 0.16f, 1f);

    public static readonly Color TextBright = new Color(0.945f, 0.925f, 0.886f, 1f);
    public static readonly Color TextBody = new Color(0.800f, 0.769f, 0.722f, 1f);
    public static readonly Color TextMuted = new Color(0.549f, 0.510f, 0.455f, 1f);
    public static readonly Color TextDisabled = new Color(0.361f, 0.329f, 0.290f, 1f);

    // Charges are Shift-blue on purpose: with scrap costs in rust-orange, the only two colours on
    // the screen are the game's own two resources.
    public static readonly Color Charges = new Color(0.478f, 0.706f, 0.929f, 1f);

    // ---- themes -------------------------------------------------------------------------------
    // Screens share the IDEOLOGY (flat procedural plates, restraint, directional light, a subtle
    // particle drift, one meaningful accent) but must NOT share a skin. Each place gets its own
    // material, and the material should say what the place DOES:
    //
    //   IRON   — the Scrap Forge. A workbench. Warm charcoal, fire from BELOW, rivets, scuffs,
    //            embers rising. Industrial, used, hot.
    //   ARCANE — Blompo. A mythic creature granting a blessing. Cold indigo, light from ABOVE
    //            (the blessing descending), four-point stars instead of rivets, motes settling
    //            downward, and no wear at all — his space isn't a workshop that gets used, so
    //            leaving it pristine is itself the contrast.
    //
    // The inversions are deliberate: warm/cold, lit from below/above, rising/falling, worn/clean.
    // When adding a screen, pick a material and invert something — don't just retint Iron.
    public struct Theme
    {
        public Color Backdrop, Surface, SurfaceRaised, Border, BorderSoft, EdgeLight, Accent;
        public Color TextBright, TextBody, TextMuted, TextDisabled;
    }

    public static readonly Theme Iron = new Theme
    {
        Backdrop = Backdrop,
        Surface = Surface,
        SurfaceRaised = SurfaceRaised,
        Border = Border,
        BorderSoft = BorderSoft,
        EdgeLight = EdgeLight,
        Accent = Ember,
        TextBright = TextBright,
        TextBody = TextBody,
        TextMuted = TextMuted,
        TextDisabled = TextDisabled,
    };

    public static readonly Theme Arcane = new Theme
    {
        Backdrop = new Color(0.016f, 0.014f, 0.026f, 0.92f),
        Surface = new Color(0.069f, 0.064f, 0.098f, 0.99f),
        SurfaceRaised = new Color(0.110f, 0.102f, 0.149f, 1f),
        Border = new Color(0.263f, 0.239f, 0.361f, 1f),
        BorderSoft = new Color(0.216f, 0.196f, 0.298f, 1f),
        EdgeLight = new Color(0.451f, 0.412f, 0.596f, 1f),
        Accent = new Color(0.686f, 0.522f, 0.965f, 1f),   // arcane violet
        TextBright = new Color(0.929f, 0.925f, 0.961f, 1f),
        TextBody = new Color(0.769f, 0.761f, 0.831f, 1f),
        TextMuted = new Color(0.522f, 0.510f, 0.612f, 1f),
        TextDisabled = new Color(0.337f, 0.325f, 0.412f, 1f),
    };

    // The font procedural UI should use.
    //
    // Deliberately NOT `FindAnyObjectByType<TMP_Text>().font`, which several older screens use:
    // that returns an ARBITRARY text object, so the font a procedural panel picks up changes
    // between runs. It was caught by two screenshots of the same screen coming back in different
    // typefaces, and again when Blompo rendered in generic sans while the Forge rendered in the
    // pixel font. Count the fonts actually in use and take the most common — the project's real UI
    // font, deterministically, whatever a stray debug label happens to use.
    public static TMPro.TMP_FontAsset UIFont()
    {
        var counts = new System.Collections.Generic.Dictionary<TMPro.TMP_FontAsset, int>();
        foreach (TMPro.TMP_Text t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.font == null) continue;
            counts.TryGetValue(t.font, out int n);
            counts[t.font] = n + 1;
        }

        TMPro.TMP_FontAsset best = null;
        int bestCount = 0;
        foreach (var kv in counts)
        {
            // Ties break on instance ID so the result is stable across runs rather than
            // dictionary-order dependent.
            if (kv.Value > bestCount ||
                (kv.Value == bestCount && best != null && kv.Key.GetInstanceID() < best.GetInstanceID()))
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }

        return best != null ? best : TMPro.TMP_Settings.defaultFontAsset;
    }

    // LOADOUT — the relic bar and its tooltip.
    //
    // The other two themes dress a PLACE, where the material is the subject. This one dresses
    // what you're CARRYING, and it lives over gameplay permanently rather than taking the screen.
    // So its defining choice is the inverse: the chrome recedes to near-colourless, because the
    // relic icons are the subject and they're the only colour that should be in the row. It's also
    // the quietest theme by weight — a permanent HUD element must not compete with the game behind
    // it the way a modal panel can afford to.
    public static readonly Theme Loadout = new Theme
    {
        Backdrop = new Color(0.02f, 0.019f, 0.018f, 0.86f),
        Surface = new Color(0.078f, 0.075f, 0.071f, 0.94f),
        SurfaceRaised = new Color(0.110f, 0.106f, 0.100f, 0.96f),
        Border = new Color(0.239f, 0.231f, 0.216f, 1f),
        BorderSoft = new Color(0.180f, 0.173f, 0.161f, 1f),
        EdgeLight = new Color(0.361f, 0.349f, 0.325f, 1f),
        Accent = new Color(0.847f, 0.804f, 0.706f, 1f),   // pale bone, not a hue
        TextBright = new Color(0.937f, 0.929f, 0.910f, 1f),
        TextBody = new Color(0.788f, 0.776f, 0.749f, 1f),
        TextMuted = new Color(0.529f, 0.518f, 0.494f, 1f),
        TextDisabled = new Color(0.341f, 0.333f, 0.318f, 1f),
    };

    // VERDIGRIS — the run map.
    //
    // The other themes dress a PLACE and render it with light on material. A map is not a place:
    // you are not standing in it, you are READING it. So this one inverts the lighting model
    // itself — it is flat, matte and unlit, with no glow source anywhere. Motion lives in the
    // INFORMATION (branches you can take pulse, the route you've walked shimmers) rather than in
    // the air, because a chart has no air. That is why it carries no particle field while every
    // other screen does.
    //
    // The material is oxidised copper, which is Act 1's own chemistry — the Oxidation District is
    // rust and corrosion, and verdigris is what copper does there. It also pays off the accent:
    // the route you have ALREADY TRAVELLED is drawn in warm bare copper, as though the patina were
    // worn back to metal by walking it. Every other theme's accent is light being ADDED (forge
    // fire, arcane glow); this one's is surface being WORN AWAY.
    public static readonly Theme Verdigris = new Theme
    {
        Backdrop = new Color(0.012f, 0.020f, 0.019f, 0.93f),
        Surface = new Color(0.055f, 0.082f, 0.078f, 0.99f),
        SurfaceRaised = new Color(0.082f, 0.115f, 0.108f, 1f),
        Border = new Color(0.180f, 0.255f, 0.235f, 1f),
        BorderSoft = new Color(0.130f, 0.190f, 0.176f, 1f),
        EdgeLight = new Color(0.290f, 0.400f, 0.360f, 1f),
        Accent = new Color(0.855f, 0.545f, 0.290f, 1f),   // bare copper, worn through the patina
        TextBright = new Color(0.878f, 0.925f, 0.905f, 1f),
        TextBody = new Color(0.706f, 0.780f, 0.755f, 1f),
        TextMuted = new Color(0.478f, 0.545f, 0.522f, 1f),
        TextDisabled = new Color(0.310f, 0.365f, 0.349f, 1f),
    };

    // Rarity colours tuned to read on a DARK surface. The old chrome carried rarity on a gem set
    // in gold; without that frame the colour has to stand on its own, so these are brighter and
    // more separated than jewel tones would be.
    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return new Color(0.980f, 0.757f, 0.361f, 1f);   // amber
            case Rarity.Epic: return new Color(0.729f, 0.529f, 0.961f, 1f);        // violet
            case Rarity.Rare: return new Color(0.416f, 0.702f, 0.980f, 1f);        // azure
            // Common is muted on purpose. At a lighter slate it rendered near-white, and since the
            // sigil is large that made the WEAKEST offer the brightest thing on the screen.
            default: return new Color(0.510f, 0.549f, 0.635f, 1f);                 // cool slate
        }
    }
}

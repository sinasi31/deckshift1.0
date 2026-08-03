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
    private static Sprite softGlow, verticalFade, fadedRule, rivet, pixel;

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

    // Opaque at the top fading to nothing downward. Used for the top-lip sheen, and rotated 180°
    // for the ember glow rising off the bottom edge.
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
}

using UnityEngine;

// A quiet, flat UI sprite kit — the alternative to RelicUISprites' ornate stone-and-gold chrome.
//
// Everything here is a WHITE shape meant to be tinted by Image.color, so one cached sprite serves
// every panel, chip and button in any colour. Shapes are 9-sliced, so a single small texture
// stretches to any size with corners that stay crisp.
//
// Design intent (designer, 2026-08-03: "soothing, simple, understandable, but also cool"): flat
// dark surfaces, one thin outline, no texture noise, no ornament. Depth comes from a barely-there
// top sheen and a soft selection glow rather than from bevels or studs. All shapes are drawn with
// ~1px anti-aliasing so rounded corners read as smooth instead of chunky at UI scale.
public static class FlatUI
{
    private static Sprite panelR8, panelR6, outlineR8, outlineR6, softGlow, verticalFade, pixel;

    // Solid rounded rectangle. radius 8 = windows, radius 6 = cards/buttons.
    public static Sprite Panel(int radius = 8)
    {
        if (radius <= 6)
        {
            if (panelR6 == null) panelR6 = BuildRounded(6, 0);
            return panelR6;
        }
        if (panelR8 == null) panelR8 = BuildRounded(8, 0);
        return panelR8;
    }

    // Rounded-rectangle OUTLINE only (hollow centre), stacked over a Panel to tint the border
    // independently of the fill.
    public static Sprite Outline(int radius = 8, int thickness = 2)
    {
        if (radius <= 6)
        {
            if (outlineR6 == null) outlineR6 = BuildRounded(6, Mathf.Max(1, thickness));
            return outlineR6;
        }
        if (outlineR8 == null) outlineR8 = BuildRounded(8, Mathf.Max(1, thickness));
        return outlineR8;
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
                // Squared falloff — a linear one has a visible hard edge where it reaches zero.
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        softGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return softGlow;
    }

    // White at the top fading to nothing downward. Laid over a panel at very low alpha to suggest
    // light from above — the only "depth" cue in the kit.
    public static Sprite VerticalFade()
    {
        if (verticalFade != null) return verticalFade;

        const int H = 64;
        Texture2D tex = NewTex(1, H);
        for (int y = 0; y < H; y++)
        {
            float t = (float)y / (H - 1);          // 0 bottom, 1 top
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
        }
        tex.Apply();
        verticalFade = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);
        return verticalFade;
    }

    // 1x1 white — dividers and flat fills.
    public static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        Texture2D tex = NewTex(1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return pixel;
    }

    // Builds a rounded rect. thickness 0 = filled; >0 = hollow outline of that many pixels.
    // Size is derived from the radius so the 9-slice border always contains the whole corner arc
    // and leaves a 2px stretchable centre.
    private static Sprite BuildRounded(int radius, int thickness)
    {
        int pad = radius + 2;
        int S = pad * 2 + 2;

        Texture2D tex = NewTex(S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, S, radius);

                // d is signed distance to the shape edge: negative inside, positive outside.
                float outer = Mathf.Clamp01(0.5f - d);                    // ~1px AA at the edge
                float a = outer;

                if (thickness > 0)
                {
                    // Hollow: subtract an inner shape inset by `thickness`.
                    float inner = Mathf.Clamp01(0.5f - (d + thickness));
                    a = Mathf.Clamp01(outer - inner);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(pad, pad, pad, pad));
    }

    // Signed distance from a point to a rounded-rectangle edge: negative inside, positive outside,
    // and correct in both regions (the standard rounded-box SDF). Getting this right is what makes
    // the outline an even thickness all the way round instead of pinching at the corners.
    private static float RoundedDistance(float px, float py, int size, int radius)
    {
        float half = size * 0.5f;
        float qx = Mathf.Abs(px - half) - (half - radius);
        float qy = Mathf.Abs(py - half) - (half - radius);

        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                   Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return inside + outside - radius;
    }

    private static Texture2D NewTex(int w, int h = -1)
    {
        if (h < 0) h = w;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;   // Point would alias the rounded corners
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ---- shared palette -----------------------------------------------------------------------
    // One place for the flat theme's colours so panels built in different scripts match.

    public static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.06f, 0.90f);
    public static readonly Color Surface = new Color(0.086f, 0.102f, 0.129f, 0.985f);
    public static readonly Color SurfaceRaised = new Color(0.130f, 0.153f, 0.188f, 1f);
    public static readonly Color Border = new Color(0.212f, 0.251f, 0.310f, 1f);
    // Dividers use this. Kept a step brighter than it "should" be — at the theoretically correct
    // subtlety the hairlines simply did not register against the dark surface on screen.
    public static readonly Color BorderSoft = new Color(0.235f, 0.275f, 0.337f, 1f);

    public static readonly Color TextBright = new Color(0.937f, 0.949f, 0.965f, 1f);
    public static readonly Color TextBody = new Color(0.780f, 0.812f, 0.855f, 1f);
    public static readonly Color TextMuted = new Color(0.482f, 0.529f, 0.600f, 1f);
    public static readonly Color TextDisabled = new Color(0.322f, 0.357f, 0.412f, 1f);

    public static readonly Color Charges = new Color(0.478f, 0.706f, 0.929f, 1f);
}

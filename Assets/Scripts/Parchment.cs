using UnityEngine;

// The map's paper, ink and cartographic furniture — all procedural, all cached, no art files
// (house pattern, same as MapGlyphs / FlatUI / DashAfterimage).
//
// WHY THIS EXISTS: the run map was twice given a MATERIAL (flat slate, then an etched copper plate)
// and both times it still read as a diagram rather than a map. A material is not enough. What makes
// a map feel like a map is that it is a DOCUMENT — a sheet somebody printed, folded, carried and
// then scribbled on:
//
//   · PAPER, not a panel. The sheet IS the window; there is no frame around it. It has fibre,
//     blotching, foxing, aged edges and a torn deckle, so no two areas of it are identical.
//   · FOLDS. A crease says the thing was carried in a pocket and has just been opened, which is
//     the single cheapest signal that this is an object rather than a screen.
//   · DASHED TRAILS. This is the biggest one after the paper. A solid line between two points is a
//     graph edge; a dashed line is a ROUTE. Every trail here is drawn as separate short strokes
//     with a little wobble, because a hand-inked line is never straight.
//   · ANNOTATION IN RED. The map is printed in brown ink; where the player has BEEN, and what they
//     may take next, is marked over the top in red pen. That fiction does all the state signalling
//     for free and needs no colour key.
//
// ⚠️ Values here are picked for LINEAR colour space and judged on screen. Paper is a LIGHT ground,
// which inverts the usual trap: subtle marks want MORE alpha than on the dark screens, not less,
// because a low-alpha dark mark on a light ground washes out rather than glows.
public static class Parchment
{
    // ---- palette ---------------------------------------------------------------------------------

    public static readonly Color Paper = new Color(0.845f, 0.760f, 0.590f, 1f);
    public static readonly Color PaperShade = new Color(0.640f, 0.545f, 0.390f, 1f);

    public static readonly Color Ink = new Color(0.170f, 0.125f, 0.080f, 1f);
    public static readonly Color InkSoft = new Color(0.340f, 0.270f, 0.185f, 1f);
    public static readonly Color InkPale = new Color(0.510f, 0.430f, 0.320f, 1f);

    // The pen. Everything about the player's own progress is written in this.
    public static readonly Color Red = new Color(0.560f, 0.150f, 0.105f, 1f);
    public static readonly Color RedSoft = new Color(0.690f, 0.300f, 0.215f, 1f);

    private static Sprite sheet, grain, stroke, ring, ringHeavy, compass, vignette, blot;

    // ---- the sheet -------------------------------------------------------------------------------

    // Builds every sprite up front so no screen pays for them mid-play. Called from
    // RunMapManager.Awake, i.e. during a scene load. Cheap to call again — everything is cached.
    public static void Prewarm()
    {
        Sheet(); Grain(); Vignette(); Stroke();
        InkRing(false); InkRing(true); Blot(); Compass();
    }

    // Greyscale luminance + a torn alpha edge. Tint it with Paper: multiplying a warm tan through a
    // luminance map is what makes the stains read as the same paper rather than as painted-on spots.
    public static Sprite Sheet()
    {
        if (sheet != null) return sheet;

        const int S = 640;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        // A handful of large stains and a scatter of foxing specks, both fixed so the sheet is the
        // same object every time it is opened. A map that re-ages itself on each open is a screen.
        Vector3[] stains = new Vector3[7];
        for (int i = 0; i < stains.Length; i++)
            stains[i] = new Vector3(Hash(i, 11, 5) , Hash(i, 23, 9), 0.10f + Hash(i, 37, 3) * 0.22f);

        // The widest a stain can ever be, so a pixel far from every stain can skip the whole loop
        // (and the noise call inside it) instead of measuring seven distances to find that out.
        float maxStainR = 0f;
        foreach (Vector3 st in stains) maxStainR = Mathf.Max(maxStainR, st.z * (0.72f + 0.55f));

        // ⚠️ ROWS ARE COMPUTED IN PARALLEL, AND THAT IS ONLY LEGAL BECAUSE NOTHING IN HERE TOUCHES
        // UNITY. It is all Mathf/struct arithmetic writing to disjoint indices of one array; the
        // Texture2D calls below stay on the main thread where they belong. This was a 1.4-SECOND
        // freeze the first time the player opened the map — the sheet is 409,600 pixels and each
        // one costs a fistful of noise octaves.
        Color[] px = new Color[S * S];
        System.Threading.Tasks.Parallel.For(0, S, y =>
        {
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;

                // Broad blotching + finer fibre. Kept gentle: paper is subtle, and the marks drawn
                // on top are what the player is actually meant to read.
                float lum = 1f;
                lum -= (Fbm(u * 5.5f, v * 5.5f, 1, 4) - 0.5f) * 0.20f;
                lum -= (ValueNoise(u * 46f, v * 46f, 17) - 0.5f) * 0.055f;

                // ⚠️ HOISTED OUT OF THE STAIN LOOP. It reads only u and v — no stain — so the old
                // code evaluated the SAME two-octave field seven times per pixel and threw six of
                // them away. That single line was over half the cost of the whole texture.
                float radiusNoise = 0f;
                bool nearAnyStain = false;
                for (int i = 0; i < stains.Length && !nearAnyStain; i++)
                {
                    float ddx = u - stains[i].x, ddy = v - stains[i].y;
                    if (ddx * ddx + ddy * ddy < maxStainR * maxStainR) nearAnyStain = true;
                }
                if (nearAnyStain)
                {
                    radiusNoise = 0.72f + Fbm(u * 7f, v * 7f, 5, 2) * 0.55f;
                    foreach (Vector3 st in stains)
                    {
                        float ddx = u - st.x, ddy = v - st.y;
                        float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        // Noisy radius, or every stain is a perfect disc.
                        float r = st.z * radiusNoise;
                        if (d < r) lum -= (1f - d / r) * (1f - d / r) * 0.085f;
                    }
                }

                // Foxing: tiny age spots, only where a high-frequency field spikes.
                float fox = ValueNoise(u * 150f, v * 150f, 91);
                if (fox > 0.955f) lum -= (fox - 0.955f) * 6.5f;

                // The rim of an old sheet is darker and grubbier than its middle.
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                lum -= Mathf.Clamp01(1f - edge / 0.13f) * 0.16f;

                // Torn deckle. The alpha boundary itself wanders, so the sheet is not a rectangle.
                // The tear can never reach further in than 0.030 + the 0.006 falloff, so past that
                // the alpha is flatly 1 and the three-octave field behind it is wasted work.
                float a = 1f;
                if (edge < 0.045f)
                {
                    float tear = 0.010f + Fbm(u * 13f, v * 13f, 41, 3) * 0.020f;
                    a = Mathf.Clamp01((edge - tear) / 0.006f);
                }

                lum = Mathf.Clamp01(lum);
                px[y * S + x] = new Color(lum, lum, lum, a);
            }
        });

        tex.SetPixels(px);
        tex.Apply();
        sheet = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return sheet;
    }

    // Fine tileable fibre, laid over the stretched sheet at native resolution. The sheet is blown up
    // roughly 2.5x on screen, which softens everything in it — this is what puts crisp tooth back.
    public static Sprite Grain()
    {
        if (grain != null) return grain;

        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };

        Color[] px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = WrapNoise(x / (float)S * 16f, y / (float)S * 16f, 16, 3);
                float a = Mathf.Clamp01((n - 0.52f) * 1.5f) * 0.5f;
                px[y * S + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        grain = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return grain;
    }

    // Soft dark corners. A held sheet is never evenly lit, and this is what stops the paper reading
    // as a flat fill of one colour.
    public static Sprite Vignette()
    {
        if (vignette != null) return vignette;
        vignette = Build(128, (dx, dy, d, ang) =>
        {
            float k = Mathf.Clamp01((d - 0.55f) / 0.62f);
            return k * k * 0.85f;
        });
        return vignette;
    }

    // ---- ink -------------------------------------------------------------------------------------

    // One short pen stroke, with soft ends and a slightly uneven belly. Trails are built from these
    // rather than from one long bar: separate strokes are what make a line read as DASHED, and the
    // unevenness is what stops eight of them in a row looking like a dotted CSS border.
    public static Sprite Stroke()
    {
        if (stroke != null) return stroke;
        const int W = 32, H = 12;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1), v = y / (float)(H - 1);
                // Thickness swells in the middle and tapers at both ends, like a drawn stroke.
                float taper = Mathf.Sin(u * Mathf.PI);
                float half = 0.16f + 0.16f * taper;
                float d = Mathf.Abs(v - 0.5f);
                float a = Mathf.Clamp01((half - d) / 0.09f) * Mathf.Clamp01(taper * 2.6f);
                tex.SetPixel(x, y, a <= 0f ? Color.clear : new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        stroke = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return stroke;
    }

    // A hand-inked circle: the radius wanders, and the line thins where the nib lifted.
    public static Sprite InkRing(bool heavy)
    {
        if (heavy && ringHeavy != null) return ringHeavy;
        if (!heavy && ring != null) return ring;

        int seed = heavy ? 3 : 8;
        float w = heavy ? 0.052f : 0.036f;
        Sprite s = Build(96, (dx, dy, d, ang) =>
        {
            float wobble = 1f + (Fbm(Mathf.Cos(ang) * 1.6f + 3f, Mathf.Sin(ang) * 1.6f + 3f, seed, 3) - 0.5f) * 0.075f;
            float r = 0.80f * wobble;
            // The nib lifts somewhere on every hand-drawn circle.
            float lift = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(ang * 0.5f + seed));
            return Mathf.Clamp01(1f - Mathf.Abs(d - r) / w) * lift;
        });

        if (heavy) ringHeavy = s; else ring = s;
        return s;
    }

    // A soft irregular ink blot — used behind glyphs so a symbol looks absorbed into the paper.
    public static Sprite Blot()
    {
        if (blot != null) return blot;
        blot = Build(64, (dx, dy, d, ang) =>
        {
            float wob = 1f + (Fbm(Mathf.Cos(ang) + 5f, Mathf.Sin(ang) + 5f, 13, 2) - 0.5f) * 0.30f;
            return Mathf.Clamp01((0.78f * wob - d) * 3.2f);
        });
        return blot;
    }

    // ---- furniture -------------------------------------------------------------------------------

    // A compass rose. Long N/S/E/W points, short diagonals, a containing ring and a hub — the same
    // "structure, not a sparkle" rule the arcane sigil had to learn.
    public static Sprite Compass()
    {
        if (compass != null) return compass;
        compass = Build(160, (dx, dy, d, ang) =>
        {
            if (d > 1f) return 0f;

            // Four long points on the axes, four short ones on the diagonals.
            float axis = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 14f);
            float diag = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 14f);
            float reach = Mathf.Max(0.94f * axis, 0.52f * diag);
            float star = Mathf.Clamp01((reach - d) * 9f);

            float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.66f) / 0.022f);
            float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.30f) / 0.030f);
            float hub = Mathf.Clamp01((0.10f - d) * 14f);

            // Ticks OUTSIDE the ring, so they punctuate it instead of merging into it.
            float ticks = (d > 0.70f && d < 0.80f)
                ? Mathf.Pow(Mathf.Abs(Mathf.Cos(8f * ang)), 46f) : 0f;

            return Mathf.Clamp01(star * 0.92f + outer + inner * 0.8f + hub + ticks * 0.75f);
        });
        return compass;
    }

    // ---- noise -----------------------------------------------------------------------------------

    private static float Hash(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + s * 69069;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float ValueNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = Smooth(x - xi), yf = Smooth(y - yi);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
    }

    // Wrapping variant, so Grain() tiles without a visible seam.
    private static float WrapNoise(float x, float y, int period, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f;
        for (int o = 0; o < octaves; o++)
        {
            int p = Mathf.Max(1, Mathf.RoundToInt(period * freq));
            int xi = Mathf.FloorToInt(x * freq), yi = Mathf.FloorToInt(y * freq);
            float xf = Smooth(x * freq - xi), yf = Smooth(y * freq - yi);
            int x0 = ((xi % p) + p) % p, x1 = (x0 + 1) % p;
            int y0 = ((yi % p) + p) % p, y1 = (y0 + 1) % p;
            float a = Hash(x0, y0, 3), b = Hash(x1, y0, 3);
            float c = Hash(x0, y1, 3), d = Hash(x1, y1, 3);
            sum += Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf) * amp;
            amp *= 0.5f; freq *= 2f;
        }
        return Mathf.Clamp01(sum * 1.6f);
    }

    private static float Fbm(float x, float y, int seed, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(x * freq, y * freq, seed + o * 31) * amp;
            norm += amp;
            amp *= 0.5f; freq *= 2f;
        }
        return sum / Mathf.Max(0.0001f, norm);
    }

    // ---- shared builder --------------------------------------------------------------------------

    private delegate float Field(float dx, float dy, float d, float ang);

    private static Sprite Build(int size, Field f)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(f(dx, dy, d, Mathf.Atan2(dy, dx)));
                tex.SetPixel(x, y, a <= 0f ? Color.clear : new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

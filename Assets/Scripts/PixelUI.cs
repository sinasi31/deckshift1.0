using UnityEngine;

// Pixel-art procedural UGUI sprites — the crunchy counterpart to RelicUISprites' smooth
// rounded panels. Everything is generated at low resolution with FilterMode.Point and hard
// edges (no anti-aliased feather), so it reads as hand-placed pixels instead of vector-smooth
// "engine default" UI. Cached statically. Panels/frames are grayscale so callers tint them
// via Image.color, exactly like RelicUISprites — drop-in replacements.
public static class PixelUI
{
    private static Sprite panelSprite, frameSprite, grainSprite;

    // Solid beveled panel: dark 1px outline, lit top-left inner edge, shadowed bottom-right,
    // chamfered corners. Grayscale → tint with Image.color. 9-sliced so the bevel stays crisp.
    public static Sprite Panel()
    {
        if (panelSprite != null) return panelSprite;
        const int s = 24, b = 6;
        Texture2D tex = NewTex(s);
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dL = x, dR = s - 1 - x, dB = y, dT = s - 1 - y;
                int e = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dB, dT));
                float a = 1f, g;
                bool corner = (x == 0 || x == s - 1) && (y == 0 || y == s - 1);
                if (corner) { a = 0f; g = 0f; }              // chamfer the 1px corners
                else if (e == 0) g = 0.30f;                  // outline
                else if (e == 1) g = (dT < dB || dL < dR) ? 1.0f : 0.64f;   // lit TL / shadow BR
                else g = 0.88f;                              // fill
                px[y * s + x] = new Color32((byte)(g * 255), (byte)(g * 255), (byte)(g * 255), (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        panelSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        return panelSprite;
    }

    // Hard 2px pixel border ring (chamfered corners), transparent center. White → tint per use
    // (e.g. rarity colour). 9-sliced.
    public static Sprite Frame()
    {
        if (frameSprite != null) return frameSprite;
        const int s = 24, b = 6;
        Texture2D tex = NewTex(s);
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int e = Mathf.Min(Mathf.Min(x, s - 1 - x), Mathf.Min(y, s - 1 - y));
                bool corner = (x == 0 || x == s - 1) && (y == 0 || y == s - 1);
                byte a = (!corner && e < 2) ? (byte)255 : (byte)0;
                px[y * s + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        return frameSprite;
    }

    // Tileable grayscale wood grain — subtle horizontal streaks, plank seams, faint speckle.
    // Tinted by Image.color and used with Image.Type.Tiled for a material fill. Point + Repeat.
    public static Sprite Grain()
    {
        if (grainSprite != null) return grainSprite;
        const int s = 32;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        Color32[] px = new Color32[s * s];
        // Pattern periodic over the tile so it repeats seamlessly (no visible grid):
        // horizontal streaks depend only on y (period divides s); seams on aligned rows.
        const float tau = 6.2831853f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float g = 0.52f;
                g += 0.065f * Mathf.Sin(y * tau / 16f);          // horizontal wood streaks
                if (y % 16 == 0 || y % 16 == 7) g -= 0.11f;      // plank seams (aligned to tile)
                float n = Frac(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.55f);
                g += (n - 0.5f) * 0.06f;                         // fine speckle
                g = Mathf.Clamp(g, 0.30f, 0.74f);
                px[y * s + x] = new Color32((byte)(g * 255), (byte)(g * 255), (byte)(g * 255), 255);
            }
        tex.SetPixels32(px); tex.Apply();
        grainSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return grainSprite;
    }

    private static Texture2D NewTex(int s) =>
        new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

    private static float Frac(float v) => v - Mathf.Floor(v);
}

using UnityEngine;

// Shared procedural UGUI sprites for the slot-relic UI (loadout bar, tooltip, and the
// Manage / Swap panels to come). House style — no art, all generated in code and cached
// statically so every panel draws from one source. Mirrors the rounded-rect maths already
// proven in RelicIcon.cs; kept separate so the new panels don't each re-roll their own.
public static class RelicUISprites
{
    private static Sprite panelSprite;   // solid rounded fill (plates / panels)
    private static Sprite frameSprite;   // rounded border ring (slot frames)
    private static Sprite glowSprite;    // soft radial glow
    private static Sprite whiteSprite;   // 1x1 white (bars / dividers)

    // The shared rarity palette — identical to RelicIcon.RarityColor so filled slots,
    // empty frames, tooltips and panels all speak the same colour language.
    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return new Color(1f, 0.80f, 0.25f);
            case Rarity.Epic:      return new Color(0.72f, 0.38f, 1f);
            case Rarity.Rare:      return new Color(0.35f, 0.62f, 1f);
            default:               return new Color(0.75f, 0.78f, 0.85f);
        }
    }

    public static Sprite Panel()
    {
        if (panelSprite != null) return panelSprite;
        int s = 64; float radius = s * 0.22f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float a = Mathf.Clamp01(d);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        panelSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return panelSprite;
    }

    public static Sprite Frame()
    {
        if (frameSprite != null) return frameSprite;
        int s = 64; float radius = s * 0.22f, border = s * 0.09f, feather = 1.5f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float outer = Mathf.Clamp01(d / feather);
                float inner = Mathf.Clamp01((border - d) / feather);
                float a = d < 0f ? 0f : Mathf.Min(outer, inner);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return frameSprite;
    }

    public static Sprite Glow()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }

    public static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return whiteSprite;
    }

    // Signed distance (px) to the nearest edge of a rounded square; >0 inside.
    private static float RoundedRectEdge(int x, int y, int s, float radius)
    {
        float half = s / 2f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;
        float ax = Mathf.Abs(px) - (half - radius);
        float ay = Mathf.Abs(py) - (half - radius);
        float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
        float inside = Mathf.Min(Mathf.Max(ax, ay), 0f);
        return radius - (outside + inside);
    }
}

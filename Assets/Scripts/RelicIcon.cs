using UnityEngine;
using UnityEngine.UI;

// Procedural "chip" styling + pop-in for one relic icon in the RelicHUD. Added to each instantiated
// RelicIconPrefab; builds a rarity-coloured glow aura + dark rounded plate + framed icon in code
// (house style, no art) and animates an EaseOutBack pop-in when it first becomes visible. Epic/Legendary
// get a gentle idle glow pulse. Update-driven so it's robust even when the HUD is hidden at grant time
// (relics are usually awarded from the shop/slot while GameplayHUD is inactive) — it simply pops in the
// moment the HUD is shown again.
//
// NOTE: purely visual juice. No tooltips/interaction — that belongs to the planned slot-relic redesign.
public class RelicIcon : MonoBehaviour
{
    private static Sprite glowSprite, plateSprite, frameSprite;

    private Image glow;
    private Color glowBase;
    private Rarity rarity;

    private bool popping;
    private float popT;
    const float POP_DUR = 0.32f;

    public void Build(RelicData relic)
    {
        rarity = relic != null ? relic.rarity : Rarity.Common;
        Color tint = RarityColor(rarity);

        // The prefab's plain root Image becomes an inert container; we draw everything as children.
        Image rootImg = GetComponent<Image>();
        if (rootImg != null) rootImg.enabled = false;

        RectTransform root = GetComponent<RectTransform>();
        float size = root.rect.width > 1f ? root.rect.width : 48f;

        // Back-to-front: soft rarity glow aura → dark rounded plate → relic art → rarity frame.
        glow = MakeChild("Glow", GetGlowSprite(), tint, size * 1.4f);
        glowBase = tint;
        SetGlowAlpha(0.5f);

        Image plate = MakeChild("Plate", GetPlateSprite(), new Color(0.10f, 0.10f, 0.13f, 0.9f), size);
        plate.type = Image.Type.Simple;

        Image icon = MakeChild("Icon", relic != null ? relic.relicArt : null, Color.white, size * 0.66f);
        icon.preserveAspect = true;
        if (relic == null || relic.relicArt == null) icon.enabled = false;

        MakeChild("Frame", GetFrameSprite(), tint, size);

        // Start hidden and pop in via Update (works whether or not the HUD is currently visible).
        transform.localScale = Vector3.zero;
        popping = true;
        popT = 0f;
    }

    private void Update()
    {
        if (popping)
        {
            popT += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(popT / POP_DUR);
            transform.localScale = Vector3.one * Mathf.Max(0f, EaseOutBack(n));
            if (n >= 1f) { popping = false; transform.localScale = Vector3.one; }
        }

        if (glow != null && rarity >= Rarity.Epic)
            SetGlowAlpha(0.4f + 0.22f * Mathf.Sin(Time.unscaledTime * 2.5f));
    }

    private void SetGlowAlpha(float a)
    {
        Color c = glowBase; c.a = a; glow.color = c;
    }

    private Image MakeChild(string n, Sprite sprite, Color color, float size)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return new Color(1f, 0.80f, 0.25f);
            case Rarity.Epic:      return new Color(0.72f, 0.38f, 1f);
            case Rarity.Rare:      return new Color(0.35f, 0.62f, 1f);
            default:               return new Color(0.75f, 0.78f, 0.85f);
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // --- procedural sprites (cached + shared) ---

    private static Sprite GetGlowSprite()
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

    private static Sprite GetPlateSprite()
    {
        if (plateSprite != null) return plateSprite;
        int s = 64; float radius = s * 0.28f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);       // >0 inside
                float a = Mathf.Clamp01(d);                       // 1px AA edge
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        plateSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return plateSprite;
    }

    private static Sprite GetFrameSprite()
    {
        if (frameSprite != null) return frameSprite;
        int s = 64; float radius = s * 0.28f, border = s * 0.11f, feather = 1.5f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float outer = Mathf.Clamp01(d / feather);          // fade in from the outer edge
                float inner = Mathf.Clamp01((border - d) / feather); // fade out at band width inward
                float a = d < 0f ? 0f : Mathf.Min(outer, inner);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return frameSprite;
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

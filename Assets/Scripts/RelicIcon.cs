using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One relic slot in the loadout bar. Added to each instantiated RelicIconPrefab.
//
// Built in the FlatUI LOADOUT theme: a dark chamfered socket, the relic's icon, and a rarity
// strip. Replaced a gold-on-stone medallion (ornate border + four gem bosses in the corners),
// which the designer had disliked since it was introduced.
//
// WHAT CARRIES RARITY, now that there is no gem: a coloured STRIP along the bottom of the socket,
// plus a matching tint on the socket's outline. The strip is the load-bearing signal — at 52px,
// over moving gameplay, a tinted hairline alone is not reliably readable, whereas a solid bar is
// legible at a glance. Epic and Legendary additionally get a slow glow pulse, so the two rarities
// worth noticing are the only ones that move.
//
// THE GOVERNING RULE FOR THIS BAR: the chrome recedes. Relic art is colourful pixel work and it is
// the subject; the socket around it is near-colourless on purpose. Do not add a hue here.
public class RelicIcon : MonoBehaviour
{
    private Image glow;
    private Color glowBase;
    private Rarity rarity;

    private bool popping;
    private float popT;
    const float POP_DUR = 0.32f;

    public void Build(RelicData relic)
    {
        rarity = relic != null ? relic.rarity : Rarity.Common;
        Color rarityCol = FlatUI.RarityColor(rarity);
        FlatUI.Theme T = FlatUI.Loadout;

        // The prefab's plain root Image becomes an inert container; we draw everything as children.
        Image rootImg = GetComponent<Image>();
        if (rootImg != null) rootImg.enabled = false;

        RectTransform root = GetComponent<RectTransform>();
        float size = root.rect.width > 1f ? root.rect.width : 48f;

        // 1. Rarity aura behind the socket — only really visible on Epic/Legendary, which pulse.
        glow = MakeChild("Glow", FlatUI.SoftGlow(), rarityCol, size * 1.30f, Vector2.zero);
        glowBase = rarityCol;
        SetGlowAlpha(rarity >= Rarity.Epic ? 0.34f : 0.10f);

        // 2. The socket itself.
        Image socket = MakeChild("Socket", FlatUI.Panel(5), T.SurfaceRaised, size, Vector2.zero);
        socket.type = Image.Type.Sliced;

        Image outline = MakeChild("Outline", FlatUI.Outline(5, 1), RarityOutline(rarityCol), size, Vector2.zero);
        outline.type = Image.Type.Sliced;

        // 3. Relic art, nudged up to leave the rarity strip its own band at the bottom.
        if (relic != null && relic.relicArt != null)
        {
            Image icon = MakeChild("Icon", relic.relicArt, Color.white, size * 0.60f, new Vector2(0f, size * 0.06f));
            icon.preserveAspect = true;
        }
        else
        {
            // No art yet: the relic's initial, in its rarity colour. More use than a blank socket,
            // and unlike the old gem stand-in it doesn't reintroduce the jewel language.
            string label = relic != null && !string.IsNullOrEmpty(relic.relicName)
                ? relic.relicName.Substring(0, 1).ToUpper()
                : "?";
            MakeLabel("Initial", label, size * 0.46f, rarityCol, new Vector2(0f, size * 0.06f));
        }

        // 4. Rarity strip along the bottom — the primary rarity read.
        Image strip = MakeChild("RarityStrip", FlatUI.Pixel(), rarityCol, size, new Vector2(0f, -size * 0.5f + 6f));
        strip.rectTransform.sizeDelta = new Vector2(size * 0.52f, 3f);

        // Start hidden and pop in via Update (works whether or not the HUD is currently visible).
        transform.localScale = Vector3.zero;
        popping = true;
        popT = 0f;
    }

    // Outline sits between the theme border and the full rarity colour: present enough to tie the
    // socket to its strip, muted enough that five filled slots don't read as five coloured boxes.
    private static Color RarityOutline(Color rarityCol)
    {
        return Color.Lerp(FlatUI.Loadout.Border, rarityCol, 0.45f);
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
            SetGlowAlpha(0.26f + 0.14f * Mathf.Sin(Time.unscaledTime * 2.5f));
    }

    private void SetGlowAlpha(float a)
    {
        if (glow == null) return;
        Color c = glowBase; c.a = a; glow.color = c;
    }

    private Image MakeChild(string n, Sprite sprite, Color color, float size, Vector2 offset)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void MakeLabel(string n, string text, float fontSize, Color color, Vector2 offset)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(fontSize * 2f, fontSize * 2f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = FlatUI.UIFont();
        if (f != null) t.font = f;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

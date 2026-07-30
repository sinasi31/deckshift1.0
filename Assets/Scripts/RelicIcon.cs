using UnityEngine;
using UnityEngine.UI;

// Procedural styling + pop-in for one relic icon in the RelicHUD. Added to each instantiated
// RelicIconPrefab. Builds a crafted medallion in Deckshift's own HUD language (Assets/Art/panel 1.png):
//
//   rarity glow  ->  mottled-STONE socket  ->  relic icon (+ drop shadow)  ->  ornate GOLD border
//   ->  four GEM BOSSES in the corners (gold setting + a rarity-coloured gem)
//
// The relic's pixel-art icon comes from RelicData.relicArt (wired to the Cainos RPG icon pack).
// Rarity is read from the gem colour, not by recolouring the gold — so every relic looks like the
// same gold-on-stone object the rest of the HUD is built from. If a relic has no art yet, a rarity
// gem fills the socket so it never looks empty. EaseOutBack pop-in on first show; Epic/Legendary get
// a gentle idle glow pulse. Update-driven so it survives being built while the HUD is hidden.
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
        Color rarityCol = RelicUISprites.RarityColor(rarity);
        Color gemCol = RelicUISprites.GemColor(rarity);

        // The prefab's plain root Image becomes an inert container; we draw everything as children.
        Image rootImg = GetComponent<Image>();
        if (rootImg != null) rootImg.enabled = false;

        RectTransform root = GetComponent<RectTransform>();
        float size = root.rect.width > 1f ? root.rect.width : 48f;

        // 1. Soft rarity aura behind the medallion.
        glow = MakeChild("Glow", RelicUISprites.Glow(), rarityCol, size * 1.34f, Vector2.zero);
        glowBase = rarityCol;
        SetGlowAlpha(rarity >= Rarity.Epic ? 0.5f : 0.28f);

        // 2. Mottled-stone socket the relic sits inside.
        Image stone = MakeChild("Stone", RelicUISprites.StonePanel(), Color.white, size * 0.80f, Vector2.zero);
        stone.type = Image.Type.Simple;

        // 3. Relic icon (+ drop shadow), or a rarity gem stand-in if art is missing.
        if (relic != null && relic.relicArt != null)
        {
            Image shadow = MakeChild("IconShadow", relic.relicArt, new Color(0f, 0f, 0f, 0.5f), size * 0.52f, new Vector2(1.5f, -1.5f));
            shadow.preserveAspect = true;
            Image icon = MakeChild("Icon", relic.relicArt, Color.white, size * 0.52f, Vector2.zero);
            icon.preserveAspect = true;
        }
        else
        {
            MakeChild("GemPlaceholderSet", RelicUISprites.GemSetting(), Color.white, size * 0.42f, Vector2.zero);
            MakeChild("GemPlaceholder", RelicUISprites.Gem(), gemCol, size * 0.42f, Vector2.zero);
        }

        // 4. Ornate gold border framing the socket.
        Image border = MakeChild("GoldBorder", RelicUISprites.GoldBorder(), Color.white, size, Vector2.zero);
        border.type = Image.Type.Simple;

        // 5. Gem bosses in the four corners (gold setting + rarity gem) — the signature Deckshift detail.
        float off = size * 0.335f, boss = size * 0.32f;
        AddGemBoss(new Vector2(-off, off), boss, gemCol);
        AddGemBoss(new Vector2(off, off), boss, gemCol);
        AddGemBoss(new Vector2(-off, -off), boss, gemCol);
        AddGemBoss(new Vector2(off, -off), boss, gemCol);

        // Start hidden and pop in via Update (works whether or not the HUD is currently visible).
        transform.localScale = Vector3.zero;
        popping = true;
        popT = 0f;
    }

    private void AddGemBoss(Vector2 pos, float size, Color gemCol)
    {
        MakeChild("Setting", RelicUISprites.GemSetting(), Color.white, size, pos);
        MakeChild("Gem", RelicUISprites.Gem(), gemCol, size * 0.62f, pos);
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
            SetGlowAlpha(0.40f + 0.20f * Mathf.Sin(Time.unscaledTime * 2.5f));
    }

    private void SetGlowAlpha(float a)
    {
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

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

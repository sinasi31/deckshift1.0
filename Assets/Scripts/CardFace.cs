using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Draws a card the way the player sees it in hand: the painted face, with its Shift cost and charge
// count sitting in the two medallions.
//
// ⚠️ THE MEDALLION NUMBERS ARE NOT PAINTED INTO THE ART. The art carries the empty gold circles; the
// digits are TMP fields in CardUI_Template. Any screen that draws `cardData.cardArt` on its own gets
// a card with two blank sockets where the cost and charges should be — which is exactly what the
// Scrap Forge and Blompo produced when they first switched to the real face, and it is why they had
// been printing SHIFT/CHARGES as separate text underneath in the first place.
//
// So the placement lives here, once, measured off the prefab, and every screen that isn't the hand
// uses it. If the card art is ever re-cut and the medallions move, this is the single place to fix.
public static class CardFace
{
    // 1024x1536 card art.
    public const float ASPECT = 1.5f;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Where the medallions sit — AS FRACTIONS OF THE SPRITE, and there are TWO ART GENERATIONS.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //
    // ⚠️ **THE TWO STYLES DO NOT PUT THEIR MEDALLIONS IN THE SAME PLACE.** This file used to carry
    // one position for both, on the assumption that "both styles put cost right / charges left, so
    // the positions hold". Measured off the sprites (blit to a RenderTexture, find the coloured blob
    // in the top band — the source textures are not import-readable), they do not:
    //
    //     classic gold sockets (fireball_0, 1024x1536)  charges (0.176, 0.909)
    //     gem     red ball     (freefallblade_0, 118x200) charges (0.218, 0.880)  cost (0.835, 0.874)
    //
    // 0.045 of a card width apart. On the gem cards the charge number sat off the left edge of the
    // red ball entirely, and got worse with every digit — which is what the designer reported as
    // "the charges look weird and bad over 10".
    //
    // ⚠️ **The generation is told apart by SPRITE ASPECT, and that is a STOPGAP.** Classic art is
    // 1024x1536 (0.667); the gem art is 118x200 (0.590). Aspect is at least a property of the art
    // FILE rather than of gameplay data — but it is still a proxy, and a third layout cut at 0.667
    // would silently take the classic positions. **When the card set is re-cut to one style, delete
    // the second entry and the chooser with it.**
    public struct Medallions { public Vector2 Uses, Cost; }

    // ⚠️ Classic keeps the ORIGINAL hand-authored values, not the measured ones (which differ by
    // 0.003/0.012 — within the noise of a blob centroid that includes the gold ring itself). These
    // have shipped and read correctly, so this change is a visual no-op on the 14 classic cards.
    private static readonly Medallions Classic = new Medallions
    {
        Cost = new Vector2(0.5f + 69.5f / 200f, 0.5f + 121.4f / 300f),
        Uses = new Vector2(0.5f - 65.4f / 200f, 0.5f + 126.4f / 300f),
    };

    private static readonly Medallions Gem = new Medallions
    {
        Cost = new Vector2(0.835f, 0.874f),
        Uses = new Vector2(0.218f, 0.880f),
    };

    /// <summary>Which generation of card art this sprite belongs to. See the stopgap note above.</summary>
    public static Medallions LayoutFor(Sprite art)
    {
        if (art == null || art.rect.height <= 0f) return Classic;
        return (art.rect.width / art.rect.height) < 0.63f ? Gem : Classic;
    }

    private const float COST_SIZE = 30f / 200f;   // font size as a fraction of the DRAWN card width
    private const float USES_SIZE = 38f / 200f;

    // ── Fitting a number to its socket ─────────────────────────────────────────────────────────
    //
    // ⚠️ **A TWO-DIGIT NUMBER IS 1.93x THE WIDTH OF A ONE-DIGIT NUMBER, and the sockets are drawn
    // for one digit.** Measured in the display face at 100pt: widest single digit "0" = 58.2px,
    // "10" = 110.8, "99" = 112.2, "100" = 176.0, "∞" = 70.9. Shuriken is the only card in the set
    // with maxUses 10, so it was the one that showed it — but nothing stops a future card, or a
    // recharged one, going higher.
    //
    // Deterministic scaling, NOT `enableAutoSizing`: auto-size settles over several frames and is
    // documented in this project as unreliable to measure, and these labels are rebuilt constantly.
    private const float GLYPH_W = 0.582f;    // widest digit, as a multiple of font size
    private const float INF_W = 0.709f;      // the infinity glyph
    private const float NUMBER_MAX_W = 0.135f;   // of the drawn card width — inside both sockets

    /// <summary>The font size at which <paramref name="text"/> still fits inside its medallion.</summary>
    public static float FitNumberSize(string text, float authoredSize, float drawnCardWidth)
    {
        if (string.IsNullOrEmpty(text) || drawnCardWidth <= 0f) return authoredSize;
        float ratio = text == "∞" ? INF_W : GLYPH_W * text.Length;
        float want = ratio * authoredSize;
        float max = NUMBER_MAX_W * drawnCardWidth;
        return want <= max ? authoredSize : authoredSize * (max / want);
    }

    /// <summary>
    /// The size the art is actually DRAWN at inside a host rect.
    ///
    /// ⚠️ **`preserveAspect` means the art is not the host.** The gem art is 0.590 where the card
    /// box is 0.667, so it letterboxes to 88.5% of the host width with bars either side — and every
    /// number stamped at a fraction of the HOST then lands outside the artwork it belongs to. This
    /// is the same letterbox `CardUI` already maps Stagger's heart and name plate through.
    /// </summary>
    public static Vector2 DrawnArtSize(Vector2 hostSize, Sprite art)
    {
        if (art == null || art.rect.height <= 0f || hostSize.x <= 0f || hostSize.y <= 0f) return hostSize;
        float artAspect = art.rect.width / art.rect.height;
        float hostAspect = hostSize.x / hostSize.y;
        return artAspect > hostAspect
             ? new Vector2(hostSize.x, hostSize.x / artAspect)      // width-limited
             : new Vector2(hostSize.y * artAspect, hostSize.y);     // height-limited
    }

    /// <summary>Offset from the host's CENTRE to a medallion, in host-local pixels.</summary>
    public static Vector2 MedallionOffset(Vector2 hostSize, Sprite art, bool cost)
    {
        Medallions m = LayoutFor(art);
        Vector2 f = cost ? m.Cost : m.Uses;
        Vector2 drawn = DrawnArtSize(hostSize, art);
        return new Vector2((f.x - 0.5f) * drawn.x, (f.y - 0.5f) * drawn.y);
    }

    /// <summary>
    /// Position and size an EXISTING medallion label against a card art image — the hand's path.
    /// `CardUI`'s `Uses_Text` / `Cost_Text` are prefab objects at authored positions, so they get
    /// moved rather than rebuilt, but through the same maths every other screen uses.
    ///
    /// ⚠️ The label must share an origin with the art image. In `CardUI_Template` both are anchored
    /// to the ROOT's centre, and the art keeps a fixed 200x300 even though the hand's layout group
    /// rewrites the root to 200x100 — so they do.
    /// </summary>
    public static void PlaceMedallion(TMP_Text label, Image artImage, bool cost)
    {
        if (label == null || artImage == null) return;

        Vector2 host = artImage.rectTransform.rect.size;
        if (host.x <= 0f || host.y <= 0f) host = artImage.rectTransform.sizeDelta;
        Vector2 drawn = DrawnArtSize(host, artImage.sprite);

        RectTransform rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = MedallionOffset(host, artImage.sprite, cost);

        label.enableWordWrapping = false;   // "10" has no break opportunity, but do not tempt it
        label.fontSize = FitNumberSize(label.text, (cost ? COST_SIZE : USES_SIZE) * drawn.x, drawn.x);
    }

    private static readonly Color COST_COLOR = new Color(0.307f, 0.304f, 0.934f, 1f);

    // The bottom name plate, same fractions CardUI uses (measured on the 118x205 Stagger sprite,
    // and the 1024x1536 cards put their plate within ~1% of the same place). CY is from the BOTTOM.
    private const float PLATE_CY = 190.5f / 205f;
    private const float PLATE_W = 96f / 118f;
    private const float PLATE_H = 16f / 118f;
    private const float NAME_SIZE = 15f / 200f;
    private static readonly Color NAME_COLOR = new Color(0.85f, 0.72f, 0.36f, 1f);

    // Fills `host` with the card. The host should already be at the card's aspect — pass a rect of
    // (w, w * CardFace.ASPECT) — or the art will letterbox inside it.
    public static void Build(RectTransform host, RuntimeCard card)
    {
        if (host == null || card == null || card.cardData == null) return;

        Vector2 hostSize = host.rect.size;
        if (hostSize.x <= 0f || hostSize.y <= 0f) hostSize = host.sizeDelta;

        // ⚠️ Everything below measures against the DRAWN art, not the host — see DrawnArtSize.
        Vector2 drawn = DrawnArtSize(hostSize, card.cardData.cardArt);
        float w = drawn.x;

        if (card.cardData.cardArt != null)
        {
            Image art = AddImage(host, "Face", card.cardData.cardArt);
            art.preserveAspect = true;
            Stretch(art.rectTransform);
        }

        // The title, for art that ships with an empty plate — the standing convention for new cards
        // (see CardData.nameIsPaintedIntoArt). Without this those cards render with a blank bar,
        // which is what they were doing here and in the hand.
        if (!card.cardData.nameIsPaintedIntoArt && !string.IsNullOrEmpty(card.cardData.cardName))
        {
            TextMeshProUGUI n = AddNumberLayer(host, "Name",
                                               new Vector2(0f, (0.5f - PLATE_CY) * drawn.y),
                                               NAME_SIZE * w, card.cardData.cardName.ToUpperInvariant(),
                                               NAME_COLOR);
            n.rectTransform.sizeDelta = new Vector2(w * PLATE_W, w * PLATE_H);
            n.enableAutoSizing = true;
            n.fontSizeMin = NAME_SIZE * w * 0.5f;
            n.fontSizeMax = NAME_SIZE * w;
        }

        // Stagger's art has NO corner medallions — it carries a single heart on the top edge — so
        // stamping numbers into those positions would print them onto bare artwork. CardUI hides
        // them for the same reason.
        if (card.cardData.actionType == CardActionType.Stagger) return;

        string cost = card.cardData.shiftCost.ToString();
        AddNumber(host, "Cost", MedallionOffset(hostSize, card.cardData.cardArt, true),
                  FitNumberSize(cost, COST_SIZE * w, w), cost, COST_COLOR);

        string charges = card.isInfinite ? "∞" : card.currentUses.ToString();
        Color chargeCol = (!card.isInfinite && card.currentUses <= 1) ? Color.red : Color.white;
        AddNumber(host, "Charges", MedallionOffset(hostSize, card.cardData.cardArt, false),
                  FitNumberSize(charges, USES_SIZE * w, w), charges, chargeCol);
    }

    // ⚠️ EVERY NUMBER GETS A DARK SHADOW, because the set currently has TWO art styles and a digit
    // that reads on one vanishes on the other. The older cards socket their medallions in dark gold
    // circles, where the Shift blue is crisp; the newer ones (Dead Weight, Freefall Blade, Glass
    // Parry) use a red ball and a BLUE CRYSTAL — and a blue digit on the blue crystal is invisible.
    // Keeping the hand's colours and adding a shadow fixes it without inventing a third palette.
    private static readonly Vector2[] OUTLINE =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f),
    };

    private static TextMeshProUGUI AddNumber(RectTransform parent, string name, Vector2 offset, float size,
                                             string text, Color color)
    {
        // A KEYLINE, not a drop shadow. A single offset copy was not enough: the Shift digit is
        // blue, the new art's cost medallion is a blue CRYSTAL, and at ~10px on screen the digit
        // simply disappeared into it — a one-sided shadow leaves most of the glyph edge unlit.
        // Four copies ring it and it reads on any medallion, old socket or new gem.
        //
        // ⚠️ The ring SCALES with the font. It used to be a flat 1.6px, which is a heavy outline on
        // a 17px number in the character select and invisible on an 84px one in the Scrap Forge.
        float ring = Mathf.Max(1.2f, size * 0.045f);
        for (int i = 0; i < OUTLINE.Length; i++)
            AddNumberLayer(parent, name + "Edge" + i, offset + OUTLINE[i] * ring, size, text,
                           new Color(0f, 0f, 0f, 0.85f));

        return AddNumberLayer(parent, name, offset, size, text, color);
    }

    private static TextMeshProUGUI AddNumberLayer(RectTransform parent, string name, Vector2 offset,
                                                  float size, string text, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size * 3.2f, size * 1.6f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = UIType.Display();
        if (f != null) t.font = f;
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

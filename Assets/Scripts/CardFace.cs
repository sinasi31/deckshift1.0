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

    // Measured from CardUI_Template (a 200x300 rect, centre origin) and expressed as fractions so
    // they hold at any chip size:
    //     Cost_Text  anchoredPosition (69.5, 121.4)
    //     Uses_Text  anchoredPosition (-65.4, 126.4)
    private static readonly Vector2 COST_POS = new Vector2(0.5f + 69.5f / 200f, 0.5f + 121.4f / 300f);
    private static readonly Vector2 USES_POS = new Vector2(0.5f - 65.4f / 200f, 0.5f + 126.4f / 300f);
    private const float COST_SIZE = 30f / 200f;   // font size as a fraction of card WIDTH
    private const float USES_SIZE = 38f / 200f;

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

        float w = host.rect.width;
        if (w <= 0f) w = host.sizeDelta.x;

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
            TextMeshProUGUI n = AddNumberLayer(host, "Name", new Vector2(0.5f, 1f - PLATE_CY),
                                               NAME_SIZE * w, card.cardData.cardName.ToUpperInvariant(),
                                               NAME_COLOR, Vector2.zero);
            n.rectTransform.sizeDelta = new Vector2(w * PLATE_W, w * PLATE_H);
            n.enableAutoSizing = true;
            n.fontSizeMin = NAME_SIZE * w * 0.5f;
            n.fontSizeMax = NAME_SIZE * w;
        }

        // Stagger's art has NO corner medallions — it carries a single heart on the top edge — so
        // stamping numbers into those positions would print them onto bare artwork. CardUI hides
        // them for the same reason.
        if (card.cardData.actionType == CardActionType.Stagger) return;

        AddNumber(host, "Cost", COST_POS, COST_SIZE * w,
                  card.cardData.shiftCost.ToString(), COST_COLOR);

        string charges = card.isInfinite ? "∞" : card.currentUses.ToString();
        Color chargeCol = (!card.isInfinite && card.currentUses <= 1) ? Color.red : Color.white;
        AddNumber(host, "Charges", USES_POS, USES_SIZE * w, charges, chargeCol);
    }

    // ⚠️ EVERY NUMBER GETS A DARK SHADOW, because the set currently has TWO art styles and a digit
    // that reads on one vanishes on the other. The older cards socket their medallions in dark gold
    // circles, where the Shift blue is crisp; the newer ones (Dead Weight, Freefall Blade, Glass
    // Parry) use a red ball and a BLUE CRYSTAL — and a blue digit on the blue crystal is invisible.
    // Keeping the hand's colours and adding a shadow fixes it without inventing a third palette.
    private static readonly Vector2[] OUTLINE =
    {
        new Vector2(-1.6f, 0f), new Vector2(1.6f, 0f),
        new Vector2(0f, -1.6f), new Vector2(0f, 1.6f),
    };

    private static TextMeshProUGUI AddNumber(RectTransform parent, string name, Vector2 anchor, float size,
                                             string text, Color color)
    {
        // A KEYLINE, not a drop shadow. A single offset copy was not enough: the Shift digit is
        // blue, the new art's cost medallion is a blue CRYSTAL, and at ~10px on screen the digit
        // simply disappeared into it — a one-sided shadow leaves most of the glyph edge unlit.
        // Four copies ring it and it reads on any medallion, old socket or new gem.
        for (int i = 0; i < OUTLINE.Length; i++)
            AddNumberLayer(parent, name + "Edge" + i, anchor, size, text,
                           new Color(0f, 0f, 0f, 0.85f), OUTLINE[i]);

        return AddNumberLayer(parent, name, anchor, size, text, color, Vector2.zero);
    }

    private static TextMeshProUGUI AddNumberLayer(RectTransform parent, string name, Vector2 anchor,
                                                  float size, string text, Color color, Vector2 nudge)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = nudge;
        rt.sizeDelta = new Vector2(size * 2.4f, size * 1.6f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = FlatUI.UIFont();
        if (f != null) t.font = f;
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
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

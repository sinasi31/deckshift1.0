using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single shared tooltip for the relic loadout bar: name + description + sell value,
// shown on hover under the hovered slot. Built procedurally (house style, RelicUISprites)
// and reused across every slot — one instance, repositioned per hover.
public class RelicTooltip : MonoBehaviour
{
    private CanvasGroup group;
    private RectTransform rt;
    private Image frame;
    private TMP_Text nameText, descText, valueText;

    private const float WIDTH = 244f;
    private const float GAP = 10f;   // distance below the hovered slot

    public void Build(TMP_FontAsset font)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 1f);              // hangs down from an anchor point
        rt.sizeDelta = new Vector2(WIDTH, 100f);

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Dark stone background (this object's own Image).
        Image bg = gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.StonePanel();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.55f, 0.53f, 0.58f, 0.97f);   // dims the baked stone to a dark tooltip
        bg.raycastTarget = false;

        // Vertical content layout, height driven by a ContentSizeFitter. Padding clears the gold border.
        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 14, 14);
        vlg.spacing = 5f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        nameText  = MakeText(font, 20f, FontStyles.Bold, Color.white);
        descText  = MakeText(font, 15f, FontStyles.Normal, new Color(0.82f, 0.84f, 0.9f));
        valueText = MakeText(font, 15f, FontStyles.Bold, new Color(1f, 0.84f, 0.34f));

        // Rarity-tinted border on top (ignored by the layout, stretched to fill).
        GameObject frameGo = new GameObject("Frame", typeof(RectTransform));
        RectTransform frt = frameGo.GetComponent<RectTransform>();
        frt.SetParent(transform, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        LayoutElement le = frameGo.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        frame = frameGo.AddComponent<Image>();
        frame.sprite = RelicUISprites.GoldBorder();
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 2.2f;   // thin the ornate border down for a small tooltip
        frame.color = Color.white;
        frame.raycastTarget = false;

        gameObject.SetActive(false);
    }

    private TMP_Text MakeText(TMP_FontAsset font, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    // Positions the tooltip just below the given slot and fills it with the relic's info.
    public void Show(RelicData relic, RectTransform slot)
    {
        if (relic == null || slot == null) { Hide(); return; }

        nameText.text = string.IsNullOrEmpty(relic.relicName) ? relic.relicID : relic.relicName;
        nameText.color = RelicUISprites.RarityColor(relic.rarity);
        descText.text = string.IsNullOrEmpty(relic.description) ? "-" : relic.description;

        int value = RelicManager.instance != null ? RelicManager.instance.SellValueFor(relic) : 0;
        valueText.text = $"Sell: {value} gold";
        // Border stays gold (Deckshift chrome); rarity reads through the name colour above.

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Anchor to the slot's bottom-centre (canvas-space agnostic — corners and
        // transform.position share the same space).
        Vector3[] corners = new Vector3[4];
        slot.GetWorldCorners(corners); // 0=BL 1=TL 2=TR 3=BR
        Vector3 bottomCentre = (corners[0] + corners[3]) * 0.5f;
        transform.position = bottomCentre + Vector3.down * GAP;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}

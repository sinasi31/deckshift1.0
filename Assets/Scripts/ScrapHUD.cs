using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The scrap counter.
//
// PLACEMENT IS DELIBERATE: scrap sits with the DECK/EXHAUST pile buttons, not in the resource
// panel with HP / Shift / gold. Scrap is a deck-maintenance currency, not a survival one, and
// where a number lives on screen teaches the player what it's for. It also keeps the survival
// panel at three values, which is where the "too many resources to track" worry actually bites.
//
// Self-bootstrapping via RuntimeInitializeOnLoadMethod: there is nothing to drag into a scene and
// therefore nothing that can go missing from one. ("The system exists in code but isn't in the
// scene" is this project's most recurrent bug — see CLAUDE.md, Common Pitfalls.)
//
// It positions itself RELATIVE to the existing ExhaustPile button (copying its anchors and sitting
// just above it) rather than at hardcoded screen coordinates, so it follows that button wherever
// the HUD is laid out. If no ExhaustPile is found it falls back to a bottom-left corner slot.
public class ScrapHUD : MonoBehaviour
{
    private TMP_Text countText;
    private PlayerController player;

    private const float CHIP_W = 132f, CHIP_H = 44f;
    private const float ABOVE_EXHAUST = 56f;   // vertical gap above the ExhaustPile button

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // The HUD only belongs in gameplay scenes. GameplayHUD is the marker for those — menus
        // and the game-over scene don't have one, so the counter simply doesn't appear there.
        GameObject hud = GameObject.Find("GameplayHUD");
        if (hud == null) return;
        if (hud.GetComponentInChildren<ScrapHUD>(true) != null) return;

        GameObject go = new GameObject("ScrapHUD", typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);
        go.AddComponent<ScrapHUD>().Build();
    }

    private void Build()
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(CHIP_W, CHIP_H);
        PositionNearExhaustPile(rt);

        // Flat iron chip, matching the Scrap Forge screen this currency is spent on. (It was
        // originally gold-on-stone like the old chrome, which left it as the one piece of the
        // scrap system still speaking the old visual language.)
        Image bg = gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = FlatUI.Surface;
        bg.raycastTarget = false;

        Image frame = AddImage(transform, "Frame", FlatUI.Outline(5, 1), FlatUI.Border);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        // Shard icon, reusing the exact pickup sprite so the HUD number and the thing on the floor
        // are visibly the same currency.
        Image icon = AddImage(transform, "Icon", ScrapPickup.ShardSprite, Color.white);
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(9f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(28f, 28f);

        GameObject textGO = new GameObject("Count", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(42f, 0f); trt.offsetMax = new Vector2(-12f, 0f);

        countText = textGO.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = FlatUI.UIFont();
        if (f != null) countText.font = f;
        countText.fontSize = 26f;
        countText.fontStyle = FontStyles.Bold;
        countText.color = ScrapEconomy.ScrapColor;
        countText.alignment = TextAlignmentOptions.Left;
        countText.raycastTarget = false;
        countText.text = "0";
    }

    private void PositionNearExhaustPile(RectTransform rt)
    {
        RectTransform pile = FindExhaustPile();
        if (pile != null)
        {
            // Sibling of the pile button (not a child — a child would inherit its Button raycast
            // area and its active state), sharing its anchors so it tracks the same HUD corner.
            rt.SetParent(pile.parent, false);
            rt.anchorMin = pile.anchorMin;
            rt.anchorMax = pile.anchorMax;
            rt.pivot = pile.pivot;
            rt.anchoredPosition = pile.anchoredPosition + new Vector2(0f, pile.rect.height * 0.5f + ABOVE_EXHAUST);
            rt.sizeDelta = new Vector2(CHIP_W, CHIP_H);
            return;
        }

        // Fallback: bottom-left, clear of the centre hand drawer.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(28f, 150f);
    }

    private RectTransform FindExhaustPile()
    {
        foreach (RectTransform t in transform.root.GetComponentsInChildren<RectTransform>(true))
            if (t != null && t.name == "ExhaustPile") return t;
        return null;
    }

    private void Start() => Resolve();

    private void Update()
    {
        // The player can be replaced (respawn / restart), so keep trying until a live one is bound.
        if (player == null) Resolve();
    }

    private void Resolve()
    {
        PlayerController found = GameManager.instance != null ? GameManager.instance.player : null;
        if (found == null) found = FindFirstObjectByType<PlayerController>();
        if (found == null || found == player) return;

        if (player != null) player.OnScrapChanged -= SetCount;
        player = found;
        player.OnScrapChanged += SetCount;
        SetCount(player.currentScrap);
    }

    private void OnDestroy()
    {
        if (player != null) player.OnScrapChanged -= SetCount;
    }

    private void SetCount(int amount)
    {
        if (countText != null) countText.text = amount.ToString();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }
}

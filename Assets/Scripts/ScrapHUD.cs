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

    // Chip size and plate now come from HudChip, shared with the gold counter above it.

    // ⚠️ Registered through SceneBootstrap, NOT called directly — RuntimeInitializeOnLoadMethod
    // fires once per play session, so the counter disappeared permanently the first time the player
    // died and restarted (two scene loads). See SceneBootstrap.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneBootstrap.Register(Create);
    }

    private static void Create()
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
        rt.sizeDelta = new Vector2(HudChip.Width, HudChip.Height);
        PositionUnderGold(rt);

        // The plate is built by HudChip so this counter and the gold counter above it are the same
        // object in two colours — they cannot drift apart the way they had.
        HudChip.Build(rt);

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

    // Sits DIRECTLY BELOW THE GOLD COUNTER, as the fourth row of the resource panel.
    //
    // ⚠️ It used to live bottom-right, above the ExhaustPile button, on the reasoning that scrap is
    // a deck-maintenance currency and belongs with the deck UI rather than with the survival stats.
    // The designer overruled that (2026-08-09): in play it read as a stray widget in a corner, and
    // the two CURRENCIES being in opposite corners of the screen made neither easy to check. Gold
    // and scrap are both "how much do I have to spend", so they stack.
    private void PositionUnderGold(RectTransform rt)
    {
        RectTransform gold = FindGoldDisplay();
        if (gold != null)
        {
            // Sibling of the gold readout, sharing its anchors so both track the same panel corner.
            rt.SetParent(gold.parent, false);
            rt.anchorMin = gold.anchorMin;
            rt.anchorMax = gold.anchorMax;
            rt.pivot = gold.pivot;
            // Gold's pivot is top-left in a top-anchored panel, so "below" is -Y.
            rt.anchoredPosition = gold.anchoredPosition + new Vector2(0f, -(HudChip.Height + HudChip.RowGap));
            rt.sizeDelta = new Vector2(HudChip.Width, HudChip.Height);
            return;
        }

        // Fallback: top-left under where the panel would be, rather than a far corner — a missing
        // gold display should not exile the counter to the other side of the screen.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(18f, -170f);
        rt.sizeDelta = new Vector2(HudChip.Width, HudChip.Height);
    }

    private RectTransform FindGoldDisplay()
    {
        // Ask the panel that owns the layout, so the two stay together if gold is ever moved.
        var panel = Object.FindFirstObjectByType<ResourcePanelHUD>(FindObjectsInactive.Include);
        if (panel != null && panel.goldDisplay != null)
            return panel.goldDisplay.GetComponent<RectTransform>();

        foreach (RectTransform t in transform.root.GetComponentsInChildren<RectTransform>(true))
            if (t != null && t.name == "GoldDisplay") return t;
        return null;
    }

    private void Start() => Resolve();

    // ⚠️ RE-ANCHOR IN LateUpdate, DON'T SNAPSHOT ONCE IN Build().
    //
    // The chip is created from a sceneLoaded bootstrap, which runs BEFORE any Start(). At that
    // moment ResourcePanelHUD has not laid the gold row out yet, so Build() read the gold rect's
    // pre-layout position and parked the counter in the wrong place. Script execution order between
    // two Start()s is undefined, so "just do it in Start" would be a coin flip.
    //
    // Following the gold rect every frame instead is exact whenever the layout runs, and keeps the
    // pair locked together if the panel is ever re-laid out at runtime. It is two Vector2 compares.
    private Vector2 lastGoldPos = new Vector2(float.NaN, float.NaN);

    private void LateUpdate()
    {
        RectTransform gold = FindGoldDisplay();
        if (gold == null) return;
        if (gold.anchoredPosition == lastGoldPos) return;

        lastGoldPos = gold.anchoredPosition;
        PositionUnderGold(GetComponent<RectTransform>());
    }

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

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }
}

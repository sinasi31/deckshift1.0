using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Scrap Forge screen — where scrap turns back into playable cards.
//
// Two operations, deliberately kept distinct because they mean different things:
//   REPAIR  — top a card you still own back up to full charges.
//   SALVAGE — drag a card back out of the exhaust pile. Costs more and returns it only half
//             charged, so exhaust still stings; a full recovery is salvage + repair.
//
// The screen only ever lists cards something can actually be DONE to (missing charges / in
// exhaust). Showing the whole deck would be a wall of full-charge cards with no affordance, and
// the player would have to hunt for the two that matter.
//
// Purchases are select-then-confirm rather than click-to-buy. At 30 scrap a salvage is roughly a
// third of an act's income, which is too expensive to lose to a misclick.
//
// Built procedurally in the shared Deckshift chrome (RelicUISprites), self-instantiating under the
// main Canvas — same pattern as BlompoScreen / RelicManagePanel, so there is nothing to wire in a
// scene and nothing that can go missing from one.
public class ScrapForgeScreen : MonoBehaviour
{
    public static ScrapForgeScreen instance;

    private enum Mode { Repair, Salvage }

    private CanvasGroup group;
    private RectTransform window;
    private Transform repairRow, salvageRow;
    private TMP_Text titleText, scrapText, repairLabel, salvageLabel, confirmLabel;
    private RectTransform confirmBar;
    private Button confirmButton;
    private Image confirmBg;
    private TMP_FontAsset font;

    private RuntimeCard selected;
    private Mode selectedMode;
    private bool isOpen;

    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    private const float WIN_W = 1600f, WIN_H = 900f;
    private const float ROW_W = WIN_W - 120f, ROW_H = 250f;
    private const float CARD_W_MAX = 170f, CARD_H_MAX = 250f, CARD_GAP = 20f;

    // ---- entry point --------------------------------------------------------------------------

    public static void Open()
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.Show();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("ScrapForgeScreen: no Canvas found in scene."); return; }
        GameObject go = new GameObject("ScrapForgeScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<ScrapForgeScreen>();
        instance.Build();
    }

    private static Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    // ---- construction -------------------------------------------------------------------------

    private void Build()
    {
        font = ResolveFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = AddImage(transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.88f), true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = RelicUISprites.StonePanel();
        winBg.type = Image.Type.Sliced;
        winBg.color = new Color(0.8f, 0.78f, 0.82f, 1f);
        winBg.raycastTarget = true;
        Image winFrame = AddImage(window, "Frame", RelicUISprites.GoldBorder(), Color.white, false);
        winFrame.type = Image.Type.Sliced;
        Stretch(winFrame.rectTransform);
        RelicUISprites.AddGemStuds(window, WIN_W, WIN_H, ScrapEconomy.ScrapColor, 60f, true, false);

        titleText = AddText(window, "Title", new Vector2(0f, 1f), new Vector2(56f, -38f), new Vector2(760f, 76f),
            "THE FORGE", 60f, FontStyles.Bold, new Color(0.98f, 0.86f, 0.55f), TextAlignmentOptions.TopLeft);

        // Right edge sits clear of the close button's 62px gem (which occupies the top-right stud
        // slot) — at -56 the counter's last digit was drawn underneath it.
        scrapText = AddText(window, "Scrap", new Vector2(1f, 1f), new Vector2(-106f, -44f), new Vector2(560f, 64f),
            "", 42f, FontStyles.Bold, ScrapEconomy.ScrapColor, TextAlignmentOptions.TopRight);

        // Close button occupies the top-right gem-stud position (AddGemStuds skips it).
        BuildCloseButton();

        repairLabel = AddText(window, "RepairLabel", new Vector2(0f, 1f), new Vector2(60f, -130f), new Vector2(1200f, 38f),
            "", 27f, FontStyles.Bold, new Color(0.88f, 0.89f, 0.93f), TextAlignmentOptions.TopLeft);
        repairRow = BuildRow("RepairRow", -170f);

        salvageLabel = AddText(window, "SalvageLabel", new Vector2(0f, 1f), new Vector2(60f, -442f), new Vector2(1200f, 38f),
            "", 27f, FontStyles.Bold, new Color(0.88f, 0.89f, 0.93f), TextAlignmentOptions.TopLeft);
        salvageRow = BuildRow("SalvageRow", -482f);

        BuildConfirmBar();

        gameObject.SetActive(false);
    }

    private void BuildCloseButton()
    {
        const float sz = 62f;
        RectTransform rt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-sz * 0.45f, -sz * 0.45f), new Vector2(sz, sz));
        Image set = AddImage(rt, "Setting", RelicUISprites.GemSetting(), Color.white, false);
        Stretch(set.rectTransform);
        Image gem = AddImage(rt, "Gem", RelicUISprites.Gem(), new Color(0.86f, 0.24f, 0.26f), false);
        gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        gem.rectTransform.sizeDelta = new Vector2(sz * 0.60f, sz * 0.60f);
        Image hit = AddImage(rt, "Hit", null, new Color(0f, 0f, 0f, 0f), true);
        Stretch(hit.rectTransform);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        btn.onClick.AddListener(Hide);
        AddText(rt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz, sz),
            "X", 26f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
    }

    // Rows use a centred HorizontalLayoutGroup and size their cards to fit, so any number of
    // entries stays centred and on-screen without a scroll view.
    private Transform BuildRow(string name, float y)
    {
        RectTransform rt = AddPoint(window, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ROW_W, ROW_H));
        rt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = CARD_GAP;
        // Left-aligned, indented to line up under the section label. Centring the cards instead
        // left the whole left half of the window empty, which read as a broken layout when only
        // one or two cards were listed.
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.padding = new RectOffset(4, 0, 0, 0);
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    private void BuildConfirmBar()
    {
        confirmBar = AddPoint(window, "ConfirmBar", new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(900f, 76f));
        confirmBar.pivot = new Vector2(0.5f, 0f);

        confirmBg = confirmBar.gameObject.AddComponent<Image>();
        confirmBg.sprite = RelicUISprites.StonePanel();
        confirmBg.type = Image.Type.Sliced;
        confirmBg.color = new Color(0.72f, 0.70f, 0.74f, 1f);
        confirmBg.raycastTarget = true;

        Image frame = AddImage(confirmBar, "Frame", RelicUISprites.GoldBorder(), Color.white, false);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.1f;
        Stretch(frame.rectTransform);

        confirmLabel = AddText(confirmBar, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 70f),
            "", 30f, FontStyles.Bold, new Color(0.97f, 0.93f, 0.85f), TextAlignmentOptions.Center);

        confirmButton = confirmBar.gameObject.AddComponent<Button>();
        confirmButton.transition = Selectable.Transition.None;
        confirmButton.targetGraphic = confirmBg;
        confirmButton.onClick.AddListener(Commit);
    }

    // ---- open / close -------------------------------------------------------------------------

    private void Show()
    {
        isOpen = true;
        selected = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        prevState = GameManager.instance != null ? GameManager.instance.currentState : GameState.Playing;
        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        Refresh();

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(prevState);
        }
        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    private IEnumerator OpenAnim()
    {
        float t = 0f; const float dur = 0.22f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * (0.92f + 0.08f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    // ---- content ------------------------------------------------------------------------------

    private void Refresh()
    {
        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        int scrap = player != null ? player.currentScrap : 0;

        scrapText.text = $"SCRAP  {scrap}";

        ClearRow(repairRow);
        ClearRow(salvageRow);

        List<RuntimeCard> damaged = CollectDamaged();
        List<RuntimeCard> exhausted = CollectExhausted();

        repairLabel.text = damaged.Count > 0
            ? $"REPAIR  —  {ScrapEconomy.RECHARGE_PER_CHARGE} scrap per charge"
            : "REPAIR  —  every card you own is at full charges";
        salvageLabel.text = exhausted.Count > 0
            ? $"SALVAGE  —  {ScrapEconomy.SALVAGE_COST} scrap, returns half charged"
            : "SALVAGE  —  nothing has burned out yet";

        BuildCards(repairRow, damaged, Mode.Repair, scrap);
        BuildCards(salvageRow, exhausted, Mode.Salvage, scrap);

        UpdateConfirmBar(scrap);
    }

    private void BuildCards(Transform row, List<RuntimeCard> cards, Mode mode, int scrap)
    {
        if (cards.Count == 0) return;

        // Shrink to fit rather than overflow — a deck with a lot of damaged cards still lays out
        // on one centred line.
        float w = Mathf.Min(CARD_W_MAX, (ROW_W - CARD_GAP * (cards.Count - 1)) / cards.Count);
        float h = Mathf.Min(CARD_H_MAX, w / CARD_W_MAX * CARD_H_MAX);

        foreach (RuntimeCard card in cards)
        {
            int cost = mode == Mode.Repair ? ScrapEconomy.RechargeCost(card) : ScrapEconomy.SALVAGE_COST;
            BuildCardChip(row, card, mode, w, h, cost, scrap >= cost);
        }
    }

    private void BuildCardChip(Transform parent, RuntimeCard card, Mode mode, float w, float h, int cost, bool affordable)
    {
        RectTransform rt = AddPoint(parent, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;

        bool isSelected = card == selected;

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.StonePanel();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.9f, 0.87f, 0.82f, 1f);
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", RelicUISprites.GoldBorder(), isSelected ? Color.white : new Color(0.78f, 0.76f, 0.72f), false);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.1f;
        Stretch(frame.rectTransform);

        if (isSelected)
        {
            Image glow = AddImage(rt, "Glow", RelicUISprites.Glow(), new Color(ScrapEconomy.ScrapColor.r, ScrapEconomy.ScrapColor.g, ScrapEconomy.ScrapColor.b, 0.5f), false);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-18f, -18f);
            glow.rectTransform.offsetMax = new Vector2(18f, 18f);
            glow.transform.SetAsFirstSibling();
        }

        if (card.cardData != null && card.cardData.cardArt != null)
        {
            Image art = AddImage(rt, "Art", card.cardData.cardArt, Color.white, false);
            art.preserveAspect = true;
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            art.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.24f);
            art.rectTransform.sizeDelta = new Vector2(w - 46f, w - 46f);
        }

        TMP_Text nameText = AddText(rt, "Name", new Vector2(0.5f, 0f), new Vector2(0f, h * 0.30f), new Vector2(w - 18f, 50f),
            card.cardData != null ? card.cardData.cardName : "?", 20f, FontStyles.Bold,
            new Color(0.97f, 0.93f, 0.85f), TextAlignmentOptions.Bottom);
        nameText.enableWordWrapping = true;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 13f; nameText.fontSizeMax = 20f;

        // Charges: current out of max, so the player can see exactly what they're buying back.
        int max = card.cardData != null ? card.cardData.maxUses : 0;
        string charges = card.isInfinite ? "∞" : $"{card.currentUses}/{max}";
        AddText(rt, "Charges", new Vector2(0.5f, 0f), new Vector2(0f, h * 0.16f), new Vector2(w - 18f, 32f),
            charges, 22f, FontStyles.Bold, new Color(0.65f, 0.86f, 1f), TextAlignmentOptions.Center);

        AddText(rt, "Cost", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(w - 18f, 34f),
            $"{cost}", 26f, FontStyles.Bold,
            affordable ? ScrapEconomy.ScrapColor : new Color(0.55f, 0.34f, 0.30f), TextAlignmentOptions.Center);

        if (!affordable) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;
        btn.interactable = affordable;
        RuntimeCard captured = card;
        Mode capturedMode = mode;
        btn.onClick.AddListener(() => OnCardClicked(captured, capturedMode));
    }

    private void OnCardClicked(RuntimeCard card, Mode mode)
    {
        // Clicking the selected card again deselects it.
        if (selected == card) selected = null;
        else { selected = card; selectedMode = mode; }
        Refresh();
    }

    private void UpdateConfirmBar(int scrap)
    {
        if (selected == null)
        {
            confirmLabel.text = "Pick a card to work on";
            confirmLabel.color = new Color(0.72f, 0.72f, 0.76f);
            confirmBg.color = new Color(0.55f, 0.53f, 0.57f, 1f);
            confirmButton.interactable = false;
            return;
        }

        string cardName = selected.cardData != null ? selected.cardData.cardName : "?";
        int cost = selectedMode == Mode.Repair ? ScrapEconomy.RechargeCost(selected) : ScrapEconomy.SALVAGE_COST;
        bool canAfford = scrap >= cost;

        if (selectedMode == Mode.Repair)
        {
            int max = selected.cardData != null ? selected.cardData.maxUses : 0;
            confirmLabel.text = $"REPAIR  {cardName}  →  {max}/{max} charges     —     {cost} scrap";
        }
        else
        {
            confirmLabel.text = $"SALVAGE  {cardName}  →  back in your deck at " +
                                $"{ScrapEconomy.SalvageCharges(selected)}/{(selected.cardData != null ? selected.cardData.maxUses : 0)}     —     {cost} scrap";
        }

        confirmLabel.color = canAfford ? new Color(0.97f, 0.93f, 0.85f) : new Color(0.75f, 0.55f, 0.52f);
        confirmBg.color = canAfford ? new Color(0.72f, 0.70f, 0.74f, 1f) : new Color(0.5f, 0.46f, 0.46f, 1f);
        confirmButton.interactable = canAfford;
    }

    private void Commit()
    {
        if (selected == null || DeckManager.instance == null) return;

        bool ok = selectedMode == Mode.Repair
            ? DeckManager.instance.TryRechargeCard(selected)
            : DeckManager.instance.TrySalvageCard(selected);

        if (ok)
        {
            SfxManager.PlayAtPoint(ProcSfx.ScrapPickup, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.7f);
            selected = null;
        }

        Refresh();
    }

    // Every card the player still owns and could put charges back onto. Exhausted cards are
    // excluded here — they belong to the salvage row, and must be rescued before they can be
    // repaired.
    private List<RuntimeCard> CollectDamaged()
    {
        List<RuntimeCard> result = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return result;

        foreach (RuntimeCard c in d.GetCurrentHand()) if (ScrapEconomy.MissingCharges(c) > 0) result.Add(c);
        foreach (RuntimeCard c in d.GetDrawPile()) if (ScrapEconomy.MissingCharges(c) > 0) result.Add(c);
        foreach (RuntimeCard c in d.GetDiscardPile()) if (ScrapEconomy.MissingCharges(c) > 0) result.Add(c);
        return result;
    }

    private List<RuntimeCard> CollectExhausted()
    {
        List<RuntimeCard> result = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return result;
        result.AddRange(d.GetExhaustPile());
        return result;
    }

    private void ClearRow(Transform row)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
    }

    // ---- small UGUI builders (house style) ----------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false; t.richText = true;
        return t;
    }

    private TMP_FontAsset ResolveFont() => ScrapEconomy.UIFont();

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

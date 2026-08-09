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
// Built procedurally in the FlatUI theme — an iron plate on a workbench, lit by the forge below.
// Self-instantiating under the main Canvas, same pattern as BlompoScreen / RelicManagePanel, so
// there is nothing to wire in a scene and nothing that can go missing from one.
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
    private Image confirmBg, confirmOutline;
    private TMP_FontAsset font;

    private RuntimeCard selected;
    private Mode selectedMode;
    private bool isOpen;

    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    // Deliberately smaller than the old 1600x900. At that size four cards floated in a huge empty
    // field, which is most of why the screen felt unfinished; a tighter window reads as a focused
    // dialog instead.
    //
    // HEIGHT IS DYNAMIC (see LayoutSections): a section with no cards collapses to a single line
    // of explanatory text, and the window shrinks to match. Early in a run nothing is damaged and
    // nothing is exhausted, so the all-empty state is common and must not be a tall grey void.
    private const float WIN_W = 1180f;
    private const float PAD = 44f;
    private const float ROW_W = WIN_W - PAD * 2f, ROW_H = 205f, EMPTY_ROW_H = 38f;
    private const float CARD_W_MAX = 140f, CARD_H_MAX = 205f, CARD_GAP = 16f;

    // Vertical rhythm, all measured downward from the window's top edge.
    private const float HEADER_RULE_Y = 96f;   // hairline under the title
    private const float LABEL_GAP = 22f;       // rule -> section label
    private const float ROW_GAP = 36f;         // label -> cards
    private const float SECTION_GAP = 26f;     // cards -> next hairline
    private const float BAR_GAP = 34f;         // last cards -> confirm bar
    private const float BAR_H = 58f, BOTTOM_PAD = 30f;

    private RectTransform sectionRule, confirmBarRT, repairRowRT, salvageRowRT;

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

        Image backdrop = AddImage(transform, "Backdrop", null, FlatUI.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, 600f));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel(10);
        winBg.type = Image.Type.Sliced;
        winBg.color = FlatUI.Surface;
        winBg.raycastTarget = true;

        // Forge fire under the bench: a warm wash rising off the BOTTOM edge. Flipped by scaling
        // Y negative about a centred pivot, so the fade's opaque end sits at the bottom.
        // Kept low: at 0.085 over 140px this was an orange wash owning the bottom third of the
        // panel and reading as "glowing UI". It should be firelight you notice only if you look.
        //
        // Uses BottomGlow, which falls off horizontally as well as vertically. The earlier version
        // reused VerticalFade inset from the sides, and because that sprite has hard left/right
        // ends it drew a visible vertical seam down both edges of the window.
        Image ember = AddImage(window, "Ember", FlatUI.BottomGlow(),
            new Color(FlatUI.Ember.r, FlatUI.Ember.g, FlatUI.Ember.b, 0.055f), false);
        ember.rectTransform.anchorMin = new Vector2(0f, 0f);
        ember.rectTransform.anchorMax = new Vector2(1f, 0f);
        ember.rectTransform.pivot = new Vector2(0.5f, 0f);
        ember.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        ember.rectTransform.sizeDelta = new Vector2(-6f, 120f);

        // Embers drifting up and to the left — the forge breathing behind the work surface. Added
        // here so it sits above the glow but beneath every label, card and button added later.
        UIEmberField.Attach(window, 18, new Color(1f, 0.62f, 0.30f, 1f), UIEmberField.Settings.Embers);

        Image winFrame = AddImage(window, "Frame", FlatUI.Outline(10, 2), FlatUI.Border, false);
        winFrame.type = Image.Type.Sliced;
        Stretch(winFrame.rectTransform);

        // Light catches the TOP LIP only. A uniformly bright border reads as a UI widget; light
        // coming from one direction reads as a physical plate sitting on a bench.
        Image lip = AddImage(window, "TopLip", FlatUI.Pixel(), FlatUI.EdgeLight, false);
        lip.rectTransform.anchorMin = new Vector2(0f, 1f);
        lip.rectTransform.anchorMax = new Vector2(1f, 1f);
        lip.rectTransform.pivot = new Vector2(0.5f, 1f);
        lip.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        lip.rectTransform.sizeDelta = new Vector2(-26f, 1f);   // inset clear of the cut corners

        AddWear();
        AddRivets();

        titleText = AddText(window, "Title", new Vector2(0f, 1f), new Vector2(PAD, -34f), new Vector2(600f, 52f),
            "THE FORGE", 38f, FontStyles.Bold, FlatUI.TextBright, TextAlignmentOptions.TopLeft);
        titleText.characterSpacing = 6f;

        scrapText = AddText(window, "Scrap", new Vector2(1f, 1f), new Vector2(-PAD - 40f, -36f), new Vector2(420f, 46f),
            "", 30f, FontStyles.Bold, ScrapEconomy.ScrapColor, TextAlignmentOptions.TopRight);

        BuildCloseButton();

        // Hairline under the header, so the title reads as a header rather than floating text.
        AddDivider(-HEADER_RULE_Y);

        repairLabel = AddText(window, "RepairLabel", new Vector2(0f, 1f), new Vector2(PAD, 0f), new Vector2(ROW_W, 30f),
            "", 19f, FontStyles.Bold, FlatUI.TextMuted, TextAlignmentOptions.TopLeft);
        repairLabel.characterSpacing = 4f;
        repairRowRT = BuildRow("RepairRow", 0f);
        repairRow = repairRowRT;

        sectionRule = AddDivider(0f);

        salvageLabel = AddText(window, "SalvageLabel", new Vector2(0f, 1f), new Vector2(PAD, 0f), new Vector2(ROW_W, 30f),
            "", 19f, FontStyles.Bold, FlatUI.TextMuted, TextAlignmentOptions.TopLeft);
        salvageLabel.characterSpacing = 4f;
        salvageRowRT = BuildRow("SalvageRow", 0f);
        salvageRow = salvageRowRT;

        BuildConfirmBar();

        gameObject.SetActive(false);
    }

    private RectTransform AddDivider(float y)
    {
        // Scored line that fades out at both ends, rather than a rule running edge to edge.
        Image d = AddImage(window, "Divider", FlatUI.FadedRule(), FlatUI.BorderSoft, false);
        d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        d.rectTransform.pivot = new Vector2(0.5f, 1f);
        d.rectTransform.anchoredPosition = new Vector2(0f, y);
        d.rectTransform.sizeDelta = new Vector2(ROW_W, 1f);
        return d.rectTransform;
    }

    // Four fasteners holding the plate down. Small, dark and functional — the point is that the
    // panel looks made, not decorated. Corner-anchored so they survive the dynamic height.
    private void AddRivets()
    {
        Vector2[] anchors = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        Vector2[] offsets = { new Vector2(19f, -19f), new Vector2(-19f, -19f), new Vector2(19f, 19f), new Vector2(-19f, 19f) };

        for (int i = 0; i < 4; i++)
        {
            RectTransform rt = AddPoint(window, "Rivet", anchors[i], offsets[i], new Vector2(9f, 9f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = FlatUI.Rivet();
            img.color = new Color(0.34f, 0.30f, 0.25f, 1f);
            img.raycastTarget = false;
        }
    }

    // A handful of faint scuffs on the plate. Deliberately fixed rather than random — they must not
    // shuffle every time the screen opens — and barely visible: imperfection is what stops a panel
    // reading as generated, but the moment you consciously see it, it's noise.
    //
    // Two rules learned the hard way: keep them WELL under 0.03 alpha (at 0.045 they read as
    // rendering glitches, not wear), and keep them out of the content columns — the first pass ran
    // a streak straight through the title. They live in the right-hand margin, which is empty at
    // any card count, and stay within the shortest possible window height.
    private void AddWear()
    {
        // x, y (from top-left), length, angle
        float[,] marks =
        {
            { 782f, -118f,  92f,  -6f },
            { 934f, -206f,  64f,   8f },
            { 712f, -286f, 112f,  -4f },
            { 1004f, -78f,  70f,  11f },
        };

        for (int i = 0; i < marks.GetLength(0); i++)
        {
            RectTransform rt = AddPoint(window, "Scuff", new Vector2(0f, 1f),
                new Vector2(marks[i, 0], marks[i, 1]), new Vector2(marks[i, 2], 1f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, marks[i, 3]);

            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = FlatUI.FadedRule();
            img.color = new Color(1f, 0.93f, 0.85f, 0.022f);
            img.raycastTarget = false;
        }
    }

    // Places every section top-down and resizes the window to fit. Called after each Refresh, so
    // collapsing an empty section actually shrinks the dialog instead of leaving a hole.
    private void LayoutSections(float repairH, float salvageH)
    {
        float y = HEADER_RULE_Y + LABEL_GAP;
        repairLabel.rectTransform.anchoredPosition = new Vector2(PAD, -y);

        y += ROW_GAP;
        repairRowRT.anchoredPosition = new Vector2(0f, -y);
        repairRowRT.sizeDelta = new Vector2(ROW_W, repairH);

        y += repairH + SECTION_GAP;
        sectionRule.anchoredPosition = new Vector2(0f, -y);

        y += LABEL_GAP;
        salvageLabel.rectTransform.anchoredPosition = new Vector2(PAD, -y);

        y += ROW_GAP;
        salvageRowRT.anchoredPosition = new Vector2(0f, -y);
        salvageRowRT.sizeDelta = new Vector2(ROW_W, salvageH);

        y += salvageH + BAR_GAP;
        confirmBarRT.anchoredPosition = new Vector2(0f, -y);

        window.sizeDelta = new Vector2(WIN_W, y + BAR_H + BOTTOM_PAD);
    }

    // A plain X in the corner. The old version was a red gem in an ornate setting, which drew more
    // attention than the actual content.
    private void BuildCloseButton()
    {
        const float sz = 34f;
        RectTransform rt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-PAD * 0.5f, -PAD * 0.5f), new Vector2(sz, sz));
        rt.pivot = new Vector2(1f, 1f);

        Image hit = AddImage(rt, "Hit", FlatUI.Panel(5), new Color(1f, 1f, 1f, 0.05f), true);
        hit.type = Image.Type.Sliced;
        Stretch(hit.rectTransform);

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        btn.onClick.AddListener(Hide);

        AddText(rt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz, sz),
            "X", 20f, FontStyles.Bold, FlatUI.TextMuted, TextAlignmentOptions.Center);
    }

    // ⚠️ ROWS WRAP. They used to be a single HorizontalLayoutGroup that shrank the cards to fit
    // however many there were, which is fine at three and falls apart at a real deck size: at twelve
    // the chips were 76px wide with the name printed over the artwork, and past that they run off
    // the window entirely (the designer hit this).
    //
    // A grid wraps onto a second and third line instead, so a chip keeps a legible size and the
    // SECTION grows in height — which costs nothing, because LayoutSections already resizes the
    // window to its content. Shrinking is now the last resort rather than the first (see GridCell).
    private RectTransform BuildRow(string name, float y)
    {
        RectTransform rt = AddPoint(window, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ROW_W, ROW_H));
        rt.pivot = new Vector2(0.5f, 1f);
        GridLayoutGroup g = rt.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(CARD_W_MAX, CARD_H_MAX);
        g.spacing = new Vector2(CARD_GAP, CARD_GAP);
        // Left-aligned, indented to line up under the section label. Centring the cards instead
        // left the whole left half of the window empty, which read as a broken layout when only
        // one or two cards were listed.
        g.childAlignment = TextAnchor.UpperLeft;
        g.padding = new RectOffset(4, 0, 0, 0);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 1;   // recomputed per refresh in BuildCards
        return rt;
    }

    // Chooses the cell size and column count for `count` chips.
    //
    // Wrap first, shrink only when forced: full-size cells are used while the section stays within
    // MAX_ROWS, and only a deck big enough to overflow that starts scaling them down — with a floor,
    // because an illegible chip is no more useful than an off-screen one.
    private const int MAX_ROWS = 3;
    private const float CARD_W_MIN = 84f;

    private static void GridCell(int count, out Vector2 cell, out int columns)
    {
        columns = Mathf.Max(1, Mathf.FloorToInt((ROW_W + CARD_GAP) / (CARD_W_MAX + CARD_GAP)));
        float w = CARD_W_MAX;

        int rows = Mathf.CeilToInt((float)count / columns);
        if (rows > MAX_ROWS)
        {
            // Need this many across to fit inside MAX_ROWS; size the cell to match.
            int needed = Mathf.CeilToInt((float)count / MAX_ROWS);
            w = Mathf.Max(CARD_W_MIN, (ROW_W - CARD_GAP * (needed - 1)) / needed);
            columns = Mathf.Max(1, Mathf.FloorToInt((ROW_W + CARD_GAP) / (w + CARD_GAP)));
        }

        cell = new Vector2(w, w / CARD_W_MAX * CARD_H_MAX);
    }

    private void BuildConfirmBar()
    {
        // Anchored to the window TOP like everything else, so LayoutSections can place it by the
        // same downward cursor rather than fighting a bottom anchor as the window resizes.
        confirmBar = AddPoint(window, "ConfirmBar", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(620f, BAR_H));
        confirmBar.pivot = new Vector2(0.5f, 1f);
        confirmBarRT = confirmBar;

        confirmBg = confirmBar.gameObject.AddComponent<Image>();
        confirmBg.sprite = FlatUI.Panel(5);
        confirmBg.type = Image.Type.Sliced;
        confirmBg.color = FlatUI.SurfaceRaised;
        confirmBg.raycastTarget = true;

        confirmOutline = AddImage(confirmBar, "Outline", FlatUI.Outline(5, 2), FlatUI.BorderSoft, false);
        confirmOutline.type = Image.Type.Sliced;
        Stretch(confirmOutline.rectTransform);

        confirmLabel = AddText(confirmBar, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(596f, 54f),
            "", 21f, FontStyles.Bold, FlatUI.TextDisabled, TextAlignmentOptions.Center);

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

        repairLabel.text = $"REPAIR   ·   {ScrapEconomy.RECHARGE_PER_CHARGE} SCRAP PER CHARGE";
        salvageLabel.text = $"SALVAGE   ·   {ScrapEconomy.SALVAGE_COST} SCRAP, RETURNS HALF CHARGED";

        float repairH = BuildCards(repairRow, damaged, Mode.Repair, scrap);
        float salvageH = BuildCards(salvageRow, exhausted, Mode.Salvage, scrap);

        // Empty rows get an explicit line rather than nothing. With both sections empty the old
        // screen was a blank field with two headings floating in it and no explanation.
        if (damaged.Count == 0) AddEmptyNote(repairRow, "Every card is at full charges.");
        if (exhausted.Count == 0) AddEmptyNote(salvageRow, "Nothing has burned out yet.");

        LayoutSections(repairH, salvageH);

        UpdateConfirmBar(scrap);
    }

    // A quiet placeholder so an empty section still reads as "nothing to do here" rather than as a
    // rendering failure. Sits inside the row's layout group, so it lines up with where cards would.
    private void AddEmptyNote(Transform row, string message)
    {
        // ⚠️ The row's GridLayoutGroup would force this line into a single card-sized cell and clip
        // it. A grid controls its children's size unconditionally — a LayoutElement can't opt out —
        // so the group is switched off while the section is empty and back on in BuildCards.
        GridLayoutGroup g = row.GetComponent<GridLayoutGroup>();
        if (g != null) g.enabled = false;

        RectTransform rt = AddPoint(row, "Empty", new Vector2(0f, 1f), Vector2.zero, new Vector2(ROW_W - 8f, EMPTY_ROW_H));
        rt.anchoredPosition = new Vector2(4f, 0f);

        TMP_Text t = AddText(rt, "Text", new Vector2(0f, 1f), new Vector2(2f, -4f), new Vector2(ROW_W - 12f, 28f),
            message, 16f, FontStyles.Italic, FlatUI.TextDisabled, TextAlignmentOptions.TopLeft);
    }

    // Returns how tall the section ended up, so LayoutSections can place what follows it.
    private float BuildCards(Transform row, List<RuntimeCard> cards, Mode mode, int scrap)
    {
        if (cards.Count == 0) return EMPTY_ROW_H;

        Vector2 cell; int columns;
        GridCell(cards.Count, out cell, out columns);

        GridLayoutGroup g = row.GetComponent<GridLayoutGroup>();
        if (g != null) { g.enabled = true; g.cellSize = cell; g.constraintCount = columns; }

        foreach (RuntimeCard card in cards)
        {
            int cost = mode == Mode.Repair ? ScrapEconomy.RechargeCost(card) : ScrapEconomy.SALVAGE_COST;
            BuildCardChip(row, card, mode, cell.x, cell.y, cost, scrap >= cost);
        }

        int rows = Mathf.CeilToInt((float)cards.Count / columns);
        return rows * cell.y + (rows - 1) * CARD_GAP;
    }

    private void BuildCardChip(Transform parent, RuntimeCard card, Mode mode, float w, float h, int cost, bool affordable)
    {
        RectTransform rt = AddPoint(parent, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;

        bool isSelected = card == selected;
        Color accent = ScrapEconomy.ScrapColor;

        // Selection glow sits behind the chip and bleeds outward — the highlight is light, not a
        // heavier frame, so a picked card brightens rather than gaining ornament.
        if (isSelected)
        {
            Image glow = AddImage(rt, "Glow", FlatUI.SoftGlow(), new Color(accent.r, accent.g, accent.b, 0.30f), false);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-26f, -26f);
            glow.rectTransform.offsetMax = new Vector2(26f, 26f);
        }

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = isSelected ? new Color(0.165f, 0.180f, 0.204f, 1f) : FlatUI.SurfaceRaised;
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", FlatUI.Outline(5, isSelected ? 2 : 1), isSelected ? accent : FlatUI.Border, false);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        if (card.cardData != null && card.cardData.cardArt != null)
        {
            Image art = AddImage(rt, "Art", card.cardData.cardArt, Color.white, false);
            art.preserveAspect = true;
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            art.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            art.rectTransform.sizeDelta = new Vector2(w - 34f, w - 34f);
            art.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        TMP_Text nameText = AddText(rt, "Name", new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(w - 16f, 42f),
            card.cardData != null ? card.cardData.cardName : "?", 15f, FontStyles.Bold,
            FlatUI.TextBody, TextAlignmentOptions.Bottom);
        nameText.enableWordWrapping = true;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 11f; nameText.fontSizeMax = 15f;

        // Charges: current out of max, so the player can see exactly what they're buying back.
        int max = card.cardData != null ? card.cardData.maxUses : 0;
        string charges = card.isInfinite ? "∞" : $"{card.currentUses}/{max}";
        AddText(rt, "Charges", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(w - 16f, 24f),
            charges, 16f, FontStyles.Bold, FlatUI.Charges, TextAlignmentOptions.Center);

        // Cost as plain accent-coloured text. A shard icon was tried here at 17px and read as a
        // smudge fused to the first digit — at this size the accent colour alone carries "scrap",
        // and the section header already states the unit.
        Color costCol = affordable ? accent : new Color(0.50f, 0.33f, 0.30f);
        AddText(rt, "Cost", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(w - 16f, 26f),
            $"{cost}", 20f, FontStyles.Bold, costCol, TextAlignmentOptions.Center);

        if (!affordable) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.40f;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;
        btn.interactable = affordable;
        RuntimeCard captured = card;
        Mode capturedMode = mode;
        btn.onClick.AddListener(() => OnCardClicked(captured, capturedMode));

        // Hovering a chip turns it over, exactly as it does in the hand. The chip face is a name, a
        // charge count and a price — everything about WHAT THE CARD DOES was missing, so choosing
        // what to repair meant remembering it. Same component, same back, so the two never diverge.
        CardHoverFlip.Attach(rt).Bind(card);
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
            confirmLabel.text = "SELECT A CARD";
            confirmLabel.color = FlatUI.TextDisabled;
            confirmBg.color = FlatUI.SurfaceRaised;
            confirmOutline.color = FlatUI.BorderSoft;
            confirmButton.interactable = false;
            return;
        }

        string cardName = selected.cardData != null ? selected.cardData.cardName : "?";
        int cost = selectedMode == Mode.Repair ? ScrapEconomy.RechargeCost(selected) : ScrapEconomy.SALVAGE_COST;
        bool canAfford = scrap >= cost;

        int maxUses = selected.cardData != null ? selected.cardData.maxUses : 0;
        if (selectedMode == Mode.Repair)
            confirmLabel.text = $"REPAIR {cardName} TO {maxUses}/{maxUses}   ·   {cost} SCRAP";
        else
            confirmLabel.text = $"SALVAGE {cardName} AT {ScrapEconomy.SalvageCharges(selected)}/{maxUses}   ·   {cost} SCRAP";

        Color accent = ScrapEconomy.ScrapColor;
        confirmLabel.color = canAfford ? accent : new Color(0.55f, 0.38f, 0.35f);
        // Actionable state is carried by an accent outline and a faint accent wash rather than a
        // loud filled button — reads as clearly clickable without shouting.
        confirmBg.color = canAfford ? new Color(accent.r * 0.22f, accent.g * 0.18f, accent.b * 0.16f, 1f) : FlatUI.SurfaceRaised;
        confirmOutline.color = canAfford ? accent : new Color(0.34f, 0.26f, 0.25f, 1f);
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

    private TMP_FontAsset ResolveFont() => FlatUI.UIFont();

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

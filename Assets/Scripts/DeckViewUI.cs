using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Deck inspection popup. The scene's original panel was a broken GridLayoutGroup (300px row spacing,
// off-centre, no scrolling) so the deck flew off-screen. This rebuilds the whole popup procedurally
// (house style — framed window + scrollable grid, sprites generated in code) on top of the existing
// full-screen backdrop (viewPanel). The HUD's pile buttons + count labels are untouched.
//
// Cards are the clean CardUI_Template prefab (200x300). CardUI.Update continuously lerps a card's
// localScale back to 1, which fights any resize, so after Setup() we DISABLE the CardUI component
// (its visuals persist) and then scale the card freely to fit the grid cell.
public class DeckViewUI : MonoBehaviour
{
    public static DeckViewUI instance;

    [Header("Butonlar & Sayaçlar")]
    public Button drawPileButton;
    public TextMeshProUGUI drawCountText;
    public Button discardPileButton;
    public TextMeshProUGUI discardCountText;

    [Header("Açılır Pencere (Pop-up)")]
    public GameObject viewPanel;
    public Transform cardContainer;      // legacy (broken) — disabled at build; population uses gridContent
    public GameObject cardUIPrefab;
    public TextMeshProUGUI titleText;    // legacy — disabled at build; only used to inherit the font
    public Button closeButton;           // legacy — disabled at build; a new close button is built

    [Header("Exhaust (Tükenenler)")]
    public Button exhaustButton;
    public TextMeshProUGUI exhaustCountText;

    [Header("Deck View Style")]
    public float cardScale = 0.72f;      // CardUI_Template is 200x300; cells are 200*scale x 300*scale
    public int columns = 5;
    public Vector2 spacing = new Vector2(28f, 28f);

    private static readonly Color PanelColor  = new Color(0.09f, 0.10f, 0.13f, 0.98f);
    private static readonly Color FrameColor   = new Color(0.82f, 0.62f, 0.30f, 1f);   // bronze/gold
    private static readonly Color AccentColor  = new Color(1f, 0.82f, 0.42f, 1f);
    private static readonly Color CloseColor   = new Color(0.55f, 0.17f, 0.17f, 0.96f);

    // --- runtime (built once) ---
    private bool built;
    private RectTransform windowRT;
    private CanvasGroup windowCG;
    private RectTransform gridContent;
    private TextMeshProUGUI headerTitle;
    private TextMeshProUGUI headerCount;
    private ScrollRect scrollRect;
    private TMP_FontAsset font;

    private static Sprite panelSprite, frameSprite, glowSprite, circleSprite;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (viewPanel != null) viewPanel.SetActive(false);
        if (exhaustButton) exhaustButton.onClick.AddListener(ShowExhaustPile);
        if (drawPileButton) drawPileButton.onClick.AddListener(ShowDrawPile);
        if (discardPileButton) discardPileButton.onClick.AddListener(ShowDiscardPile);
        // closeButton listener intentionally dropped — the legacy button is replaced by a built-in one.
    }

    private void Update()
    {
        if (exhaustCountText && DeckManager.instance != null)
            exhaustCountText.text = DeckManager.instance.GetExhaustPile().Count.ToString();

        if (DeckManager.instance != null)
        {
            if (drawCountText)
                drawCountText.text = DeckManager.instance.GetDrawPile().Count.ToString();
            if (discardCountText)
                discardCountText.text = DeckManager.instance.GetDiscardPile().Count.ToString();
        }
    }

    // ---------------------------------------------------------------- public API

    public void ShowDrawPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DRAW PILE", DeckManager.instance.GetDrawPile());
    }

    public void ShowDiscardPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DISCARD PILE", DeckManager.instance.GetDiscardPile());
    }

    public void ShowExhaustPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("EXHAUST PILE", DeckManager.instance.GetExhaustPile());
    }

    // The whole owned deck (hand + draw + discard + exhaust) in one list. Used by the reward screen's
    // "View Deck" button so the player can inspect the deck while the GameplayHUD is hidden.
    public void ShowFullDeck()
    {
        if (DeckManager.instance == null) return;
        List<RuntimeCard> all = new List<RuntimeCard>();
        all.AddRange(DeckManager.instance.GetCurrentHand());
        all.AddRange(DeckManager.instance.GetDrawPile());
        all.AddRange(DeckManager.instance.GetDiscardPile());
        all.AddRange(DeckManager.instance.GetExhaustPile());
        OpenView("YOUR DECK", all);
    }

    public void CloseView()
    {
        if (viewPanel != null) viewPanel.SetActive(false);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
    }

    // ---------------------------------------------------------------- open / populate

    private void OpenView(string title, List<RuntimeCard> cardsToList)
    {
        if (viewPanel == null) return;
        viewPanel.SetActive(true);
        viewPanel.transform.SetAsLastSibling();   // draw above the HUD / reward screen
        if (!built) BuildUI();

        if (windowCG != null) windowCG.alpha = 0f;                         // avoid a first-frame flash
        if (windowRT != null) windowRT.localScale = Vector3.one * 0.95f;
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        if (headerTitle != null) headerTitle.text = title;
        if (headerCount != null) headerCount.text = cardsToList.Count + (cardsToList.Count == 1 ? " CARD" : " CARDS");

        // Clear previous contents.
        for (int i = gridContent.childCount - 1; i >= 0; i--)
            Destroy(gridContent.GetChild(i).gameObject);

        // Populate. Each card lives in an empty "cell": the grid sizes the CELL, while the card keeps
        // its native 200x300 layout and is just uniformly scaled to fit — so its internal proportions
        // never distort. Setup() draws the card, then CardUI is disabled so it stops resetting the scale.
        foreach (RuntimeCard card in cardsToList)
        {
            GameObject cell = NewRect("Cell", gridContent);   // grid controls this; no graphic on it

            GameObject cardObj = Instantiate(cardUIPrefab);
            cardObj.transform.SetParent(cell.transform, false);
            RectTransform crt = cardObj.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                crt.localScale = Vector3.one * cardScale;
            }

            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.Setup(card, -1);
                if (ui.keyHintText != null) ui.keyHintText.text = "";   // no "[0]" in the deck view
                ui.enabled = false;
            }

            CanvasGroup g = cardObj.GetComponent<CanvasGroup>();
            if (g == null) g = cardObj.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;   // cards don't eat scroll drags
        }

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        // Let layout settle, then snap the scroll to the top.
        yield return null;
        if (gridContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

        // Fade + gentle pop (unscaled so it works while the reward screen has the game paused).
        float t = 0f, dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            if (windowCG != null) windowCG.alpha = n;
            if (windowRT != null) windowRT.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, EaseOut(n));
            yield return null;
        }
        if (windowCG != null) windowCG.alpha = 1f;
        if (windowRT != null) windowRT.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- build (once)

    private void BuildUI()
    {
        built = true;
        font = titleText != null ? titleText.font : null;

        // Retire the broken legacy children so they never show.
        if (cardContainer != null) cardContainer.gameObject.SetActive(false);
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        RectTransform root = viewPanel.GetComponent<RectTransform>();

        // Window — a framed panel with margins from the screen edges.
        GameObject win = NewRect("DeckWindow", root);
        windowRT = win.GetComponent<RectTransform>();
        windowRT.anchorMin = new Vector2(0.07f, 0.09f);
        windowRT.anchorMax = new Vector2(0.93f, 0.91f);
        windowRT.offsetMin = Vector2.zero;
        windowRT.offsetMax = Vector2.zero;
        Image panel = win.AddComponent<Image>();
        panel.sprite = GetPanelSprite(); panel.type = Image.Type.Sliced; panel.color = PanelColor;
        windowCG = win.AddComponent<CanvasGroup>();

        Image frame = MakeImage(windowRT, "Frame", GetFrameSprite(), FrameColor, false);
        Stretch(frame.rectTransform);
        frame.type = Image.Type.Sliced;

        // Header: title + count + accent rule.
        headerTitle = MakeText(windowRT, "Title", "YOUR DECK", 46f, TextAlignmentOptions.Center);
        headerTitle.fontStyle = FontStyles.Bold;
        headerTitle.color = new Color(0.97f, 0.94f, 0.86f, 1f);
        RectTransform htr = headerTitle.rectTransform;
        htr.anchorMin = new Vector2(0f, 1f); htr.anchorMax = new Vector2(1f, 1f); htr.pivot = new Vector2(0.5f, 1f);
        htr.sizeDelta = new Vector2(-40f, 60f); htr.anchoredPosition = new Vector2(0f, -20f);

        headerCount = MakeText(windowRT, "Count", "", 24f, TextAlignmentOptions.Center);
        headerCount.color = new Color(1f, 0.82f, 0.42f, 0.9f);
        RectTransform hcr = headerCount.rectTransform;
        hcr.anchorMin = new Vector2(0f, 1f); hcr.anchorMax = new Vector2(1f, 1f); hcr.pivot = new Vector2(0.5f, 1f);
        hcr.sizeDelta = new Vector2(-40f, 30f); hcr.anchoredPosition = new Vector2(0f, -80f);

        Image accent = MakeImage(windowRT, "HeaderAccent", GetGlowSprite(), AccentColor, false);
        RectTransform acr = accent.rectTransform;
        acr.anchorMin = new Vector2(0.5f, 1f); acr.anchorMax = new Vector2(0.5f, 1f); acr.pivot = new Vector2(0.5f, 1f);
        acr.sizeDelta = new Vector2(360f, 16f); acr.anchoredPosition = new Vector2(0f, -116f);

        // Close button (top-right).
        GameObject cb = NewRect("CloseBtn", windowRT);
        RectTransform cbr = cb.GetComponent<RectTransform>();
        cbr.anchorMin = cbr.anchorMax = new Vector2(1f, 1f); cbr.pivot = new Vector2(1f, 1f);
        cbr.anchoredPosition = new Vector2(-16f, -16f); cbr.sizeDelta = new Vector2(54f, 54f);
        Image cbImg = cb.AddComponent<Image>(); cbImg.sprite = GetCircleSprite(); cbImg.color = CloseColor;
        Button cbBtn = cb.AddComponent<Button>(); cbBtn.targetGraphic = cbImg;
        cbBtn.onClick.AddListener(CloseView);
        TextMeshProUGUI x = MakeText(cbr, "X", "X", 30f, TextAlignmentOptions.Center);
        x.fontStyle = FontStyles.Bold; x.color = Color.white; Stretch(x.rectTransform);

        // Scroll view (below the header).
        GameObject sv = NewRect("ScrollView", windowRT);
        RectTransform svr = sv.GetComponent<RectTransform>();
        svr.anchorMin = Vector2.zero; svr.anchorMax = Vector2.one;
        svr.offsetMin = new Vector2(30f, 26f); svr.offsetMax = new Vector2(-30f, -132f);
        scrollRect = sv.AddComponent<ScrollRect>();

        GameObject vp = NewRect("Viewport", svr);
        RectTransform vpr = vp.GetComponent<RectTransform>();
        vpr.anchorMin = Vector2.zero; vpr.anchorMax = Vector2.one;
        vpr.offsetMin = Vector2.zero; vpr.offsetMax = new Vector2(-18f, 0f);   // leave room for the scrollbar
        vpr.pivot = new Vector2(0f, 1f);
        Image vpImg = vp.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f); vpImg.raycastTarget = true;
        vp.AddComponent<RectMask2D>();

        GameObject content = NewRect("Content", vpr);
        gridContent = content.GetComponent<RectTransform>();
        gridContent.anchorMin = new Vector2(0f, 1f); gridContent.anchorMax = new Vector2(1f, 1f);
        gridContent.pivot = new Vector2(0.5f, 1f); gridContent.anchoredPosition = Vector2.zero; gridContent.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.cellSize = new Vector2(200f * cardScale, 300f * cardScale);
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        ContentSizeFitter fit = content.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Scrollbar (right edge).
        GameObject sb = NewRect("Scrollbar", svr);
        RectTransform sbr = sb.GetComponent<RectTransform>();
        sbr.anchorMin = new Vector2(1f, 0f); sbr.anchorMax = new Vector2(1f, 1f); sbr.pivot = new Vector2(1f, 0.5f);
        sbr.sizeDelta = new Vector2(12f, 0f); sbr.anchoredPosition = Vector2.zero;
        // Circle sprite stretches into a clean rounded pill at this narrow width (avoids 9-slice clamping).
        Image sbImg = sb.AddComponent<Image>(); sbImg.sprite = GetCircleSprite(); sbImg.color = new Color(0f, 0f, 0f, 0.35f);
        Scrollbar scrollbar = sb.AddComponent<Scrollbar>();
        GameObject slide = NewRect("SlidingArea", sbr); Stretch(slide.GetComponent<RectTransform>());
        GameObject handle = NewRect("Handle", slide.GetComponent<RectTransform>());
        Image handleImg = handle.AddComponent<Image>(); handleImg.sprite = GetCircleSprite();
        handleImg.color = new Color(0.82f, 0.62f, 0.30f, 0.9f);
        RectTransform handleRT = handle.GetComponent<RectTransform>(); Stretch(handleRT);
        scrollbar.handleRect = handleRT; scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Wire the scroll rect.
        scrollRect.content = gridContent;
        scrollRect.viewport = vpr;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 38f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    // ---------------------------------------------------------------- helpers

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private GameObject NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private Image MakeImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = NewRect(name, parent);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = NewRect(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.alignment = align; t.raycastTarget = false;
        return t;
    }

    private static float EaseOut(float t) { float p = 1f - t; return 1f - p * p * p; }

    // --- procedural sprites (cached + shared) ---

    private static Sprite GetPanelSprite()
    {
        if (panelSprite != null) return panelSprite;
        int s = 64; float radius = s * 0.22f;
        panelSprite = BuildRounded(s, radius, -1f);   // filled
        return panelSprite;
    }

    private static Sprite GetFrameSprite()
    {
        if (frameSprite != null) return frameSprite;
        int s = 64; float radius = s * 0.22f;
        frameSprite = BuildRounded(s, radius, s * 0.055f);   // border band
        return frameSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c - 1f;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(rad - d);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return circleSprite;
    }

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

    // Rounded rect (9-sliced). border<0 => filled; border>0 => just the outline band of that width.
    private static Sprite BuildRounded(int s, float radius, float border)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);   // >0 inside
                float a;
                if (border < 0f) a = Mathf.Clamp01(d);
                else
                {
                    float outer = Mathf.Clamp01(d / 1.5f);
                    float inner = Mathf.Clamp01((border - d) / 1.5f);
                    a = d < 0f ? 0f : Mathf.Min(outer, inner);
                }
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        float b = radius + 2f;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

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

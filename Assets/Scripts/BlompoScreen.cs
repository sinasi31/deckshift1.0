using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Blompo's blessing screen. Two steps: pick a card from your deck, then pick 1 of 3 randomly
// offered enhancements for it. Free at the point of use (reaching Blompo is the cost) and
// strictly ONE card per visit.
//
// Built procedurally in the shared Deckshift chrome (RelicUISprites: stone + ornate gold +
// gem studs), self-instantiating under the main Canvas — no scene/prefab wiring, same pattern
// as RelicManagePanel / RelicSwapScreen.
public class BlompoScreen : MonoBehaviour
{
    public static BlompoScreen instance;

    private CanvasGroup group;
    private RectTransform window;
    private Transform cardRow, offerRow;
    private TMP_Text titleText, promptText;
    private Image portraitImage;
    private TMP_FontAsset font;

    private Sprite portrait;
    private RuntimeCard chosenCard;
    private bool isOpen, blessingSpent;

    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    // WIN_H is sized to the taller of the two steps (the offer chips) plus margin — the card step
    // is shorter, so it simply sits with a little more air beneath it.
    private const float WIN_W = 760f, WIN_H = 396f, CARD_W = 100f, CARD_H = 138f;

    public static void Open(Sprite blompoPortrait = null)
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.portrait = blompoPortrait;
        instance.Show();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("BlompoScreen: no Canvas found in scene."); return; }
        GameObject go = new GameObject("BlompoScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<BlompoScreen>();
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

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = AddImage(transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.84f), true);
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
        RelicUISprites.AddGemStuds(window, WIN_W, WIN_H, RelicUISprites.GemColor(Rarity.Common), topRight: false);

        // Blompo's portrait, floating at the top-left of the window.
        RectTransform portraitRt = AddPoint(window, "Portrait", new Vector2(0f, 1f), new Vector2(62f, -30f), new Vector2(96f, 96f));
        portraitRt.pivot = new Vector2(0f, 1f);
        portraitImage = portraitRt.gameObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        titleText = AddText(window, "Title", new Vector2(0f, 1f), new Vector2(172f, -36f), new Vector2(420f, 44f),
            "BLOMPO", 34f, FontStyles.Bold, new Color(0.98f, 0.86f, 0.55f), TextAlignmentOptions.TopLeft);
        promptText = AddText(window, "Prompt", new Vector2(0f, 1f), new Vector2(172f, -78f), new Vector2(520f, 30f),
            "Choose a card to bless.", 17f, FontStyles.Normal, new Color(0.86f, 0.87f, 0.92f), TextAlignmentOptions.TopLeft);

        // Close button — the top-right gem stud position (AddGemStuds skips it).
        const float closeSz = 46f;
        RectTransform closeRt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-closeSz * 0.45f, -closeSz * 0.45f), new Vector2(closeSz, closeSz));
        Image closeSet = AddImage(closeRt, "Setting", RelicUISprites.GemSetting(), Color.white, false);
        Stretch(closeSet.rectTransform);
        Image closeGem = AddImage(closeRt, "Gem", RelicUISprites.Gem(), new Color(0.86f, 0.24f, 0.26f), false);
        closeGem.rectTransform.anchorMin = closeGem.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        closeGem.rectTransform.sizeDelta = new Vector2(closeSz * 0.60f, closeSz * 0.60f);
        Image closeHit = AddImage(closeRt, "Hit", null, new Color(0f, 0f, 0f, 0f), true);
        Stretch(closeHit.rectTransform);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.transition = Selectable.Transition.None;
        closeBtn.targetGraphic = closeHit;
        closeBtn.onClick.AddListener(Hide);
        AddText(closeRt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(closeSz, closeSz),
            "X", 18f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        cardRow = BuildRow("CardRow", -138f, 16f);
        offerRow = BuildRow("OfferRow", -138f, 20f);

        gameObject.SetActive(false);
    }

    private Transform BuildRow(string name, float y, float spacing)
    {
        RectTransform rt = AddPoint(window, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(WIN_W - 120f, 300f));
        rt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    // ---- open / close ----
    private void Show()
    {
        isOpen = true;
        blessingSpent = false;
        chosenCard = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

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

        ShowCardStep();

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

    private IEnumerator OpenAnim()
    {
        float t = 0f; const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * (0.9f + 0.1f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    // ---- step 1: choose a card ----
    private void ShowCardStep()
    {
        ClearRow(cardRow); ClearRow(offerRow);
        cardRow.gameObject.SetActive(true);
        offerRow.gameObject.SetActive(false);

        List<RuntimeCard> deck = CollectDeck();
        List<RuntimeCard> eligible = new List<RuntimeCard>();
        foreach (RuntimeCard c in deck)
            if (CardEnhancements.CanEnhance(c)) eligible.Add(c);

        if (eligible.Count == 0)
        {
            promptText.text = deck.Count == 0
                ? "Your deck is empty. Blompo shrugs."
                : "Every card you own is already blessed. Blompo is out of ideas.";
            return;
        }

        promptText.text = "Choose a card to bless.";
        // Cap the row so a large deck doesn't overflow the window.
        int max = Mathf.Min(eligible.Count, 6);
        for (int i = 0; i < max; i++)
        {
            RuntimeCard card = eligible[i];
            GameObject chip = BuildCardChip(cardRow, card);
            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            RuntimeCard captured = card;
            b.onClick.AddListener(() => ChooseCard(captured));
        }
    }

    private void ChooseCard(RuntimeCard card)
    {
        if (blessingSpent) return;
        chosenCard = card;
        ShowOfferStep();
    }

    // ---- step 2: pick 1 of 3 offers ----
    private void ShowOfferStep()
    {
        ClearRow(cardRow); ClearRow(offerRow);
        cardRow.gameObject.SetActive(false);
        offerRow.gameObject.SetActive(true);

        List<CardEnhancement> offers = CardEnhancements.RollOffers(chosenCard, 3);
        string cardName = chosenCard.cardData != null ? chosenCard.cardData.cardName : "that card";
        promptText.text = $"Blompo considers <b>{cardName}</b>. Pick a blessing.";

        foreach (CardEnhancement e in offers)
        {
            GameObject chip = BuildOfferChip(offerRow, e);
            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            CardEnhancement captured = e;
            b.onClick.AddListener(() => ChooseOffer(captured));
        }
    }

    private void ChooseOffer(CardEnhancement e)
    {
        if (blessingSpent || chosenCard == null) return;
        if (!CardEnhancements.Apply(chosenCard, e)) return;

        blessingSpent = true;   // one card per visit
        ClearRow(offerRow);
        offerRow.gameObject.SetActive(false);
        cardRow.gameObject.SetActive(true);
        ClearRow(cardRow);
        BuildCardChip(cardRow, chosenCard);

        string cardName = chosenCard.cardData != null ? chosenCard.cardData.cardName : "It";
        promptText.text = $"<b>{cardName}</b> is now <b>{CardEnhancements.Name(e)}</b>. Blompo is pleased.";
        StartCoroutine(CloseAfter(1.6f));
    }

    private IEnumerator CloseAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        Hide();
    }

    // ---- chip builders ----
    private GameObject BuildCardChip(Transform parent, RuntimeCard card)
    {
        RectTransform rt = AddPoint(parent, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CARD_W, CARD_H));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = CARD_W; le.preferredHeight = CARD_H;

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.StonePanel();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", RelicUISprites.GoldBorder(), Color.white, false);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.8f;
        Stretch(frame.rectTransform);

        if (card.cardData != null && card.cardData.cardArt != null)
        {
            Image art = AddImage(rt, "Art", card.cardData.cardArt, Color.white, false);
            art.preserveAspect = true;
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            art.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            art.rectTransform.sizeDelta = new Vector2(CARD_W - 30f, CARD_W - 30f);
        }

        // Wraps + shrinks so long names ("Create Platform") don't clip the card edge.
        TMP_Text nameText = AddText(rt, "Name", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(CARD_W - 10f, 30f),
            card.cardData != null ? card.cardData.cardName : "?", 12f, FontStyles.Bold,
            new Color(0.97f, 0.93f, 0.85f), TextAlignmentOptions.Center);
        nameText.enableWordWrapping = true;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 9f;
        nameText.fontSizeMax = 12f;

        string charges = card.isInfinite ? "∞" : card.currentUses.ToString();
        AddText(rt, "Charges", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(CARD_W - 14f, 20f),
            charges, 13f, FontStyles.Bold, new Color(0.65f, 0.86f, 1f), TextAlignmentOptions.Center);

        // Existing blessing badge.
        if (card.enhancement != CardEnhancement.None)
        {
            Color gem = RelicUISprites.GemColor(CardEnhancements.RarityOf(card.enhancement));
            AddText(rt, "Badge", new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(CARD_W - 10f, 20f),
                CardEnhancements.Name(card.enhancement), 11f, FontStyles.Bold, gem, TextAlignmentOptions.Center);
        }
        return rt.gameObject;
    }

    private GameObject BuildOfferChip(Transform parent, CardEnhancement e)
    {
        const float W = 190f, H = 182f;
        RectTransform rt = AddPoint(parent, "Offer", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = W; le.preferredHeight = H;

        Color gem = RelicUISprites.GemColor(CardEnhancements.RarityOf(e));

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.StonePanel();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.9f, 0.87f, 0.82f, 1f);
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", RelicUISprites.GoldBorder(), Color.white, false);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.5f;
        Stretch(frame.rectTransform);

        // A single rarity gem stands in for the enhancement's icon.
        Image set = AddImage(rt, "Setting", RelicUISprites.GemSetting(), Color.white, false);
        set.rectTransform.anchorMin = set.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        set.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        set.rectTransform.sizeDelta = new Vector2(52f, 52f);
        Image gemImg = AddImage(rt, "Gem", RelicUISprites.Gem(), gem, false);
        gemImg.rectTransform.anchorMin = gemImg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        gemImg.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        gemImg.rectTransform.sizeDelta = new Vector2(32f, 32f);

        AddText(rt, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(W - 22f, 30f),
            CardEnhancements.Name(e), 18f, FontStyles.Bold, gem, TextAlignmentOptions.Top);

        TMP_Text desc = AddText(rt, "Desc", new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(W - 30f, 80f),
            CardEnhancements.Description(e), 14f, FontStyles.Normal,
            new Color(0.88f, 0.89f, 0.93f), TextAlignmentOptions.Top);
        desc.enableWordWrapping = true;

        return rt.gameObject;
    }

    // Every card the player still owns. Exhausted cards are deliberately excluded — they're out
    // of the run, and blessing one would be a dead pick.
    private List<RuntimeCard> CollectDeck()
    {
        List<RuntimeCard> all = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return all;
        all.AddRange(d.GetCurrentHand());
        all.AddRange(d.GetDrawPile());
        all.AddRange(d.GetDiscardPile());
        return all;
    }

    private void ClearRow(Transform row)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
    }

    // ---- small UGUI builders (house style) ----
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
        t.enableWordWrapping = false; t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    private TMP_FontAsset ResolveFont()
    {
        TMP_Text any = FindAnyObjectByType<TMP_Text>();
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

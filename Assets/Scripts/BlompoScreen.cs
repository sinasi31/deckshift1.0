using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Blompo's blessing screen.
//
// Flow (designer-set 2026-07-27): the player sees the three ENHANCEMENT OFFERS FIRST, then picks
// which card receives the one they chose. Choosing the card first made no sense — you'd be
// committing before knowing what you were committing to.
//
// Offers are rolled so each is valid for at least one card in the deck (CardEnhancements
// .RollOffersForDeck), so an offer is never a dead end. Free at the point of use, ONE card per
// visit; Blompo then leaves (BlompoNPC handles the vanish).
//
// Built procedurally in the FlatUI ARCANE theme, self-instantiating under the main Canvas. Where
// the Scrap Forge is a workbench (warm iron, fire from below, rivets, embers rising), Blompo's
// screen inverts every cue: cold indigo, light descending from above, four-point stars instead of
// fasteners, motes settling downward, and no wear — his space is not a workshop that gets used.
public class BlompoScreen : MonoBehaviour
{
    public static BlompoScreen instance;

    private CanvasGroup group;
    private RectTransform window;
    private Transform offerRow, cardRow;
    private RectTransform forgeStage;   // centred area the forging plays out in
    private TMP_Text titleText, promptText;
    private Image portraitImage;
    private TMP_FontAsset font;

    private Sprite portrait;
    private Sprite hammerSprite;
    private List<CardEnhancement> offers;
    private System.Action onBlessed;
    private CardEnhancement chosenOffer = CardEnhancement.None;
    private bool isOpen, blessingSpent;

    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    // Blompo runs the ARCANE theme, not the Forge's iron (see FlatUI.Theme). Every cue is the
    // inverse of the workbench: cold instead of warm, lit from above instead of below, motes
    // settling instead of embers rising, stars instead of rivets, and no wear at all — his space
    // isn't a workshop that gets used.
    private static readonly FlatUI.Theme T = FlatUI.Arcane;

    // Sized against the project's 1920x1080 canvas. Height came down from 900 once the offer chips
    // shrank — at 900 there was ~200px of dead panel under them. Still tall enough for the forging
    // stage, which needs room for a 1.55x card struck at the centre.
    private const float WIN_W = 1600f, WIN_H = 762f;
    private const float CARD_W = 200f, CARD_H = 286f;
    // Shorter than the original 560: the ornate chrome used to fill the lower third, and without
    // it the chip was mostly empty space under the description.
    private const float OFFER_W = 380f, OFFER_H = 470f;

    // `fixedOffers` are THIS Blompo's three blessings, rolled once and owned by the NPC — the
    // screen must never re-roll, or leaving and re-entering would let the player reroll for free.
    public static void Open(Sprite blompoPortrait, Sprite hammer, List<CardEnhancement> fixedOffers, System.Action blessedCallback = null)
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.portrait = blompoPortrait;
        instance.hammerSprite = hammer;
        instance.offers = fixedOffers;
        instance.onBlessed = blessedCallback;
        instance.Show();
    }

    // Lets BlompoNPC roll its offers against the same deck view the screen uses.
    public static List<RuntimeCard> CollectDeckStatic()
    {
        List<RuntimeCard> all = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return all;
        all.AddRange(d.GetCurrentHand());
        all.AddRange(d.GetDrawPile());
        all.AddRange(d.GetDiscardPile());
        return all;
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

        Image backdrop = AddImage(transform, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel(10);
        winBg.type = Image.Type.Sliced;
        winBg.color = T.Surface;
        winBg.raycastTarget = true;

        // The Forge is lit by fire from BELOW; Blompo is lit from ABOVE — the blessing descending.
        // Same BottomGlow sprite (it falls off on both axes, so no seam at the sides), flipped in
        // place by scaling Y negative about a centred pivot.
        Image halo = AddImage(window, "Halo", FlatUI.BottomGlow(),
            new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.075f), false);
        halo.rectTransform.anchorMin = new Vector2(0f, 1f);
        halo.rectTransform.anchorMax = new Vector2(1f, 1f);
        halo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        halo.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        halo.rectTransform.sizeDelta = new Vector2(-6f, 180f);
        halo.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        // Motes settling downward — magic coming to rest, the inverse of the Forge's rising heat.
        UIEmberField.Attach(window, 22, new Color(0.80f, 0.72f, 1f, 1f), UIEmberField.Settings.Motes);

        Image winFrame = AddImage(window, "Frame", FlatUI.Outline(10, 2), T.Border, false);
        winFrame.type = Image.Type.Sliced;
        Stretch(winFrame.rectTransform);

        // Four-point stars where the Forge has rivets: his panel is pinned by light, not fasteners.
        AddCornerStars();

        // A soft aura behind the portrait so Blompo reads as the source of the light in the room.
        RectTransform auraRt = AddPoint(window, "Aura", new Vector2(0f, 1f), new Vector2(150f, -140f), new Vector2(340f, 340f));
        auraRt.pivot = new Vector2(0.5f, 0.5f);
        Image aura = auraRt.gameObject.AddComponent<Image>();
        aura.sprite = FlatUI.SoftGlow();
        aura.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.14f);
        aura.raycastTarget = false;

        RectTransform portraitRt = AddPoint(window, "Portrait", new Vector2(0f, 1f), new Vector2(70f, -48f), new Vector2(190f, 190f));
        portraitRt.pivot = new Vector2(0f, 1f);
        portraitImage = portraitRt.gameObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        titleText = AddText(window, "Title", new Vector2(0f, 1f), new Vector2(290f, -52f), new Vector2(900f, 72f),
            "BLOMPO", 46f, FontStyles.Bold, T.TextBright, TextAlignmentOptions.TopLeft);
        titleText.characterSpacing = 8f;

        promptText = AddText(window, "Prompt", new Vector2(0f, 1f), new Vector2(292f, -124f), new Vector2(1100f, 44f),
            "", 24f, FontStyles.Normal, T.TextBody, TextAlignmentOptions.TopLeft);

        BuildCloseButton();

        offerRow = BuildRow("OfferRow", -230f, 44f);
        cardRow = BuildRow("CardRow", -250f, 26f);

        // Forging stage: centred, no layout group (the FX drives positions directly). Sits a little
        // below centre so the struck card doesn't collide with the title.
        forgeStage = AddPoint(window, "ForgeStage", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(WIN_W - 200f, 560f));
        forgeStage.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private void BuildCloseButton()
    {
        const float sz = 34f;
        RectTransform rt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(sz, sz));
        rt.pivot = new Vector2(1f, 1f);

        Image hit = AddImage(rt, "Hit", FlatUI.Panel(5), new Color(1f, 1f, 1f, 0.05f), true);
        hit.type = Image.Type.Sliced;
        Stretch(hit.rectTransform);

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        btn.onClick.AddListener(Hide);

        AddText(rt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz, sz),
            "X", 20f, FontStyles.Bold, T.TextMuted, TextAlignmentOptions.Center);
    }

    // Corner stars, offset inward like the Forge's rivets so the two screens share a rhythm even
    // though the marks themselves are opposites (light vs hardware).
    private void AddCornerStars()
    {
        Vector2[] anchors = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        Vector2[] offsets = { new Vector2(22f, -22f), new Vector2(-22f, -22f), new Vector2(22f, 22f), new Vector2(-22f, 22f) };

        for (int i = 0; i < 4; i++)
        {
            RectTransform rt = AddPoint(window, "Star", anchors[i], offsets[i], new Vector2(18f, 18f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = FlatUI.FourPointStar();
            img.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.55f);
            img.raycastTarget = false;
        }
    }

    private Transform BuildRow(string name, float y, float spacing)
    {
        RectTransform rt = AddPoint(window, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(WIN_W - 160f, 460f));
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
        chosenOffer = CardEnhancement.None;

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

        ShowOfferStep();

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

        // Only now does Blompo leave — closing without blessing keeps him around.
        if (blessingSpent)
        {
            System.Action cb = onBlessed;
            onBlessed = null;
            cb?.Invoke();
        }
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

    // ---- step 1: pick a blessing ----
    private void ShowOfferStep()
    {
        ClearRow(cardRow); ClearRow(offerRow);
        offerRow.gameObject.SetActive(true);
        cardRow.gameObject.SetActive(false);
        if (forgeStage != null) forgeStage.gameObject.SetActive(false);

        List<RuntimeCard> deck = CollectDeck();

        if (offers == null || offers.Count == 0)
        {
            promptText.text = deck.Count == 0
                ? "Your deck is empty. Blompo shrugs."
                : "Every card you own is already blessed. Blompo is out of ideas.";
            return;
        }

        promptText.text = "Blompo offers three blessings. Take one.";
        foreach (CardEnhancement e in offers)
        {
            // The offers are fixed, but the DECK can change between visits (cards played, exhausted,
            // already blessed). An offer with no legal target is shown dimmed and inert rather than
            // being swapped out — swapping would be a reroll by the back door.
            bool playable = CardEnhancements.CardsFor(e, deck).Count > 0;
            GameObject chip = BuildOfferChip(offerRow, e, playable);
            if (!playable) continue;

            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            CardEnhancement captured = e;
            b.onClick.AddListener(() => ChooseOffer(captured));
        }
    }

    private void ChooseOffer(CardEnhancement e)
    {
        if (blessingSpent) return;
        chosenOffer = e;
        ShowCardStep();
    }

    // ---- step 2: pick the card that receives it ----
    private void ShowCardStep()
    {
        ClearRow(offerRow); ClearRow(cardRow);
        offerRow.gameObject.SetActive(false);
        cardRow.gameObject.SetActive(true);
        if (forgeStage != null) forgeStage.gameObject.SetActive(false);

        List<RuntimeCard> valid = CardEnhancements.CardsFor(chosenOffer, CollectDeck());
        promptText.text = $"<b>{CardEnhancements.Name(chosenOffer)}</b> — {CardEnhancements.Description(chosenOffer)}  Choose a card.";

        int max = Mathf.Min(valid.Count, 8);

        // Shrink to fit. Eight cards at full size need ~1780px against a 1440px row, so a full
        // spread used to run off both ends of the window — only invisible today because decks are
        // small. Scale is applied to the LayoutElement too, or the layout group keeps reserving
        // the unscaled width.
        const float ROW_SPACING = 26f;
        float rowW = WIN_W - 160f;
        float needed = max * CARD_W + Mathf.Max(0, max - 1) * ROW_SPACING;
        float fit = needed > rowW ? rowW / needed : 1f;

        for (int i = 0; i < max; i++)
        {
            RuntimeCard card = valid[i];
            GameObject chip = BuildCardChip(cardRow, card, fit);
            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            RuntimeCard captured = card;
            b.onClick.AddListener(() => ChooseCard(captured));
        }
    }

    private void ChooseCard(RuntimeCard card)
    {
        if (blessingSpent || chosenOffer == CardEnhancement.None) return;
        if (!CardEnhancements.CanApplyTo(chosenOffer, card)) return;

        blessingSpent = true;   // lock immediately so a double-click can't forge twice
        StartCoroutine(ForgeRoutine(card));
    }

    // Blompo hammers the blessing into the card: the chosen card takes centre stage and gets
    // struck three times, the enhancement landing on the final blow.
    private IEnumerator ForgeRoutine(RuntimeCard card)
    {
        ClearRow(cardRow);
        cardRow.gameObject.SetActive(false);
        offerRow.gameObject.SetActive(false);
        forgeStage.gameObject.SetActive(true);
        for (int i = forgeStage.childCount - 1; i >= 0; i--) Destroy(forgeStage.GetChild(i).gameObject);

        promptText.text = "Blompo gets to work.";

        // A single big copy of the card, centred on the stage.
        GameObject chipGo = BuildCardChip(forgeStage, card, 1.55f);
        RectTransform chip = chipGo.GetComponent<RectTransform>();
        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = Vector2.zero;

        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(chosenOffer));

        // The enhancement lands on the LAST hammer blow, and the chip is updated on that same
        // frame so the badge appears exactly when the metal is struck.
        //
        // CRITICAL: this must MUTATE the existing chip, never destroy and rebuild it. The FX
        // coroutine holds a reference to `chip` and keeps animating it after this callback — if
        // the object were destroyed here, the next line of the FX would throw on a dead
        // RectTransform, abort the coroutine, and the screen would hang on the last frame with
        // only the X button to escape. That was exactly the "gets stuck" bug.
        yield return StartCoroutine(BlompoForgeFX.Play(this, forgeStage, chip, gem, hammerSprite, () =>
        {
            CardEnhancements.Apply(card, chosenOffer);
            if (DeckManager.instance != null) DeckManager.instance.RefreshHandUI();
            StampChip(chip, card);
        }));

        string cardName = card.cardData != null ? card.cardData.cardName : "It";
        promptText.text = $"<b>{cardName}</b> is now <b>{CardEnhancements.Name(chosenOffer)}</b>. Blompo is pleased.";
        yield return StartCoroutine(CloseAfter(1.4f));
    }

    // Updates an already-built card chip in place to show its new blessing (badge + charge count).
    private void StampChip(RectTransform chip, RuntimeCard card)
    {
        if (chip == null || card == null) return;

        Transform charges = chip.Find("Charges");
        if (charges != null)
        {
            TMP_Text ct = charges.GetComponent<TMP_Text>();
            if (ct != null) ct.text = card.isInfinite ? "∞" : card.currentUses.ToString();
        }

        if (card.enhancement == CardEnhancement.None || chip.Find("Badge") != null) return;

        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(card.enhancement));
        AddText(chip, "Badge", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(CARD_W - 16f, 28f),
            CardEnhancements.Name(card.enhancement), 16f, FontStyles.Bold, gem, TextAlignmentOptions.Center);
    }

    private IEnumerator CloseAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        Hide();
    }

    // ---- chip builders ----
    private GameObject BuildCardChip(Transform parent, RuntimeCard card, float scale = 1f)
    {
        RectTransform rt = AddPoint(parent, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CARD_W, CARD_H));
        rt.localScale = Vector3.one * scale;
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        // Scaled, so a shrunk card actually reserves less room in the layout group.
        le.preferredWidth = CARD_W * scale; le.preferredHeight = CARD_H * scale;

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = T.SurfaceRaised;
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", FlatUI.Outline(5, 1), T.Border, false);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        if (card.cardData != null && card.cardData.cardArt != null)
        {
            Image art = AddImage(rt, "Art", card.cardData.cardArt, Color.white, false);
            art.preserveAspect = true;
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            art.rectTransform.pivot = new Vector2(0.5f, 1f);
            art.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            art.rectTransform.sizeDelta = new Vector2(CARD_W - 46f, CARD_W - 46f);
        }

        // Two lines of room so long names ("Create Platform") wrap clear of the art above.
        TMP_Text nameText = AddText(rt, "Name", new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(CARD_W - 20f, 52f),
            card.cardData != null ? card.cardData.cardName : "?", 19f, FontStyles.Bold,
            T.TextBody, TextAlignmentOptions.Bottom);
        nameText.enableWordWrapping = true;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 13f; nameText.fontSizeMax = 19f;

        string charges = card.isInfinite ? "∞" : card.currentUses.ToString();
        AddText(rt, "Charges", new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(CARD_W - 22f, 32f),
            charges, 21f, FontStyles.Bold, FlatUI.Charges, TextAlignmentOptions.Center);

        if (card.enhancement != CardEnhancement.None)
        {
            Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(card.enhancement));
            AddText(rt, "Badge", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(CARD_W - 16f, 26f),
                CardEnhancements.Name(card.enhancement), 15f, FontStyles.Bold, gem, TextAlignmentOptions.Center);
        }
        return rt.gameObject;
    }

    private GameObject BuildOfferChip(Transform parent, CardEnhancement e, bool playable = true)
    {
        RectTransform rt = AddPoint(parent, "Offer", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(OFFER_W, OFFER_H));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = OFFER_W; le.preferredHeight = OFFER_H;

        // Rarity used to be a gem set in gold. Without that frame the COLOUR has to carry rarity
        // by itself, so it now tints the sigil, the border, the name and the label together —
        // four quiet signals instead of one loud jewel.
        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(e));
        if (!playable)
        {
            // Drained of colour so it reads as "offered, but nothing here can take it".
            float grey = (gem.r + gem.g + gem.b) / 3f;
            gem = Color.Lerp(new Color(grey, grey, grey), gem, 0.22f);
        }

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = T.SurfaceRaised;
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", FlatUI.Outline(5, 1), new Color(gem.r, gem.g, gem.b, 0.45f), false);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        Image glow = AddImage(rt, "Glow", FlatUI.SoftGlow(), new Color(gem.r, gem.g, gem.b, 0.20f), false);
        glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        glow.rectTransform.anchoredPosition = new Vector2(0f, -112f);
        glow.rectTransform.sizeDelta = new Vector2(220f, 220f);

        // The sigil: a point of light where the gem used to sit.
        Image star = AddImage(rt, "Sigil", FlatUI.FourPointStar(), gem, false);
        star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        star.rectTransform.anchoredPosition = new Vector2(0f, -112f);
        star.rectTransform.sizeDelta = new Vector2(104f, 104f);

        AddText(rt, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(OFFER_W - 34f, 52f),
            CardEnhancements.Name(e), 30f, FontStyles.Bold, gem, TextAlignmentOptions.Top);

        TMP_Text desc = AddText(rt, "Desc", new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(OFFER_W - 60f, 130f),
            CardEnhancements.Description(e), 22f, FontStyles.Normal,
            T.TextBody, TextAlignmentOptions.Top);
        desc.enableWordWrapping = true;

        AddText(rt, "Rarity", new Vector2(0.5f, 0f), new Vector2(0f, 30f),  new Vector2(OFFER_W - 40f, 34f),
            playable ? CardEnhancements.RarityOf(e).ToString().ToUpper() : "NO VALID CARD", 18f, FontStyles.Bold,
            new Color(gem.r, gem.g, gem.b, 0.80f), TextAlignmentOptions.Center);

        if (!playable) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

        return rt.gameObject;
    }

    // Every card the player still owns. Exhausted cards are excluded — they're out of the run, so
    // blessing one would be a dead pick.
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

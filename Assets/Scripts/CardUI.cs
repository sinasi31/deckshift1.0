using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Görseller")]
    public Image cardArtImage;
    public TextMeshProUGUI keyHintText;
    public GameObject selectionFrame;
    public TextMeshProUGUI usesText;
    public Transform shiftCostContainer;
    public GameObject shiftPointPrefab;
    public TextMeshProUGUI costText; // Maliyet sayısı (büyük sol-üst daire)

    [Header("Hizalama Ayarları")]
    public float pointSpacing = 20f; // Noktalar arası boşluk (Bunu Inspector'dan değiştirebilirsin)

    [Header("Hover")]
    public GameObject descriptionPanel;
    public TextMeshProUGUI descriptionText;
    public float selectionLiftAmount = 50f;

    [Header("Hover Art Fade")]
    [SerializeField] private float hoverFadeTargetAlpha = 0.12f;
    [SerializeField] private float hoverFadeDuration = 0.15f;

    private Coroutine artFadeCoroutine;

    private RuntimeCard myCard;
    private int myIndex;
    private Vector3 originalScale;
    private RectTransform rectTransform;

    public RuntimeCard GetCard()
    {
        return myCard;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;
    }

    public void Setup(RuntimeCard card, int index)
    {
        myCard = card;
        myIndex = index;

        if (cardArtImage != null) cardArtImage.sprite = card.cardData.cardArt;
        if (keyHintText != null) keyHintText.text = $"[{index + 1}]";

        // Açıklama
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false);
            if (descriptionText != null)
                descriptionText.text = $"<b>{card.cardData.cardName}</b>\n\n{card.cardData.description}";
        }

        // Uses
        if (usesText != null)
        {
            if (card.isInfinite) usesText.text = "∞";
            else
            {
                usesText.text = card.currentUses.ToString();
                usesText.color = (card.currentUses == 1) ? Color.red : Color.white;
            }
        }

        // --- MALİYET: tek sayı (büyük sol-üst daire) ---
        // Eski nokta (dot) sistemi kaldırıldı; maliyet artık costText'te sayı olarak gösteriliyor.
        if (costText != null) costText.text = card.cardData.shiftCost.ToString();
        // -----------------------------

        RefreshBlessingBadge(card);
        RefreshStaggerFace(card);

        UpdateSelectionVisual();
    }

    // --- Stagger's card face ------------------------------------------------------------------
    // Stagger's art is not built like the rest of the set. Every other card carries two painted
    // medallions in its top corners (Shift cost left, charges right); Stagger's has NEITHER, and
    // instead a single HEART centred along the top edge. That heart is the cost slot, because what
    // Stagger costs is HP — an escalating price that goes up every play, for the whole run
    // (PlayerController.PerformStagger).
    //
    // So the two normal readouts are switched OFF for this card rather than left to float over
    // artwork that has no sockets for them, and the price is drawn into the heart instead.
    //
    // ⚠️ THE NUMBER IS PLACED AGAINST THE DISPLAYED IMAGE, NOT THE RECT. CardArt is 200x300 with
    // preserveAspect ON, and the Stagger art is 124x210 — a narrower aspect — so it letterboxes
    // with a bar down each side and the artwork is ~177px wide, not 200. Measuring the heart as a
    // fraction of the RECT would put the number off-centre and too wide. It is measured as a
    // fraction of the SPRITE and mapped through the letterbox each time the rect resizes.
    //
    // Fractions below are measured off Assets/Art/stagger.png. The heart's outline spans x 38-86,
    // y 2-44 in the FILE, and its widest legible band — where a number actually fits, above the
    // taper — is centred on y 19. Interior fill is a flat #550A0F, so pale text reads cleanly.
    //
    // ⚠️ They are expressed against the SPRITE RECT, not the file. Unity's auto-slice trimmed the
    // transparent margin, so the sprite is 118x205 at offset (3,3) inside the 124x210 png — a
    // fraction taken against the file would sit the number low and slightly small. art.rect below
    // is the sprite rect, which is why the two agree.
    private const float HEART_CX = 59f / 118f;      // dead-centre, as it happens
    private const float HEART_CY = 17f / 205f;      // from the TOP of the sprite
    private const float HEART_W = 44f / 118f;
    private const float HEART_H = 28f / 205f;

    // The name plate along the bottom edge. Every other card in the set has its title PAINTED into
    // the artwork; Stagger's plate was left empty, which renders as a black bar and reads as an
    // unfinished card next to its neighbours in the hand. Same measurement basis as the heart:
    // the framed interior is file-y 184-201, inset here so the text never touches the frame.
    private const float PLATE_CY = 190.5f / 205f;
    private const float PLATE_W = 96f / 118f;
    private const float PLATE_H = 16f / 205f;
    // The set's title gold, matching the painted plates on the other cards.
    private static readonly Color STAGGER_NAME_COLOR = new Color(0.85f, 0.72f, 0.36f, 1f);

    private static readonly Color STAGGER_COST_COLOR = new Color(0.98f, 0.90f, 0.87f, 1f);
    // Shown when the next Stagger costs at least as much HP as the player has left. This is the
    // only place the run's actual fail state is visible before it happens, so it must be loud.
    private static readonly Color STAGGER_LETHAL_COLOR = new Color(1f, 0.30f, 0.28f, 1f);

    private TextMeshProUGUI staggerCostText;
    private TextMeshProUGUI staggerNameText;
    private Vector2 staggerHostSize = Vector2.zero;
    private bool isStaggerCard;

    private void RefreshStaggerFace(RuntimeCard card)
    {
        isStaggerCard = card != null && card.cardData != null
                        && card.cardData.actionType == CardActionType.Stagger;

        // The two painted medallions belong to the normal card frame, which this art doesn't have.
        if (costText != null) costText.gameObject.SetActive(!isStaggerCard);
        if (usesText != null) usesText.gameObject.SetActive(!isStaggerCard);

        if (!isStaggerCard)
        {
            if (staggerCostText != null) staggerCostText.gameObject.SetActive(false);
            if (staggerNameText != null) staggerNameText.gameObject.SetActive(false);
            return;
        }

        EnsureStaggerFace();
        staggerCostText.gameObject.SetActive(true);
        staggerNameText.gameObject.SetActive(true);
        staggerNameText.text = card.cardData.cardName.ToUpperInvariant();
        staggerHostSize = Vector2.zero;   // force a re-place against the current rect
        TickStaggerFace();
    }

    private void EnsureStaggerFace()
    {
        if (staggerCostText != null) return;

        staggerCostText = MakeStaggerLabel("StaggerCost", 12f, 30f, STAGGER_COST_COLOR);
        staggerNameText = MakeStaggerLabel("StaggerName", 8f, 15f, STAGGER_NAME_COLOR);
    }

    private TextMeshProUGUI MakeStaggerLabel(string name, float minSize, float maxSize, Color color)
    {
        RectTransform host = cardArtImage != null ? cardArtImage.rectTransform : (RectTransform)transform;

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // top-left; placed by pixel offset below
        rt.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (costText != null) t.font = costText.font;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;   // 3-digit costs arrive fast: 8, 16, 24... 104
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // Placement and value are refreshed from Update rather than set once: the price is read off the
    // live player, and the lethal warning depends on current HP, which changes while the card just
    // sits in the hand. Both are a couple of compares — nothing is written unless it changed.
    private void TickStaggerFace()
    {
        if (!isStaggerCard || staggerCostText == null) return;

        RectTransform host = (RectTransform)staggerCostText.transform.parent;
        Vector2 size = host.rect.size;

        if (size != staggerHostSize && size.x > 0f && size.y > 0f)
        {
            staggerHostSize = size;

            // Map the sprite-space fractions through preserveAspect's letterbox.
            Sprite art = cardArtImage != null ? cardArtImage.sprite : null;
            float aspect = art != null && art.rect.height > 0f ? art.rect.width / art.rect.height
                                                               : size.x / size.y;
            float dh = Mathf.Min(size.y, size.x / aspect);
            float dw = dh * aspect;
            float padX = (size.x - dw) * 0.5f;
            float padY = (size.y - dh) * 0.5f;

            RectTransform rt = staggerCostText.rectTransform;
            rt.sizeDelta = new Vector2(HEART_W * dw, HEART_H * dh);
            rt.anchoredPosition = new Vector2(padX + HEART_CX * dw, -(padY + HEART_CY * dh));

            RectTransform nrt = staggerNameText.rectTransform;
            nrt.sizeDelta = new Vector2(PLATE_W * dw, PLATE_H * dh);
            nrt.anchoredPosition = new Vector2(padX + 0.5f * dw, -(padY + PLATE_CY * dh));
        }

        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (p == null) return;

        float cost = p.NextStaggerCost;
        string label = Mathf.RoundToInt(cost).ToString();
        if (staggerCostText.text != label) staggerCostText.text = label;

        Color want = cost >= p.CurrentHealth ? STAGGER_LETHAL_COLOR : STAGGER_COST_COLOR;
        if (staggerCostText.color != want) staggerCostText.color = want;

        // Say the trade out loud on hover — the heart shows the price, but not what it buys.
        if (descriptionText != null && myCard != null)
        {
            string text = $"<b>{myCard.cardData.cardName}</b>\n\n" +
                          $"Pay <b>{label} HP</b> to claw back <b>{p.staggerShiftGain} Shift</b>.\n" +
                          $"The price rises by {Mathf.RoundToInt(p.staggerHealthStep)} every time you use it. " +
                          $"It cannot be discarded.";
            if (descriptionText.text != text) descriptionText.text = text;
        }
    }

    // --- Blompo blessing mark ------------------------------------------------------------
    // A blessed card must be identifiable at a glance in the hand. Built procedurally here (house
    // style) rather than in the CardTemplate prefab: that prefab has known scale corruption and is
    // blocked on new art, so we deliberately don't touch it.
    //
    // IT IS PARENTED TO THE ART, NOT THE CARD ROOT. The root's RectTransform is much taller than
    // the visible card, so the old badge — anchored to the root's top-right corner — floated off
    // the card's right EDGE at mid-height instead of sitting on the card at all. cardArtImage is
    // the frame the player actually sees, so anchoring there lands the mark correctly no matter
    // what the corrupt root geometry is doing.
    //
    // THE LOOK inverts the gem it replaces. The old badge was an OBJECT: a jewel set in an ornate
    // gold ring — the chrome this UI pass exists to remove. This is LIGHT INSCRIBED ON THE CARD:
    // Blompo's own sigil glowing over a soft dark halo, which lifts it off busy artwork without
    // drawing a frame.
    //
    // ⚠️ IT IS ONE COLOUR ON EVERY BLESSING, AND MUST STAY THAT WAY (designer, 2026-08-06). The
    // incoming card art telegraphs each CARD's rarity in colour — dark grey Common, light grey
    // Uncommon, yellow Rare, purple Epic (no Legendary cards yet). An earlier pass here tinted the
    // mark by the BLESSING's rarity, which is a different thing but would not survive contact with
    // a player: two colour-coded rarity systems on one card, disagreeing with each other (this one
    // called Rare azure where the art calls it yellow). So the mark carries no rarity at all, and
    // its colour is chosen to sit OUTSIDE that palette — teal is nowhere near grey, yellow or
    // purple, and is pushed green of Shift-blue so it can't be misread as a Shift cost either.
    //
    // Blessing hierarchy moved to a channel the art doesn't use: only Epic and Legendary blessings
    // PULSE. If the rarity palette ever changes, re-pick this colour against it — do not reach for
    // FlatUI.RarityColor here.
    //
    // The mark deliberately does NOT say WHICH blessing this is; all seven share one sigil and the
    // hover text names it. Seven legible glyphs at this size is a bespoke-art job, not a procedural
    // one.
    private static readonly Color BLESSED_COLOR = new Color(0.42f, 0.90f, 0.82f, 1f);
    private const float MARK_SIZE = 34f;    // the sigil itself
    private const float MARK_GLOW = 46f;    // rarity halo
    private const float MARK_SHADE = 58f;   // dark halo that separates it from the artwork
    // Offsets from the art rect's bottom-left, in its own 200x300 units.
    //
    // These are NOT arbitrary padding. cardArtImage's sprite is the WHOLE card face — painted
    // frame, cost medallions and name plate included — not just the inner picture. Measured on the
    // real cards, the inner picture panel occupies roughly 10%-80% of the card's height, so an
    // inset of 25 put the mark down inside the painted NAME PLATE, where it clipped the card's
    // title. 62 sits it just inside the picture's bottom-left corner.
    private const float MARK_INSET_X = 34f;
    private const float MARK_INSET_Y = 62f;

    private GameObject blessMark;
    private Image blessShade, blessGlow, blessSigil;
    private bool blessPulses;
    private float blessGlowAlpha;

    private void RefreshBlessingBadge(RuntimeCard card)
    {
        bool blessed = card != null && card.enhancement != CardEnhancement.None;

        if (!blessed)
        {
            if (blessMark != null) blessMark.SetActive(false);
            return;
        }

        Rarity rarity = CardEnhancements.RarityOf(card.enhancement);

        // Motion, not colour, is the hierarchy — see the note above. Only the two blessings worth
        // noticing animate, which is what makes one catch your eye in a full hand.
        blessPulses = rarity == Rarity.Epic || rarity == Rarity.Legendary;
        blessGlowAlpha = 0.30f;

        EnsureBlessMark();
        blessMark.SetActive(true);

        blessShade.color = new Color(0.02f, 0.02f, 0.03f, 0.72f);
        blessGlow.color = new Color(BLESSED_COLOR.r, BLESSED_COLOR.g, BLESSED_COLOR.b, blessGlowAlpha);
        blessSigil.color = BLESSED_COLOR;

        // Say what it does on hover, alongside the card's own text.
        if (descriptionText != null)
            descriptionText.text =
                $"<b>{card.cardData.cardName}</b>\n\n{card.cardData.description}\n\n" +
                $"<b>{CardEnhancements.Name(card.enhancement)}</b> — {CardEnhancements.Description(card.enhancement)}";
    }

    private void EnsureBlessMark()
    {
        if (blessMark != null) return;

        RectTransform host = cardArtImage != null ? cardArtImage.rectTransform : (RectTransform)transform;

        blessMark = new GameObject("BlessMark", typeof(RectTransform));
        RectTransform rt = blessMark.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        // Bottom-left of the picture. The cost medallion owns the top-left and the card's own
        // rarity tag owns the top-right, so this is the one corner free on every card in the set.
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(MARK_INSET_X, MARK_INSET_Y);
        rt.sizeDelta = new Vector2(MARK_SIZE, MARK_SIZE);

        blessShade = AddMarkLayer("Shade", FlatUI.SoftGlow(), MARK_SHADE);
        blessGlow = AddMarkLayer("Glow", FlatUI.SoftGlow(), MARK_GLOW);
        blessSigil = AddMarkLayer("Sigil", FlatUI.ArcaneSigil(), MARK_SIZE);
    }

    private Image AddMarkLayer(string name, Sprite sprite, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(blessMark.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // Unscaled on purpose: blessed cards are on screen during the reward screen, which holds the
    // game paused at timeScale 0.
    private void TickBlessMark()
    {
        if (!blessPulses || blessGlow == null || blessMark == null || !blessMark.activeSelf) return;

        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f);
        Color c = blessGlow.color;
        c.a = blessGlowAlpha * Mathf.Lerp(0.55f, 1.25f, t);
        blessGlow.color = c;
    }

    private void Update()
    {
        if (myCard == null) return;
        UpdateSelectionVisual();
        TickBlessMark();
        TickStaggerFace();
    }

    private void UpdateSelectionVisual()
    {
        bool isSelected = myCard.isSelected;
        if (selectionFrame != null) selectionFrame.SetActive(isSelected);

        Vector3 targetScale = isSelected ? originalScale * 1.1f : originalScale;
        float targetY = isSelected ? selectionLiftAmount : 0f;
        float speed = Time.deltaTime * 15f;

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed);

        if (rectTransform != null)
        {
            Vector2 currentPos = rectTransform.anchoredPosition;
            Vector2 targetPos = new Vector2(currentPos.x, targetY);
            rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos, speed);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (DeckManager.instance != null) DeckManager.instance.SelectCard(myIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(true);
            descriptionPanel.transform.SetAsLastSibling();
        }
        StartArtFade(hoverFadeTargetAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
        StartArtFade(1f);
    }

    // Cleanly (re)starts the artwork alpha fade so rapid enter/exit can't leave it stuck.
    private void StartArtFade(float targetAlpha)
    {
        if (cardArtImage == null) return;
        if (artFadeCoroutine != null) StopCoroutine(artFadeCoroutine);
        artFadeCoroutine = StartCoroutine(FadeArtAlpha(targetAlpha));
    }

    private IEnumerator FadeArtAlpha(float targetAlpha)
    {
        Color c = cardArtImage.color;
        float startAlpha = c.a;

        if (hoverFadeDuration <= 0f)
        {
            c.a = targetAlpha;
            cardArtImage.color = c;
            artFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < hoverFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hoverFadeDuration);
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            cardArtImage.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        cardArtImage.color = c;
        artFadeCoroutine = null;
    }
}
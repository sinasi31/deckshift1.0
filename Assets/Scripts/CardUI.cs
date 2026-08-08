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

        UpdateSelectionVisual();
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
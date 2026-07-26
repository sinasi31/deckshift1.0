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

    // --- Blompo blessing badge -----------------------------------------------------------
    // A blessed card must be identifiable at a glance in the hand. Built procedurally here
    // (house style) so no CardTemplate prefab rewiring is needed — that prefab has known scale
    // corruption and is blocked on new art, so we deliberately don't touch it.
    // PLACEHOLDER LOOK: rarity gem + glow in the top-right corner. Intended to be replaced with
    // bespoke per-enhancement art later.
    private GameObject blessBadge;

    private void RefreshBlessingBadge(RuntimeCard card)
    {
        bool blessed = card != null && card.enhancement != CardEnhancement.None;

        if (!blessed)
        {
            if (blessBadge != null) blessBadge.SetActive(false);
            return;
        }

        Color gem = RelicUISprites.GemColor(CardEnhancements.RarityOf(card.enhancement));

        if (blessBadge == null)
        {
            blessBadge = new GameObject("BlessBadge", typeof(RectTransform));
            RectTransform brt = blessBadge.GetComponent<RectTransform>();
            brt.SetParent(transform, false);
            // Top-right corner, hanging slightly off the card so it reads as attached-on.
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-14f, -14f);
            brt.sizeDelta = new Vector2(34f, 34f);

            AddBadgePart("Glow", RelicUISprites.Glow(), 54f);
            AddBadgePart("Setting", RelicUISprites.GemSetting(), 34f);
            AddBadgePart("Gem", RelicUISprites.Gem(), 21f);
        }

        blessBadge.SetActive(true);
        Transform glowT = blessBadge.transform.Find("Glow");
        Transform gemT = blessBadge.transform.Find("Gem");
        if (glowT != null) glowT.GetComponent<Image>().color = new Color(gem.r, gem.g, gem.b, 0.55f);
        if (gemT != null) gemT.GetComponent<Image>().color = gem;

        // Say what it does on hover, alongside the card's own text.
        if (descriptionText != null)
            descriptionText.text =
                $"<b>{card.cardData.cardName}</b>\n\n{card.cardData.description}\n\n" +
                $"<b>{CardEnhancements.Name(card.enhancement)}</b> — {CardEnhancements.Description(card.enhancement)}";
    }

    private void AddBadgePart(string name, Sprite sprite, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(blessBadge.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
    }

    private void Update()
    {
        if (myCard != null) UpdateSelectionVisual();
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
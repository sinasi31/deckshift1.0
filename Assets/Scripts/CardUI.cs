using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Temel Görseller")]
    public Image cardArtImage;
    public TextMeshProUGUI keyHintText;

    [Header("Seçim Görselleri")]
    public GameObject selectionFrame;

    // Kart seçilince ne kadar yukarı zıplasın? (Örn: 50 idealdir)
    public float selectionLiftAmount = 50f;

    [Header("Mekanik Görseller")]
    public TextMeshProUGUI usesText;
    public Transform shiftCostContainer;
    public GameObject shiftPointPrefab;

    [Header("Hover (Açıklama) Ayarları")]
    public GameObject descriptionPanel;
    public TextMeshProUGUI descriptionText;

    private RuntimeCard myCard;
    private int myIndex;
    private Vector3 originalScale;
    private RectTransform rectTransform; // UI Pozisyonu için gerekli
    private bool isInitialized = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;
        isInitialized = true;
    }

    public void Setup(RuntimeCard card, int index)
    {
        if (!isInitialized)
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = transform.localScale;
            isInitialized = true;
        }

        myCard = card;
        myIndex = index;

        cardArtImage.sprite = card.cardData.cardArt;
        keyHintText.text = $"[{index + 1}]";

        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false);
            if (descriptionText != null)
            {
                descriptionText.text = $"<b>{card.cardData.cardName}</b>\n\n{card.cardData.description}";
            }
        }

        if (usesText != null)
        {
            if (card.isInfinite)
                usesText.text = "∞";
            else
            {
                usesText.text = card.currentUses.ToString();
                usesText.color = (card.currentUses == 1) ? Color.red : Color.white;
            }
        }

        if (shiftCostContainer != null && shiftPointPrefab != null)
        {
            foreach (Transform child in shiftCostContainer) Destroy(child.gameObject);
            for (int i = 0; i < card.cardData.shiftCost; i++)
            {
                Instantiate(shiftPointPrefab, shiftCostContainer);
            }
        }

        UpdateSelectionVisual();
    }

    private void Update()
    {
        if (myCard != null)
        {
            UpdateSelectionVisual();
        }
    }

    private void UpdateSelectionVisual()
    {
        bool isSelected = myCard.isSelected;

        if (selectionFrame != null)
            selectionFrame.SetActive(isSelected);

        // --- SCALE (BÜYÜME) AYARI ---
        // Seçiliyse %10 büyüt, değilse orijinal boyutta kalsın (küçültmeyelim ki net dursun)
        // Eğer seçili olmayanı küçültmek istersen 0.85f yapabilirsin.
        Vector3 targetScale = isSelected ? originalScale * 1.1f : originalScale * 0.9f;

        // --- POZİSYON (YUKARI KALDIRMA) AYARI ---
        // Seçiliyse Y ekseninde yukarı kaldır, değilse 0'a (yerine) indir.
        float targetY = isSelected ? selectionLiftAmount : 0f;

        // Lerp ile yumuşak geçişler
        float speed = Time.deltaTime * 15f; // Biraz hızlandırdım daha tepkisel olsun

        // 1. Boyutlandır
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed);

        // 2. Yukarı Taşı (Sadece Y eksenini değiştiriyoruz, X Layout Group tarafından yönetiliyor)
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
            if (DeckManager.instance != null)
            {
                DeckManager.instance.SelectCard(myIndex);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(true);
            descriptionPanel.transform.SetAsLastSibling();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false);
        }
    }

    public void PlayUseAnimation()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        group.interactable = false;
        group.blocksRaycasts = false;

        StartCoroutine(AnimateRoutine(group));
    }

    private IEnumerator AnimateRoutine(CanvasGroup group)
    {
        float timer = 0f;
        float duration = 0.3f;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (Vector3.up * 200f);

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.3f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / duration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (group != null) group.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
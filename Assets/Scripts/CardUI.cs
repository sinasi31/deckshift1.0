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

    [Header("Hizalama Ayarları")]
    public float pointSpacing = 20f; // Noktalar arası boşluk (Bunu Inspector'dan değiştirebilirsin)

    [Header("Hover")]
    public GameObject descriptionPanel;
    public TextMeshProUGUI descriptionText;
    public float selectionLiftAmount = 50f;

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

        // --- YENİ HİZALAMA SİSTEMİ ---
        if (shiftCostContainer != null && shiftPointPrefab != null)
        {
            // Önce eskileri temizle
            foreach (Transform child in shiftCostContainer) Destroy(child.gameObject);

            int cost = card.cardData.shiftCost;

            // Eğer maliyet 0 ise hiçbir şey yapma
            if (cost > 0)
            {
                // Toplam genişliği hesapla (Nokta sayısı - 1 * Boşluk)
                float totalWidth = (cost - 1) * pointSpacing;

                // Başlangıç noktası (Merkezden sola doğru yarım genişlik kadar git)
                float startX = -totalWidth / 2f;

                for (int i = 0; i < cost; i++)
                {
                    GameObject p = Instantiate(shiftPointPrefab, shiftCostContainer);
                    RectTransform rt = p.GetComponent<RectTransform>();

                    // Pozisyonu ayarla: Başlangıç + (Sıra * Boşluk)
                    rt.anchoredPosition = new Vector2(startX + (i * pointSpacing), 0f);
                }
            }
        }
        // -----------------------------

        UpdateSelectionVisual();
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
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
    }
}
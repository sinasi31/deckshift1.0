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

    public float selectionLiftAmount = 50f;

    [Header("Mekanik Görseller")]
    public TextMeshProUGUI usesText;
    public Transform shiftCostContainer;
    public GameObject shiftPointPrefab;

    [Header("Layout Ayarları (Nokta Hizalama)")]
    public float firstDotXPosition = -25f; 
    public float pointSpacing = 15f;       

    [Header("Hover (Açıklama) Ayarları")]
    public GameObject descriptionPanel;
    public TextMeshProUGUI descriptionText;

    private RuntimeCard myCard;
    private int myIndex;
    private Vector3 originalScale;
    private RectTransform rectTransform;
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

        if (cardArtImage != null) cardArtImage.sprite = card.cardData.cardArt;
        if (keyHintText != null) keyHintText.text = $"[{index + 1}]";

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

            int cost = card.cardData.shiftCost;
            if (cost > 0)
            {
                for (int i = 0; i < cost; i++)
                {
                    GameObject point = Instantiate(shiftPointPrefab, shiftCostContainer);
                    RectTransform rt = point.GetComponent<RectTransform>();

                    // İlk nokta sabit bir yerde başlar, diğerleri sağa doğru eklenir
                    float xPos = firstDotXPosition + (i * pointSpacing);

                    rt.anchoredPosition = new Vector2(xPos, 0f);
                }
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

        Vector3 targetScale = isSelected ? originalScale * 1.1f : originalScale * 0.9f;
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
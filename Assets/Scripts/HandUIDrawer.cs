using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandUIDrawer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Bağlantıları")]
    public RectTransform handContainer;

    [Header("Pozisyon Ayarları")]
    public float hiddenY = -150f;
    public float visibleY = 50f;
    public float slideSpeed = 15f;

    [Header("Durum Kontrolü (Debug İçin)")]
    public bool isHovered = false;
    public bool isCardDragging = false;
    public bool isLocked = false;

    private float targetY;
    private Image drawerImage;

    public static HandUIDrawer instance;

    private void Awake()
    {
        instance = this;
        drawerImage = GetComponent<Image>();
    }

    void Start()
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();
        targetY = hiddenY;
        handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, hiddenY);
    }

    void Update()
    {
        bool shouldBeOpen = !isLocked && (isHovered || isCardDragging);

        targetY = shouldBeOpen ? visibleY : hiddenY;

        if (Mathf.Abs(handContainer.anchoredPosition.y - targetY) > 0.5f)
        {
            float newY = Mathf.Lerp(handContainer.anchoredPosition.y, targetY, Time.deltaTime * slideSpeed);
            handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, newY);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    public void SetCardDraggingState(bool state)
    {
        isCardDragging = state;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (locked)
        {
            isHovered = false;
        }
        if (drawerImage != null) drawerImage.raycastTarget = !locked;
    }
}
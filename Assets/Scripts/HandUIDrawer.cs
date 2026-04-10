using UnityEngine;
using UnityEngine.EventSystems; // Mouse'un UI üzerindeki hareketlerini algılamak için şart

// IPointerEnter ve Exit, Unity'nin UI sisteminin kalbidir.
public class HandUIDrawer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Bağlantıları")]
    public RectTransform handContainer; // Kartların içinde durduğu asıl panel

    [Header("Pozisyon Ayarları")]
    public float hiddenY = -150f; // Gizliyken Y koordinatı
    public float visibleY = 50f;  // Açıkken Y koordinatı
    public float slideSpeed = 15f;

    [Header("Durum Kontrolü (Debug İçin)")]
    public bool isHovered = false;       // Mouse panelin üzerinde mi?
    public bool isCardDragging = false;  // Oyuncu şu an bir kart sürüklüyor mu?

    private float targetY;

    void Start()
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        targetY = hiddenY;
        handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, hiddenY);
    }

    void Update()
    {
        // 1. MANTIK: Çekmece ne zaman açık kalmalı?
        // - Ya mouse çekmecenin üzerindeyse
        // - VEYA oyuncu bir kart tutuyorsa (savaş alanına sürüklerken çekmece kapanmasın diye)
        bool shouldBeOpen = isHovered || isCardDragging;

        // 2. Hedefi Belirle
        targetY = shouldBeOpen ? visibleY : hiddenY;

        // 3. Performanslı Kaydırma (Eğer zaten hedefine ulaştıysa Lerp yapmayı bırak)
        if (Mathf.Abs(handContainer.anchoredPosition.y - targetY) > 0.5f)
        {
            float newY = Mathf.Lerp(handContainer.anchoredPosition.y, targetY, Time.deltaTime * slideSpeed);
            handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, newY);
        }
    }

    // --- UNITY UI EVENT SİSTEMİ ---

    // Mouse panelin içine girdiğinde otomatik çalışır
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    // Mouse panelden çıktığında otomatik çalışır
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    // Kart scriptlerin (örn: CardDrag.cs) bu fonksiyonu çağıracak
    public void SetCardDraggingState(bool state)
    {
        isCardDragging = state;
    }
}
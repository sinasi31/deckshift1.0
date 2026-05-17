using UnityEngine;
using UnityEngine.EventSystems; // UI olaylarýný yakalamak için bu kütüphane ÞART!

// Bu scripti hangi butona atarsan, mouse üstüne gelince o buton büyür.
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    public float scaleAmount = 1.1f; // Yüzde 10 büyüsün (1.1 katý)

    void Start()
    {
        // Oyun baþýnda butonun orijinal boyutunu kaydet
        originalScale = transform.localScale;
    }

    // Mouse butonun üstüne GELDÝÐÝNDE çalýþýr
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * scaleAmount; // Büyüt
    }

    // Mouse butonun üstünden GÝTTÝÐÝNDE çalýþýr
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale; // Eski haline döndür
    }
}

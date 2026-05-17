using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    public float hoverScale = 1.2f;
    public float speed = 10f;

    private Vector3 targetScale;
    private Canvas myCanvas; // Kartýn üzerindeki Canvas

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        // Canvas bileþenini al
        myCanvas = GetComponent<Canvas>();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;

        // --- DEÐÝÞÝKLÝK BURADA ---
        // SetAsLastSibling YERÝNE Sorting Order kullanýyoruz.
        // Bu, kartý fiziksel olarak taþýmaz ama görsel olarak en öne çizer.
        if (myCanvas != null)
        {
            myCanvas.sortingOrder = 100; // Diðer her þeyin önüne geç
        }
        // -------------------------
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;

        // --- DEÐÝÞÝKLÝK BURADA ---
        // Eski haline (sýfýra) döndür
        if (myCanvas != null)
        {
            myCanvas.sortingOrder = 0;
        }
        // -------------------------
    }
}
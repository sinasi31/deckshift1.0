using System.Collections; // <-- Bunu en tepeye ekle
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Hover iþlemleri için bu kütüphane þart!

// Artýk kartýmýz fare hareketlerini dinleyecek (IPointer...)
public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Temel Görseller")]
    public Image cardArtImage; // Kartýn resmi
    public TextMeshProUGUI keyHintText; // [1], [2] yazýsý

    [Header("Mekanik Görseller")]
    public TextMeshProUGUI usesText; // Büyük Daire
    public Transform shiftCostContainer; // Küçük Dairelerin Kutusu
    public GameObject shiftPointPrefab;  // Küçük Daire Prefabý

    [Header("Hover (Açýklama) Ayarlarý")]
    public GameObject descriptionPanel; // Fare üzerine gelince açýlacak kutu
    public TextMeshProUGUI descriptionText; // O kutunun içindeki yazý

    // Setup sýrasýnda kartýn açýklamasýný panele yazacaðýz ama paneli gizli tutacaðýz
    public void Setup(RuntimeCard card, int keyHintNumber)
    {
        // 1. Resmi ve Ýpucunu ayarla (Ýsim artýk resimde olduðu için koda gerek yok)
        cardArtImage.sprite = card.cardData.cardArt;
        keyHintText.text = $"[{keyHintNumber}]";

        // 2. Açýklama Metnini Hazýrla (Ama henüz gösterme)
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false); // Baþlangýçta gizle
            if (descriptionText != null)
            {
                // Ýstersen kartýn ismini de açýklamaya ekleyebilirsin
                descriptionText.text = $"<b>{card.cardData.cardName}</b>\n\n{card.cardData.description}";
            }
        }

        // 3. Kullaným Hakký (Büyük Daire)
        if (usesText != null)
        {
            usesText.text = card.currentUses.ToString();
            if (card.currentUses == 1) usesText.color = Color.red;
            else usesText.color = Color.white;
        }

        // 4. Shift Maliyeti (Küçük Daireler)
        if (shiftCostContainer != null && shiftPointPrefab != null)
        {
            foreach (Transform child in shiftCostContainer) Destroy(child.gameObject);
            for (int i = 0; i < card.cardData.shiftCost; i++)
            {
                Instantiate(shiftPointPrefab, shiftCostContainer);
            }
        }
    }

    // --- HOVER (FARE ÜZERÝNE GELÝNCE) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Fare kartýn üzerine geldiðinde paneli aç
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(true);

            // Panelin her zaman en önde görünmesi için (diðer kartlarýn altýnda kalmasýn)
            descriptionPanel.transform.SetAsLastSibling();
        }
    }

    // --- HOVER ÇIKIÞ (FARE GÝDÝNCE) ---
    public void OnPointerExit(PointerEventData eventData)
    {
        // Fare karttan ayrýldýðýnda paneli kapat
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false);
        }
    }
    // --- BURADAN BAÞLA ---

    // Kart oynandýðýnda çaðrýlacak ana fonksiyon
    public void PlayUseAnimation()
    {
        // Kartýn týklanabilirliðini kapat (tekrar basýlmasýn)
        CanvasGroup group = GetComponent<CanvasGroup>();

        // Eðer kartta CanvasGroup yoksa kodla biz ekleyelim
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        group.interactable = false;
        group.blocksRaycasts = false;

        StartCoroutine(AnimateRoutine(group));
    }

    // Animasyonun saniye saniye iþlediði yer
    private IEnumerator AnimateRoutine(CanvasGroup group)
    {
        float timer = 0f;
        float duration = 0.4f; // Animasyon süresi

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(0, 300f, 0); // 300 birim yukarý

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * 1.5f; // 1.5 kat büyü

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / duration;

            // Hareketi yumuþatmak için SmoothStep formülü
            t = t * t * (3f - 2f * t);

            // 1. Yukarý Taþý
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            // 2. Büyüt
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // 3. Þeffaflaþtýr
            if (group != null) group.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        Destroy(gameObject); // Animasyon bitince kartý tamamen sil
    }
    // --- BURADA BÝTÝR ---
}
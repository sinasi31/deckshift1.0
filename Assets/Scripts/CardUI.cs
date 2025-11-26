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
}
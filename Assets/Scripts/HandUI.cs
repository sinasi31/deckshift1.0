using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HandUI : MonoBehaviour
{
    [Header("UI Ayarlarý")]
    public GameObject cardUIPrefab;
    public Transform handContainer;

    private void OnEnable()
    {
        DeckManager.OnHandChanged += UpdateHandDisplay;
    }

    private void OnDisable()
    {
        DeckManager.OnHandChanged -= UpdateHandDisplay;
    }

    private void Start()
    {
        UpdateHandDisplay();
    }

    private void UpdateHandDisplay()
    {
        foreach (Transform child in handContainer)
        {
            Destroy(child.gameObject);
        }

        List<RuntimeCard> currentHand = DeckManager.instance.GetCurrentHand();

        for (int i = 0; i < currentHand.Count; i++)
        {
            RuntimeCard card = currentHand[i];
            GameObject cardUIObject = Instantiate(cardUIPrefab, handContainer);

            CardUI cardUI = cardUIObject.GetComponent<CardUI>();
            if (cardUI != null)
            {
                // DEÐÝÞÝKLÝK BURADA:
                // Eskiden: cardUI.Setup(card, i + 2);
                // Þimdi: i + 1 yapýyoruz ki [1]'den baþlasýn.
                cardUI.Setup(card, i + 1);
            }
        }
    }
    // --- BURAYI YAPIÞTIR ---

    // Bu fonksiyonu DeckManager çaðýracak
    public void AnimateCardFromHand(int index)
    {
        // Eðer index hatalýysa veya kart yoksa iþlem yapma
        if (index < 0 || index >= handContainer.childCount) return;

        // 1. Oynanan kartý bul
        Transform cardTransform = handContainer.GetChild(index);

        // 2. KARTI KUTUDAN KOPAR (Çok Önemli!)
        // Kartý HandContainer'ýn dýþýna (bir üst ebeveynine) taþýyoruz.
        // Böylece diðer kartlar boþalan yeri hemen doldurabilir.
        cardTransform.SetParent(handContainer.parent);

        // 3. KARTI EN ÖNE AL
        // Uçarken diðer panellerin arkasýnda kalmamasý için
        cardTransform.SetAsLastSibling();

        // 4. ANÝMASYONU BAÞLAT
        // Az önce CardUI'ya eklediðimiz o büyüyüp uçma kodunu çalýþtýr
        CardUI cardUI = cardTransform.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.PlayUseAnimation();
        }
    }
    // -----------------------
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DeckViewUI : MonoBehaviour
{
    [Header("Butonlar & Sayaçlar")]
    public Button drawPileButton;      // Sað alttaki Deste butonu
    public TextMeshProUGUI drawCountText; // Üzerindeki sayý

    public Button discardPileButton;   // Sol alttaki Iskarta butonu
    public TextMeshProUGUI discardCountText; // Üzerindeki sayý

    [Header("Açýlýr Pencere (Pop-up)")]
    public GameObject viewPanel;       // Tüm ekraný kaplayan panel
    public Transform cardContainer;    // Kartlarýn dizileceði kutu (Grid Layout)
    public GameObject cardUIPrefab;    // Kart görsel prefabý
    public TextMeshProUGUI titleText;  // "Draw Pile" veya "Discard Pile" yazýsý
    public Button closeButton;         // Paneli kapatma butonu

    private void Start()
    {
        // Panel baþlangýçta kapalý olsun
        if (viewPanel != null) viewPanel.SetActive(false);

        // Buton týklamalarýný baðla
        if (drawPileButton) drawPileButton.onClick.AddListener(ShowDrawPile);
        if (discardPileButton) discardPileButton.onClick.AddListener(ShowDiscardPile);
        if (closeButton) closeButton.onClick.AddListener(CloseView);
    }

    private void Update()
    {
        // Sayaçlarý her karede güncelle (En kolayý bu)
        if (DeckManager.instance != null)
        {
            if (drawCountText)
                drawCountText.text = DeckManager.instance.GetDrawPile().Count.ToString();

            if (discardCountText)
                discardCountText.text = DeckManager.instance.GetDiscardPile().Count.ToString();
        }
    }

    // Deste Butonuna basýnca
    public void ShowDrawPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DRAW PILE", DeckManager.instance.GetDrawPile());
    }

    // Iskarta Butonuna basýnca
    public void ShowDiscardPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DISCARD PILE", DeckManager.instance.GetDiscardPile());
    }

    // Ortak Görüntüleme Fonksiyonu
    private void OpenView(string title, List<RuntimeCard> cardsToList)
    {
        viewPanel.SetActive(true);
        if (titleText) titleText.text = title;

        // 1. Önceki kartlarý temizle
        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Yeni kartlarý yarat
        foreach (RuntimeCard card in cardsToList)
        {
            GameObject cardObj = Instantiate(cardUIPrefab, cardContainer);

            // Mevcut CardUI scriptini kullanýyoruz
            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                // Index önemli deðil, sadece görüntüleme yapýyoruz (-1 verdim)
                ui.Setup(card, -1);

                // Týklanma özelliðini kapatalým ki pop-up içinden kart oynamasýnlar
                CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
                if (group == null) group = cardObj.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false; // Týklamayý engeller
            }
        }
    }

    public void CloseView()
    {
        viewPanel.SetActive(false);
    }
}
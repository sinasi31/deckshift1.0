using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DeckViewUI : MonoBehaviour
{
    [Header("Butonlar & Sayaçlar")]
    public Button drawPileButton;
    public TextMeshProUGUI drawCountText;
    public Button discardPileButton;
    public TextMeshProUGUI discardCountText;

    [Header("Açılır Pencere (Pop-up)")]
    public GameObject viewPanel;
    public Transform cardContainer;
    public GameObject cardUIPrefab;
    public TextMeshProUGUI titleText;
    public Button closeButton;

    [Header("Exhaust (Tükenenler)")]
    public Button exhaustButton;
    public TextMeshProUGUI exhaustCountText;

    private void Start()
    {
        if (viewPanel != null) viewPanel.SetActive(false);
        if (exhaustButton) exhaustButton.onClick.AddListener(ShowExhaustPile);
        if (drawPileButton) drawPileButton.onClick.AddListener(ShowDrawPile);
        if (discardPileButton) discardPileButton.onClick.AddListener(ShowDiscardPile);
        if (closeButton) closeButton.onClick.AddListener(CloseView);
    }

    private void Update()
    {
        if (exhaustCountText && DeckManager.instance != null)
            exhaustCountText.text = DeckManager.instance.GetExhaustPile().Count.ToString();

        if (DeckManager.instance != null)
        {
            if (drawCountText)
                drawCountText.text = DeckManager.instance.GetDrawPile().Count.ToString();
            if (discardCountText)
                discardCountText.text = DeckManager.instance.GetDiscardPile().Count.ToString();
        }
    }

    public void ShowDrawPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DRAW PILE", DeckManager.instance.GetDrawPile());
    }

    public void ShowDiscardPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("DISCARD PILE", DeckManager.instance.GetDiscardPile());
    }

    public void ShowExhaustPile()
    {
        if (DeckManager.instance == null) return;
        OpenView("EXHAUST PILE", DeckManager.instance.GetExhaustPile());
    }

    private void OpenView(string title, List<RuntimeCard> cardsToList)
    {
        viewPanel.SetActive(true);
        if (titleText) titleText.text = title;

        // Hand drawer'ı kilitle
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (RuntimeCard card in cardsToList)
        {
            GameObject cardObj = Instantiate(cardUIPrefab, cardContainer);
            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.Setup(card, -1);
                CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
                if (group == null) group = cardObj.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }
        }
    }

    public void CloseView()
    {
        viewPanel.SetActive(false);

        // Hand drawer kilidini aç
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
    }
}
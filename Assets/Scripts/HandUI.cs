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
}
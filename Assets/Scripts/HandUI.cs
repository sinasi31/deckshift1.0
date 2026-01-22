using System.Collections.Generic;
using UnityEngine;

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

    // Oyun baþlar baþlamaz eli çiz
    private void Start()
    {
        // Biraz bekletelim ki DeckManager listeyi doldursun
        Invoke("UpdateHandDisplay", 0.1f);
    }

    private void UpdateHandDisplay()
    {
        // Önce temizle
        foreach (Transform child in handContainer)
        {
            Destroy(child.gameObject);
        }

        if (DeckManager.instance == null) return;

        List<RuntimeCard> currentHand = DeckManager.instance.GetCurrentHand();

        for (int i = 0; i < currentHand.Count; i++)
        {
            RuntimeCard card = currentHand[i];
            GameObject cardUIObject = Instantiate(cardUIPrefab, handContainer);

            CardUI cardUI = cardUIObject.GetComponent<CardUI>();
            if (cardUI != null)
            {
                // ÖNEMLÝ: Setup'a gerçek index'i (i) gönderiyoruz. 
                // CardUI kendi içinde bunu (i+1) yapýp ekrana yazacak.
                cardUI.Setup(card, i);
            }
        }
    }

    public void AnimateCardFromHand(int index)
    {
        if (index < 0 || index >= handContainer.childCount) return;

        Transform cardTransform = handContainer.GetChild(index);

        // Layout grubundan çýkar ki serbestçe uçabilsin
        if (handContainer.parent != null)
            cardTransform.SetParent(handContainer.parent);
        else
            cardTransform.SetParent(transform.root); // Garanti olsun

        cardTransform.SetAsLastSibling();

        CardUI cardUI = cardTransform.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.PlayUseAnimation();
        }
    }
}
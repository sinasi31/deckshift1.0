using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RecallUI : MonoBehaviour
{
    [Header("UI Elemanları")]
    public Button recallButton;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI labelText; // "Recall (R)" yazısı için

    private void Start()
    {
        // Butona tıklanınca DeckManager'daki fonksiyonu çalıştır
        if (recallButton != null)
        {
            recallButton.onClick.RemoveAllListeners();
            recallButton.onClick.AddListener(() =>
            {
                if (DeckManager.instance != null) DeckManager.instance.TryRecall();
            });
        }

        UpdateVisuals(1); // Başlangıçta 1 yazsın
    }

    private void OnEnable()
    {
        // Maliyet değişince haberdar ol
        DeckManager.OnRecallCostChanged += UpdateVisuals;

        // Eğer oyun ortasında panel açılıp kapanıyorsa güncel değeri al
        if (DeckManager.instance != null)
            UpdateVisuals(DeckManager.instance.currentRecallCost);
    }

    private void OnDisable()
    {
        DeckManager.OnRecallCostChanged -= UpdateVisuals;
    }

    private void UpdateVisuals(int newCost)
    {
        if (costText != null)
        {
            costText.text = newCost.ToString();

            // Opsiyonel: Maliyet arttıkça rengi kırmızıya dönsün
            if (newCost > 3) costText.color = Color.red;
            else costText.color = Color.white;
        }
    }
}
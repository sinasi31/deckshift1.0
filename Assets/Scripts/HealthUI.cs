using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthUI : MonoBehaviour
{
    public PlayerController playerController; // Deðiþiklik: Artýk PlayerController'ý referans alýyor
    [Header("UI Elemanlarý")]
    public Image healthFillImage;
    public TextMeshProUGUI healthText;

    void Update()
    {
        if (playerController == null) return;

        float currentHealth = playerController.CurrentHealth;
        float maxHealth = playerController.MaxHealth;
        float fillRatio = currentHealth / maxHealth;
        healthFillImage.fillAmount = fillRatio;
        healthText.text = Mathf.RoundToInt(currentHealth) + " / " + Mathf.RoundToInt(maxHealth);
    }
}
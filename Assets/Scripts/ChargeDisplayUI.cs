using UnityEngine;
using TMPro;

public class ChargeDisplayUI : MonoBehaviour
{
    // 1. Bu referansýn Inspector'da atanmýþ olmasý gerekiyor
    public PlayerController playerController;

    // 2. Bu referansýn da Inspector'da atanmýþ olmasý gerekiyor
    public TextMeshProUGUI shiftTextElement; // Adýný daha anlaþýlýr yaptým

    void Update()
    {
        if (playerController != null && shiftTextElement != null)
        {
            // 3. 'GetCurrentShift()' fonksiyonunu çaðýrýyoruz
            int charges = playerController.GetCurrentShift();
            shiftTextElement.text = $"Shift: {charges}"; // Metni 'Shift' olarak güncelledim
        }
    }
}
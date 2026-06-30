using UnityEngine;
using TMPro;

public class ChargeDisplayUI : MonoBehaviour
{
    // 1. Bu referans�n Inspector'da atanm�� olmas� gerekiyor
    public PlayerController playerController;

    // 2. Bu referans�n da Inspector'da atanm�� olmas� gerekiyor
    public TextMeshProUGUI shiftTextElement; // Ad�n� daha anla��l�r yapt�m

    void Update()
    {
        if (playerController != null && shiftTextElement != null)
        {
            // 3. 'GetCurrentShift()' fonksiyonunu �a��r�yoruz
            int charges = playerController.GetCurrentShift();
            shiftTextElement.text = $"SHIFT: {charges}"; // Display label; cost logic unchanged
        }
    }
}
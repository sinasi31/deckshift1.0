using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    private bool hasBeenTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (!hasBeenTriggered && player != null)
        {
            hasBeenTriggered = true;

            // --- YENÝ KONTROL ---
            // Oyuncu bu odayý hasar almadan mý geçti?
            if (!player.TookDamageThisRoom)
            {
                // Eðer hasar almadýysa, AchievementManager'a haber ver.
                AchievementManager.instance.OnRoomClearedFlawlessly();
            }

            // Ödül ekranýný göster (bu kod ayný kalýyor).
            RewardManager.instance.ShowRewardScreen();
        }
    }
}
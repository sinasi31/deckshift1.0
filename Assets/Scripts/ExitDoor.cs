using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    private bool hasBeenTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Sadece bir kere çalýþsýn ve sadece oyuncu tetiklesin
        if (hasBeenTriggered || !other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            hasBeenTriggered = true;
            Debug.Log("Çýkýþ kapýsýna ulaþýldý! Ödül ekraný açýlýyor...");

            // Hasarsýzlýk kontrolü
            if (!player.TookDamageThisRoom)
            {
                AchievementManager.instance.OnRoomClearedFlawlessly();
            }

            // Ödül ekranýný çaðýr
            // Eðer RewardManager yoksa veya hata verirse oyun burada takýlýr.
            if (RewardManager.instance != null)
            {
                RewardManager.instance.ShowRewardScreen();
            }
            else
            {
                Debug.LogError("ExitDoor Hatasý: RewardManager bulunamadý!");
            }
        }
    }
}
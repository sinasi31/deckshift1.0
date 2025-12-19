using UnityEngine;
// TMPro kütüphanesine gerek kalmadý çünkü sadece objeyi aç/kapat yapacaðýz
// Ama eðer metni kodla deðiþtirmek istersen (örn: tuþ deðiþirse) tutabilirsin.

public class ExitDoor : MonoBehaviour
{
    private bool hasBeenTriggered = false;
    private bool isPlayerInRange = false;
    private PlayerController currentPlayer;

    [Header("Etkileþim Ayarlarý")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Görsel Referans")]
    // Unity'de kapýnýn üzerine koyacaðýmýz yazý objesini buraya sürükleyeceðiz
    public GameObject interactionPopup;

    private void Update()
    {
        if (isPlayerInRange && !hasBeenTriggered && Input.GetKeyDown(interactKey))
        {
            PerformExit();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenTriggered) return;

        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            currentPlayer = other.GetComponent<PlayerController>();

            // Yazýyý Aç
            if (interactionPopup != null)
                interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            currentPlayer = null;

            // Yazýyý Kapat
            if (interactionPopup != null)
                interactionPopup.SetActive(false);
        }
    }

    private void PerformExit()
    {
        hasBeenTriggered = true;

        // Çýkýþ yapýldýðý an yazýyý gizle
        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (currentPlayer != null && !currentPlayer.TookDamageThisRoom)
        {
            AchievementManager.instance.OnRoomClearedFlawlessly();
        }

        if (RewardManager.instance != null)
        {
            RewardManager.instance.ShowRewardScreen();
        }
    }
}
using UnityEngine;

public class QuestBoardObject : MonoBehaviour, IInteractable
{
    [Header("Görsel Ayarlar")]
    public GameObject promptUI;

    // ARTIK "public QuestBoardUI boardUI" DEÐÝÞKENÝNE ÝHTÝYACIMIZ YOK
    // Çünkü 'instance' üzerinden ulaþacaðýz.

    public void Interact()
    {
        Debug.Log("Quest ekraný açýlýyor...");

        // Singleton sayesinde sahne fark etmeksizin UI'ý bulur
        if (QuestBoardUI.instance != null)
        {
            QuestBoardUI.instance.OpenBoard();
        }
        else
        {
            Debug.LogError("HATA: QuestBoardUI bulunamadý! Oyunu 'Hub' sahnesinden baþlattýðýna emin misin?");
        }
    }

    public string GetInteractText()
    {
        return "Examine"; // Campfire için "Rest" veya "Check Quests" de diyebilirsin
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (promptUI != null) promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (promptUI != null) promptUI.SetActive(false);
        }
    }
}
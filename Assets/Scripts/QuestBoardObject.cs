using UnityEngine;

public class QuestBoardObject : MonoBehaviour, IInteractable
{
    [Header("Görsel Ayarlar")]
    public GameObject promptUI;

    public void Interact()
    {
        Debug.Log("Tabloya bakýlýyor... Quest ekraný açýlýyor.");

    }
    public string GetInteractText()
    {
        return "Examine Painting";
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
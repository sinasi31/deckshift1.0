using UnityEngine;

public class SimpleInteract : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (QuestSystem.instance != null)
        {
            QuestSystem.instance.ToggleBoard();
        }
    }

    public string GetInteractText()
    {
        return "Check Quests";
    }
    public GameObject prompt;
    private void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player") && prompt) prompt.SetActive(true); }
    private void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player") && prompt) prompt.SetActive(false); }
}
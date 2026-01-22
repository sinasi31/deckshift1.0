using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonGate : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        Debug.Log("Zindana giriliyor...");
        // Ýleride burasý harita ekranýný veya ilk leveli açacak.
        // SceneManager.LoadScene("MapScene"); 
    }

    public string GetInteractText()
    {
        return "Enter the Void";
    }

    // E yazýsý için QuestBoard'daki mantýðýn aynýsýný (OnTriggerEnter/Exit) buraya da ekleyebilirsin.
}
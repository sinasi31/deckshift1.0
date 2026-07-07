using UnityEngine;
using UnityEngine.SceneManagement; 

public class ExitDoor : MonoBehaviour
{
    [Header("T�r Ayar� (�NEML�)")]
    public bool isSceneLoader = false; 
    public string sceneToLoad = "GameScene"; 

    [Header("Etkile�im Ayarlar�")]
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactionPopup; 

    private bool hasBeenTriggered = false;
    private bool isPlayerInRange = false;
    private PlayerController currentPlayer;

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
            if (interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            currentPlayer = null;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    private void PerformExit()
    {
        if (hasBeenTriggered) return;
        hasBeenTriggered = true;

        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (isSceneLoader)
        {
            Debug.Log("Hub'dan ��k�l�yor, oyun ba�l�yor...");

            if (QuestSystem.instance != null) QuestSystem.instance.CloseBoard();

            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            // The hub is not a real combat room — leaving it undamaged must NOT count as a flawless
            // clear (this is why the "Invincible" quest was auto-completing on the very first exit).
            bool isHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();

            if (!isHub && currentPlayer != null && !currentPlayer.TookDamageThisRoom)
            {
                AchievementManager.instance.OnRoomClearedFlawlessly();

                if (QuestSystem.instance != null) QuestSystem.instance.ReportEvent(QuestType.NoDamageRoom, 1);
            }

            if (RewardManager.instance != null)
            {
                RewardManager.instance.ShowRewardScreen();
            }
        }
    }
}
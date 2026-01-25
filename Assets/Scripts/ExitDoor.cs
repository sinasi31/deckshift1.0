using UnityEngine;
using UnityEngine.SceneManagement; 

public class ExitDoor : MonoBehaviour
{
    [Header("Tür Ayarý (ÖNEMLÝ)")]
    public bool isSceneLoader = false; 
    public string sceneToLoad = "GameScene"; 

    [Header("Etkileþim Ayarlarý")]
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
        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (isSceneLoader)
        {
            Debug.Log("Hub'dan çýkýlýyor, oyun baþlýyor...");

            if (QuestSystem.instance != null) QuestSystem.instance.CloseBoard();

            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            hasBeenTriggered = true; 

            if (currentPlayer != null && !currentPlayer.TookDamageThisRoom)
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
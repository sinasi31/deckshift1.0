using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager instance;

    [Header("Challenges")]
    public List<Challenge> allChallenges; // List of all challenges

    [Header("Starting Cards")]
    public List<CardData> defaultUnlockedCards; // Cards that are always unlocked

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }

        LoadChallenges();
    }

    // Returns the current pool of playable cards for the RewardManager.
    public List<CardData> GetAvailableCardPool()
    {
        List<CardData> availableCards = new List<CardData>();

        // 1. Add all default cards to the pool
        availableCards.AddRange(defaultUnlockedCards);

        // 2. Check completed challenges and add their reward cards to the pool
        foreach (var challenge in allChallenges)
        {
            if (challenge.isCompleted && challenge.unlockableCard != null)
            {
                if (!availableCards.Contains(challenge.unlockableCard))
                {
                    availableCards.Add(challenge.unlockableCard);
                }
            }
        }
        return availableCards;
    }

    // Called by ExitDoor
    public void OnRoomClearedFlawlessly()
    {
        CompleteChallenge("KusursuzOda_1"); // You can keep the ID string as is
    }

    private void CompleteChallenge(string challengeID)
    {
        Challenge challengeToComplete = allChallenges.Find(c => c.challengeID == challengeID);
        if (challengeToComplete != null && !challengeToComplete.isCompleted)
        {
            // --- DÜZELTÝLEN SATIR BURASI ---
            // 'aciklama' -> 'description'
            // 'kartAdi' -> 'cardName'
            Debug.Log("CHALLENGE COMPLETE: " + challengeToComplete.description +
                      ". Reward: " + challengeToComplete.unlockableCard.cardName + " unlocked!");
            // --- BÝTÝÞ ---

            challengeToComplete.isCompleted = true;
            SaveChallenges();
        }
    }

    private void SaveChallenges()
    {
        foreach (var challenge in allChallenges)
        {
            PlayerPrefs.SetInt(challenge.challengeID, challenge.isCompleted ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private void LoadChallenges()
    {
        foreach (var challenge in allChallenges)
        {
            challenge.isCompleted = PlayerPrefs.GetInt(challenge.challengeID, 0) == 1;
        }
    }

    private void Update()
    {
        // F12 to reset save data (for testing)
        if (Input.GetKeyDown(KeyCode.F12))
        {
            Debug.LogWarning("!!! ALL SAVE DATA DELETED !!!");
            PlayerPrefs.DeleteAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
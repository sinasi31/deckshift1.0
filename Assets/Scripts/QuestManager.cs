using System.Collections.Generic;
using UnityEngine;
using System;

public class QuestManager : MonoBehaviour
{
    public static QuestManager instance;

    [Header("Active Quests")]
    public List<QuestData> activeQuests = new List<QuestData>();
    public int maxActiveQuests = 3; // En fazla kaç görev alabilir?

    // Görev alýndýðýnda veya bittiðinde UI güncellensin diye event
    public static event Action OnQuestListChanged;

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public bool AcceptQuest(QuestData quest)
    {
        // Zaten listede var mý?
        if (activeQuests.Contains(quest)) return false;

        // Yer var mý?
        if (activeQuests.Count >= maxActiveQuests)
        {
            Debug.Log("Quest kotasý dolu!");
            return false;
        }

        activeQuests.Add(quest);
        Debug.Log($"Görev Alýndý: {quest.questName}");

        OnQuestListChanged?.Invoke();
        return true;
    }

    public void CompleteQuest(QuestData quest)
    {
        if (activeQuests.Contains(quest))
        {
            Debug.Log($"GÖREV TAMAMLANDI: {quest.questName}");
            // BURADA ÖDÜL VERME KODU OLACAK

            activeQuests.Remove(quest);
            OnQuestListChanged?.Invoke();
        }
    }
}
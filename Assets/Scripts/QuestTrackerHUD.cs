using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class QuestTrackerHUD : MonoBehaviour
{
    [SerializeField] private Transform questRowContainer;
    [SerializeField] private GameObject questRowPrefab;

    private readonly Dictionary<QuestSystem.ActiveQuest, GameObject> rows =
        new Dictionary<QuestSystem.ActiveQuest, GameObject>();

    private void Start()
    {
        if (QuestSystem.instance == null)
        {
            Debug.LogWarning("QuestTrackerHUD: QuestSystem.instance is null. Tracker will not function.");
            return;
        }

        QuestSystem.instance.OnQuestAccepted += AddRow;
        QuestSystem.instance.OnQuestProgress += UpdateRow;
        QuestSystem.instance.OnQuestCompleted += RemoveRow;

        foreach (QuestSystem.ActiveQuest quest in QuestSystem.instance.activeQuests)
        {
            if (!quest.isCompleted)
                AddRow(quest);
        }
    }

    private void OnDestroy()
    {
        if (QuestSystem.instance == null) return;
        QuestSystem.instance.OnQuestAccepted -= AddRow;
        QuestSystem.instance.OnQuestProgress -= UpdateRow;
        QuestSystem.instance.OnQuestCompleted -= RemoveRow;
    }

    private void AddRow(QuestSystem.ActiveQuest quest)
    {
        if (questRowPrefab == null || questRowContainer == null) return;

        GameObject row = Instantiate(questRowPrefab, questRowContainer);
        row.transform.localScale = Vector3.one;

        SetChildText(row, "Title", quest.data.questName);
        SetChildText(row, "Progress", $"{quest.currentAmount}/{quest.data.targetAmount}");

        rows[quest] = row;
    }

    private void UpdateRow(QuestSystem.ActiveQuest quest)
    {
        if (!rows.ContainsKey(quest))
        {
            AddRow(quest);
            return;
        }

        SetChildText(rows[quest], "Progress", $"{quest.currentAmount}/{quest.data.targetAmount}");
    }

    private void RemoveRow(QuestSystem.ActiveQuest quest)
    {
        if (!rows.ContainsKey(quest)) return;
        Destroy(rows[quest]);
        rows.Remove(quest);
    }

    private void SetChildText(GameObject row, string childName, string text)
    {
        Transform child = row.transform.Find(childName);
        if (child == null)
        {
            Debug.LogWarning($"QuestTrackerHUD: Row prefab has no child named '{childName}'.");
            return;
        }
        TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogWarning($"QuestTrackerHUD: Child '{childName}' has no TextMeshProUGUI component.");
            return;
        }
        tmp.text = text;
    }
}

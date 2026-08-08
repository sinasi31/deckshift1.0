using System.Collections.Generic;
using UnityEngine;

public class QuestTrackerHUD : MonoBehaviour
{
    [SerializeField] private Transform questRowContainer;
    [SerializeField] private GameObject questRowPrefab;

    private readonly Dictionary<QuestSystem.ActiveQuest, QuestTrackerRow> rows =
        new Dictionary<QuestSystem.ActiveQuest, QuestTrackerRow>();

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

        QuestTrackerRow view = row.GetComponent<QuestTrackerRow>();
        if (view == null) view = row.AddComponent<QuestTrackerRow>();
        view.Build(quest.data.questName, quest.currentAmount, quest.data.targetAmount);

        rows[quest] = view;
    }

    private void UpdateRow(QuestSystem.ActiveQuest quest)
    {
        if (!rows.ContainsKey(quest))
        {
            AddRow(quest);
            return;
        }

        rows[quest].SetProgress(quest.currentAmount, quest.data.targetAmount);
    }

    // On completion the row celebrates + self-destroys, so drop it from the map immediately.
    private void RemoveRow(QuestSystem.ActiveQuest quest)
    {
        if (!rows.ContainsKey(quest)) return;
        QuestTrackerRow view = rows[quest];
        rows.Remove(quest);
        if (view != null) view.PlayComplete(quest.data.rewardText);
    }
}

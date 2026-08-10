using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem instance;

    // How many contracts the player may carry at once. The board greys out the rest rather than
    // hiding them, so the limit is visible instead of being discovered by a click that does nothing.
    public const int MaxActiveQuests = 3;

    // How many contracts are PINNED UP, which is deliberately a separate number from how many you
    // may carry. A board that offers exactly as many jobs as you have room for is a checklist, not
    // a decision — the choice only exists once it shows more than you can take, and the screen
    // already draws that state (the ones you have no room for grey out rather than vanishing).
    //
    // It is 3 today only because there are three quest assets in the project. Raise it as soon as
    // there are more to show, and widen BOARD_W in QuestBoardScreen to match — the slips are laid
    // out in one row.
    public const int BoardSlots = 3;

    [Header("Görev Havuzu")]
    public List<QuestData> allQuests;
    public List<ActiveQuest> activeQuests = new List<ActiveQuest>();

    // The contracts currently pinned up. Rolled ONCE and then kept.
    //
    // ⚠️ It must not be re-rolled on each open. The old board regenerated its three slips every time
    // it was opened, which with a pool bigger than three would make closing and re-opening the board
    // a free reroll — the player would simply spam E until the quest they wanted appeared. Because
    // QuestSystem is scene-local and dies with the run, this list is naturally per-run.
    private List<QuestData> offer;

    public event System.Action<ActiveQuest> OnQuestAccepted;
    public event System.Action<ActiveQuest> OnQuestProgress;
    public event System.Action<ActiveQuest> OnQuestCompleted;

    private void Awake()
    {
        // Scene-local on purpose: quests are per-run by design and reset on death/restart.
        // DontDestroyOnLoad was removed because the survivor kept dead references to the old scene's
        // UI, breaking the quest board after the first death. If quest meta-progression is ever
        // wanted, persist it via the save system (PlayerPrefs, like AchievementManager) — do not
        // re-add DontDestroyOnLoad.
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ---- the board ---------------------------------------------------------------------------------
    // The UI is QuestBoardScreen: procedural, self-instantiating, built on demand. QuestSystem holds
    // no UI references at all any more — the painted overlay panel, its container and the quest-slip
    // prefab are gone, along with the three Inspector fields that pointed at them.

    // "E"ye basınca bu çağrılacak
    public void ToggleBoard()
    {
        QuestBoardScreen.Toggle();
    }

    public void CloseBoard()
    {
        QuestBoardScreen.Close();
    }

    // ---- offer -------------------------------------------------------------------------------------

    public IReadOnlyList<QuestData> Offer
    {
        get { EnsureOffer(); return offer; }
    }

    private void EnsureOffer()
    {
        if (offer != null) return;

        offer = new List<QuestData>();
        if (allQuests == null) return;

        // Shuffle a copy, then take the first few. The old board took allQuests[0..2] in order, so
        // with a pool bigger than three the later quests could never be offered at all.
        List<QuestData> pool = new List<QuestData>();
        foreach (QuestData q in allQuests)
            if (q != null && !pool.Contains(q)) pool.Add(q);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            QuestData tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int take = Mathf.Min(BoardSlots, pool.Count);
        for (int i = 0; i < take; i++) offer.Add(pool[i]);
    }

    // ---- active quests -----------------------------------------------------------------------------

    public int ActiveCount { get { return activeQuests.Count; } }

    public ActiveQuest FindActive(QuestData quest)
    {
        foreach (ActiveQuest q in activeQuests)
            if (q.data == quest) return q;
        return null;
    }

    // Returns whether the quest was actually taken, so the board can tell a real acceptance from a
    // refusal instead of playing the seal animation over nothing.
    public bool AcceptQuest(QuestData quest)
    {
        if (quest == null) return false;
        if (FindActive(quest) != null) return false;
        if (activeQuests.Count >= MaxActiveQuests) return false;

        ActiveQuest newQuest = new ActiveQuest(quest);
        activeQuests.Add(newQuest);
        OnQuestAccepted?.Invoke(newQuest);
        return true;
    }

    [System.Serializable]
    public class ActiveQuest
    {
        public QuestData data;      // Hangi görev?
        public int currentAmount;   // Şu an kaç yaptık? (örn: 1/3)
        public bool isCompleted;    // Bitti mi?

        public ActiveQuest(QuestData quest)
        {
            data = quest;
            currentAmount = 0;
            isCompleted = false;
        }
    }

    public void ReportEvent(QuestType type, int amount = 1)
    {
        foreach (ActiveQuest quest in activeQuests)
        {
            if (quest.isCompleted) continue;
            if (quest.data.type == type)
            {
                quest.currentAmount += amount;
                OnQuestProgress?.Invoke(quest);
                CheckCompletion(quest);
            }
        }
    }

    private void CheckCompletion(ActiveQuest quest)
    {
        if (quest.currentAmount >= quest.data.targetAmount)
        {
            quest.isCompleted = true;
            OnQuestCompleted?.Invoke(quest);
            GiveReward(quest.data);
        }
    }

    private void GiveReward(QuestData data)
    {
        // Oyuncuyu bul (Sahne değişse bile bulur)
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        switch (data.rewardType)
        {
            case RewardType.Gold:
                player.AddGold(data.rewardAmount);
                break;

            case RewardType.Heal:
                player.Heal(data.rewardAmount);
                break;

            case RewardType.ShiftCharge:
                player.IncreaseMaxShift(data.rewardAmount);
                player.ResetShiftToMax();
                break;
        }
    }
}

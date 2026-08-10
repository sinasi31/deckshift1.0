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

    // ⚠️ COMPLETED CONTRACTS DO NOT OCCUPY A SLOT. `activeQuests` is the full record for the run —
    // the board reads it to draw finished contracts as COMPLETE — but a finished job is not
    // something you are still carrying. Counting them capped the player at three contracts for the
    // WHOLE RUN: finish three and the board refuses every further offer with no explanation, which
    // is exactly the silent no-op the accept path was just rewritten to eliminate.
    public int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (ActiveQuest q in activeQuests)
                if (!q.isCompleted) n++;
            return n;
        }
    }

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
        if (FindActive(quest) != null) return false;   // already taken, or already finished
        if (ActiveCount >= MaxActiveQuests) return false;

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

    // ---- oaths: the per-room streak tracker --------------------------------------------------------
    //
    // Four contracts (NoCardsRoom / NoRecallRoom / LowShiftRoom / NoStaggerRoom) all ask the same
    // shape of question: "did you get through that room without doing X?" So they share one
    // recorder rather than each growing its own bookkeeping. Adding a fifth oath is a switch case in
    // EndRoom, not a new system.
    //
    // ⚠️ THESE ARE STREAKS, NOT TALLIES. Clearing a room inside the oath adds one; breaking it puts
    // the count back to ZERO. That is the whole reason they read as a commitment instead of a
    // checklist — and it's also why a failure can never dead-end a run: the next room starts fresh,
    // so the contract is always still winnable.
    //
    // Nothing is judged until the room is LEFT (ExitDoor), because a violation isn't final until
    // then — the tracker HUD shows the oath as broken live so the player isn't surprised at the
    // door, but the reset itself happens once, at the exit.

    private int roomCardsPlayed, roomRecalls, roomShiftSpent, roomStaggers;
    private bool roomActive;

    // Called from PlayerController.OnNewRoomEnter, so it covers every way a room can start.
    public void BeginRoom()
    {
        roomCardsPlayed = 0;
        roomRecalls = 0;
        roomShiftSpent = 0;
        roomStaggers = 0;
        roomActive = true;
    }

    public void NoteCardPlayed(bool isStagger)
    {
        roomCardsPlayed++;
        if (isStagger) roomStaggers++;
    }

    public void NoteRecall() { roomRecalls++; }

    public void NoteShiftSpent(int amount)
    {
        if (amount > 0) roomShiftSpent += amount;
    }

    // Has this oath already been broken in the room the player is standing in? Read by the tracker
    // HUD so a doomed room is visible immediately rather than at the exit door.
    public bool IsOathBroken(QuestData data)
    {
        if (data == null || !roomActive) return false;
        switch (data.type)
        {
            case QuestType.NoCardsRoom: return roomCardsPlayed > 0;
            case QuestType.NoRecallRoom: return roomRecalls > 0;
            case QuestType.NoStaggerRoom: return roomStaggers > 0;
            case QuestType.LowShiftRoom: return roomShiftSpent > Mathf.Max(0, data.objectiveParam);
            default: return false;
        }
    }

    public bool IsOathType(QuestType t)
    {
        return t == QuestType.NoCardsRoom || t == QuestType.NoRecallRoom
            || t == QuestType.LowShiftRoom || t == QuestType.NoStaggerRoom;
    }

    // Called by ExitDoor when a COMBAT room is cleared. The hub must never count — it is a
    // consequence-free sandbox where nothing is spent, so every oath would pass there for free.
    public void EndRoom()
    {
        if (!roomActive) return;
        roomActive = false;

        // ToArray: GiveReward on completion can add a card, and a listener could in principle touch
        // the list. Iterating a snapshot keeps a completion from invalidating the loop.
        foreach (ActiveQuest q in activeQuests.ToArray())
        {
            if (q.isCompleted || q.data == null) continue;
            if (!IsOathType(q.data.type)) continue;

            bool passed;
            switch (q.data.type)
            {
                case QuestType.NoCardsRoom: passed = roomCardsPlayed == 0; break;
                case QuestType.NoRecallRoom: passed = roomRecalls == 0; break;
                case QuestType.NoStaggerRoom: passed = roomStaggers == 0; break;
                case QuestType.LowShiftRoom: passed = roomShiftSpent <= Mathf.Max(0, q.data.objectiveParam); break;
                default: continue;
            }

            if (passed)
            {
                q.currentAmount++;
                OnQuestProgress?.Invoke(q);
                CheckCompletion(q);
            }
            else if (q.currentAmount > 0)
            {
                q.currentAmount = 0;
                OnQuestProgress?.Invoke(q);   // the HUD needs to show the streak collapsing
            }
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

            case RewardType.Scrap:
                player.AddScrap(data.rewardAmount);
                break;

            case RewardType.MaxHealth:
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null) health.IncreaseBaseMaxHealth(data.rewardAmount);
                break;

            case RewardType.Card:
                GrantCardReward(data);
                break;
        }
    }

    // A named card if the contract specifies one, otherwise a random offerable card.
    //
    // ⚠️ It goes through CardPool rather than picking from the catalogue directly, so the payout
    // obeys the same exclusions every other card source does — most importantly it can never hand
    // out Stagger, which is conjured on empty Shift and must never become a card the player OWNS.
    private void GrantCardReward(QuestData data)
    {
        if (DeckManager.instance == null) return;

        // The named card is re-checked, not trusted: a designer can drop any CardData into that
        // slot in the Inspector, including Stagger.
        CardData card = CardPool.IsRewardable(data.rewardCard) ? data.rewardCard : null;
        if (card == null) card = CardPool.PickRewardable();
        if (card == null)
        {
            Debug.LogWarning($"[QuestSystem] '{data.questName}' pays a card but none could be drawn.");
            return;
        }

        DeckManager.instance.AddCardToDeck(card);
    }
}

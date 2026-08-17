using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "Deckshift/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Info")]
    public string questName;
    [TextArea] public string description;

    [Header("Objectives")]
    public QuestType type;
    public int targetAmount;

    // A second number some objectives need, because targetAmount is always the COUNT.
    // Currently only LowShiftRoom reads it (the per-room Shift ceiling). Left at 0 it is ignored.
    [Tooltip("Extra objective parameter. LowShiftRoom: the most Shift you may spend in a room.")]
    public int objectiveParam;

    [Header("Ödüller")]
    public string rewardText; // örn: "50 Gold" (Ekranda yazacak yazı)
    public RewardType rewardType; // örn: Gold
    public int rewardAmount;

    // Only read when rewardType is Card. Left empty, a Card reward draws at random from the
    // offerable pool — which is what most contracts want. Set it for the ones whose whole point is
    // a specific card (Scrooge paying out Rich Man's Dagger).
    [Tooltip("Card reward only. Empty = a random offerable card.")]
    public CardData rewardCard;
}

public enum QuestType
{
    GoldAccumulate,
    KillEnemy,
    AirKill,
    NoDamageRoom,
    UseCardCount,

    // ---- OATHS (added 2026-08-10) --------------------------------------------------------------
    // All four are STREAKS, not tallies: clearing a room within the oath adds one, and BREAKING it
    // resets the count to zero. They share one mechanism in QuestSystem (BeginRoom / EndRoom), so
    // adding a fifth oath is a switch case, not a system.
    //
    // They are also the only quest types that can never be blocked by map generation, because they
    // ask about the player's own behaviour rather than about what a room happens to contain. Two of
    // them are underwritten by Level Design Law #1 — every room is beatable with only jumping and
    // moving, so "no cards" and "no kills" are always physically possible.
    NoCardsRoom,     // clear a room without playing a single card
    NoRecallRoom,    // clear a room without using Recall
    LowShiftRoom,    // clear a room spending no more than objectiveParam Shift
    NoStaggerRoom,   // clear a room without playing Stagger
}

public enum RewardType
{
    Gold,
    ShiftCharge,
    Heal,

    // Added 2026-08-10. The design rule is that quests pay in things THE SHOP DOES NOT SELL —
    // gold is the buying currency, so paying gold is just handing out a discount. These three are
    // the currencies a contract can actually be worth.
    Card,        // rewardCard, or a random offerable card if that's empty
    Scrap,       // deck sustain
    MaxHealth,   // permanent, and it stacks correctly with HP relics (see QuestSystem.GiveReward)
}

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
    [Header("Ödüller")]
    public string rewardText; // Örn: "50 Gold" (Ekranda yazacak yazý)
    public RewardType rewardType; // Örn: Gold
    public int rewardAmount;

}

public enum QuestType
{
    GoldAccumulate, 
    KillEnemy,      
    AirKill,        
    NoDamageRoom,
    UseCardCount
}
public enum RewardType
{
    Gold,
    ShiftCharge,
    Heal
}


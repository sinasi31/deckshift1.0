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

    [Header("Rewards")]
    public string rewardText;
}

public enum QuestType
{
    GoldAccumulate, 
    KillEnemy,      
    AirKill,        
    NoDamageRoom,
    UseCardCount
}
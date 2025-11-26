using UnityEngine;

// 'GameEnums.cs' dosyamýzda olduðu için bu satýra gerek yok
// public enum CardActionType { ... } 

[CreateAssetMenu(fileName = "New CardData", menuName = "Deckshift/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Card Info")]
    public string cardName;
    [TextArea]
    public string description;
    public Sprite cardArt;

    [Header("Card Action")]
    public CardActionType actionType;
    public float actionValue;

    [Header("Card Behavior")]
    public bool singleUse = false;

    // --- YENÝ EKLENEN KISIM ---
    [Header("Game Mechanics")]

    [Tooltip("Bu kartý oynamanýn 'Shift' maliyeti")]
    public int shiftCost = 0; // Varsayýlan maliyet 0 (yani bedava)

    [Tooltip("Bu kartýn desteye eklendiðinde sahip olacaðý maks. kullaným hakký")]
    public int maxUses = 3; // Varsayýlan kullaným hakký 3
    // --- BÝTÝÞ ---
}
using UnityEngine;

[CreateAssetMenu(fileName = "New Challenge", menuName = "Deckshift/Challenge")]
public class Challenge : ScriptableObject
{
    public string challengeID; // e.g., "FlawlessRoom_1"
    public string description;  // (eski: aciklama) "Complete a room without taking damage."
    public CardData unlockableCard; // Card unlocked when this challenge is completed
    public bool isCompleted = false; // Has this challenge been completed?
}
using UnityEngine;

// Bu, Create menüsüne "Deckshift/Relic Data" adýnda yeni bir seçenek ekler
[CreateAssetMenu(fileName = "New Relic", menuName = "Deckshift/Relic Data")]
public class RelicData : ScriptableObject
{
    [Header("Info")]
    public string relicID; // Eþyayý kodda tanýmak için benzersiz bir kimlik (örn: "LavaBoots")
    public string relicName;
    [TextArea]
    public string description;
    public Sprite relicArt;

    // TODO: Nadirlik (Rarity) gibi þeyler de buraya eklenebilir (Common, Epic, Legendary)
}
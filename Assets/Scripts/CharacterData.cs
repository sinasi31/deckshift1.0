using UnityEngine;

// A playable character. Right now the only thing a character carries is its INNATE — the free
// attack every character always has — because that is the part the game actually needed: without
// it, a room with no card charges left is a room you cannot fight in, and combat is a pure loss.
//
// Deliberately a ScriptableObject rather than fields on the Player, so a second character is an
// ASSET, not a code change. Art, alternate starting decks and unlock rules belong here too as they
// are built; nothing else should start reading the player's stats off the prefab.
[CreateAssetMenu(fileName = "Character", menuName = "Deckshift/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Wizard";
    [TextArea(2, 4)] public string description;
    public Sprite portrait;

    [Header("Innate")]
    public InnateType innate = InnateType.ArcaneBolt;
    // Display name for the innate. Player-facing, so it follows the house voice (see Tone & Voice):
    // a wink that still hints at what the thing does.
    public string innateName = "Nothing Up My Sleeve";
    [TextArea(2, 4)] public string innateDescription =
        "Right-click to fire an arcane bolt. Costs nothing, and it is slow.";

    [Header("Innate Tuning")]
    // ⚠️ FLAT DAMAGE, ON PURPOSE (designer 2026-08-14). An earlier version scaled it with how empty
    // your hand was, so it was feeble while you held cards and real damage once you ran dry. It was
    // cut for being invisible arithmetic on an attack the player uses constantly — the innate should
    // be one predictable thing you always know the value of, not a number you have to work out.
    public float innateDamage = 7f;
    // Long enough that the innate can never out-damage the deck: 5.8 DPS, against a Fireball's 15
    // on demand. Fodder (12 HP) takes two bolts, a 40 HP melee enemy takes six.
    public float innateCooldown = 1.2f;
}

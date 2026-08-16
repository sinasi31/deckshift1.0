using System.Collections.Generic;
using UnityEngine;

// Every playable character, and the one the player picked.
//
// ⚠️ NO HAND-KEPT LIST AND NO CATALOGUE ASSET. Characters live in Resources/Characters and are
// found by scanning that folder, so adding one is dropping in an asset — nothing to rebuild and
// nothing to remember. This project has twice shipped content that existed but could never be
// reached because a list was never updated (three unobtainable cards; a shop stocking 3 of 18
// relics), and a roster of two is exactly the size where that rot starts.
public static class CharacterRoster
{
    public const string ResourceFolder = "Characters";

    private static List<CharacterData> all;

    public static IReadOnlyList<CharacterData> All
    {
        get
        {
            if (all == null)
            {
                all = new List<CharacterData>(Resources.LoadAll<CharacterData>(ResourceFolder));
                // Stable, so the select screen doesn't reshuffle itself between visits.
                all.Sort((a, b) => string.Compare(a.characterName, b.characterName, System.StringComparison.Ordinal));
            }
            return all;
        }
    }

    public static CharacterData ByName(string name)
    {
        foreach (CharacterData c in All)
            if (c != null && c.characterName == name) return c;
        return null;
    }
}

// The character chosen for THIS run.
//
// ⚠️ A PLAIN STATIC IS NOT ENOUGH, because the choice is made in MainMenu and read in SampleScene —
// a scene load away. Statics do survive a scene load within one session, but not a domain reload
// or a fresh launch, so the pick is also written to PlayerPrefs and read back. That is the same
// arrangement AchievementManager uses, and it keeps the last pick as a sensible default.
public static class CharacterSelection
{
    private const string Key = "SelectedCharacter";

    private static CharacterData chosen;

    public static CharacterData Chosen
    {
        get
        {
            if (chosen == null)
            {
                string saved = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(saved)) chosen = CharacterRoster.ByName(saved);
            }
            return chosen;
        }
        set
        {
            chosen = value;
            if (value != null)
            {
                PlayerPrefs.SetString(Key, value.characterName);
                PlayerPrefs.Save();
            }
        }
    }
}

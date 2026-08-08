using System.Collections.Generic;
using UnityEngine;

// The single place that answers "which cards may be offered to the player".
//
// ⚠️ CARD AVAILABILITY IS NO LONGER GATED BY ACHIEVEMENTS (designer 2026-08-09: the achievement
// system went in far too early and is regretted; a proper one for cards/relics comes near release).
// RewardManager used to pull its pool from AchievementManager.GetAvailableCardPool(), which
// returned only `defaultUnlockedCards` plus the reward cards of COMPLETED challenges — with one
// challenge authored, that meant most of the roster was unreachable and nothing said so.
//
// AchievementManager still tracks and saves challenges; it simply no longer decides what exists.
public static class CardPool
{
    private static CardCatalogue cached;

    private static CardCatalogue Catalogue
    {
        get
        {
            if (cached == null) cached = Resources.Load<CardCatalogue>(CardCatalogue.ResourcePath);
            return cached;
        }
    }

    // Every card asset in the project, including ones that must never be offered.
    public static IReadOnlyList<CardData> All
    {
        get
        {
            CardCatalogue c = Catalogue;
            return c != null ? (IReadOnlyList<CardData>)c.all : System.Array.Empty<CardData>();
        }
    }

    // Stagger is the FAIL-STATE card: DeckManager forces it into your hand when you are out of
    // Shift and out of plays, and three of them ends the run. Offering it as a reward or selling it
    // would be offering the player a way to lose. Identified by DeckManager's own reference rather
    // than by name, so renaming the asset cannot silently reintroduce it.
    public static bool IsRewardable(CardData card)
    {
        if (card == null) return false;
        if (DeckManager.instance != null && card == DeckManager.instance.staggerCardData) return false;
        return true;
    }

    // Everything that may legitimately be offered, optionally narrowed to a curated subset.
    // An empty/absent `restrictTo` means the whole roster, which is the normal case.
    public static List<CardData> Offerable(IReadOnlyList<CardData> restrictTo = null)
    {
        IReadOnlyList<CardData> source = (restrictTo != null && restrictTo.Count > 0) ? restrictTo : All;
        List<CardData> result = new List<CardData>();
        HashSet<CardData> seen = new HashSet<CardData>();

        foreach (CardData c in source)
        {
            if (!IsRewardable(c)) continue;
            if (!seen.Add(c)) continue;      // a pool listing the same card twice would skew the draw
            result.Add(c);
        }
        return result;
    }

    // Draw up to `count` DISTINCT cards — for a reward screen or a shop shelf.
    public static List<CardData> DrawDistinct(int count, IReadOnlyList<CardData> restrictTo = null)
    {
        List<CardData> pool = Offerable(restrictTo);
        List<CardData> picked = new List<CardData>();
        int n = Mathf.Min(count, pool.Count);
        for (int i = 0; i < n; i++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }
}

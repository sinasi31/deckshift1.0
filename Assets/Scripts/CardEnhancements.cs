using System.Collections.Generic;
using UnityEngine;

// Blompo's card enhancements. An enhancement is a permanent (for the run) upgrade attached to
// ONE COPY of a card in the player's deck.
//
// CRITICAL: enhancements live on RuntimeCard, never on CardData. CardData is a shared
// ScriptableObject asset — writing to it would upgrade that card in every future run AND dirty
// the asset on disk. RuntimeCard is per-copy, per-run, which is also what makes deck identity
// work: with two Fireballs you can bless one and leave the other alone.
//
// Design rules (designer-set 2026-07-27):
//   - ONE enhancement per card. This is what makes the "no Shift cost" + "gain Shift" +
//     "infinite charges" exploit impossible by construction rather than by special-casing.
//   - Blompo is free at the point of use; reaching him is the cost.
//   - One card blessed per visit.
//   - The player picks 1 of 3 randomly offered (valid) enhancements, then picks the card.
public enum CardEnhancement
{
    None = 0,
    Overstuffed = 1,   // +2 charges, right now
    OnTheHouse = 2,    // costs no Shift
    Kickback = 3,      // refunds Shift when played
    Clingy = 4,        // survives Recall, stays in hand
    ExtraSpicy = 5,    // +50% damage        (damage cards only)
    NeverSayDie = 6,   // infinite charges
    DoubleDip = 7,     // plays twice        (instant cards only — see CanApplyTo)
}

public static class CardEnhancements
{
    public const int OVERSTUFFED_CHARGES = 2;
    public const int KICKBACK_SHIFT = 1;
    public const float EXTRA_SPICY_MULT = 1.5f;

    public static readonly CardEnhancement[] All =
    {
        CardEnhancement.Overstuffed, CardEnhancement.OnTheHouse, CardEnhancement.Kickback,
        CardEnhancement.Clingy,      CardEnhancement.ExtraSpicy, CardEnhancement.NeverSayDie,
        CardEnhancement.DoubleDip,
    };

    public static string Name(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.Overstuffed: return "Overstuffed";
            case CardEnhancement.OnTheHouse:  return "On the House";
            case CardEnhancement.Kickback:    return "Kickback";
            case CardEnhancement.Clingy:      return "Clingy";
            case CardEnhancement.ExtraSpicy:  return "Extra Spicy";
            case CardEnhancement.NeverSayDie: return "Never Say Die";
            case CardEnhancement.DoubleDip:   return "Double Dip";
            default: return "";
        }
    }

    // Player-facing effect text. Keep these literal — per the tone brief the humour lives in the
    // NAME, never at the cost of the player understanding what the thing does.
    public static string Description(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.Overstuffed: return $"+{OVERSTUFFED_CHARGES} charges.";
            case CardEnhancement.OnTheHouse:  return "Costs no Shift to play.";
            case CardEnhancement.Kickback:    return $"Gain {KICKBACK_SHIFT} Shift when played.";
            case CardEnhancement.Clingy:      return "Stays in your hand through Recall.";
            case CardEnhancement.ExtraSpicy:  return "Deals 50% more damage.";
            case CardEnhancement.NeverSayDie: return "Never runs out of charges.";
            case CardEnhancement.DoubleDip:   return "Plays twice.";
            default: return "";
        }
    }

    // Rarity drives the offer weighting and the badge colour. Never Say Die is the jackpot.
    public static Rarity RarityOf(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.NeverSayDie: return Rarity.Legendary;
            case CardEnhancement.DoubleDip:   return Rarity.Epic;
            case CardEnhancement.ExtraSpicy:  return Rarity.Rare;
            case CardEnhancement.Clingy:      return Rarity.Rare;
            default:                          return Rarity.Common;
        }
    }

    // --- card categories ------------------------------------------------------------------

    // Cards whose actionValue is a damage number, so "+50% damage" means something.
    public static bool IsDamageCard(CardActionType t)
    {
        switch (t)
        {
            case CardActionType.Fireball:
            case CardActionType.VampiricBite:
            case CardActionType.GlassWail:
            case CardActionType.CometDive:
            case CardActionType.FreefallBlade:
                return true;
            default:
                return false;
        }
    }

    // Cards that resolve INSTANTLY and hold no ConflictFlags while they run.
    //
    // This is a safety filter, not just a design one. A second cast of a *stateful* card is
    // refused by CardActionExecutor.TryExecute because its ModifiedState overlaps the flags the
    // first cast still holds — that's exactly why the Echo Chamber skill silently no-ops on
    // Dash / Phase / Adrenaline / ReverseGravity / CometDive, and Fireball holds
    // AnimatorAttackState for its cast. By only ever OFFERING Double Dip on flagless cards, the
    // collision cannot happen. If you add a new instant card, add it here; if you add a stateful
    // one, do NOT.
    public static bool IsInstantCard(CardActionType t)
    {
        switch (t)
        {
            case CardActionType.Jump:
            case CardActionType.GlassWail:
            case CardActionType.PlatformCreate:
            case CardActionType.DeadWeight:
            case CardActionType.GlassParry:
                return true;
            default:
                return false;
        }
    }

    // --- eligibility ----------------------------------------------------------------------

    public static bool CanApplyTo(CardEnhancement e, RuntimeCard card)
    {
        if (card == null || card.cardData == null) return false;
        if (e == CardEnhancement.None) return false;
        if (card.enhancement != CardEnhancement.None) return false;   // one per card

        CardData d = card.cardData;

        // Stagger is the fail-state card — never blessable.
        if (d.actionType == CardActionType.Stagger) return false;

        switch (e)
        {
            case CardEnhancement.ExtraSpicy:
                return IsDamageCard(d.actionType);

            case CardEnhancement.DoubleDip:
                return IsInstantCard(d.actionType);

            // Meaningless on a card that already costs nothing.
            case CardEnhancement.OnTheHouse:
                return d.shiftCost > 0;

            // Meaningless on a card that can't run out.
            case CardEnhancement.Overstuffed:
            case CardEnhancement.NeverSayDie:
                return !card.isInfinite;

            default:
                return true;
        }
    }

    public static bool CanEnhance(RuntimeCard card)
    {
        foreach (CardEnhancement e in All)
            if (CanApplyTo(e, card)) return true;
        return false;
    }

    // Rolls up to `count` distinct enhancements that are valid for `card`, weighted so the
    // strong ones stay rare. Returns fewer than `count` only if the card has fewer valid options.
    public static List<CardEnhancement> RollOffers(RuntimeCard card, int count = 3)
    {
        List<CardEnhancement> pool = new List<CardEnhancement>();
        foreach (CardEnhancement e in All)
            if (CanApplyTo(e, card)) pool.Add(e);

        List<CardEnhancement> picked = new List<CardEnhancement>();
        while (picked.Count < count && pool.Count > 0)
        {
            int total = 0;
            foreach (CardEnhancement e in pool) total += Weight(e);

            int roll = Random.Range(0, total);
            int idx = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Weight(pool[i]);
                if (roll < 0) { idx = i; break; }
            }

            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }

    private static int Weight(CardEnhancement e)
    {
        switch (RarityOf(e))
        {
            case Rarity.Legendary: return 1;
            case Rarity.Epic:      return 3;
            case Rarity.Rare:      return 6;
            default:               return 10;
        }
    }

    // --- application ----------------------------------------------------------------------

    // Attaches the enhancement and applies any one-shot effect. Returns false if not legal.
    public static bool Apply(RuntimeCard card, CardEnhancement e)
    {
        if (!CanApplyTo(e, card)) return false;

        card.enhancement = e;

        switch (e)
        {
            case CardEnhancement.Overstuffed:
                card.currentUses += OVERSTUFFED_CHARGES;
                break;

            // Reuses the existing isInfinite plumbing (charge checks, decrement, exhaust routing
            // all already honour it) rather than adding a parallel code path.
            case CardEnhancement.NeverSayDie:
                card.isInfinite = true;
                break;
        }

        Debug.Log($"BLOMPO: {card.cardData.cardName} is now {Name(e)}");
        return true;
    }
}

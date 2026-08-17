using UnityEngine;

public class PortalAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Portal;

    // `value` (the card's actionValue) is deliberately unused. It used to be Portal's real Shift
    // price, duplicating the shiftCost the card face showed — DeckManager now charges shiftCost like
    // every other card, so there is one number instead of two that had to agree.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        return player.TryPlacePortal(out keepCardInHand);
    }
}

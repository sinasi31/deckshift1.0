using System.Collections;
using UnityEngine;

public class VampiricBiteAction : CardAction
{
    public override CardActionType ActionType => CardActionType.VampiricBite;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        // Refuse the play when nothing is in bite range — returns false so PlayCard skips the cost
        // and keeps the card in hand (same "failed play" path as Comet Dive while grounded).
        return player.PerformVampiricBite(value);
    }
}

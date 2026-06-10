using System.Collections;
using UnityEngine;

public class StaggerAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Stagger;
    // No declared flags: the hop is a one-frame impulse. Stagger is the forced
    // resource-exhaustion escape valve — it must NEVER be Blocked, or the player
    // could be left with an unplayable hand.

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.PerformStagger();
        return true;
    }
}

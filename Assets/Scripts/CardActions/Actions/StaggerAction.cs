using System.Collections;
using UnityEngine;

public class StaggerAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Stagger;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.PerformStagger();
        return true;
    }
}

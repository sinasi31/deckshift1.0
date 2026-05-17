using System.Collections;
using UnityEngine;

public class PhaseAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Phase;
    public override ConflictFlags ModifiedState =>
        ConflictFlags.GravityScale | ConflictFlags.LayerCollisionMatrix | ConflictFlags.PlayerVelocity;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.PerformPhase(value);
        return true;
    }
}

using System.Collections;
using UnityEngine;

public class ReverseGravityAction : CardAction
{
    public override CardActionType ActionType => CardActionType.ReverseGravity;

    // StartGravityReversal manages the coroutine reference on PlayerController (stop-and-restart
    // on replay). Delegating to that helper preserves the restart logic without restructuring it.
    // The executor does not track ReverseGravity's duration in runningEffects (IsCoroutine = false).
    public override ConflictFlags ModifiedState =>
        ConflictFlags.GravityScale | ConflictFlags.VisualTransform;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.StartGravityReversal();
        return true;
    }
}

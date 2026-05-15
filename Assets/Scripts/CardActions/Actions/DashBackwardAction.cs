using System.Collections;
using UnityEngine;

public class DashBackwardAction : CardAction
{
    public override CardActionType ActionType => CardActionType.DashBackward;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState =>
        ConflictFlags.PlayerVelocity | ConflictFlags.Invincibility | ConflictFlags.GravityScale;

    // Gate check: do not dash if already dashing.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player.currentState != PlayerState.Dashing;
    }

    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        int direction = -1 * (player.isFacingRight ? 1 : -1);
        yield return player.StartCoroutine(player.PerformDash(value, direction));
    }
}

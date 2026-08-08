using System.Collections;
using UnityEngine;

public class DashAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Dash;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity | ConflictFlags.Invincibility;

    // Coroutine action: no instant gate work. The dash is a driven movement state on the
    // player (PlayerController.DashRoutine) rather than a one-shot AddForce impulse — the old
    // impulse was overwritten the next physics frame by the grounded movement code, which is
    // why Dash did nothing on the ground. Returning true simply lets the coroutine start.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return true;
    }

    // Holds the PlayerVelocity | Invincibility flags for the full dash (i-frames + velocity drive).
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.DashRoutine());
    }
}

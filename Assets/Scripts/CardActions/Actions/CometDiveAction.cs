using UnityEngine;

public class CometDiveAction : CardAction
{
    public override CardActionType ActionType => CardActionType.CometDive;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (player.isGrounded)
        {
            Debug.Log("Comet Dive için havada olmalısın!");
            return false;
        }
        player.StartCometDive();
        return true;
    }
}

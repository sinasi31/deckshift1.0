using System.Collections;
using UnityEngine;

public class AdrenalineAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Adrenaline;

    // Adrenaline branches: slow-mo path writes TimeScale; speed-boost path writes MoveSpeed.
    // Both flags are declared since either branch may be active at any given play.
    // Note: UseAdrenaline manages its sub-coroutines internally on PlayerController, so the
    // executor does not track Adrenaline's duration in runningEffects (IsCoroutine = false).
    public override ConflictFlags ModifiedState => ConflictFlags.TimeScale | ConflictFlags.MoveSpeed;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.UseAdrenaline(value);
        return true;
    }
}

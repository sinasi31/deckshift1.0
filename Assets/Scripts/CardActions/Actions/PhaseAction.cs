using System.Collections;
using UnityEngine;

public class PhaseAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Phase;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState =>
        ConflictFlags.GravityScale | ConflictFlags.LayerCollisionMatrix | ConflictFlags.PlayerVelocity;

    // Gate check: sound fires here so it plays same-frame as card play.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        SfxManager.PlayOn(player.audioSource, player.phaseSound);
        return true;
    }

    // Duration path: holds activeFlags live through the base duration and the wall-stuck extension.
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.PhaseRoutine(value));
    }
}

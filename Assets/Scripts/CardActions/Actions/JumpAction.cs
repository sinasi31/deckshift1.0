using System.Collections;
using UnityEngine;

public class JumpAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Jump;
    // No declared flags: the impulse is a one-frame write with nothing to restore.
    // Declaring PlayerVelocity would wrongly Block jumps during dives/dash i-frames —
    // basic mobility must never be refused by the conflict system.

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        SfxManager.PlayOn(player.audioSource, player.leapSound);
        player.rb.linearVelocity = new Vector2(player.rb.linearVelocity.x, 0);
        float jumpDir = player.isGravityReversed ? -1f : 1f;
        player.rb.AddForce(new Vector2(0f, value * jumpDir), ForceMode2D.Impulse);
        if (player.leapEffectPrefab != null)
        {
            Vector3 spawnPos = player.transform.position + new Vector3(0f, -0.8f, 0f);
            Object.Instantiate(player.leapEffectPrefab, spawnPos, Quaternion.identity);
        }
        player.ChangeState(PlayerState.Jumping);
        return true;
    }
}

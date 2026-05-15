using System.Collections;
using UnityEngine;

public class JumpAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Jump;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (player.audioSource != null && player.leapSound != null)
            player.audioSource.PlayOneShot(player.leapSound);
        player.rb.linearVelocity = new Vector2(player.rb.linearVelocity.x, 0);
        player.rb.AddForce(new Vector2(0f, value), ForceMode2D.Impulse);
        if (player.leapEffectPrefab != null)
        {
            Vector3 spawnPos = player.transform.position + new Vector3(0f, -0.8f, 0f);
            Object.Instantiate(player.leapEffectPrefab, spawnPos, Quaternion.identity);
        }
        player.ChangeState(PlayerState.Jumping);
        return true;
    }
}

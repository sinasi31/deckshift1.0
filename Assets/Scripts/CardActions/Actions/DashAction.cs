using UnityEngine;

public class DashAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Dash;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity | ConflictFlags.Invincibility;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;

        float direction = player.isFacingRight ? 1f : -1f;
        player.rb.AddForce(new Vector2(direction * player.dashImpulse, 0f), ForceMode2D.Impulse);

        player.StartCoroutine(player.DashIFrames(player.dashIFrameDuration));

        if (player.dashSound != null && player.audioSource != null)
            player.audioSource.PlayOneShot(player.dashSound);

        if (player.dashEffectPrefab != null)
            Object.Instantiate(player.dashEffectPrefab, player.transform.position, Quaternion.identity);

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.08f, 0.3f);

        return true;
    }
}

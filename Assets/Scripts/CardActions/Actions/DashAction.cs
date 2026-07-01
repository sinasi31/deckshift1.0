using System.Collections;
using UnityEngine;

public class DashAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Dash;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState => ConflictFlags.PlayerVelocity | ConflictFlags.Invincibility;

    // Gate check: instant work only. Impulse fires here so it applies same-frame as card play.
    // Returns false to abort before the i-frame coroutine starts.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;

        float direction = player.isFacingRight ? 1f : -1f;
        player.rb.AddForce(new Vector2(direction * player.dashImpulse, 0f), ForceMode2D.Impulse);

        SfxManager.PlayOn(player.audioSource, player.dashSound);

        if (player.dashEffectPrefab != null)
            Object.Instantiate(player.dashEffectPrefab, player.transform.position, Quaternion.identity);

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.08f, 0.3f);

        return true;
    }

    // Duration path: holds activeFlags live for the full i-frame window.
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.DashIFrames(player.dashIFrameDuration));
    }
}

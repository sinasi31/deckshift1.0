using System.Collections;

public class GlassParryAction : CardAction
{
    public override CardActionType ActionType => CardActionType.GlassParry;
    public override bool IsCoroutine => true;

    // Always plays: the charge is spent up front and missing the 0.5s window simply
    // loses it. Success refunds the charge inside the routine (Glass identity —
    // brutal on failure, self-sustaining on mastery).
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return true;
    }

    // Deliberately NOT an iterator method: an iterator body doesn't run until the first
    // MoveNext, which happens after PlayCard has finished — too late to see which card
    // is being played. This wrapper runs synchronously inside PlayCard's call stack,
    // captures the card, and hands the real routine to the executor.
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        RuntimeCard playedCard = DeckManager.instance != null ? DeckManager.instance.CardBeingPlayed : null;
        return player.GlassParryRoutine(value, playedCard);
    }
}

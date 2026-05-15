using System.Collections;

public abstract class CardAction
{
    public abstract CardActionType ActionType { get; }

    public virtual ConflictFlags ModifiedState => ConflictFlags.None;

    // False means this action runs synchronously inside TryExecute.
    // True means TryExecute will call ExecuteCoroutine and wrap it in a managed coroutine.
    public virtual bool IsCoroutine => false;

    // For instant actions: performs the work and returns success.
    // For coroutine actions: performs the gate check only — return false to abort before
    // the coroutine starts, true to proceed. keepCardInHand is only meaningful for Portal.
    public virtual bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return true;
    }

    // For coroutine actions: returns the IEnumerator the executor will run and track.
    // Called only when Execute returned true. Return null to abort.
    public virtual IEnumerator ExecuteCoroutine(PlayerController player, float value) => null;
}

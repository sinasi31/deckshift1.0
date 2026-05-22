using System.Collections;
using UnityEngine;

public class Trapdoor : MonoBehaviour
{
    [SerializeField] float triggerDelay = 0.4f;
    [SerializeField] float resetDelay = 3f;
    // AC Trapdoor 01 uses a single Bool parameter, not separate open/close triggers.
    // "Open" and "Close" are state names; the parameter that drives them is "IsOpened".
    [SerializeField] string isOpenedParam = "IsOpened";
    // Matches AM Trapdoor 01 - Close.anim m_StopTime (0.433s). Tune per variant.
    [SerializeField] float closeAnimDuration = 0.433f;

    private Animator animator;
    private Collider2D platformCollider;
    private Vector2 boundsCenter;
    private Vector2 boundsSize;
    private Coroutine activeCoroutine;

    private enum State { Closed, Triggered, Open, Closing }
    private State state = State.Closed;

    private const int MaxExtensionAttempts = 5;

    void Awake()
    {
        animator = GetComponent<Animator>();
        platformCollider = GetComponent<Collider2D>();
        // Cache world-space bounds while collider is enabled (trapdoor is static).
        boundsCenter = platformCollider.bounds.center;
        boundsSize = platformCollider.bounds.size;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (state != State.Closed)
            return;
        if (collision.gameObject.GetComponentInParent<PlayerController>() == null)
            return;

        state = State.Triggered;
        activeCoroutine = StartCoroutine(TriggerRoutine());
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (state != State.Triggered)
            return;
        if (collision.gameObject.GetComponentInParent<PlayerController>() == null)
            return;

        // Player ran across fast enough — cancel countdown.
        StopCoroutine(activeCoroutine);
        activeCoroutine = null;
        state = State.Closed;
    }

    private IEnumerator TriggerRoutine()
    {
        yield return new WaitForSeconds(triggerDelay);
        if (state == State.Triggered)
            OpenTrapdoor();
    }

    private void OpenTrapdoor()
    {
        state = State.Open;
        animator.SetBool(isOpenedParam, true);
        // Disable immediately; the Open animation also disables it at 0.05s (belt-and-suspenders).
        platformCollider.enabled = false;
        activeCoroutine = StartCoroutine(CloseAfterDelay());
    }

    // External entry point for lever/button UnityEvent wiring.
    public void Trigger()
    {
        if (state != State.Closed && state != State.Triggered)
            return;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        OpenTrapdoor();
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);

        // Extend open state if the player is still standing over the hole.
        // Cap at MaxExtensionAttempts to prevent permanent open state if something goes wrong.
        int extensions = 0;
        while (extensions < MaxExtensionAttempts && IsPlayerOverlapping())
        {
            yield return new WaitForSeconds(0.5f);
            extensions++;
        }

        state = State.Closing;
        animator.SetBool(isOpenedParam, false); // triggers Close animation → auto-transitions to Closed state

        // Wait for the close animation to finish before re-enabling the collider.
        // Re-enabling before the animation ends would let the player land on an invisible shelf.
        yield return new WaitForSeconds(closeAnimDuration);

        platformCollider.enabled = true;
        state = State.Closed;
    }

    private bool IsPlayerOverlapping()
    {
        // Use cached bounds — platformCollider.bounds is unreliable when the collider is disabled.
        Collider2D[] hits = Physics2D.OverlapBoxAll(boundsCenter, boundsSize, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit.GetComponentInParent<PlayerController>() != null)
                return true;
        }
        return false;
    }
}

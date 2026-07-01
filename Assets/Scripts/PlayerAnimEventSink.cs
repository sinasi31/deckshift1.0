using UnityEngine;

// The Cainos Customizable Pixel Character clips fire a whole set of animation events (OnFootstep,
// OnAttackCast, etc.). The pack's own AnimationEventReceiver is intentionally kept DISABLED because
// it NullReferences on a footstep/audio system this project doesn't use — but with no active
// receiver, every event logs "'OnFootstep' has no receiver!" spam each step.
//
// This is a deliberately inert sink: it defines every event method the pack's clips call, all empty,
// so the events land somewhere harmless. Attach it to the same GameObject as the character's Animator
// (the child named "Animator" under the visual model). If footstep/attack SFX are ever wanted, this is
// the natural place to hook them.
//
// Method names (including the pack's "OnLedgeClimbFinised" typo) must match the clips exactly.
public class PlayerAnimEventSink : MonoBehaviour
{
    public void OnFootstep(AnimationEvent evt) { }

    public void OnReviveStart() { }
    public void OnReviveEnd() { }

    public void OnAttackStart(AnimationEvent evt) { }
    public void OnAttackHit(AnimationEvent evt) { }
    public void OnAttackCast(AnimationEvent evt) { }
    public void OnAttackEnd(AnimationEvent evt) { }

    public void OnThrow() { }

    public void OnArrowDraw() { }
    public void OnArrowNock() { }
    public void OnArrowReady() { }
    public void OnArrowPutBack() { }

    public void OnLedgeClimbLocked() { }
    public void OnLedgeClimbFinised() { }

    public void OnLadderEntered() { }
    public void OnLadderExited() { }

    public void OnCrawlEnter() { }
    public void OnCrawlEntered() { }
    public void OnCrawlExit() { }
    public void OnCrawlExited() { }

    public void OnDodgeStart() { }
    public void OnDodgeEnd() { }

    public void OnSwimHand(AnimationEvent evt) { }
    public void OnSwimFoot(AnimationEvent evt) { }
}

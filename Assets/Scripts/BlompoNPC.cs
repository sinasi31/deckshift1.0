using UnityEngine;

// Blompo, the card-blesser. Press E to open the enhancement screen; he vanishes once he's blessed
// something, so each Blompo is a one-shot.
//
// Placement: must sit on the Interactable layer (12) with a trigger Collider2D, exactly like the
// hub's QuestBoard — PlayerController's sweep uses `interactableLayer` (4096). The `prompt` child
// is an InteractPrompt prefab instance, shown while the player is inside the trigger (same pattern
// as ExitDoor's interactionPopup).
//
// Blompo is FREE at the point of use; reaching him is the cost.
public class BlompoNPC : MonoBehaviour, IInteractable
{
    [Header("Art")]
    [Tooltip("Blompo's portrait, shown on the blessing screen. Assets/Art/blompo-removebg-preview (1).png")]
    public Sprite portrait;

    [Header("Behaviour")]
    [Tooltip("Blompo leaves after granting a blessing, so each one is a single use.")]
    public bool vanishAfterBlessing = true;

    [Tooltip("Optional 'press E' hint (an InteractPrompt prefab instance), shown while in range.")]
    public GameObject prompt;

    private bool spent;

    public void Interact()
    {
        if (spent) return;
        // The screen calls back only if a blessing was actually applied — closing without picking
        // leaves Blompo in place.
        BlompoScreen.Open(portrait, OnBlessed);
    }

    public string GetInteractText() => "Bless a card";

    private void OnBlessed()
    {
        if (spent) return;
        spent = true;

        if (prompt != null) prompt.SetActive(false);

        if (vanishAfterBlessing)
            BlompoVanishVFX.Play(transform.position, gameObject, portrait, transform.localScale.x / 0.59f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!spent && other.CompareTag("Player") && prompt != null) prompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null) prompt.SetActive(false);
    }
}

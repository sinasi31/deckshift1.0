using UnityEngine;

// Blompo, the card-blesser. Press E to open the enhancement screen.
//
// Placement: must sit on the Interactable layer (12) with a trigger Collider2D, exactly like the
// hub's QuestBoard — PlayerController's interactionRange check uses `interactableLayer` (4096).
//
// Blompo is FREE at the point of use; reaching him is the cost. In the hub that means one free
// blessing at the start of every run; as a mid-run node it means the detour you spent to get
// here. `oncePerVisit` enforces the one-card-per-visit rule across repeat interactions, so the
// player can't stand in the hub and bless their whole deck.
public class BlompoNPC : MonoBehaviour, IInteractable
{
    [Header("Art")]
    [Tooltip("Blompo's portrait, shown on the blessing screen. Assets/Art/blompo-removebg-preview (1).png")]
    public Sprite portrait;

    [Header("Behaviour")]
    [Tooltip("One blessing per visit. Re-entering the room (or a new run) resets it.")]
    public bool oncePerVisit = true;

    [Tooltip("Optional 'press E' hint object, shown while the player is in range.")]
    public GameObject prompt;

    private bool used;

    public void Interact()
    {
        if (oncePerVisit && used)
        {
            Debug.Log("BLOMPO: already blessed something here. Come back later.");
            return;
        }
        used = true;
        BlompoScreen.Open(portrait);
    }

    public string GetInteractText()
    {
        return (oncePerVisit && used) ? "Blompo is done with you" : "Bless a card";
    }

    // Reset when the room is (re)entered so a fresh visit grants a fresh blessing.
    private void OnEnable() => used = false;

    private void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player") && prompt) prompt.SetActive(true); }
    private void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player") && prompt) prompt.SetActive(false); }
}

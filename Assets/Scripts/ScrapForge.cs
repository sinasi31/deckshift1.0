using UnityEngine;

// The Scrap Forge — an interactable station that opens ScrapForgeScreen, where scrap is spent to
// repair cards and salvage them out of exhaust.
//
// Placement: must sit on the Interactable layer (12) with a trigger Collider2D, exactly like the
// hub's QuestBoard and BlompoNPC — PlayerController's sweep uses `interactableLayer` (4096). The
// `prompt` child is an InteractPrompt prefab instance, shown while the player is inside the
// trigger. Make the trigger slightly wider than PlayerController.interactionRange so the prompt
// appears exactly when E actually works.
//
// UNLIKE Blompo, the forge does NOT vanish after use. Blompo is a one-shot blessing; the forge is
// a workbench, and the player should be able to repair several cards in one visit if they've got
// the scrap for it. The cost is the scrap itself, so there's no need to also ration the visit.
public class ScrapForge : MonoBehaviour, IInteractable
{
    [Tooltip("Optional 'press E' hint (an InteractPrompt prefab instance), shown while in range.")]
    public GameObject prompt;

    public void Interact()
    {
        ScrapForgeScreen.Open();
    }

    public string GetInteractText() => "Use the forge";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null) prompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null) prompt.SetActive(false);
    }
}

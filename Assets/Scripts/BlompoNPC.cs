using System.Collections.Generic;
using UnityEngine;

// Blompo, the card-blesser. Press E to open the enhancement screen; he vanishes once he's blessed
// something, so each Blompo is a one-shot.
//
// Placement: must sit on the Interactable layer (12) with a trigger Collider2D, exactly like the
// hub's QuestBoard — PlayerController's sweep uses `interactableLayer` (4096). The `prompt` child
// is an InteractPrompt prefab instance, shown while the player is inside the trigger (same pattern
// as ExitDoor's interactionPopup). The trigger is deliberately a little wider than
// PlayerController.interactionRange so the prompt appears exactly when E actually works.
//
// Blompo is FREE at the point of use; reaching him is the cost.
public class BlompoNPC : MonoBehaviour, IInteractable
{
    [Header("Art")]
    [Tooltip("Blompo's portrait, shown on the blessing screen. Assets/Art/blompo-removebg-preview (1).png")]
    public Sprite portrait;

    [Tooltip("Hammer used in the forging animation. Assigned on the prefab from " +
             "PF Weapon - Hammer (Cainos Customizable Pixel Character). Falls back to a " +
             "generated hammer if left empty.")]
    public Sprite hammerSprite;

    [Header("Behaviour")]
    [Tooltip("Blompo leaves after granting a blessing, so each one is a single use.")]
    public bool vanishAfterBlessing = true;

    [Tooltip("Optional 'press E' hint (an InteractPrompt prefab instance), shown while in range.")]
    public GameObject prompt;

    // THIS Blompo's three offers. Rolled ONCE, on first interaction, and kept — walking away and
    // coming back must show the SAME blessings, or the player can reroll for free by leaving the
    // room. Different Blompos roll independently, so each one in a run is its own decision.
    private List<CardEnhancement> offers;
    private bool spent;

    public void Interact()
    {
        if (spent) return;

        if (offers == null)
        {
            List<RuntimeCard> deck = BlompoScreen.CollectDeckStatic();
            offers = CardEnhancements.RollOffersForDeck(deck, 3);
        }

        // The screen calls back only if a blessing was actually applied — closing without picking
        // leaves Blompo (and his offers) exactly as they were.
        BlompoScreen.Open(portrait, hammerSprite, offers, OnBlessed);
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

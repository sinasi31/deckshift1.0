using UnityEngine;

// A chest that offers CARDS. Placed in levels as loot, the same way relic chests are.
//
// Deliberately a separate component and a separate prop from `Chest` (relics), not a mode on it:
// the two pay out completely different things, and a player walking up to a chest should know what
// kind of decision they are about to make before they open it.
//
// ⚠️ IT IS SPENT WHETHER YOU TAKE A CARD OR NOT. A chest you can close and reopen until the offer
// suits you is not a choice, it is a reroll button.
public class CardChest : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string animatorBoolParam = "IsOpened";   // AC Chest 01.controller, m_Type 4 (Bool)
    [SerializeField] private AudioClip openSound;
    [SerializeField] private GameObject prompt;

    private bool isOpened = false;
    private Animator animator;
    private AudioSource audioSource;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        if (isOpened) return;
        isOpened = true;

        if (animator != null) animator.SetBool(animatorBoolParam, true);

        if (openSound != null)
        {
            if (audioSource != null) SfxManager.PlayOn(audioSource, openSound);
            else SfxManager.PlayAtPoint(openSound, transform.position);
        }

        if (prompt != null) prompt.SetActive(false);

        CardChestScreen.Open(null);
    }

    public string GetInteractText()
    {
        return isOpened ? "Already Opened" : "Open Card Chest";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpened && other.CompareTag("Player") && prompt != null)
            prompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(false);
    }
}

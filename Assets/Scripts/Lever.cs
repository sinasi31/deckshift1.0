using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour, IInteractable
{
    // AC Switch 01.controller uses a single Bool parameter "IsOn" (confirmed from YAML).
    // States are Off / Turn On / On / Turn Of — "IsOn" is the only parameter.
    [SerializeField] string animatorBoolParam = "IsOn";
    [SerializeField] bool startOn = false;
    [SerializeField] UnityEvent OnFlippedOn;
    [SerializeField] UnityEvent OnFlippedOff;
    [SerializeField] AudioClip flipSound;
    [SerializeField] GameObject prompt;

    private bool isOn;
    private Animator animator;
    private AudioSource audioSource;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        isOn = startOn;
        animator.SetBool(animatorBoolParam, isOn);
        // No UnityEvent invocation at Start — initial state is visual-only.
    }

    // Called by PlayerController.CheckInteraction() when the player presses E.
    public void Interact()
    {
        isOn = !isOn;
        animator.SetBool(animatorBoolParam, isOn);

        if (flipSound != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(flipSound);
            else
                AudioSource.PlayClipAtPoint(flipSound, transform.position);
        }

        if (isOn)
            OnFlippedOn.Invoke();
        else
            OnFlippedOff.Invoke();
    }

    public string GetInteractText()
    {
        return isOn ? "Turn Off" : "Turn On";
    }

    // Mirrors SimpleInteract's prompt pattern exactly.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(false);
    }
}

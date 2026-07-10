using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    [Header("Relic Pools")]
    [SerializeField] private List<RelicData> commonRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> rareRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> epicRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> legendaryRewards = new List<RelicData>();

    [Header("Interaction")]
    [SerializeField] private string animatorBoolParam = "IsOpened"; // Confirmed: AC Chest 01.controller m_Type:4 (Bool)
    [SerializeField] private AudioClip openSound;
    [SerializeField] private GameObject prompt;

    [Header("Open VFX")]
    [Tooltip("Height above the chest where the reward burst spawns.")]
    [SerializeField] private float openVfxHeight = 0.6f;

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

        if (animator != null)
            animator.SetBool(animatorBoolParam, true);

        if (openSound != null)
        {
            if (audioSource != null)
                SfxManager.PlayOn(audioSource, openSound);
            else
                SfxManager.PlayAtPoint(openSound, transform.position);
        }

        RelicData relic = PickRandomRelic();
        if (relic != null)
        {
            if (RelicManager.instance != null)
                RelicManager.instance.TryGrantRelic(relic);   // full loadout -> Swap Screen
            else
                Debug.LogWarning($"[Chest] '{name}': RelicManager.instance is null, relic grant skipped.");

            // Reward burst, colour-coded to the relic's rarity, spawned above the lid.
            GameObject fxGO = new GameObject("ChestOpenVFX");
            fxGO.transform.position = transform.position + Vector3.up * openVfxHeight;
            fxGO.AddComponent<ChestOpenVFX>().Play(relic.rarity, relic.relicArt);
        }
    }

    public string GetInteractText()
    {
        return isOpened ? "Already Opened" : "Open Chest";
    }

    private RelicData PickRandomRelic()
    {
        // Mirrors SlotMachineUI.CheckRewards tier logic exactly.
        // Dice 1-7 (skull value 0 excluded) so a chest always grants a relic.
        int r1 = Random.Range(1, 8);
        int r2 = Random.Range(1, 8);
        int r3 = Random.Range(1, 8);
        int total = r1 + r2 + r3;

        // Fallback chain: if the rolled tier's pool is empty, step down to the next lower tier.
        List<RelicData>[] fallbackChain;
        if (total == 21)
            fallbackChain = new[] { legendaryRewards, epicRewards, rareRewards, commonRewards };
        else if (total >= 16)
            fallbackChain = new[] { epicRewards, rareRewards, commonRewards };
        else if (total >= 11)
            fallbackChain = new[] { rareRewards, commonRewards };
        else
            fallbackChain = new[] { commonRewards };

        foreach (var pool in fallbackChain)
        {
            if (pool != null && pool.Count > 0)
                return pool[Random.Range(0, pool.Count)];
        }

        Debug.LogWarning($"[Chest] '{name}': all relic pools are empty, no relic granted.");
        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(false);
    }
}

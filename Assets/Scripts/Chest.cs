using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    [Header("Relic Pools")]
    [Tooltip("OPTIONAL. Leave empty (the normal case) and the chest draws from every relic in the " +
             "project via RelicPool, minus the ones the player already owns. Fill a tier in only to " +
             "restrict THIS chest to a curated set — a hand-maintained list is how the pools fell " +
             "13 relics behind the roster in the first place.")]
    [SerializeField] private List<RelicData> commonRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> rareRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> epicRewards = new List<RelicData>();
    [SerializeField] private List<RelicData> legendaryRewards = new List<RelicData>();

    // Only reachable if the player somehow owns every relic the chest could offer (18 relics vs
    // 5 slots makes that impossible today, but a pool can always run dry).
    private const int FallbackGold = 50;

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

        // ⚠️ A CHEST IS NEVER EMPTY. Once the loadout was full every chest became a swap offer the
        // player usually refused, so chests stopped being worth opening for the rest of the run.
        // A chest is treasure; if the relic cannot be carried, the player pockets what it is WORTH
        // instead. Using the relic's own sell value keeps the payout scaled to the rarity that was
        // rolled, so a Legendary chest still feels like a Legendary chest.
        if (relic != null && RelicManager.instance != null)
        {
            int consolation = RelicManager.instance.SellValueFor(relic);
            RelicManager.instance.TryGrantRelic(
                relic,
                onAcquired: null,
                onDeclined: () => PayConsolation(consolation));
        }
        else if (relic != null)
        {
            Debug.LogWarning($"[Chest] '{name}': RelicManager.instance is null, relic grant skipped.");
        }
        else
        {
            // No relic could be offered at all (the player owns everything this chest can give).
            PayConsolation(FallbackGold);
        }

        if (relic != null)
        {
            // Reward burst, colour-coded to the relic's rarity, spawned above the lid.
            GameObject fxGO = new GameObject("ChestOpenVFX");
            fxGO.transform.position = transform.position + Vector3.up * openVfxHeight;
            fxGO.AddComponent<ChestOpenVFX>().Play(relic.rarity, relic.relicArt);
        }
    }

    // Paid when the relic can't be taken. Goes straight to the wallet rather than spawning pickups:
    // the chest may well have been opened from a menu (the swap screen), so there is no guarantee
    // the player is still standing next to it to collect anything.
    private void PayConsolation(int gold)
    {
        if (gold <= 0) return;
        PlayerController pc = GameManager.instance != null && GameManager.instance.player != null
                            ? GameManager.instance.player.GetComponent<PlayerController>()
                            : FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.AddGold(gold);
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

        Rarity rolled;
        if (total == 21) rolled = Rarity.Legendary;
        else if (total >= 16) rolled = Rarity.Epic;
        else if (total >= 11) rolled = Rarity.Rare;
        else rolled = Rarity.Common;

        // RelicPool does the tier fallback AND excludes relics the player already holds — a chest
        // handing back a relic you are already wearing is a dead reward, and the room's cost was
        // paid for nothing. Because ownership is read here, at open time, selling a relic puts it
        // back in circulation automatically.
        List<RelicData> restrictTo = CuratedPoolFor(rolled);
        RelicData relic = RelicPool.PickOfferable(rolled, restrictTo);

        if (relic == null)
            Debug.LogWarning($"[Chest] '{name}': no un-owned relic available to grant " +
                             "(player owns everything this chest can offer).");
        return relic;
    }

    // A chest normally draws from the whole roster; a tier list is only consulted when someone has
    // deliberately curated THIS chest. Empty lists mean "no restriction", not "no relics".
    private List<RelicData> CuratedPoolFor(Rarity rarity)
    {
        List<RelicData> curated =
            rarity == Rarity.Legendary ? legendaryRewards :
            rarity == Rarity.Epic ? epicRewards :
            rarity == Rarity.Rare ? rareRewards : commonRewards;
        return (curated != null && curated.Count > 0) ? curated : null;
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

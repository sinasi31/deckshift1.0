using System.Collections.Generic;
using UnityEngine;
// TMPro using'ini silebilirsin eðer kodla metin deðiþtirmeyeceksen.

public class WorldSlotMachine : MonoBehaviour
{
    [Header("Ödül Havuzlarý")]
    public List<RelicData> commonRewards;
    public List<RelicData> rareRewards;
    public List<RelicData> epicRewards;
    public List<RelicData> legendaryRewards;

    [Header("Etkileþim")]
    public KeyCode interactKey = KeyCode.E;
    private bool playerInRange = false;

    [Header("Görsel Referans")]
    // Unity'de makinenin üzerine koyacaðýmýz yazý objesini buraya sürükle
    public GameObject interactionPopup;

    private void Start()
    {
        // Oyun baþladýðýnda yazýnýn kapalý olduðundan emin olalým
        if (interactionPopup != null) interactionPopup.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            if (SlotMachineUI.instance != null)
            {
                // Etkileþime girince yazýyý kapat (görüntü kirliliði olmasýn)
                if (interactionPopup != null) interactionPopup.SetActive(false);

                SlotMachineUI.instance.OpenSlotMachine(commonRewards, rareRewards, epicRewards, legendaryRewards, this.gameObject);
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class WorldSlotMachine : MonoBehaviour
{
    [Header("Ödül Havuzlarý")]
    public List<RelicData> commonRewards;
    public List<RelicData> epicRewards;
    public List<RelicData> legendaryRewards;

    [Header("Etkileþim")]
    private TextMeshProUGUI promptText_UI;
    public KeyCode interactKey = KeyCode.E;
    private bool playerInRange = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            // Tembel Yükleme ile metni bul
            if (promptText_UI == null)
            {
                GameObject obj = GameObject.Find("InteractPrompt_Text");
                if (obj != null) promptText_UI = obj.GetComponent<TextMeshProUGUI>();
            }

            if (promptText_UI != null)
            {
                promptText_UI.gameObject.SetActive(true);
                promptText_UI.text = $"Press [{interactKey}] to Spin";
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptText_UI != null) promptText_UI.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            // UI Manager'ý çaðýr ve havuzlarý gönder
            if (SlotMachineUI.instance != null)
            {
                // Yazýyý gizle
                if (promptText_UI != null) promptText_UI.gameObject.SetActive(false);

                // Ekraný aç
                SlotMachineUI.instance.OpenSlotMachine(commonRewards, epicRewards, legendaryRewards, this.gameObject);
            }
            else
            {
                Debug.LogError("SlotMachineUI sahneden bulunamadý!");
            }
        }
    }
}
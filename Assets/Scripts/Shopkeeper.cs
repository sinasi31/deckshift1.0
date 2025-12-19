using UnityEngine;

public class Shopkeeper : MonoBehaviour
{
    private bool playerInRange = false;
    public KeyCode interactKey = KeyCode.E;

    [Header("Görsel Referans")]
    public GameObject interactionPopup; // [E] Shop

    private void Start()
    {
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

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            // Etkileþim yazýsýný kapat
            if (interactionPopup != null) interactionPopup.SetActive(false);

            // Dükkaný aç
            if (ShopManager.instance != null)
            {
                ShopManager.instance.OpenShop();
            }
        }
    }
}
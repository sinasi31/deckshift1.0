using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform;

    [Header("Oda Ayarlarý")]
    public List<GameObject> roomPrefabs; // Tüm odalarýn listesi (Prefablar)

    // --- YENÝ: HAVUZ SÝSTEMÝ ---
    private List<int> availableRoomIndices = new List<int>(); // Henüz oynanmamýþ odalarýn listesi
    // ---------------------------

    private GameObject currentRoom;

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
    }

    private void Start()
    {
        // Havuzu ilk kez doldur
        RefillRoomPool();

        SpawnNextRoom();
    }

    // --- YENÝ: HAVUZU DOLDURMA FONKSÝYONU ---
    private void RefillRoomPool()
    {
        availableRoomIndices.Clear();
        for (int i = 0; i < roomPrefabs.Count; i++)
        {
            availableRoomIndices.Add(i);
        }
        Debug.Log("Oda havuzu yenilendi/dolduruldu.");
    }
    // ----------------------------------------

    public void SpawnNextRoom()
    {
        TemporaryObject[] junk = FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None);
        foreach (TemporaryObject obj in junk) Destroy(obj.gameObject);

        if (currentRoom != null) Destroy(currentRoom);

        if (availableRoomIndices.Count == 0)
        {
            RefillRoomPool();
        }
        int randomIndexInPool = Random.Range(0, availableRoomIndices.Count); 
        int selectedRoomIndex = availableRoomIndices[randomIndexInPool]; 
        availableRoomIndices.RemoveAt(randomIndexInPool);

        Debug.Log($"Seçilen Oda Indexi: {selectedRoomIndex}. Kalan Oda Sayýsý: {availableRoomIndices.Count}");

        GameObject selectedRoomPrefab = roomPrefabs[selectedRoomIndex];
        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);
        CameraController camScript = Camera.main.GetComponent<CameraController>();

        if (camScript != null)
        {
            camScript.target = playerTransform;
            Transform boundsObj = currentRoom.transform.Find("CameraBounds");

            if (boundsObj != null)
            {
                BoxCollider2D boundsCollider = boundsObj.GetComponent<BoxCollider2D>();
                camScript.SetBounds(boundsCollider);
            }
            else
            {
                Debug.LogWarning("DÝKKAT: Bu oda prefabýnda 'CameraBounds' objesi yok! Kamera sýnýrsýz hareket edecek.");
                camScript.SetBounds(null);
            }
        }

        Transform entryPoint = currentRoom.transform.Find("GirisNoktasi");
        if (entryPoint != null && playerTransform != null)
        {
            playerTransform.position = entryPoint.position;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.OnNewRoomEnter();
                playerController.SetCurrentEntryPoint(entryPoint.position);
            }
        }
        if (DeckManager.instance != null)
        {
            DeckManager.instance.ReloadHand();
        }
    }
}
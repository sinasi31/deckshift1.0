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
        // 1. Temizlik (Eski objeleri sil)
        TemporaryObject[] junk = FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None);
        foreach (TemporaryObject obj in junk) Destroy(obj.gameObject);

        if (currentRoom != null) Destroy(currentRoom);

        // 2. Havuz Kontrolü
        if (availableRoomIndices.Count == 0)
        {
            // Eðer tüm odalar bittiyse havuzu tekrar doldur
            // (Ýstersen burada "Act 2'ye geç" mantýðý da kurabilirsin)
            RefillRoomPool();
        }

        // 3. Rastgele Seçim (Torbadan Çek)
        int randomIndexInPool = Random.Range(0, availableRoomIndices.Count); // Listeden rastgele bir sýra seç
        int selectedRoomIndex = availableRoomIndices[randomIndexInPool]; // O sýradaki gerçek oda numarasýný al

        // 4. Seçileni Torbadan At (Ýþaretle)
        availableRoomIndices.RemoveAt(randomIndexInPool);

        Debug.Log($"Seçilen Oda Indexi: {selectedRoomIndex}. Kalan Oda Sayýsý: {availableRoomIndices.Count}");

        // 5. Odayý Yarat
        GameObject selectedRoomPrefab = roomPrefabs[selectedRoomIndex];
        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);

        // 6. Oyuncuyu Iþýnla
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
            DeckManager.instance.RefillHand();
        }

        // Kamera sýnýrlarýný ayarla (CameraFollow iptal edildiði için bu kýsým opsiyonel veya sabit kamera için gereksiz olabilir)
        // Ama eðer ileride tekrar eklersen kod burada durabilir.
    }
}
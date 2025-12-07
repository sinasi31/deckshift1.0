using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform; // Oyuncunun Transform'u

    [Header("Oda Ayarlarý")]
    public List<GameObject> roomPrefabs; // Oluþturulabilecek tüm oda prefab'larýnýn listesi

    private GameObject currentRoom;
    private Transform currentExitPoint;

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        // Oyuna ilk odayý yaratarak baþla
        SpawnNextRoom();
    }

    public void SpawnNextRoom()
    {
        // Eðer mevcut bir oda varsa, önce onu yok et
        if (currentRoom != null)
        {
            Destroy(currentRoom);
        }

        TemporaryObject[] junk = FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None);

        foreach (TemporaryObject obj in junk)
        {
            Destroy(obj.gameObject);
        }

        // Listeden rastgele bir oda prefab'ý seç
        int randomIndex = Random.Range(0, roomPrefabs.Count);
        GameObject selectedRoomPrefab = roomPrefabs[randomIndex];

        // Yeni odayý (0,0,0) pozisyonunda yarat
        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);

        // Yeni odanýn giriþ noktasýný bul
        Transform entryPoint = currentRoom.transform.Find("GirisNoktasi");

        // Oyuncuyu yeni odanýn giriþ noktasýna ýþýnla
        if (entryPoint != null && playerTransform != null)
        {
            playerTransform.position = entryPoint.position;
            playerTransform.GetComponent<PlayerController>().OnNewRoomEnter();
            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            playerController.OnNewRoomEnter();
            playerController.SetCurrentEntryPoint(entryPoint.position);

        }
        else
        {
            Debug.LogError("Yeni odanýn Giriþ Noktasý bulunamadý veya oyuncu referansý eksik!");
        }
    }
}
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform;

    [Header("Oda Ayarları")]
    public List<GameObject> roomPrefabs;

    private List<int> availableRoomIndices = new List<int>();
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
        RefillRoomPool();
        SpawnNextRoom();
    }

    private void RefillRoomPool()
    {
        availableRoomIndices.Clear();
        for (int i = 0; i < roomPrefabs.Count; i++)
        {
            availableRoomIndices.Add(i);
        }
        Debug.Log("Oda havuzu yenilendi/dolduruldu.");
    }

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

        Debug.Log($"Seçilen Oda Indexi: {selectedRoomIndex}. Kalan Oda Sayısı: {availableRoomIndices.Count}");

        GameObject selectedRoomPrefab = roomPrefabs[selectedRoomIndex];
        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);

        Transform boundsObj = currentRoom.transform.Find("CameraBounds");
        if (boundsObj != null)
        {
            Debug.Log("CameraBounds bulundu: " + boundsObj.name);
            BoxCollider2D[] zones = boundsObj.GetComponentsInChildren<BoxCollider2D>();
            Debug.Log("Zone sayısı: " + zones.Length);
            CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null)
            {
                cam.SetZones(zones);
                Debug.Log("SetZones çağrıldı!");
            }
            else
            {
                Debug.LogError("CameraFollow bulunamadı!");
            }
        }
        else
        {
            Debug.LogError("CameraBounds objesi bulunamadı!");
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
            DeckManager.instance.ResetRecallCost();
        }
    }

    // Returns true when the active room has a HubMarker on its root.
    // Uses currentRoom — the single authoritative field set by SpawnNextRoom.
    public bool IsCurrentRoomHub()
    {
        if (currentRoom == null) return false;
        return currentRoom.GetComponent<HubMarker>() != null;
    }
}
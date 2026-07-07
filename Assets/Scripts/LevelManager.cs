using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform;

    [Header("Oda Ayarları")]
    [Tooltip("Element 0 must be the hub. The rest are the run's levels, each played once per run.")]
    public List<GameObject> roomPrefabs;
    [Tooltip("Boss room — spawned after every level in roomPrefabs has been played. Leave empty to just loop back to the hub.")]
    public GameObject bossRoomPrefab;

    private List<int> availableRoomIndices = new List<int>();
    private GameObject currentRoom;
    private bool hasSpawnedFirstRoom = false;
    private bool bossSpawned = false;

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
        SpawnNextRoom();
    }

    // Queue every non-hub level (indices 1..n). The hub (0) is the always-first room and the boss
    // is a separate prefab, so neither belongs in this queue.
    private void BuildLevelQueue()
    {
        availableRoomIndices.Clear();
        for (int i = 1; i < roomPrefabs.Count; i++)
            availableRoomIndices.Add(i);
    }

    // Run order: hub first → each queued level once (random order, no repeats) → boss room →
    // then loop back to a fresh run from the hub.
    private GameObject PickNextRoomPrefab()
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0) return null;

        // 1) Hub is always the first room of the run.
        if (!hasSpawnedFirstRoom)
        {
            hasSpawnedFirstRoom = true;
            BuildLevelQueue();
            return roomPrefabs[0];
        }

        // 2) Then every other level, once each, in random order.
        if (availableRoomIndices.Count > 0)
        {
            int pick = Random.Range(0, availableRoomIndices.Count);
            int idx = availableRoomIndices[pick];
            availableRoomIndices.RemoveAt(pick);
            return roomPrefabs[idx];
        }

        // 3) Pool exhausted → the boss room.
        if (!bossSpawned && bossRoomPrefab != null)
        {
            bossSpawned = true;
            return bossRoomPrefab;
        }

        // 4) After the boss (or if no boss is assigned) → restart the cycle from the hub.
        hasSpawnedFirstRoom = false;
        bossSpawned = false;
        return PickNextRoomPrefab();
    }

    public void SpawnNextRoom()
    {
        TemporaryObject[] junk = FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None);
        foreach (TemporaryObject obj in junk) Destroy(obj.gameObject);

        if (currentRoom != null) Destroy(currentRoom);

        GameObject selectedRoomPrefab = PickNextRoomPrefab();
        if (selectedRoomPrefab == null)
        {
            Debug.LogError("LevelManager: no room prefab to spawn (is roomPrefabs empty?).");
            return;
        }

        Debug.Log($"Spawning room: {selectedRoomPrefab.name}. Levels left this run: {availableRoomIndices.Count}, bossSpawned: {bossSpawned}");

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
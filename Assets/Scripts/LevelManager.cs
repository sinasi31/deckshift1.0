using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform;

    [Header("Oda Ayarları")]
    [Tooltip("Element 0 must be the hub. The rest are the run's combat levels. Add a RoomTier " +
             "component to a room prefab to bind it to one map tier; untagged rooms serve any tier.")]
    public List<GameObject> roomPrefabs;
    [Tooltip("Boss room — spawned when the map reaches its top floor. Leave empty to just loop back to the hub.")]
    public GameObject bossRoomPrefab;

    [Header("Recharge rooms (map attachments)")]
    [Tooltip("Scrap: repair and salvage cards, Blompo. LEAVE EMPTY AND NO FOUNDRY IS EVER DRAWN ON " +
             "THE MAP — the map never promises a room it cannot spawn.")]
    public GameObject foundryRoomPrefab;
    [Tooltip("Gold: the shop. Leave empty and no Market is ever drawn on the map.")]
    public GameObject marketRoomPrefab;
    [Tooltip("Shift and healing. Leave empty and no Well is ever drawn on the map.")]
    public GameObject wellRoomPrefab;

    private GameObject currentRoom;
    private bool hasSpawnedFirstRoom = false;

    // Rooms already spawned this run, so an act doesn't repeat a layout while it still has unused ones.
    private readonly List<GameObject> usedRoomPrefabs = new List<GameObject>();

    // A node's recharge room is entered AFTER its combat room and is NOT a floor, so it spawns
    // without advancing the map. Held here between the two spawns.
    private RechargeType pendingRecharge = RechargeType.None;

    // State for the pre-map room order, kept only as the fallback below.
    private List<int> availableRoomIndices = new List<int>();
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

    // Run order is now driven by the RunMap: hub → the branch the player picked, floor by floor →
    // boss → loop back to a fresh act. RunMapManager owns the graph and the position in it; this
    // method only translates "which node am I on" into "which prefab do I spawn".
    private GameObject PickNextRoomPrefab()
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0) return null;

        RunMapManager mapMgr = RunMapManager.instance;
        if (mapMgr == null) return PickNextRoomPrefabWithoutMap();

        // 1) First room of the run: build the act, then the hub. The hub is the map's Start node,
        //    so entering it here and entering it on the map are the same event.
        if (!hasSpawnedFirstRoom)
        {
            hasSpawnedFirstRoom = true;
            usedRoomPrefabs.Clear();
            pendingRecharge = RechargeType.None;
            mapMgr.BeginRun(SpawnableRecharges());
            mapMgr.EnterStart();
            return roomPrefabs[0];
        }

        // 2) A recharge room hangs off the node the player just cleared. It is not a floor, so it
        //    spawns WITHOUT advancing the map.
        if (pendingRecharge != RechargeType.None)
        {
            GameObject recharge = RechargeRoomPrefab(pendingRecharge);
            pendingRecharge = RechargeType.None;
            if (recharge != null) return recharge;
        }

        // 3) Move onto the next node.
        MapNode node = mapMgr.AdvanceToNext();

        // 4) Nothing left above the boss → the act is over, start a fresh one from the hub.
        if (node == null)
        {
            hasSpawnedFirstRoom = false;
            mapMgr.ClearMap();
            return PickNextRoomPrefab();
        }

        if (node.type == MapNodeType.Boss)
            return bossRoomPrefab != null ? bossRoomPrefab : roomPrefabs[0];

        pendingRecharge = node.recharge;
        return PickRoomForTier(node.type);
    }

    // Which recharge rooms this project can currently spawn. The map is generated against exactly
    // this list, so an unassigned slot means that room type simply never appears — rather than
    // appearing as an icon the player routes toward and finds empty.
    private List<RechargeType> SpawnableRecharges()
    {
        List<RechargeType> list = new List<RechargeType>();
        if (foundryRoomPrefab != null) list.Add(RechargeType.Foundry);
        if (marketRoomPrefab != null) list.Add(RechargeType.Market);
        if (wellRoomPrefab != null) list.Add(RechargeType.Well);
        return list;
    }

    private GameObject RechargeRoomPrefab(RechargeType type)
    {
        switch (type)
        {
            case RechargeType.Foundry: return foundryRoomPrefab;
            case RechargeType.Market: return marketRoomPrefab;
            case RechargeType.Well: return wellRoomPrefab;
            default: return null;
        }
    }

    // Picks a room built for this tier, preferring one that hasn't been used yet this run.
    private GameObject PickRoomForTier(MapNodeType tier)
    {
        GameObject pick = TryPickRoomForTier(tier);
        if (pick != null) { usedRoomPrefabs.Add(pick); return pick; }

        // Everything eligible has been used. Repeat a layout rather than spawn nothing: with only
        // seven combat rooms an act can legitimately outlast the pool, and a missing room is an
        // unfinishable run.
        usedRoomPrefabs.Clear();
        pick = TryPickRoomForTier(tier);
        if (pick != null) { usedRoomPrefabs.Add(pick); return pick; }

        Debug.LogError("LevelManager: no combat room available — roomPrefabs needs at least one level after the hub.");
        return null;
    }

    private GameObject TryPickRoomForTier(MapNodeType tier)
    {
        List<GameObject> tagged = new List<GameObject>();
        List<GameObject> untagged = new List<GameObject>();

        for (int i = 1; i < roomPrefabs.Count; i++)
        {
            GameObject room = roomPrefabs[i];
            if (room == null || usedRoomPrefabs.Contains(room)) continue;

            RoomTier rt = room.GetComponent<RoomTier>();
            if (rt == null) untagged.Add(room);
            else if (rt.Serves(tier)) tagged.Add(room);
        }

        // Prefer a room actually authored for this tier; otherwise any untagged room will do, which
        // is what keeps the map working with rooms that predate RoomTier.
        List<GameObject> pool = tagged.Count > 0 ? tagged : untagged;
        if (pool.Count == 0) return null;

        return pool[Random.Range(0, pool.Count)];
    }

    // The pre-map room order, used only if RunMapManager is somehow absent. It should be
    // unreachable — RunMapManager bootstraps itself — but a missing manager silently reverting to
    // random rooms would look almost right, so this stays as a named, obvious fallback.
    private GameObject PickNextRoomPrefabWithoutMap()
    {
        if (!hasSpawnedFirstRoom)
        {
            hasSpawnedFirstRoom = true;
            BuildLevelQueue();
            return roomPrefabs[0];
        }

        if (availableRoomIndices.Count > 0)
        {
            int pick = Random.Range(0, availableRoomIndices.Count);
            int idx = availableRoomIndices[pick];
            availableRoomIndices.RemoveAt(pick);
            return roomPrefabs[idx];
        }

        if (!bossSpawned && bossRoomPrefab != null)
        {
            bossSpawned = true;
            return bossRoomPrefab;
        }

        hasSpawnedFirstRoom = false;
        bossSpawned = false;
        return PickNextRoomPrefabWithoutMap();
    }

    // Leaving a room through the ExitDoor. THE MAP IS THE ONLY THING THAT OPENS HERE.
    //
    // ⚠️ This logic used to live in RewardManager.FinishReward, because a card reward screen was
    // forced on the player between every pair of rooms and the map choice was bolted onto the end of
    // it. That screen is gone (designer 2026-08-09: cards come from chests placed in levels, so
    // taking one is a decision the player makes, not a toll they pay). The map hook had to move with
    // it — left in RewardManager it would simply have stopped running, and a missing route choice
    // does not error, it silently falls back to random room order.
    //
    // The map is skipped when the act offers only one way on, or when the player already planned a
    // branch with M: a forced screen with a single button is ceremony, not a decision.
    public void AdvanceToNextRoom()
    {
        if (RunMapManager.instance != null && RunMapManager.instance.NeedsRouteChoice)
            RunMapScreen.OpenForChoice(SpawnNextRoom);
        else
            SpawnNextRoom();
    }

    public void SpawnNextRoom()
    {
        // Room-end Held payoffs (Dead Weight): fire while the ending room's hand still
        // exists — the ReloadHand below discards it. Only when actually leaving a combat
        // room: not on the first spawn (currentRoom null), not when leaving the hub.
        if (currentRoom != null && !IsCurrentRoomHub() && DeckManager.instance != null)
            DeckManager.instance.OnRoomEnd();

        TemporaryObject[] junk = FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None);
        foreach (TemporaryObject obj in junk) Destroy(obj.gameObject);

        if (currentRoom != null) Destroy(currentRoom);

        GameObject selectedRoomPrefab = PickNextRoomPrefab();
        if (selectedRoomPrefab == null)
        {
            Debug.LogError("LevelManager: no room prefab to spawn (is roomPrefabs empty?).");
            return;
        }

        MapNode at = RunMapManager.instance != null ? RunMapManager.instance.CurrentNode : null;
        Debug.Log($"Spawning room: {selectedRoomPrefab.name}" + (at != null ? $" — map node {at}" : " — no map"));

        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);

        // Put every actor on the shared draw plane and shove decoration behind it. Opaque sprites
        // sort by camera depth, not sortingOrder, and each room had been authored at its own Z —
        // which is why the player and enemies sometimes rendered behind props. See PlayPlane.
        PlayPlane.Apply(currentRoom);

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
            // ⚠️ Take X and Y from the entry point but NOT its Z. Rooms disagree wildly about depth
            // (spawn Z ranged from -1.06 to +2.56 across the pool), and copying the full Vector3 is
            // what put the player on a different plane in every room. The player belongs on the
            // play plane, always.
            Vector3 spawn = new Vector3(entryPoint.position.x, entryPoint.position.y, PlayPlane.Z);
            playerTransform.position = spawn;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.OnNewRoomEnter();
                playerController.SetCurrentEntryPoint(spawn);
            }
        }

        if (DeckManager.instance != null)
        {
            DeckManager.instance.ReloadHand();
            DeckManager.instance.ResetRecallCost();
            DeckManager.instance.ResetRoomRelicState();
        }

        // Per-room relic triggers (Pocket Battery, Flux Regulator, ...).
        if (RelicManager.instance != null)
            RelicManager.instance.OnRoomStart();
    }

    // Returns true when the active room has a HubMarker on its root.
    // Uses currentRoom — the single authoritative field set by SpawnNextRoom.
    public bool IsCurrentRoomHub()
    {
        if (currentRoom == null) return false;
        return currentRoom.GetComponent<HubMarker>() != null;
    }
}
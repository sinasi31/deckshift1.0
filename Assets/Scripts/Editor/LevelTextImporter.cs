using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Tilemaps;

// Deckshift level-from-text importer.
//
// Reads an ASCII grid (.txt) and builds a room prefab that satisfies the room
// contract LevelManager expects:
//   - a child named exactly "CameraBounds" with a BoxCollider2D zone
//   - a child named exactly "GirisNoktasi" (player spawn)
//   - an ExitDoor instance somewhere in the room
//
// Visual language learned from EfeVrl7 (tile-by-tile audit, 2026-07-13):
//   - a full-room BACKDROP of "TX Tileable - Dungeon Wall" tiles (BGPalette base)
//   - a room FRAME painted with edge tiles: _153/_154 top, _156/_157 left,
//     _188/_189 right, _144 floor surface over _186/_185 fill, _96 ceiling face
//   - free-standing interior platforms use chunky "Ground Dirt" block tiles
// The importer reproduces that: a "BackWall" tilemap (no collider, order 0)
// plus a "Ground" tilemap (layer 3, TilemapCollider2D, order 1, at z=1).
//
// Marker legend and example: Assets/LevelTexts/TestRoom1.txt
public static class LevelTextImporter
{
    private const string OutputFolder = "Assets/LevelGenerated";
    private const string InputFolder = "Assets/LevelTexts";

    // Level geometry layer index (matches PlayerController.groundLayer mask, see CLAUDE.md).
    private const int GroundLayer = 3;
    private const int GroundSortingOrder = 1;   // hand-built rooms use 1 for ground
    private const int BackWallSortingOrder = 0; // backdrop renders behind ground
    private const float GroundZ = 1f;           // hand-built rooms keep the tile grid at z=1

    private const string SpawnPrefabPath = "Assets/Prefabs/GirisNoktasi.prefab";
    private const string CameraBoundsPrefabPath = "Assets/Prefabs/CameraBounds.prefab";

    // ASCII marker -> prefab asset path. '.', ' ' = empty, '#' = ground tile,
    // 'S' = spawn (handled separately so we can enforce exactly one).
    private static readonly Dictionary<char, string> MarkerPrefabs = new Dictionary<char, string>
    {
        { 'X', "Assets/Prefabs/ExitDoor.prefab" },
        { 'm', "Assets/YeniLeveller/MeleeEnemy.prefab" },
        { 'r', "Assets/YeniLeveller/RangedEnemy.prefab" },
        { 'l', "Assets/YeniLeveller/SlimeEnemy.prefab" },
        { 'M', "Assets/YeniLeveller/Mimic.prefab" },
        // Zombie early-enemy tiers (Cainos rig + game AI). See CardAnchors.md §6.
        // 'z' Shambler: fodder (12 HP, one-shot), melee contact. Built from PF Zombie - A.
        // 'Z' Rotbrute: grunt (25 HP), bigger/slower, harder contact hit. PF Zombie - B skin.
        // 's' Spitter: weak ranged (18 HP), lobs a projectile via ZombieSpitterAI. PF Zombie - C skin.
        { 'z', "Assets/YeniLeveller/Shambler.prefab" },
        { 'Z', "Assets/YeniLeveller/Rotbrute.prefab" },
        { 's', "Assets/YeniLeveller/Spitter.prefab" },
        // NOTE: the flying bat is BatMan.prefab (has AeroBatAI). Assets/Prefabs/AeroBat.prefab
        // is a legacy husk without AI — do not point markers at it.
        { 'b', "Assets/YeniLeveller/BatMan.prefab" },
        { '^', "Assets/LevelEfeVrl/spikers.prefab" },
        { 'T', "Assets/YeniLeveller/Trapdoor.prefab" },
        { 'W', "Assets/YeniLeveller/BreakableWall_Bookshelf.prefab" },
        { '+', "Assets/Prefabs/ShiftCrystal.prefab" },
        { 'g', "Assets/YeniLeveller/Gold New.prefab" },
        { 'C', "Assets/YeniLeveller/Chest.prefab" },
        // mechanics (added for GenLevel3):
        { 'E', "Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Props/PF Dungeon Props - Elevator.prefab" }, // moving platform; tune travel in Inspector
        { 'F', "Assets/Prefabs/UpdraftFan.prefab" },        // updraft zone ~3 tall, liftForce 20 (~5-7 tiles of lift)
        { 'w', "Assets/Prefabs/AcidWater.prefab" },         // acid pool ~6 wide; damages + slows
        { 'K', "Assets/Prefabs/WreckingBall.prefab" },      // swinging hazard; placed at cell center, tune anchor
        { 'c', "Assets/Prefabs/CrumblingPlatform.prefab" }, // platform that crumbles underfoot
        { 't', "Assets/Prefabs/Taret.prefab" },             // stationary turret enemy
        { '$', "Assets/YeniLeveller/Shopkeeper_NPC.prefab" }, // shop NPC (its 'missing' scripts are TMP/UI package scripts — fine)
        { 'L', "Assets/YeniLeveller/Lever.prefab" },          // lever; importer wires it to the NEAREST gate (On=Open, Off=Close)
    };

    // Non-prefab structural markers, built procedurally:
    //   '=' one-way platform tiles (jump up through, land on top)
    //   'G' gate cell — vertical runs of G become one sliding Gate (portcullis)
    //   'A' Shift Altar — pay Shift, fires OnPaid (wired to the nearest gate)
    private const string PropsTexturePath = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Props.png";
    private const string GateSpriteName = "TX Dungeon Props - Gate 01";
    private const string AltarSpriteName = "TX Dungeon Props - Wall Altar 01 Lit";
    private const int InteractableLayer = 12; // "Interactable" (PlayerController.interactableLayer)

    // Markers that stand ON the ground: after spawning, the instance is shifted
    // so the bottom of its measured visual bounds sits exactly on the cell floor.
    // (Enemies here don't fall — most have kinematic physics — so placement must
    // be exact.) Floaty pickups ('+', 'g') and flyers ('b') stay at cell center.
    // 'E' (elevator) and 'K' (wrecking ball) are NOT grounded: they stay at cell
    // center — the elevator's rest position and the ball's hang point are tuned
    // by hand in the Inspector.
    private static readonly HashSet<char> GroundedMarkers = new HashSet<char>
    {
        'X', 'm', 'r', 'l', 'M', 'z', 'Z', 's', 'C', '^', 'W', 'T', 'F', 'w', 'c', 't', '$', 'L',
    };

    // ---- Tile roles (the biseyler copies are the ones hand-made levels reference) ----
    private const string TileDir = "Assets/LevelSinasi/biseyler/";
    private const string TileTopOuter = TileDir + "TX Tileset - Dungeon Ground Extra_153.asset"; // top border, outer row
    private const string TileTopInner = TileDir + "TX Tileset - Dungeon Ground Extra_154.asset"; // top border, row under outer
    private const string TileCeilFace = TileDir + "TX Tileset - Dungeon Ground Extra_96.asset";  // ceiling face (bricks on bottom edge)
    private const string TileLeftOuter = TileDir + "TX Tileset - Dungeon Ground Extra_156.asset";
    private const string TileLeftInner = TileDir + "TX Tileset - Dungeon Ground Extra_157.asset"; // bricks face the interior
    private const string TileRightInner = TileDir + "TX Tileset - Dungeon Ground Extra_188.asset";
    private const string TileRightOuter = TileDir + "TX Tileset - Dungeon Ground Extra_189.asset";
    private const string TileFloorTop = TileDir + "TX Tileset - Dungeon Ground Extra_144.asset";  // walkable surface
    private const string TileFloorMid = TileDir + "TX Tileset - Dungeon Ground Extra_186.asset";
    private const string TileFloorDeep = TileDir + "TX Tileset - Dungeon Ground Extra_185.asset";

    // Free-standing platform STRIPS (2+ cells wide): left cap / middle / right cap.
    // Learned from EfeVrl6's interior platforms (Extra_112/113/114 painted as runs).
    private const string TilePlatCapL = TileDir + "TX Tileset - Dungeon Ground Extra_112.asset";
    private const string TilePlatMid = TileDir + "TX Tileset - Dungeon Ground Extra_113.asset";
    private const string TilePlatCapR = TileDir + "TX Tileset - Dungeon Ground Extra_114.asset";

    // Lone free-standing blocks / pillars: chunky block tiles (what the hand-made
    // rooms use as spaced stepping stones, e.g. '#..#..#').
    private static readonly string[] DirtTiles =
    {
        TileDir + "TX Tileset - Dungeon Ground Dirt_14.asset",
        TileDir + "TX Tileset - Dungeon Ground Dirt_12.asset",
        TileDir + "TX Tileset - Dungeon Ground Dirt_4.asset",
        TileDir + "TX Tileset - Dungeon Ground Dirt_0.asset",
    };

    // Backdrop: the same wall tiles BGPalette's pre-painted background uses.
    private const string WallDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Tileset Pallete/TP Dungeon Wall/";
    private static readonly string[] BackWallTiles =
    {
        WallDir + "TX Tileable - Dungeon Wall_15.asset",
        WallDir + "TX Tileable - Dungeon Wall_17.asset",
        WallDir + "TX Tileable - Dungeon Wall_31.asset",
        WallDir + "TX Tileable - Dungeon Wall_32.asset",
        WallDir + "TX Tileable - Dungeon Wall_33.asset",
        WallDir + "TX Tileable - Dungeon Wall_35.asset",
    };

    [MenuItem("Deckshift/Import Level From Text...")]
    public static void ImportFromText()
    {
        string startDir = Directory.Exists(InputFolder) ? Path.GetFullPath(InputFolder) : Application.dataPath;
        string filePath = EditorUtility.OpenFilePanel("Select level text file", startDir, "txt");
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            string savedPath = Build(filePath);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Level import failed", e.Message, "OK");
            Debug.LogError("[LevelTextImporter] " + e);
        }
    }

    private static string Build(string filePath)
    {
        // ---------- Parse ----------
        var directives = new Dictionary<string, string>();
        var gridLines = new List<string>();

        foreach (string raw in File.ReadAllLines(filePath))
        {
            string line = raw.TrimEnd('\r', '\n');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("//"))
                continue; // comment

            if (trimmed.StartsWith("!"))
            {
                int colon = trimmed.IndexOf(':');
                if (colon > 1)
                {
                    string key = trimmed.Substring(1, colon - 1).Trim().ToLowerInvariant();
                    string value = trimmed.Substring(colon + 1).Trim();
                    directives[key] = value;
                }
                continue;
            }

            gridLines.Add(line);
        }

        // Trim leading/trailing fully-empty lines (interior empty lines are valid rows).
        while (gridLines.Count > 0 && gridLines[0].Trim().Length == 0) gridLines.RemoveAt(0);
        while (gridLines.Count > 0 && gridLines[gridLines.Count - 1].Trim().Length == 0) gridLines.RemoveAt(gridLines.Count - 1);

        if (gridLines.Count == 0)
            throw new Exception("No grid found in file. Add rows of '#' and '.' below the '!' directives.");

        int height = gridLines.Count;
        int width = 0;
        foreach (string l in gridLines)
            width = Mathf.Max(width, l.Length);

        string levelName = directives.TryGetValue("name", out string dirName) && dirName.Length > 0
            ? dirName
            : Path.GetFileNameWithoutExtension(filePath);

        // char at (col, rowFromTop); short lines count as empty
        char At(int col, int row)
        {
            string l = gridLines[row];
            return col < l.Length ? l[col] : '.';
        }
        bool InGrid(int col, int row) => col >= 0 && col < width && row >= 0 && row < height;
        // outside the grid counts as SOLID so border outer faces don't read as exposed
        bool IsSolid(int col, int row)
        {
            if (!InGrid(col, row)) return true;
            return At(col, row) == '#';
        }

        // ---------- Validate ----------
        var warnings = new List<string>();
        var unknownChars = new HashSet<char>();
        int spawnCount = 0, exitCount = 0;

        bool hasOneWay = false, hasGate = false, hasAltar = false;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                char c = At(col, row);
                if (c == '.' || c == ' ' || c == '#') continue;
                if (c == '=') { hasOneWay = true; continue; }
                if (c == 'G') { hasGate = true; continue; }
                if (c == 'A') { hasAltar = true; continue; }
                if (c == 'S') { spawnCount++; continue; }
                if (c == 'X') exitCount++;
                if (!MarkerPrefabs.ContainsKey(c)) unknownChars.Add(c);
            }
        }

        if (spawnCount != 1)
            throw new Exception($"A room needs exactly one 'S' (player spawn). Found {spawnCount}.");
        if (exitCount == 0)
            warnings.Add("No 'X' (ExitDoor) — the room will have no way to leave.");
        foreach (char c in unknownChars)
            warnings.Add($"Unknown marker '{c}' ignored.");

        // ---------- Preload assets ----------
        TileBase topOuter = LoadTile(TileTopOuter), topInner = LoadTile(TileTopInner), ceilFace = LoadTile(TileCeilFace);
        TileBase leftOuter = LoadTile(TileLeftOuter), leftInner = LoadTile(TileLeftInner);
        TileBase rightInner = LoadTile(TileRightInner), rightOuter = LoadTile(TileRightOuter);
        TileBase floorTop = LoadTile(TileFloorTop), floorMid = LoadTile(TileFloorMid), floorDeep = LoadTile(TileFloorDeep);
        TileBase platCapL = LoadTile(TilePlatCapL), platMid = LoadTile(TilePlatMid), platCapR = LoadTile(TilePlatCapR);
        TileBase[] dirt = LoadTileSet(DirtTiles);
        TileBase[] backWall = LoadTileSet(BackWallTiles);

        var prefabCache = new Dictionary<char, GameObject>();
        var missing = new List<string>();
        foreach (var kv in MarkerPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
            if (prefab == null) missing.Add($"'{kv.Key}' -> {kv.Value}");
            else prefabCache[kv.Key] = prefab;
        }
        var spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefabPath);
        if (spawnPrefab == null) missing.Add($"'S' -> {SpawnPrefabPath}");
        var camBoundsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraBoundsPrefabPath);
        if (camBoundsPrefab == null) missing.Add($"CameraBounds -> {CameraBoundsPrefabPath}");

        Sprite gateSprite = LoadPropSprite(GateSpriteName);
        Sprite altarSprite = LoadPropSprite(AltarSpriteName);
        if (hasGate && gateSprite == null) missing.Add($"'G' gate sprite '{GateSpriteName}' in {PropsTexturePath}");
        if (hasAltar && altarSprite == null) missing.Add($"'A' altar sprite '{AltarSpriteName}' in {PropsTexturePath}");

        if (missing.Count > 0)
            throw new Exception("Prefab paths in LevelTextImporter are stale, fix them:\n" + string.Join("\n", missing));

        // deterministic per-cell variety, independent of iteration order
        TileBase Pick(TileBase[] set, int col, int cellY)
        {
            int h = (col * 73856093) ^ (cellY * 19349663);
            return set[Math.Abs(h) % set.Length];
        }

        // ---------- Build ----------
        var root = new GameObject(levelName);
        try
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform);
            gridGo.transform.localPosition = new Vector3(0f, 0f, GroundZ);
            gridGo.AddComponent<Grid>(); // default 1x1 cells, like BGPalette

            // Backdrop: OPT-IN via "!backwall: on" (the designer usually places the
            // backdrop/decoration by hand). When on, it must live on the "Background"
            // sorting LAYER (like BGPalette's backdrop): several sprites (e.g.
            // ExitDoor at Default order -1) get swallowed by a Default-layer backdrop.
            bool backwallOn = directives.TryGetValue("backwall", out string bwv)
                && (bwv.Trim().ToLowerInvariant() == "on" || bwv.Trim().ToLowerInvariant() == "true");
            Tilemap backMap = null;
            if (backwallOn)
            {
                var backGo = new GameObject("BackWall");
                backGo.transform.SetParent(gridGo.transform);
                backGo.transform.localPosition = Vector3.zero;
                backMap = backGo.AddComponent<Tilemap>();
                var backRenderer = backGo.AddComponent<TilemapRenderer>();
                backRenderer.sortingLayerName = "Background";
                backRenderer.sortingOrder = BackWallSortingOrder;
            }

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            groundGo.transform.localPosition = Vector3.zero;
            groundGo.layer = GroundLayer;
            var tilemap = groundGo.AddComponent<Tilemap>();
            groundGo.AddComponent<TilemapRenderer>().sortingOrder = GroundSortingOrder;
            groundGo.AddComponent<TilemapCollider2D>();

            // One-way platforms ('='): their own tilemap with a one-way
            // PlatformEffector2D — jump up through them, land on top.
            Tilemap oneWayMap = null;
            if (hasOneWay)
            {
                var oneWayGo = new GameObject("OneWay");
                oneWayGo.transform.SetParent(gridGo.transform);
                oneWayGo.transform.localPosition = Vector3.zero;
                oneWayGo.layer = GroundLayer; // ground checks must see it
                oneWayMap = oneWayGo.AddComponent<Tilemap>();
                oneWayGo.AddComponent<TilemapRenderer>().sortingOrder = GroundSortingOrder;
                var twc = oneWayGo.AddComponent<TilemapCollider2D>();
                var composite = oneWayGo.AddComponent<CompositeCollider2D>(); // auto-adds a Rigidbody2D
                oneWayGo.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                twc.compositeOperation = Collider2D.CompositeOperation.Merge;
                composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
                composite.usedByEffector = true;
                var effector = oneWayGo.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
            }

            int tileCount = 0;
            var entityCounts = new Dictionary<string, int>();
            var gateColumns = new Dictionary<int, List<int>>(); // col -> rows with 'G'
            var levers = new List<Lever>();
            var altars = new List<ShiftAltar>();

            for (int row = 0; row < height; row++)
            {
                int cellY = height - 1 - row; // row 0 is the TOP line of the file
                for (int col = 0; col < width; col++)
                {
                    if (backMap != null)
                        backMap.SetTile(new Vector3Int(col, cellY, 0), Pick(backWall, col, cellY));

                    char c = At(col, row);
                    if (c == '.' || c == ' ')
                        continue;

                    if (c == '#')
                    {
                        bool airUp = !IsSolid(col, row - 1);
                        bool airDown = !IsSolid(col, row + 1);
                        bool airLeft = !IsSolid(col - 1, row);
                        bool airRight = !IsSolid(col + 1, row);

                        // A "strip" cell is 1 tile thick (air above AND below):
                        // floating platforms and wall-attached shelves alike.
                        bool IsStrip(int cc) => cc >= 0 && cc < width && At(cc, row) == '#'
                            && !IsSolid(cc, row - 1) && !IsSolid(cc, row + 1);

                        TileBase tile;
                        if (airUp && airDown)
                        {
                            // strip: caps on OPEN ends only (an end abutting a wall stays a middle)
                            int runStart = col;
                            while (IsStrip(runStart - 1)) runStart--;
                            int runEnd = col;
                            while (IsStrip(runEnd + 1)) runEnd++;
                            bool openL = !IsSolid(runStart - 1, row);
                            bool openR = !IsSolid(runEnd + 1, row);

                            if (runStart == runEnd)
                                tile = openL && openR ? Pick(dirt, col, cellY)      // lone stepping stone
                                     : openR ? platCapR : openL ? platCapL : platMid;
                            else if (col == runStart && openL) tile = platCapL;
                            else if (col == runEnd && openR) tile = platCapR;
                            else tile = platMid;
                        }
                        else if (airUp) tile = floorTop;        // walkable surface of a thick mass
                        else if (airDown) tile = ceilFace;      // ceiling face
                        // Wall faces: the "inner" tiles (_188/_157) are accent tiles with
                        // protruding brick nubs — only correct when BACKED by a real solid
                        // tile (2-thick walls, like hand-made borders). A 1-thick wall at
                        // the grid edge uses the clean outer tiles (_189/_156) instead.
                        else if (airLeft) tile = InGrid(col + 1, row) && At(col + 1, row) == '#' ? rightInner : rightOuter;
                        else if (airRight) tile = InGrid(col - 1, row) && At(col - 1, row) == '#' ? leftInner : leftOuter;
                        // buried cells
                        else if (row == 0) tile = topOuter;
                        else if (row == 1 && At(col, 0) == '#') tile = topInner;
                        else if (col == 0) tile = leftOuter;
                        else if (col == width - 1) tile = rightOuter;
                        // hand-made layering: the gappy sub-surface tile (_186) goes in
                        // exactly ONE row directly under a walkable surface; everything
                        // deeper is solid dark fill (_185). Repeating _186 reads as a
                        // broken colonnade.
                        else if (!IsSolid(col, row - 2)) tile = floorMid;
                        else tile = floorDeep;

                        tilemap.SetTile(new Vector3Int(col, cellY, 0), tile);
                        tileCount++;
                        continue;
                    }

                    if (c == '=')
                    {
                        // one-way lip: thin brick-top tile, visually distinct from solid strips
                        oneWayMap.SetTile(new Vector3Int(col, cellY, 0), floorTop);
                        tileCount++;
                        continue;
                    }

                    if (c == 'G')
                    {
                        if (!gateColumns.TryGetValue(col, out var rows)) gateColumns[col] = rows = new List<int>();
                        rows.Add(row);
                        continue;
                    }

                    Vector3 worldPos = new Vector3(col + 0.5f, cellY + 0.5f, 0f);

                    if (c == 'A')
                    {
                        var altarGo = new GameObject("ShiftAltar");
                        altarGo.transform.SetParent(root.transform);
                        altarGo.transform.position = worldPos;
                        altarGo.layer = InteractableLayer;
                        var asr = altarGo.AddComponent<SpriteRenderer>();
                        asr.sprite = altarSprite;
                        asr.sortingOrder = 2;
                        var trigger = altarGo.AddComponent<BoxCollider2D>();
                        trigger.isTrigger = true;
                        trigger.size = new Vector2(1.2f, 1.7f);
                        trigger.offset = new Vector2(0f, 0.55f);
                        altars.Add(altarGo.AddComponent<ShiftAltar>());
                        GroundToSurface(altarGo, cellY);
                        entityCounts.TryGetValue("ShiftAltar", out int na);
                        entityCounts["ShiftAltar"] = na + 1;
                        continue;
                    }

                    if (c == 'S')
                    {
                        var spawn = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab);
                        spawn.name = "GirisNoktasi"; // LevelManager finds it by this exact name
                        spawn.transform.SetParent(root.transform); // must be a DIRECT child of the room root
                        spawn.transform.position = worldPos;
                        GroundToSurface(spawn, cellY); // player pivot is at the feet; spawn at floor level
                        continue;
                    }

                    if (prefabCache.TryGetValue(c, out GameObject prefab))
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        go.transform.SetParent(root.transform);
                        go.transform.position = worldPos;
                        if (GroundedMarkers.Contains(c))
                            GroundToSurface(go, cellY);
                        if (c == 'L')
                        {
                            var lever = go.GetComponent<Lever>();
                            if (lever != null) levers.Add(lever);
                            else warnings.Add("Lever prefab has no Lever component?");
                        }
                        entityCounts.TryGetValue(prefab.name, out int n);
                        entityCounts[prefab.name] = n + 1;
                    }
                }
            }

            // ---------- Gates: vertical runs of 'G' become sliding portcullises ----------
            var gates = new List<Gate>();
            foreach (var kv in gateColumns)
            {
                int col = kv.Key;
                var rows = kv.Value;
                rows.Sort();
                int runStart = 0;
                for (int i = 1; i <= rows.Count; i++)
                {
                    if (i < rows.Count && rows[i] == rows[i - 1] + 1) continue;
                    int rowTop = rows[runStart], rowBottom = rows[i - 1];
                    float h = rowBottom - rowTop + 1;
                    float bottomY = height - 1 - rowBottom; // cellY of the lowest gate cell

                    var gateGo = new GameObject("Gate");
                    gateGo.transform.SetParent(root.transform);
                    gateGo.transform.position = new Vector3(col + 0.5f, bottomY + h / 2f, 0f);
                    gateGo.layer = GroundLayer; // solid: blocks the player while closed
                    var solid = gateGo.AddComponent<BoxCollider2D>();
                    solid.size = new Vector2(1f, h);

                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(gateGo.transform, false);
                    var vsr = visual.AddComponent<SpriteRenderer>();
                    vsr.sprite = gateSprite;
                    vsr.sortingOrder = 2;
                    float spriteH = gateSprite.bounds.size.y;
                    if (spriteH > 0.01f)
                    {
                        float scale = h / spriteH;
                        visual.transform.localScale = Vector3.one * scale;
                        // Cainos props pivot at their BASE — recenter so the sprite's
                        // visual middle sits on the gate's (collider) middle.
                        visual.transform.localPosition = -(Vector3)gateSprite.bounds.center * scale;
                    }

                    var gate = gateGo.AddComponent<Gate>();
                    gate.openOffset = new Vector2(0f, -h); // sinks fully into the floor
                    gates.Add(gate);
                    runStart = i;
                }
            }
            if (gates.Count > 0) entityCounts["Gate"] = gates.Count;

            // ---------- Auto-wiring: levers and altars drive their NEAREST gate ----------
            Gate Nearest(Vector3 from)
            {
                Gate best = null; float bestD = float.MaxValue;
                foreach (var g2 in gates)
                {
                    float d = (g2.transform.position - from).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = g2; }
                }
                return best;
            }
            foreach (var lever in levers)
            {
                var g2 = Nearest(lever.transform.position);
                if (g2 == null) { warnings.Add("Lever placed but no 'G' gate to wire it to."); continue; }
                UnityEventTools.AddPersistentListener(lever.OnFlippedOn, g2.Open);
                UnityEventTools.AddPersistentListener(lever.OnFlippedOff, g2.Close);
            }
            foreach (var altar in altars)
            {
                var g2 = Nearest(altar.transform.position);
                if (g2 == null) { warnings.Add("Shift Altar placed but no 'G' gate to wire it to."); continue; }
                UnityEventTools.AddPersistentListener(altar.OnPaid, g2.Open);
                altar.signalTarget = g2.transform; // the signal orb flies here on payment
            }

            // Camera zone: one BoxCollider2D covering the whole grid (with margin).
            var camBounds = (GameObject)PrefabUtility.InstantiatePrefab(camBoundsPrefab);
            camBounds.name = "CameraBounds"; // LevelManager finds it by this exact name
            camBounds.transform.SetParent(root.transform);
            camBounds.transform.position = new Vector3(width / 2f, height / 2f, 0f);
            var zone = camBounds.GetComponent<BoxCollider2D>();
            if (zone != null)
            {
                zone.offset = Vector2.zero;
                zone.size = new Vector2(Mathf.Max(width + 4f, 32f), Mathf.Max(height + 4f, 20f));
            }
            else
            {
                warnings.Add("CameraBounds prefab has no BoxCollider2D on its root — zone not resized.");
            }

            // ---------- Save ----------
            // Unity refuses to save a prefab whose hierarchy contains missing
            // scripts (this is how the dead AeroBat.prefab broke GenLevel1's
            // import). Detect it up front and name the culprit.
            var withMissing = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    withMissing.Add(t.name);
            }
            if (withMissing.Count > 0)
                throw new Exception(
                    "These objects contain MISSING SCRIPTS, and Unity refuses to save a prefab containing them.\n" +
                    "Fix (or stop using) the source prefab(s) of:\n  " + string.Join("\n  ", withMissing));

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(OutputFolder));

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{levelName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, assetPath, out bool success);
            if (!success)
                throw new Exception("Unity failed to save the prefab (see Console).");

            // ---------- Report ----------
            var report = new StringBuilder();
            report.AppendLine($"Saved: {assetPath}");
            report.AppendLine($"Size: {width} x {height} cells, {tileCount} ground tiles");
            foreach (var kv in entityCounts)
                report.AppendLine($"  {kv.Value} x {kv.Key}");
            if (warnings.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Warnings:");
                foreach (string w in warnings)
                    report.AppendLine("  - " + w);
            }
            report.AppendLine();
            report.AppendLine("Remember: add the prefab to LevelManager's Room Prefabs list to put it in the run.");

            Debug.Log("[LevelTextImporter] " + report);
            EditorUtility.DisplayDialog("Level imported", report.ToString(), "OK");
            return assetPath;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // Shift an instantiated object so the bottom of its visual bounds sits
    // exactly at surfaceY. Uses renderers (ignoring particles/trails), falling
    // back to 2D colliders. Prefab pivots vary wildly; measuring beats guessing.
    private static void GroundToSurface(GameObject go, float surfaceY)
    {
        Bounds? b = null;
        void Add(Bounds nb)
        {
            if (b == null) { b = nb; return; }
            var bb = b.Value; bb.Encapsulate(nb); b = bb;
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || !r.enabled) continue;
            Add(r.bounds);
        }
        if (b == null)
        {
            foreach (var c in go.GetComponentsInChildren<Collider2D>())
                Add(c.bounds);
        }
        if (b == null) return;

        float dy = surfaceY - b.Value.min.y;
        go.transform.position += new Vector3(0f, dy, 0f);
    }

    private static Sprite LoadPropSprite(string spriteName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PropsTexturePath))
            if (o is Sprite s && s.name == spriteName)
                return s;
        return null;
    }

    private static TileBase LoadTile(string path)
    {
        var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (tile == null)
            throw new Exception($"Tile asset not found: {path}");
        return tile;
    }

    private static TileBase[] LoadTileSet(string[] paths)
    {
        var tiles = new TileBase[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            tiles[i] = LoadTile(paths[i]);
        return tiles;
    }
}

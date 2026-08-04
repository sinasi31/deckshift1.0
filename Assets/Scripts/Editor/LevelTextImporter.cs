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

    // Summary of the most recent Build(), shown by the interactive menu path only.
    private static string lastReport = "";

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
        { 'C', "Assets/YeniLeveller/RelicChest.prefab" },   // renamed from Chest.prefab now that card chests exist
        // Card chest — the ONLY source of cards in a level now that the between-rooms reward screen
        // is gone. Counts as loot exactly like 'C' does, so mind the per-tier chest budget.
        { 'D', "Assets/YeniLeveller/CardChest.prefab" },
        // mechanics (added for GenLevel3):
        // Village elevator (designer's pick 2026-08-07) — reads better than the dungeon one.
        { 'E', "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/PF Village Props - Elevator.prefab" }, // moving platform; tune travel in Inspector
        { 'F', "Assets/Prefabs/UpdraftFan.prefab" },        // updraft zone ~3 tall, liftForce 20 (~5-7 tiles of lift)
        { 'w', "Assets/Prefabs/AcidWater.prefab" },         // acid pool ~6 wide; damages + slows
        { 'K', "Assets/Prefabs/WreckingBall.prefab" },      // swinging hazard; placed at cell center, tune anchor
        { 'c', "Assets/Prefabs/CrumblingPlatform.prefab" }, // platform that crumbles underfoot
        { 't', "Assets/Prefabs/Taret.prefab" },             // stationary turret enemy
        { '$', "Assets/YeniLeveller/Shopkeeper_NPC.prefab" }, // shop NPC (its 'missing' scripts are TMP/UI package scripts — fine)
        { 'B', "Assets/Prefabs/Blompo.prefab" },              // Blompo — card-blessing NPC; a way to SPEND loot, so he counts as loot
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
        'X', 'm', 'r', 'l', 'M', 'z', 'Z', 's', 'C', 'D', '^', 'W', 'T', 'F', 'w', 'c', 't', '$', 'B', 'L',
    };

    // ---- Tile roles ---------------------------------------------------------------------------
    //
    // MEASURED, NOT GUESSED (2026-08-07). Every set below is the real frequency distribution from
    // the six hand-made rooms — efeslevel2/3, EfeVrl4/5/6/7 — counted by classifying each painted
    // cell by its neighbours. Entries are REPEATED to weight them, so the deterministic picker
    // reproduces roughly the same mix the designer paints by hand.
    //
    // WHY THIS WAS REWRITTEN. The previous table was a guess and it was wrong in three ways that
    // together made generated rooms read as flat grey wallpaper:
    //
    //   1. ⚠️ PLATFORMS WERE PAINTED WITH CEILING TILES. Extra_112/113/114 were used as the
    //      free-standing platform strip set. In the hand-made rooms those three tiles appear 18
    //      times each and are CEILING in 100% of cases — never once a platform. Every floating
    //      ledge in a generated room was wearing ceiling trim, complete with the toothy underside.
    //
    //   2. ⚠️ PLATFORMS COME FROM A DIFFERENT PALETTE ENTIRELY. The designer paints mid-air
    //      platforms from the Cainos "TP Dungeon Ground" palette (Dirt_* and Ground_*), NOT from
    //      the biseyler "Ground Extra_*" sheet that the masses use. Note Ground_* exists ONLY in
    //      the Cainos folder. This is the single biggest visual difference.
    //
    //   3. ⚠️ LEFT AND RIGHT WALL FACES WERE INVERTED. Extra_156 was used for "air to the right";
    //      it is the designer's most common tile for "air to the LEFT" (57 uses).
    //
    // Also: the hand-made rooms use 46-58 DISTINCT ground tiles each. Picking one fixed tile per
    // role is most of why generated rooms look like wallpaper, hence weighted sets everywhere.
    private const string TileDir = "Assets/LevelSinasi/biseyler/";
    private const string CainosDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Tileset Pallete/TP Dungeon Ground/";

    // Darkened copies of the interior tiles, used for rock more than 2 cells from air.
    // Generated by Deckshift -> Generate Tile Variants; see the note at the call site.
    // Indexed by recession step: [0] = depth 3, [1] = depth 4, [2] = depth 5+.
    private static readonly string[][] DeepFillTiles =
    {
        new[] { "Ground Extra_153 Deep1", "Ground Extra_185 Deep1", "Ground Extra_101 Deep1", "Ground Extra_49 Deep1" },
        new[] { "Ground Extra_153 Deep2", "Ground Extra_185 Deep2", "Ground Extra_101 Deep2", "Ground Extra_49 Deep2" },
        new[] { "Ground Extra_153 Deep3", "Ground Extra_185 Deep3", "Ground Extra_101 Deep3", "Ground Extra_49 Deep3" },
    };

    // Every tile name the importer can paint. TileVariantGenerator uses this to build a
    // Grid-collision copy of each — see the note on Resolve.
    public static IEnumerable<string> AllPaintedTileNames()
    {
        var seen = new HashSet<string>();
        foreach (var kv in MaskTiles) if (seen.Add(kv.Value)) yield return kv.Value;
        foreach (string n in Mask4Tiles) if (seen.Add(n)) yield return n;
        foreach (string p in PlatformSingleTiles) { string n = ShortNameOf(p); if (seen.Add(n)) yield return n; }
    }

    // "…/TX Tileset - Dungeon Ground Dirt_0.asset" -> "Ground Dirt_0"
    private static string ShortNameOf(string assetPath) =>
        Path.GetFileNameWithoutExtension(assetPath).Replace("TX Tileset - Dungeon ", "");

    private static string Bis(string n) => TileDir + "TX Tileset - Dungeon Ground Extra_" + n + ".asset";
    private static string Dirt(string n) => CainosDir + "TX Tileset - Dungeon Ground Dirt_" + n + ".asset";
    private static string Grnd(string n) => CainosDir + "TX Tileset - Dungeon Ground_" + n + ".asset";

    // ---- neighbour-mask tile selection ---------------------------------------------------------
    //
    // ⚠️ THIS TILESET IS DIRECTIONAL, NOT A BAG OF TEXTURES. Each tile means a POSITION — top edge,
    // inner corner, west face, ceiling underside. Picking one at random from a role bucket scatters
    // edge and corner art through the middle of a solid mass, which is exactly what made the walls
    // read as noise and made neighbouring platform cells fail to line up ("platforms inside one
    // another" — their surfaces are drawn at different heights, so mixing tiles along a run breaks
    // the silhouette).
    //
    // There are no Rule Tiles in this project (1225 plain Tile assets, zero rule-based), so nothing
    // does this automatically. The table below IS the auto-tiling: it was measured by classifying
    // every painted cell in the six hand-made rooms by its 8-neighbour configuration and taking the
    // tile the designer used most for that configuration. 55 configurations, 3482 cells.
    //
    // Bits: N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128, set when that neighbour is SOLID.
    //
    // THE RULE THAT MATTERS: one tile per configuration, deterministically. Variety is safe only on
    // ISOLATED tiles (mask 0), which have no neighbours to disagree with — see PlatformSingleTiles.
    // ⚠️ ONLY CONFIGURATIONS WITH n >= 10 SAMPLES ARE LISTED HERE (trimmed 2026-08-08).
    //
    // The table originally kept all 55 measured configurations, including ones decided by a
    // handful of cells. Those were not measurements, they were coin flips — and the coin flips are
    // exactly the OUTER-CORNER configurations, because a room has hundreds of buried and wall-face
    // cells and only a few of any given corner. The visible symptom the designer reported: mask 193
    // (a wall's bottom-right outer corner) resolved to "Ground Extra_205" on **2 votes out of 8**,
    // and Extra_205 is a brown interior-looking block, so every wall in every generated room had a
    // brown nub sticking out of that corner.
    //
    // Judge these entries by SAMPLE COUNT, never by winner share. Share is low almost everywhere
    // (mask 255 has n=1231 and its winner takes only 12%) because the designer deliberately varies
    // tiles across a mass — that is variety, not uncertainty, and trimming on share would gut the
    // table. Low n is the only honest signal of "we don't actually know".
    //
    // Dropped configurations now fall through to Mask4Tiles, which is hand-written and internally
    // consistent. Mask 193 collapses to cardinals N+W -> "Ground Extra_114" — corroborated by mask
    // 195 (the same corner plus one diagonal), which has n=27 and picks Extra_114 seventeen times.
    private static readonly Dictionary<int, string> MaskTiles = new Dictionary<int, string>
    {
        {14,"Ground Dirt_11"}, {16,"Ground_11"}, {28,"Ground Dirt_13"},
        {31,"Ground Extra_156"}, {60,"Ground Extra_148"}, {63,"Ground Extra_188"},
        {68,"Ground_11"}, {95,"Ground Dirt_11"}, {112,"Ground Dirt_13"},
        {120,"Ground Extra_146"}, {124,"Ground Extra_162"}, {125,"Ground Extra_144"},
        {126,"Ground Extra_162"}, {127,"Ground Extra_164"}, {135,"Ground Extra_112"},
        {159,"Ground Extra_172"}, {195,"Ground Extra_114"}, {199,"Ground Extra_96"},
        {207,"Ground Extra_98"}, {223,"Ground Extra_96"}, {224,"Ground Dirt_11"},
        {231,"Ground Extra_96"}, {241,"Ground Extra_154"}, {243,"Ground Extra_138"},
        {245,"Ground Extra_186"}, {247,"Ground Extra_98"}, {249,"Ground Extra_170"},
        {252,"Ground Extra_162"}, {253,"Ground Extra_162"}, {255,"Ground Extra_153"},
    };

    // Fallback when an 8-bit configuration wasn't seen in the hand-made rooms: collapse to the four
    // cardinals only. Complete for all 16, so selection can never fall through to nothing.
    private static readonly string[] Mask4Tiles =
    {
        /* 0 ----  */ "Ground Dirt_0",     /* 1  N--- */ "Ground Dirt_4",
        /* 2 -E--  */ "Ground_11",         /* 3  NE-- */ "Ground Extra_112",
        /* 4 --S-  */ "Ground_11",         /* 5  N-S- */ "Ground_11",
        /* 6 -ES-  */ "Ground_11",         /* 7  NES- */ "Ground Extra_156",   // air to the WEST
        /* 8 ---W  */ "Ground_11",         /* 9  N--W */ "Ground Extra_114",
        /*10 -E-W  */ "Ground_11",         /*11  NE-W */ "Ground Extra_96",    // air BELOW: ceiling
        /*12 --SW  */ "Ground_11",         /*13  N-SW */ "Ground Extra_154",   // air to the EAST
        /*14 -ESW  */ "Ground Extra_162",  /*15  NESW */ "Ground Extra_153",   // air ABOVE: surface
    };

    // LONE free-standing block — a single stepping stone with air on all four sides (mask 0). This
    // is the dominant mid-air idiom: 104 of the 217 platform cells in the hand-made rooms are
    // 1-wide singles. Variety is SAFE here precisely because an isolated tile has no neighbour to
    // misalign with.
    private static readonly string[] PlatformSingleTiles =
    {
        Dirt("0"), Dirt("0"), Dirt("0"), Dirt("0"),
        Dirt("14"), Dirt("14"), Dirt("14"), Dirt("14"),
        Dirt("4"), Dirt("4"), Dirt("4"),
        Grnd("3"), Grnd("3"),
        Grnd("11"), Grnd("11"),
        Dirt("12"), Dirt("3"), Grnd("1"), Grnd("0"),
    };

    // Platform RUNS of 2+ cells, and wall-attached shelves. Overwhelmingly Ground_13 repeated
    // (50 of 113), which is why runs read as one continuous ledge rather than cap/middle/cap.
    private static readonly string[] PlatformRunTiles =
    {
        Grnd("13"), Grnd("13"), Grnd("13"), Grnd("13"), Grnd("13"), Grnd("13"), Grnd("13"), Grnd("13"),
        Dirt("11"), Dirt("11"),
        Grnd("11"), Dirt("14"), Grnd("10"),
    };

    // Walkable top of a thick mass. Extra_162 is the designer's primary (147 uses); the old table
    // led with Extra_144, which is only their third choice.
    private static readonly string[] SurfaceTiles =
    {
        Bis("162"), Bis("162"), Bis("162"), Bis("162"), Bis("162"), Bis("162"),
        Bis("101"), Bis("101"), Bis("101"),
        Bis("144"), Bis("144"), Bis("144"),
        Grnd("13"), Grnd("13"),
        Bis("154"), Bis("154"),
        Bis("166"), Bis("166"),
        Bis("97"), Bis("49"),
    };

    // Underside of an overhang.
    private static readonly string[] CeilingTiles =
    {
        Bis("96"), Bis("96"), Bis("96"), Bis("96"),
        Bis("189"), Bis("189"), Bis("97"), Bis("97"),
        Bis("3"), Bis("3"), Bis("186"), Bis("186"),
        Bis("98"), Bis("98"), Bis("160"), Bis("160"), Bis("49"),
    };

    // Vertical face with open air to the LEFT (a west-facing wall).
    private static readonly string[] FaceWestTiles =
    {
        Bis("156"), Bis("156"), Bis("156"), Bis("156"),
        Bis("172"), Bis("172"), Bis("172"),
        Bis("188"), Bis("188"), Bis("137"), Bis("137"),
        Bis("153"), Bis("140"),
    };

    // Vertical face with open air to the RIGHT (an east-facing wall).
    private static readonly string[] FaceEastTiles =
    {
        Bis("154"), Bis("154"), Bis("154"),
        Bis("170"), Bis("170"), Bis("170"),
        Bis("157"), Bis("157"), Bis("173"), Bis("173"),
        Bis("189"), Bis("189"), Bis("188"), Bis("186"),
    };

    // Buried rock with solid neighbours all round — the bulk of any thick mass.
    private static readonly string[] InteriorTiles =
    {
        Bis("153"), Bis("153"), Bis("153"), Bis("153"), Bis("153"),
        Bis("185"), Bis("185"), Bis("185"), Bis("185"),
        Bis("101"), Bis("101"), Bis("101"), Bis("101"),
        Bis("49"), Bis("49"), Bis("49"),
        Bis("157"), Bis("157"), Bis("157"),
        Bis("169"), Bis("169"), Bis("169"),
        Bis("97"), Bis("97"), Bis("189"), Bis("189"), Bis("173"), Bis("173"), Bis("100"),
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

    // Chebyshev distance from a solid cell to the nearest open one, capped at `cap`.
    // Used to leave deep interiors unpainted — see the note at the call site.
    private static int DepthFromAir(int col, int row, int cap, Func<int, int, bool> IsSolid)
    {
        for (int d = 1; d <= cap; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dy = -d; dy <= d; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != d) continue;
                    if (!IsSolid(col + dx, row + dy)) return d;
                }
        return cap;
    }

    // Scales and positions an acid pool so it exactly fills the hole it was placed in.
    //
    // The pit is measured from the GRID, not from the prefab: walk left/right along the marker's
    // row while the cells are open, and down while they are open, to get the hole's true extent.
    // The pool is then scaled to that size and seated so its TOP sits flush with the pit's rim
    // rather than floating at a cell centre.
    private static void FitAcidToPit(GameObject go, int col, int row, int cellY, int width, int height,
                                     Func<int, int, char> At, Func<int, int, bool> IsSolid)
    {
        int left = col;
        while (left - 1 >= 0 && !IsSolid(left - 1, row)) left--;
        int right = col;
        while (right + 1 < width && !IsSolid(right + 1, row)) right++;

        int bottom = row;
        while (bottom + 1 < height && !IsSolid(col, bottom + 1)) bottom++;

        // ⚠️ The pit's top is where its SIDE WALLS run out — NOT the first solid cell above.
        // A pit opens upward into the room, so walking up until something is solid measures the
        // whole chamber (9 tiles here instead of 2) and launches the pool into the air. The hole
        // only exists while there is rock beside it, so climb while either flank is still solid.
        int top = bottom;
        while (top - 1 >= 0
               && !IsSolid(col, top - 1)
               && (IsSolid(left - 1, top - 1) || IsSolid(right + 1, top - 1)))
            top--;

        float w = right - left + 1;
        float h = bottom - top + 1;
        if (w <= 0f || h <= 0f) return;

        var box = go.GetComponentInChildren<BoxCollider2D>();
        Vector2 native = box != null ? box.size : new Vector2(5.98f, 2.53f);
        if (native.x <= 0.01f || native.y <= 0.01f) return;

        // ⚠️ The pool is NOT centred on its own transform. AcidWater's BoxCollider2D carries an
        // offset of (0, 1.27), so positioning the transform at the pit centre floats the water a
        // scaled 1.27 units too high - half the pool ends up hanging above the floor it should be
        // sunk into. Back the offset out, scaled, or the fit is silently off by it.
        Vector2 nativeOffset = box != null ? box.offset : Vector2.zero;

        Vector3 scale = new Vector3(w / native.x, h / native.y, 1f);
        go.transform.localScale = scale;

        // Grid rows run DOWNWARD while world Y runs up, so a row above the marker is (row - top)
        // cells higher. The rim is the top edge of that cell; the pool centres half its height below.
        float rimY = cellY + (row - top) + 1f;
        go.transform.position = new Vector3(left + w * 0.5f - nativeOffset.x * scale.x,
                                            rimY - h * 0.5f - nativeOffset.y * scale.y,
                                            0f);
    }

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
            // Only the interactive menu path shows the modal summary — see the note in Build().
            EditorUtility.DisplayDialog("Level imported", lastReport, "OK");
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
        TileBase[] platSingle = LoadTileSet(PlatformSingleTiles);
        TileBase[] backWall = LoadTileSet(BackWallTiles);

        // Resolve a measured tile NAME to an asset. Extra_* live in the biseyler copies; the
        // Ground_*/Dirt_* platform tiles exist ONLY in the Cainos palette folder, so try both.
        var tileByName = new Dictionary<string, TileBase>();
        Func<string, TileBase> Resolve = shortName =>
        {
            TileBase cached;
            if (tileByName.TryGetValue(shortName, out cached)) return cached;
            string file = "TX Tileset - Dungeon " + shortName + ".asset";
            // ⚠️ PREFER THE " Solid" VARIANT. The pack's tiles use colliderType = Sprite, so
            // collision traces the sprite's ALPHA OUTLINE — including the little protruding brick
            // nubs on the wall-face tiles. The player catches on them, and the collision gizmo
            // shows a bumpy edge instead of a clean wall. The Solid variants are identical art
            // with colliderType = Grid, which is what a solid cell in a platformer wants.
            string solid = "TX Tileset - Dungeon " + shortName + " Solid.asset";
            TileBase tb = AssetDatabase.LoadAssetAtPath<TileBase>(TileVariantGenerator.VariantFolder + "/" + solid)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(TileVariantGenerator.VariantFolder + "/" + file)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(TileDir + file)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(CainosDir + file);
            tileByName[shortName] = tb;
            return tb;
        };

        // Warm the cache and fail loudly rather than silently painting holes.
        //
        // ⚠️ THE SPRITE CHECK IS THE IMPORTANT HALF. `TX Tileset - Dungeon Ground_13` exists as an
        // asset but ships with a NULL SPRITE — the pack's valid range stops at Ground_12, so it is
        // one entry past the end. It was the tile used for every platform run, so 30 of 499 cells
        // in a generated room were painted with nothing and the ledges were literally invisible;
        // the room looked like it had no mid-air platforms at all because it didn't. A
        // resolve-only check passes that happily, so both conditions are enforced here.
        var broken = new List<string>();
        Action<string> Check = n =>
        {
            TileBase tb = Resolve(n);
            if (tb == null) { broken.Add(n + "  (asset missing)"); return; }
            Tile asTile = tb as Tile;
            if (asTile != null && asTile.sprite == null) broken.Add(n + "  (SPRITE IS NULL — would paint an invisible cell)");
        };
        foreach (var kv in MaskTiles) Check(kv.Value);
        foreach (string n in Mask4Tiles) Check(n);
        foreach (string n in PlatformSingleTiles) Check(ShortNameOf(n));
        if (broken.Count > 0)
            throw new Exception("LevelTextImporter: unusable tiles in the mask table:\n  "
                                + string.Join("\n  ", broken.ToArray()));

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

                        // ---- DEEP INTERIORS ARE NOT PAINTED --------------------------------
                        //
                        // Measured across the six hand-made rooms: 2355 solid cells sit 1 away
                        // from open air, 1074 at 2, 134 at 3, 23 at 4, and NOTHING deeper. The
                        // designer never builds a deep solid interior, so this tileset has never
                        // had to be a fill texture — it is a FACING set, all edges and near-edge
                        // detail. Painted across a 10-deep mass it reads as ugly repeating
                        // wallpaper, which is exactly what generated rooms were doing.
                        //
                        // ⚠️ An earlier version left these cells UNPAINTED so the backdrop showed
                        // through. That was worse: solid rock then reads as open background, which
                        // misleads the player while moving and while peeking with Ctrl. Deep rock
                        // must still look SOLID — just recessed.
                        //
                        // So deep cells are painted with DARKENED COPIES of the interior tiles
                        // (TileVariantGenerator). Same art, same collision, pushed dark and
                        // slightly cool so a mass reads as receding shadow instead of another lit
                        // surface — and the brick detail stays faintly legible rather than going
                        // to flat black.
                        // ⚠️ DO NOT JITTER THIS THRESHOLD. A hashed -1/0/+1 nudge was added on
                        // 2026-08-08 to break up the Chebyshev metric's rectangular contours, and
                        // the designer rejected it — the interiors read better with the hard step.
                        // It also let a +1 push DEEP tiles out to depth 2, one cell from the face,
                        // which is exactly the dark block sticking out of a wall edge that the
                        // change was supposed to help. Deep fill starts at a hard depth of 3.
                        int depth = DepthFromAir(col, row, 5, IsSolid);
                        if (depth >= 3)
                        {
                            string[] step = DeepFillTiles[Mathf.Min(depth - 3, DeepFillTiles.Length - 1)];
                            TileBase deep = Resolve(step[Mathf.Abs((col * 73856093) ^ (cellY * 19349663)) % step.Length]);
                            if (deep != null)
                            {
                                tilemap.SetTile(new Vector3Int(col, cellY, 0), deep);
                                tileCount++;
                                continue;
                            }
                        }

                        // Tile chosen by the cell's 8-neighbour configuration — this IS the
                        // auto-tiling (see MaskTiles). One tile per configuration, deterministic,
                        // so edges land on edges and a run of platform cells shares one silhouette.
                        int mask = 0;
                        if (IsSolid(col, row - 1)) mask |= 1;     // N
                        if (IsSolid(col + 1, row - 1)) mask |= 2; // NE
                        if (IsSolid(col + 1, row)) mask |= 4;     // E
                        if (IsSolid(col + 1, row + 1)) mask |= 8; // SE
                        if (IsSolid(col, row + 1)) mask |= 16;    // S
                        if (IsSolid(col - 1, row + 1)) mask |= 32;// SW
                        if (IsSolid(col - 1, row)) mask |= 64;    // W
                        if (IsSolid(col - 1, row - 1)) mask |= 128;// NW

                        TileBase tile;
                        if (mask == 0)
                        {
                            // Isolated stepping stone. The ONLY place variety is safe, because
                            // there is no neighbour for it to disagree with.
                            tile = Pick(platSingle, col, cellY);
                        }
                        else
                        {
                            string name;
                            if (!MaskTiles.TryGetValue(mask, out name))
                            {
                                // Configuration never seen in the hand-made rooms: collapse to the
                                // four cardinals, which is complete for all 16 cases.
                                int m4 = ((mask & 1) != 0 ? 1 : 0) | ((mask & 4) != 0 ? 2 : 0)
                                       | ((mask & 16) != 0 ? 4 : 0) | ((mask & 64) != 0 ? 8 : 0);
                                name = Mask4Tiles[m4];
                            }
                            tile = Resolve(name);
                        }

                        tilemap.SetTile(new Vector3Int(col, cellY, 0), tile);
                        tileCount++;
                        continue;
                    }

                    if (c == '=')
                    {
                        // one-way lip: thin brick-top tile, visually distinct from solid strips.
                        // (Level Design Law 4 bans these in new rooms; kept so old texts import.)
                        oneWayMap.SetTile(new Vector3Int(col, cellY, 0), Resolve("Ground Extra_162"));
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

                        // Acid pools must FIT THEIR PIT, not sit at a cell centre at a fixed size.
                        //
                        // AcidWater is a fixed ~5.98 x 2.53 box whose water is a shader/mesh with NO
                        // SpriteRenderer, so the usual auto-grounding (which measures renderer
                        // bounds) measures nothing and leaves the pool floating, overflowing the
                        // hole it is supposed to be filling. Measure the actual hole from the grid
                        // and scale to it instead.
                        if (c == 'w')
                        {
                            FitAcidToPit(go, col, row, cellY, width, height, At, IsSolid);
                        }
                        else if (GroundedMarkers.Contains(c))
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

            // Decoration pass. Hand-built rooms carry 55-124 props each — 70-86% of everything
            // in them (census in LevelDesignRules.md 2b). A room that ships with bare tiles
            // gets judged naked against them, which is exactly what happened.
            DressRoom(root, At, IsSolid, width, height, levelName, entityCounts, warnings);

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

            // NOTE: the summary dialog belongs to the MENU path only (see ImportFromText).
            // EditorUtility.DisplayDialog is modal and blocks Unity's main thread until someone
            // clicks OK, which makes Build() impossible to call from a script — it hangs the
            // editor, and any batch import or automated re-import with it.
            Debug.Log("[LevelTextImporter] " + report);
            lastReport = report.ToString();
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

    // ================= Decoration pass =================
    //
    // Dresses a freshly built room the way the hand-built ones are dressed. Measured from
    // efeslevel1-3 and EfeVrl4-6 (Tools/LevelLab -- objects): ~68 props per room, split
    // 44% small floor clutter / 23% large floor furniture / 24% wall decoration /
    // 6% ceiling hangings / 3% wall dirt, drawn from a wide variety rather than repeats.
    //
    // Safe by construction: every Cainos Dungeon Props prefab is a pure visual with NO
    // collider, so nothing placed here can change what the player can reach. Anything that
    // IS functional (platforms, ladders, traps, gates, chests, doors) is blocklisted.

    private const string PropsFolder = "Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Props";
    private const string WallDecoFolder = "Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Wall Deco";
    private const string WallDirtFolder = "Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Wall Dirt";

    // Ground tilemap renders at GroundSortingOrder (1); the player's body is at 1000.
    // Sitting decoration at 2 keeps it above the tiles and always behind the player.
    private const int DecorSortingOrder = 2;

    // One prop per ~18 cells reproduces the hand-built density (~68 in a 48x26 room).
    private const float CellsPerProp = 18f;

    private static readonly string[] CeilingKeywords =
        { "Chandelier", "Ceiling Chain", "Lamp", "Hanger", "Cage", "Manacle" };
    private static readonly string[] WallKeywords =
        { "Painting", "Window", "Banner", "Wall Altar", "Torch", "Wall Cave", "Shelf Side", "Key Holder", "Rack" };
    private static readonly string[] FurnitureKeywords =
        { "Table", "Chair", "Bench", "Stool", "Bed", "Bookshelf", "Cupboard", "Cabinet", "Shelf",
          "Barrel", "Crate", "Coffin", "Statue", "Stove", "Fireplace", "Chimney", "Pillar", "Beam",
          "Caudron", "Lectern", "Pulpit" };
    private static readonly string[] ClutterKeywords =
        { "Book", "Bottle", "Pot", "Jar", "Bag", "Basket", "Bone", "Skull", "Coin Pile", "Debris",
          "Fry Pan", "Kettle", "Bowl", "Bucket", "Cup", "Candle", "Silver", "Rotten Food",
          "Package", "Knife", "Arrow", "Chopping Board", "Bookend" };

    // Functional or gameplay objects that happen to live in the same folder. Never auto-place.
    private static readonly string[] DecorBlocklist =
        { "Platform", "Ladder", "Stairs", "Trapdoor", "Switch", "Gate", "Elevator",
          "Spike", "Trap", "Chest", "Door", "Fence", "Stage", "Toilet", "Sword", "Spear" };

    private static bool MatchesAny(string name, string[] keys)
    {
        foreach (string k in keys)
            if (name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static List<GameObject> LoadPalette(string folder, string[] keywords)
    {
        var list = new List<GameObject>();
        if (!AssetDatabase.IsValidFolder(folder)) return list;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (MatchesAny(name, DecorBlocklist)) continue;
            if (keywords != null && !MatchesAny(name, keywords)) continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }
        return list;
    }

    /// <summary>Deterministic per level name, so re-importing a level gives the same dressing.</summary>
    private static int StableSeed(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void DressRoom(GameObject root, Func<int, int, char> at, Func<int, int, bool> isSolid,
                                  int width, int height, string levelName,
                                  Dictionary<string, int> entityCounts, List<string> warnings)
    {
        var clutter = LoadPalette(PropsFolder, ClutterKeywords);
        var furniture = LoadPalette(PropsFolder, FurnitureKeywords);
        var wallDecor = LoadPalette(PropsFolder, WallKeywords);
        wallDecor.AddRange(LoadPalette(WallDecoFolder, null));
        var ceiling = LoadPalette(PropsFolder, CeilingKeywords);
        var dirt = LoadPalette(WallDirtFolder, null);

        if (clutter.Count == 0 && furniture.Count == 0)
        {
            warnings.Add("Decoration pass found no props — is the Cainos Dungeon pack still at "
                         + PropsFolder + "? Room saved undressed.");
            return;
        }

        // Keep the spawn and the exit clear — nobody wants to wake up inside a wardrobe.
        var keepClear = new List<Vector2Int>();
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                if (at(col, row) == 'S' || at(col, row) == 'X')
                    keepClear.Add(new Vector2Int(col, row));

        bool TooCloseToDoorway(int col, int row)
        {
            foreach (var k in keepClear)
                if (Mathf.Abs(k.x - col) <= 2 && Mathf.Abs(k.y - row) <= 1) return true;
            return false;
        }

        // ---- collect candidate spots ----
        var floorSpots = new List<Vector2Int>();
        var wallSpots = new List<Vector2Int>();
        var ceilSpots = new List<Vector2Int>();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (isSolid(col, row)) continue;
                if (at(col, row) != '.' && at(col, row) != ' ') continue;   // a marker lives here
                if (TooCloseToDoorway(col, row)) continue;

                bool floorBelow = isSolid(col, row + 1);
                bool airAbove = !isSolid(col, row - 1);
                bool solidLeft = isSolid(col - 1, row);
                bool solidRight = isSolid(col + 1, row);

                if (floorBelow && airAbove) floorSpots.Add(new Vector2Int(col, row));
                else if ((solidLeft || solidRight) && !floorBelow) wallSpots.Add(new Vector2Int(col, row));

                if (isSolid(col, row - 1) && !isSolid(col, row + 1) && !floorBelow)
                    ceilSpots.Add(new Vector2Int(col, row));
            }
        }

        var rng = new System.Random(StableSeed(levelName));
        Shuffle(floorSpots, rng);
        Shuffle(wallSpots, rng);
        Shuffle(ceilSpots, rng);

        int budget = Mathf.RoundToInt(width * height / CellsPerProp);
        int wantClutter = Mathf.RoundToInt(budget * 0.44f);
        int wantFurniture = Mathf.RoundToInt(budget * 0.23f);
        int wantWall = Mathf.RoundToInt(budget * 0.24f);
        int wantCeiling = Mathf.RoundToInt(budget * 0.06f);
        int wantDirt = Mathf.RoundToInt(budget * 0.03f);

        var decorRoot = new GameObject("Decoration");
        decorRoot.transform.SetParent(root.transform);
        decorRoot.transform.localPosition = Vector3.zero;

        int placed = 0;
        int floorCursor = 0, wallCursor = 0, ceilCursor = 0;

        void PlaceFloor(List<GameObject> palette, int count)
        {
            if (palette.Count == 0) return;
            for (int i = 0; i < count && floorCursor < floorSpots.Count; i++, floorCursor++)
            {
                var cell = floorSpots[floorCursor];
                int cellY = height - 1 - cell.y;
                var go = Spawn(palette[rng.Next(palette.Count)], decorRoot,
                               new Vector3(cell.x + 0.5f, cellY + 0.5f, 0f), rng.Next(2) == 0);
                GroundToSurface(go, cellY);
                placed++;
            }
        }

        PlaceFloor(clutter, wantClutter);
        PlaceFloor(furniture, wantFurniture);

        // Wall props hug the face they are attached to.
        for (int i = 0; i < wantWall && wallCursor < wallSpots.Count && wallDecor.Count > 0; i++, wallCursor++)
        {
            var cell = wallSpots[wallCursor];
            int cellY = height - 1 - cell.y;
            bool onLeft = isSolid(cell.x - 1, cell.y);
            float x = cell.x + (onLeft ? 0.25f : 0.75f);
            Spawn(wallDecor[rng.Next(wallDecor.Count)], decorRoot, new Vector3(x, cellY + 0.5f, 0f), !onLeft);
            placed++;
        }

        for (int i = 0; i < wantCeiling && ceilCursor < ceilSpots.Count && ceiling.Count > 0; i++, ceilCursor++)
        {
            var cell = ceilSpots[ceilCursor];
            int cellY = height - 1 - cell.y;
            Spawn(ceiling[rng.Next(ceiling.Count)], decorRoot, new Vector3(cell.x + 0.5f, cellY + 0.9f, 0f), false);
            placed++;
        }

        for (int i = 0; i < wantDirt && wallCursor < wallSpots.Count && dirt.Count > 0; i++, wallCursor++)
        {
            var cell = wallSpots[wallCursor];
            int cellY = height - 1 - cell.y;
            Spawn(dirt[rng.Next(dirt.Count)], decorRoot, new Vector3(cell.x + 0.5f, cellY + 0.5f, 0f), rng.Next(2) == 0);
            placed++;
        }

        entityCounts["(decoration props)"] = placed;

        if (placed < budget / 2)
            warnings.Add($"Decoration pass placed only {placed} of a target {budget} props — the room "
                         + "has few floor/wall faces to dress. Consider more small ledges and alcoves.");
    }

    private static GameObject Spawn(GameObject prefab, GameObject parent, Vector3 pos, bool flipX)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        if (flipX)
        {
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(-s.x, s.y, s.z);
        }
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder += DecorSortingOrder;
        return go;
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

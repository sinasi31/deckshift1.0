using LevelLab;

if (args.Length == 0)
{
    Console.WriteLine("""
        LevelLab — Deckshift level measuring / validation tools

          metrics                     Measure real jump & fall reach in tiles (live physics values)
          layers <level.prefab>       List the tilemap layers inside a level prefab
          extract <level.prefab> [out] Convert a hand-built level's solid tiles to an ASCII grid
          stats <file...>             Shape metrics for levels (.txt grids or .prefab rooms)
          check <level.txt>          Validate a level: reachability + hand-built style bands
        """);
    return 0;
}

switch (args[0].ToLowerInvariant())
{
    case "metrics": Metrics(); return 0;
    case "layers": return Layers(args);
    case "extract": return Extract(args);
    case "stats": return StatsCmd(args);
    case "check": return CheckCmd(args);
    case "objects": return ObjectsCmd(args);
    default:
        Console.Error.WriteLine($"unknown command: {args[0]}");
        return 1;
}

static int Layers(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: layers <level.prefab>"); return 1; }

    foreach (var layer in TilemapExtract.Layers(args[1]))
    {
        if (layer.Tiles.Count == 0) { Console.WriteLine($"{layer.Name,-22} empty"); continue; }
        int minX = layer.Tiles.Min(t => t.X), maxX = layer.Tiles.Max(t => t.X);
        int minY = layer.Tiles.Min(t => t.Y), maxY = layer.Tiles.Max(t => t.Y);
        Console.WriteLine($"{layer.Name,-22} {layer.Tiles.Count,6} tiles  "
                          + $"x[{minX}..{maxX}] y[{minY}..{maxY}]  "
                          + $"{(maxX - minX + 1)}x{(maxY - minY + 1)}  "
                          + (layer.HasCollider ? "SOLID" : "decor"));
    }
    return 0;
}

static int ObjectsCmd(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("usage: objects <AssetsRoot> <level.prefab...>"); return 1; }

    var guids = Objects.GuidIndex(args[1]);
    var grand = new Dictionary<string, int>();

    foreach (var level in args.Skip(2))
    {
        var placed = Objects.Placements(level, guids);
        Console.WriteLine($"## {Path.GetFileNameWithoutExtension(level)} — {placed.Count} placed objects");
        foreach (var grp in placed.GroupBy(p => p.Name).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {grp.Count(),4}x  {grp.Key}");
            grand[grp.Key] = grand.GetValueOrDefault(grp.Key) + grp.Count();
        }
        Console.WriteLine();
    }

    if (args.Length > 3)
    {
        Console.WriteLine("## TOTAL across all rooms");
        foreach (var kv in grand.OrderByDescending(k => k.Value))
            Console.WriteLine($"  {kv.Value,4}x  {kv.Key}");
    }
    return 0;
}

static int CheckCmd(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: check <level.txt> [--map]"); return 1; }

    var g = Grid.FromText(args[1]);
    bool showMap = args.Contains("--map");
    int problems = 0;

    Console.WriteLine($"# check: {g.Name}  ({g.Width}x{g.Height})");
    Console.WriteLine();

    // --- reachability -------------------------------------------------------------
    var spawn = Check.Find(g, 'S');
    var exit = Check.Find(g, 'X');
    HashSet<(int, int)> reach = null;

    // --auto lets an extracted hand-built room (which has no markers) be measured too:
    // start at the lowest-leftmost standing spot, aim for the lowest-rightmost.
    if (args.Contains("--auto"))
    {
        // Try every standing cell as a start and keep the one that opens up the most room.
        // This is how an extracted hand-built level (which carries no markers) gets measured,
        // and it is also the honest calibration target: a good room is mostly one big
        // connected space, wherever you drop the player.
        var stands = new List<(int X, int Y)>();
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (Check.IsStand(g, x, y)) stands.Add((x, y));

        (int X, int Y) bestSeed = default;
        HashSet<(int, int)> best = null;
        foreach (var c in stands)
        {
            var r = Check.Reachable(g, c.X, c.Y);
            if (best == null || r.Count > best.Count) { best = r; bestSeed = c; }
        }
        if (best != null)
        {
            Console.WriteLine($"(auto) best start {bestSeed} opens {best.Count}/{stands.Count} standable cells "
                              + $"({100.0 * best.Count / stands.Count:0}%)");
            spawn ??= bestSeed;
            exit ??= best.OrderByDescending(c => c.Item1).First();
        }
    }

    if (spawn is null) { Console.WriteLine("FAIL  no spawn 'S' in the grid"); problems++; }
    else
    {
        reach = Check.Reachable(g, spawn.Value.X, spawn.Value.Y);

        int standable = 0;
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (Check.IsStand(g, x, y)) standable++;

        double pct = standable == 0 ? 0 : 100.0 * reach.Count / standable;
        Console.WriteLine($"reachable ground: {reach.Count}/{standable} standable cells ({pct:0}%)");

        if (exit is null) { Console.WriteLine("FAIL  no exit 'X' in the grid"); problems++; }
        else
        {
            // The exit marker sits in the air; the player reaches it from the floor under it.
            int ey = exit.Value.Y;
            while (ey + 1 < g.Height && !g.Solid(exit.Value.X, ey + 1)) ey++;
            bool ok = reach.Contains((exit.Value.X, ey));
            Console.WriteLine(ok ? "OK    exit is reachable from the spawn on jumps alone"
                                 : "FAIL  exit is NOT reachable from the spawn without cards");
            if (!ok) problems++;
        }

        // Any content the player can never get to.
        foreach (char marker in "gC+AzZsmrlMbt$")
        {
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    if (g.Cells[y, x] != marker) continue;
                    int fy = y;
                    while (fy + 1 < g.Height && !g.Solid(x, fy + 1)) fy++;
                    if (!reach.Contains((x, fy)))
                        Console.WriteLine($"warn  '{marker}' at ({x},{y}) sits on ground the player can't reach");
                }
        }
    }
    Console.WriteLine();

    // --- style bands --------------------------------------------------------------
    var s = Stats.Of(g);
    Console.WriteLine("| metric | value | limit | typical | |");
    Console.WriteLine("|---|---:|---|---|---|");
    foreach (var b in Check.StyleBands(s))
    {
        if (!b.Ok) problems++;
        string verdict = !b.Ok ? "OFF — " + b.Note
                       : !b.Typical ? "outside the typical range — worth a second look"
                       : "ok";
        Console.WriteLine($"| {b.Name} | {b.Value:0.0} | {b.Lo:0}-{b.Hi:0} | {b.CoreLo:0}-{b.CoreHi:0} | {verdict} |");
    }
    Console.WriteLine();

    if (showMap && reach != null)
    {
        Console.WriteLine("map: '#' solid, ':' standable but UNREACHABLE, ',' reachable floor");
        for (int y = 0; y < g.Height; y++)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < g.Width; x++)
            {
                if (Check.IsStand(g, x, y)) sb.Append(reach.Contains((x, y)) ? ',' : ':');
                else sb.Append(g.Cells[y, x]);
            }
            Console.WriteLine(sb.ToString());
        }
        Console.WriteLine();
    }

    Console.WriteLine(problems == 0 ? "VERDICT: passes" : $"VERDICT: {problems} problem(s)");
    return problems == 0 ? 0 : 2;
}

static int StatsCmd(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: stats <file...>"); return 1; }

    var rows = new List<Stats>();
    foreach (var path in args.Skip(1))
    {
        try
        {
            var g = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? Grid.FromPrefab(path)
                : Grid.FromText(path);
            rows.Add(Stats.Of(g));
        }
        catch (Exception e) { Console.Error.WriteLine($"skip {Path.GetFileName(path)}: {e.Message}"); }
    }

    Console.WriteLine("| level | size | open% | stand/100open | longest run | run 1 | 2-3 | 4-6 | 7-10 | 11-16 | 17+ | mean drop | deep-void% | biggest void |");
    Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (var s in rows)
        Console.WriteLine($"| {s.Name} | {s.Width}x{s.Height} | {s.OpenPct:0.0} | {s.SurfacePer100Open:0.0} "
                          + $"| {s.LongestRun} | {s.RunPct(0):0}% | {s.RunPct(1):0}% | {s.RunPct(2):0}% "
                          + $"| {s.RunPct(3):0}% | {s.RunPct(4):0}% | {s.RunPct(5):0}% "
                          + $"| {s.MeanDrop:0.0} | {s.PctOpenDeepDrop:0.0}% | {s.LargestVoidArea} |");
    return 0;
}

static int Extract(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: extract <level.prefab> [out.txt]"); return 1; }

    var grid = Grid.FromPrefab(args[1]);
    string text = grid.ToText();

    if (args.Length >= 3) { File.WriteAllText(args[2], text); Console.WriteLine($"wrote {args[2]} ({grid.Width}x{grid.Height})"); }
    else Console.WriteLine(text);
    return 0;
}

static void Metrics()
{
    var configs = new (string Name, Sim.Launch Cfg)[]
    {
        ("Running jump (hold jump + direction) — THE DESIGN BASELINE", Sim.Launch.RunningJump),
        ("Straight-up jump (no direction held)",                       Sim.Launch.StandingJump),
        ("Tapped jump (released early)",                               Sim.Launch.TappedJump),
        ("Walk off a ledge (no jump)",                                 Sim.Launch.WalkOff),
        ("Running jump IF the dead horizontal impulse worked (bug ref)", Sim.Launch.RunningJumpUnclamped),
    };

    Console.WriteLine("# Deckshift — Measured Movement Metrics");
    Console.WriteLine();
    Console.WriteLine("All values in TILES (1 tile = 1 world unit). Feet-relative: `dy` is the target");
    Console.WriteLine("surface height relative to the launch surface, `dx` the furthest that surface can");
    Console.WriteLine("sit horizontally and still be landed on.");
    Console.WriteLine();
    Console.WriteLine("Source constants: gravity " + Sim.GravityY + " x scale " + Sim.GravityScale
                      + ", moveSpeed " + Sim.MoveSpeed + ", jumpForce " + Sim.JumpForce
                      + ", fallMultiplier " + Sim.FallMultiplier + ", lowJump " + Sim.LowJumpMultiplier
                      + ", air lerp " + Sim.AirLerpT.ToString("0.###") + "/step.");
    Console.WriteLine();

    foreach (var (name, cfg) in configs)
    {
        var path = Sim.Trajectory(cfg);
        float apex = Sim.Apex(path);
        float airtime = 0f;
        for (int i = 1; i < path.Count; i++) { airtime += Sim.FixedDt; if (path[i].Y < -20f) break; }

        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine($"- Apex: **{apex:0.00}** tiles");
        if (cfg.Jump) Console.WriteLine($"- Highest ledge landable: **{Sim.MaxRise(cfg):0.00}** tiles");
        Console.WriteLine();
        Console.WriteLine("| target dy | max dx |");
        Console.WriteLine("|---:|---:|");
        for (int dy = 5; dy >= -20; dy--)
        {
            float dx = Sim.ReachAt(path, dy);
            if (dx < 0f) continue;
            Console.WriteLine($"| {(dy > 0 ? "+" : "")}{dy} | {dx:0.0} |");
        }
        Console.WriteLine();
    }

    Console.WriteLine("## Dash");
    Console.WriteLine();
    Console.WriteLine($"- Flat dash distance: **{Sim.DashDistance():0.0}** tiles "
                      + $"({Sim.DashSpeed} u/s for {Sim.DashDuration}s), plus a short momentum tail.");
    Console.WriteLine();
    Console.WriteLine("## Body");
    Console.WriteLine();
    Console.WriteLine($"- Collider: {Sim.BodyWidth:0.00} wide x {Sim.BodyHeight:0.00} tall "
                      + "=> a 1-tile-wide gap is passable, a 1-tile-tall crawl is NOT.");
    Console.WriteLine("- Walkable corridors need **2 tiles** of clear height, 3 to feel roomy.");
    Console.WriteLine();

    // --- Design bands, derived from the numbers above -------------------------------
    var run = Sim.Trajectory(Sim.Launch.RunningJump);
    float maxRise = Sim.MaxRise(Sim.Launch.RunningJump);
    float maxGap = Sim.ReachAt(run, 0f);

    Console.WriteLine("## Design bands (use these, not gut feel)");
    Console.WriteLine();
    Console.WriteLine("| Move | Trivial | Standard | Tight | Max possible |");
    Console.WriteLine("|---|---:|---:|---:|---:|");
    Console.WriteLine($"| Rise (climb onto a ledge) | {maxRise * 0.4f:0.0} | {maxRise * 0.65f:0.0} "
                      + $"| {maxRise * 0.9f:0.0} | **{maxRise:0.0}** |");
    Console.WriteLine($"| Flat gap (same height) | {maxGap * 0.4f:0.0} | {maxGap * 0.65f:0.0} "
                      + $"| {maxGap * 0.9f:0.0} | **{maxGap:0.0}** |");
    Console.WriteLine();
    Console.WriteLine("Rounded for authoring: **rise 2 / 3 / 4 / 4.8 max**, **gap 5 / 8 / 10 / 11.9 max**.");
    Console.WriteLine();
    Console.WriteLine("> The asymmetry is the thing to internalise: vertically the player is *tight*");
    Console.WriteLine("> (a rise of 4 is already 83% of maximum), horizontally the player is a *cannon*");
    Console.WriteLine("> (the 5-6 tile gaps the old level laws prescribe are under half of what a jump");
    Console.WriteLine("> clears). Building tall shafts out of rise-4 ledges and then padding the room");
    Console.WriteLine("> with 6-tile gaps produces exactly the complaint: climbs that feel fiddly and");
    Console.WriteLine("> horizontal stretches that feel empty.");
}

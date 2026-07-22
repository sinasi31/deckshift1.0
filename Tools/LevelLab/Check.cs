namespace LevelLab;

/// <summary>
/// Walks a level the way the player would: real trajectories from Sim, stepped through the
/// grid with AABB collision, flood-filled from the spawn. Answers "can this actually be
/// played" and "does it look like the rooms the designer hand-built".
/// </summary>
public static class Check
{
    /// <summary>Player body in tiles — feet at the cell floor, 2 cells of occupied height.</summary>
    const int BodyCells = 2;

    public static bool IsStand(Grid g, int x, int y) =>
        g.Solid(x, y + 1) && !g.Solid(x, y) && !g.Solid(x, y - 1);

    /// <summary>Does the body fit with its feet in cell (x,y)?</summary>
    static bool Fits(Grid g, int x, int y)
    {
        for (int i = 0; i < BodyCells; i++)
            if (g.Solid(x, y - i)) return false;
        return true;
    }

    const float HalfWidth = Sim.BodyWidth / 2f;
    const float BodyTall = Sim.BodyHeight;
    const float Eps = 0.001f;

    /// <summary>Is any part of the body solid, with the feet at grid height feetY, centre x?</summary>
    static bool Blocked(Grid g, float x, float feetY)
    {
        int c0 = (int)MathF.Floor(x - HalfWidth), c1 = (int)MathF.Floor(x + HalfWidth);
        int r0 = (int)MathF.Floor(feetY - BodyTall), r1 = (int)MathF.Floor(feetY - Eps);
        for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
                if (g.Solid(c, r)) return true;
        return false;
    }

    /// <summary>
    /// Flies one launch from a standing cell through the grid and returns the cell it lands on.
    /// This is a real little platformer step: hitting a wall only kills horizontal speed (the
    /// player keeps rising and can clear the ledge), a ceiling only kills vertical speed.
    /// </summary>
    static IEnumerable<(int X, int Y)> Land(Grid g, int sx, int sy, Sim.Launch cfg, int dir)
    {
        float x = sx + 0.5f;
        float feetY = sy + 1f;                       // feet sit on top of the support cell
        float vx = (cfg.RunUp ? Sim.MoveSpeed : 0f) * dir;
        float vy = cfg.Jump ? -Sim.JumpForce : 0f;   // grid Y grows downward, so up is negative
        if (cfg.Jump) vx += (cfg.RunUp ? dir : 0f) * Sim.JumpForce;

        bool first = true;
        int step = 0;
        for (float t = 0f; t < 6f; t += Sim.FixedDt, step++)
        {
            float target = (cfg.HoldForwardAt(step) ? Sim.MoveSpeed : 0f) * dir;

            // Same order as PlayerController: better-jump, horizontal, gravity, integrate.
            if (vy > 0f) vy -= Sim.GravityY * (Sim.FallMultiplier - 1f) * Sim.FixedDt;
            else if (vy < 0f && !cfg.HoldJumpAt(step)) vy -= Sim.GravityY * (Sim.LowJumpMultiplier - 1f) * Sim.FixedDt;

            if (first && cfg.Jump && cfg.GroundClamp) vx = target;
            else vx += (target - vx) * Sim.AirLerpT;
            first = false;

            vy -= Sim.GravityY * Sim.GravityScale * Sim.FixedDt;

            // --- horizontal, with wall stop ---
            float nx = x + vx * Sim.FixedDt;
            if (Blocked(g, nx, feetY)) vx = 0f;
            else x = nx;

            // --- vertical ---
            float ny = feetY + vy * Sim.FixedDt;
            if (vy < 0f)
            {
                if (Blocked(g, x, ny)) vy = 0f;      // ceiling
                else feetY = ny;
            }
            else
            {
                // Falling: find the first floor the feet cross.
                int from = (int)MathF.Floor(feetY), to = (int)MathF.Floor(ny);
                bool landed = false;
                for (int r = Math.Max(from, 0); r <= to; r++)
                {
                    int c0 = (int)MathF.Floor(x - HalfWidth), c1 = (int)MathF.Floor(x + HalfWidth);
                    for (int c = c0; c <= c1 && !landed; c++)
                    {
                        if (!g.Solid(c, r)) continue;
                        int stand = r - 1;
                        if (stand >= 0 && IsStand(g, c, stand)) yield return (c, stand);
                        landed = true;
                    }
                    if (landed) break;
                }
                if (landed) yield break;
                feetY = ny;
            }

            if (feetY > g.Height + 2 || x < -1 || x > g.Width + 1) yield break;
        }
    }

    /// <summary>
    /// The spread of inputs a real player has: hold the jump button anywhere from a tap to
    /// the full arc, and let go of the direction key anywhere from immediately to never.
    /// Sampling this is what lets short, precise hops onto one-tile ledges be found.
    /// </summary>
    static Sim.Launch[] LaunchFamily()
    {
        int[] jumpHolds = { 0, 4, 8, 14, 22, Sim.Always };
        int[] fwdHolds = { 0, 3, 6, 10, 16, 26, 40, Sim.Always };

        var list = new List<Sim.Launch>();
        foreach (int j in jumpHolds)
            foreach (int f in fwdHolds)
            {
                list.Add(new Sim.Launch(true, true, j, f));    // running start
                list.Add(new Sim.Launch(true, false, j, f));   // standing start
            }
        list.Add(Sim.Launch.WalkOff);
        list.Add(new Sim.Launch(false, true, 0, 6));           // step off a lip and stop pressing
        return list.ToArray();
    }

    public static HashSet<(int, int)> Reachable(Grid g, int sx, int sy)
    {
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();

        void Push(int x, int y)
        {
            if (x < 0 || y < 0 || x >= g.Width || y >= g.Height) return;
            if (!IsStand(g, x, y) || !seen.Add((x, y))) return;
            queue.Enqueue((x, y));
        }

        // Drop the spawn onto whatever is below it.
        int fy = sy;
        while (fy + 1 < g.Height && !g.Solid(sx, fy + 1)) fy++;
        Push(sx, fy);

        var launches = LaunchFamily();

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // Walk along contiguous floor.
            foreach (int d in new[] { -1, 1 })
                if (IsStand(g, x + d, y)) Push(x + d, y);

            foreach (var cfg in launches)
                foreach (int dir in new[] { -1, 1 })
                    foreach (var (lx, ly) in Land(g, x, y, cfg, dir))
                        Push(lx, ly);
        }
        return seen;
    }

    public static (int X, int Y)? Find(Grid g, char marker)
    {
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (g.Cells[y, x] == marker) return (x, y);
        return null;
    }

    /// <summary>
    /// Style bands. These are not taste — every range is measured from the seven hand-built
    /// COMBAT rooms (efeslevel1-4, EfeVrl4-6). The hub and BossRoom are deliberately excluded:
    /// a sandbox and a boss arena are not what a generated combat room should imitate, and
    /// including them was quietly widening three of the bands.
    ///
    /// Two tiers, because one room (EfeVrl5, the sprawling two-chamber one) sits far outside
    /// the other six on nearly every metric:
    ///   Lo..Hi     — hard limits, the full envelope of the seven. Outside this = FAIL.
    ///   CoreLo..Hi — where six of the seven live. Outside this = a warning worth a second look.
    /// Regenerate both with `stats` if the hand-built set changes.
    /// </summary>
    public sealed record Band(string Name, double Lo, double Hi, double CoreLo, double CoreHi, double Value, string Note)
    {
        public bool Ok => Value >= Lo && Value <= Hi;
        public bool Typical => Value >= CoreLo && Value <= CoreHi;
    }

    public static List<Band> StyleBands(Stats s) => new()
    {
        new("width",              44, 56,   44, 48,   s.Width,             "hand-built combat rooms are 44-48 wide; only the sprawling one is 56"),
        new("height",             22, 30,   22, 27,   s.Height,            "hand-built combat rooms are 22-27 tall"),
        new("area (cells)",      950, 1700, 950, 1300, s.Cells,            "the typical room is ~1000-1300 cells; bigger reads as a slog"),
        new("open %",             44, 69,   44, 55,   s.OpenPct,           "~50% open; more is an empty hall, less is a carved maze"),
        new("footholds /100 open", 8.5, 23, 11, 23,   s.SurfacePer100Open, "things to stand on per unit of air"),
        new("1-tile ledges %",    54, 85,   54, 85,   s.RunPct(0),         "THE signature: most ledges in a hand-built room are ONE tile wide"),
        new("longest ledge run",   0, 38,    0, 38,   s.LongestRun,        "long flat floors are dead walking time"),
        new("biggest void",        0, 635,   0, 130,  s.LargestVoidArea,   "one big empty rectangle = nothing to do in it"),
        new("deep-void %",         0, 41,    0, 33,   s.PctOpenDeepDrop,   "share of open space more than 8 tiles above any floor"),
    };
}

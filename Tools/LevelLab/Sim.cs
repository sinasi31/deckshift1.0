namespace LevelLab;

/// <summary>
/// Replays Deckshift's real player physics so level geometry can be measured in TILES.
/// Every constant below is read from the shipped Player.prefab / PlayerController.cs /
/// Physics2DSettings.asset — if you retune the player, update these and re-run `metrics`.
/// 1 tile = 1 world unit (Grid m_CellSize is 1,1).
/// </summary>
public static class Sim
{
    // --- Physics2DSettings.asset ---
    public const float GravityY = -9.81f;

    // --- Player.prefab (Rigidbody2D) ---
    public const float GravityScale = 1.25f;
    public const float Mass = 1f;

    // --- Player.prefab (PlayerController overrides) ---
    public const float MoveSpeed = 8f;
    public const float JumpForce = 11f;          // defaultJumpForce
    public const float FallMultiplier = 2.5f;
    public const float LowJumpMultiplier = 2f;
    public const float DashSpeed = 26f;
    public const float DashDuration = 0.16f;

    // --- PlayerController.FixedUpdate air control ---
    // newX = Lerp(vx, targetX, airControl * fixedDeltaTime * 5f) with airControl = 0.7f
    public const float AirControl = 0.7f;
    public const float FixedDt = 0.02f;
    public static float AirLerpT => AirControl * FixedDt * 5f;   // 0.07 per physics step

    // --- Player capsule collider (Player.prefab CapsuleCollider2D) ---
    public const float BodyWidth = 0.5075f;
    public const float BodyHeight = 1.6848f;

    public readonly record struct Point(float X, float Y);

    /// <summary>
    /// One launch configuration. Feet start at (0,0) on a ledge surface, moving +X.
    ///
    /// GroundClamp models a REAL quirk of the shipped code: `isGrounded` is computed in
    /// Update (line 339) but movement is applied in FixedUpdate (line 610). The first
    /// physics step after a jump still sees the stale `isGrounded == true`, so the grounded
    /// branch runs and pins vx to moveSpeed — which erases the horizontal half of
    /// PerformJump's impulse before it ever moves the player. Leave this true to measure
    /// the game as it actually plays; set it false to see what the impulse was meant to do.
    /// </summary>
    public const int Always = int.MaxValue;

    /// <summary>
    /// HoldJumpSteps / HoldForwardSteps are how many physics steps the player keeps each
    /// input down. Modelling the RELEASE matters: a player lands on a one-tile ledge by
    /// letting go, and a sim that always holds both inputs to the end can only ever make
    /// maximum-length jumps — which makes tight platforming look unreachable.
    /// </summary>
    public readonly record struct Launch(bool Jump, bool RunUp, int HoldJumpSteps, int HoldForwardSteps, bool GroundClamp = true)
    {
        public bool HoldJumpAt(int step) => step < HoldJumpSteps;
        public bool HoldForwardAt(int step) => step < HoldForwardSteps;

        public static Launch RunningJump => new(true, true, Always, Always);
        public static Launch StandingJump => new(true, false, Always, 0);
        public static Launch TappedJump => new(true, true, 0, Always);
        public static Launch WalkOff => new(false, true, 0, Always);
        public static Launch RunningJumpUnclamped => new(true, true, Always, Always, false);
    }

    /// <summary>
    /// Steps the exact FixedUpdate order: better-jump gravity mod, air control lerp,
    /// engine gravity, then integrate. Returns the feet path until it falls past minY.
    /// </summary>
    public static List<Point> Trajectory(Launch cfg, float minY = -40f, float maxTime = 6f)
    {
        float x = 0f, y = 0f;
        float vx = cfg.RunUp ? MoveSpeed : 0f;
        float vy = 0f;

        if (cfg.Jump)
        {
            // PerformJump: vy zeroed, then an impulse on BOTH axes (mass 1 => impulse == velocity).
            vy = JumpForce;
            vx += (cfg.RunUp ? 1f : 0f) * JumpForce;
        }

        var path = new List<Point> { new(x, y) };

        bool firstStep = true;
        int step = 0;
        for (float t = 0f; t < maxTime && y > minY; t += FixedDt, step++)
        {
            float targetX = cfg.HoldForwardAt(step) ? MoveSpeed : 0f;

            // 1. "BETTER JUMP" block (PlayerController.Update, framerate-independent).
            if (vy < 0f) vy += GravityY * (FallMultiplier - 1f) * FixedDt;
            else if (vy > 0f && !cfg.HoldJumpAt(step)) vy += GravityY * (LowJumpMultiplier - 1f) * FixedDt;

            // 2. Horizontal: the stale-grounded first step pins vx outright; after that it's
            //    the weak air-control lerp.
            if (firstStep && cfg.Jump && cfg.GroundClamp) vx = targetX;
            else vx = Lerp(vx, targetX, AirLerpT);
            firstStep = false;

            // 3. Engine gravity.
            vy += GravityY * GravityScale * FixedDt;

            // 4. Integrate.
            x += vx * FixedDt;
            y += vy * FixedDt;
            path.Add(new Point(x, y));
        }

        return path;
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float Apex(List<Point> path)
    {
        float m = 0f;
        foreach (var p in path) m = MathF.Max(m, p.Y);
        return m;
    }

    /// <summary>
    /// Horizontal distance at which the DESCENDING branch crosses height dy — i.e. the
    /// furthest a platform whose surface sits dy tiles above (or below) the launch surface
    /// can be and still be landed on. Returns -1 when the arc never reaches dy.
    /// </summary>
    public static float ReachAt(List<Point> path, float dy)
    {
        bool descending = false;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i].Y < path[i - 1].Y) descending = true;
            if (!descending) continue;

            if (path[i - 1].Y >= dy && path[i].Y <= dy)
            {
                float span = path[i - 1].Y - path[i].Y;
                float f = span <= 0f ? 0f : (path[i - 1].Y - dy) / span;
                return path[i - 1].X + (path[i].X - path[i - 1].X) * f;
            }
        }
        return -1f;
    }

    /// <summary>Highest ledge whose surface the player can still land their feet on.</summary>
    public static float MaxRise(Launch cfg)
    {
        var path = Trajectory(cfg);
        float apex = Apex(path);
        float best = 0f;
        for (float dy = 0f; dy <= apex + 0.01f; dy += 0.01f)
        {
            // Needs enough forward travel to actually clear the ledge lip (half body width).
            if (ReachAt(path, dy) >= BodyWidth * 0.5f) best = dy;
        }
        return best;
    }

    public static float DashDistance() => DashSpeed * DashDuration;
}

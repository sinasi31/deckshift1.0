using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Checks a level TEXT file for playability BEFORE it is imported into a prefab.
//
// WHY THIS EXISTS. LevelTextImporter's validation counts markers — one 'S', at least one 'X',
// unknown characters — and nothing else. Every one of the seven Level Design Laws in CLAUDE.md was
// enforced only by prose in a comment header, which does not work: GenLevel3's own header claims
// "All jump rises <= 3 (max jump ~4)" and was authored against movement numbers that the 2026-07-14
// playtest disproved. Nothing caught it, and that level is still in the project.
//
// This is the same move that fixed the run map: turn the rules into something executable. A level
// whose exit cannot be reached is the worst possible bug to find at playtest instead of at build
// time, so the reachability check simulates the REAL player rather than approximating with a
// rule of thumb.
//
// Menu: Deckshift -> Validate Level Text(s).
public static class LevelValidator
{
    private const string LevelTextFolder = "Assets/LevelTexts";

    // ---- the movement model -----------------------------------------------------------------
    //
    // Read out of PlayerController and Player.prefab on 2026-08-07, NOT estimated. If any of these
    // change in the game, change them here too or the validator quietly starts lying.
    //
    //   Player.prefab : mass 1, gravityScale 1.25, moveSpeed 8, defaultJumpForce 11
    //   Physics2D.gravity.y = -9.81
    //   PerformJump   : zeroes vertical velocity, then AddForce(moveInput * F, F) as an IMPULSE
    //   Update()      : while rising with Space released, adds (lowJumpMultiplier-1) * gravity
    //                   while falling,                    adds (fallMultiplier-1)   * gravity
    //   FixedUpdate() : airborne horizontal lerps toward moveInput*moveSpeed at 0.07 per step
    //
    // ⚠️ PerformJump's HORIZONTAL IMPULSE IS DEAD CODE — do not model it.
    //
    // PerformJump does AddForce(moveInput * jumpForce, jumpForce), which looks like it should launch
    // a running jump at 8 + 11 = 19 u/s. It does not. `isGrounded` is assigned ONLY in Update()
    // and nothing clears it on jumping, so the very next FixedUpdate still sees isGrounded == true
    // and runs the grounded branch — rb.linearVelocity = (moveInput * moveSpeed, y) — which
    // overwrites the horizontal impulse back to moveSpeed about 20ms later. Vertical is untouched,
    // which is why the 4.9-tile apex is unaffected and matches playtest.
    //
    // The impulse therefore buys ~0.2 tiles before being wiped, and is ignored here.
    //
    // ⚠️ LANDMINE: if anyone ever "fixes" that stale isGrounded read, every jump instantly gains a
    // large horizontal boost and every gap in every level becomes trivially clearable. Re-measure
    // here if PlayerController's grounded handling changes.
    private const float Gravity = -9.81f;
    private const float GravityScale = 1.25f;
    private const float MoveSpeed = 8f;
    private const float JumpForce = 11f;
    private const float FallMultiplier = 2.5f;
    private const float LowJumpMultiplier = 2f;
    private const float AirControlPerStep = 0.07f;   // 0.7f * 0.02f * 5f
    private const float Dt = 0.02f;
    private const int MaxSimSteps = 220;             // ~4.4s, far beyond any real arc

    // Design guidance from CLAUDE.md's Level Design Laws, reported as advice rather than failure.
    private const int ComfortableRise = 4;
    private const int MaxDropWithoutCheck = 25;

    [MenuItem("Deckshift/Validate Level Text(s)")]
    public static void ValidateAll()
    {
        if (!Directory.Exists(LevelTextFolder))
        {
            Debug.LogError($"[LevelValidator] No folder at {LevelTextFolder}");
            return;
        }

        string[] files = Directory.GetFiles(LevelTextFolder, "*.txt");
        Array.Sort(files);

        StringBuilder all = new StringBuilder();
        all.AppendLine("=== Deckshift level validation ===");
        all.AppendLine($"jump apex {ApexHeight():0.00} tiles   ·   flat jump reach {FlatReach():0.0} tiles");
        all.AppendLine();

        int failed = 0;
        foreach (string path in files)
        {
            Report r = Validate(path);
            all.Append(r.Text);
            all.AppendLine();
            if (!r.Passed) failed++;
        }

        all.AppendLine($"--- {files.Length} level(s), {failed} FAILED ---");

        string outPath = Path.Combine(LevelTextFolder, "_validation_report.txt");
        File.WriteAllText(outPath, all.ToString());
        AssetDatabase.Refresh();

        if (failed > 0) Debug.LogError(all.ToString());
        else Debug.Log(all.ToString());
    }

    public class Report
    {
        public bool Passed = true;
        public string Text = "";
    }

    // ---- grid -------------------------------------------------------------------------------

    private class Grid
    {
        public string[] rows;
        public int w, h;

        // (col, y) with y counted UPWARD from the bottom row, so the physics below reads naturally.
        public char At(int col, int y)
        {
            int row = h - 1 - y;
            if (row < 0 || row >= h || col < 0) return '#';
            string line = rows[row];
            if (col >= line.Length) return '#';
            return line[col];
        }

        // Outside the grid counts as solid so the player can never leave the room.
        public bool Solid(int col, int y)
        {
            if (col < 0 || col >= w || y < 0 || y >= h) return true;
            return At(col, y) == '#';
        }

        // The capsule is ~0.5 wide by 1.68 tall, so the player occupies ONE column and TWO rows.
        public bool Fits(int col, int y) => !Solid(col, y) && !Solid(col, y + 1);

        // Anything you can LAND on. One-way platforms ('=') support you from above but let you
        // pass through from below, so they count here and deliberately NOT in Solid/Fits. Without
        // this split the validator reports rooms built on one-ways as unreachable, which would be
        // its own false alarm — they're banned by Law 4, but the reachability answer still has to
        // be honest about why.
        public bool Support(int col, int y)
        {
            if (col < 0 || col >= w || y < 0) return true;
            if (y >= h) return false;
            char c = At(col, y);
            return c == '#' || c == '=';
        }

        public bool Grounded(int col, int y) => Fits(col, y) && Support(col, y - 1);

        public bool Find(char marker, out int col, out int y)
        {
            for (int yy = 0; yy < h; yy++)
                for (int cc = 0; cc < w; cc++)
                    if (At(cc, yy) == marker) { col = cc; y = yy; return true; }
            col = y = -1;
            return false;
        }
    }

    private static Grid Parse(string path, List<string> notes)
    {
        List<string> lines = new List<string>();
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.TrimEnd('\r', '\n');
            string t = line.TrimStart();
            if (t.StartsWith("//") || t.StartsWith("!")) continue;   // comments and directives
            if (t.Length == 0) continue;
            lines.Add(line);
        }

        if (lines.Count == 0) return null;

        int w = 0;
        foreach (string l in lines) w = Mathf.Max(w, l.Length);

        return new Grid { rows = lines.ToArray(), h = lines.Count, w = w };
    }

    // ---- the simulated jump -----------------------------------------------------------------

    private static float RiseAccel(bool holdingJump)
    {
        float a = Gravity * GravityScale;
        if (!holdingJump) a += Gravity * (LowJumpMultiplier - 1f);
        return a;
    }

    private static float FallAccel() => Gravity * GravityScale + Gravity * (FallMultiplier - 1f);

    private static float ApexHeight() => (JumpForce * JumpForce) / (2f * Mathf.Abs(RiseAccel(true)));

    // Horizontal distance of a full-hold running jump that lands back at its take-off height.
    private static float FlatReach()
    {
        float apex = ApexHeight();
        float tUp = JumpForce / Mathf.Abs(RiseAccel(true));
        float tDown = Mathf.Sqrt(2f * apex / Mathf.Abs(FallAccel()));
        float t = tUp + tDown;

        return MoveSpeed * t;   // the jump's horizontal impulse does not survive; see the constants
    }

    // Simulates one arc and returns every grounded cell it could land on.
    //
    // `dir` is the held direction (-1/0/+1), `holdSteps` how long jump is held before release, and
    // vy0 = 0 models simply WALKING OFF a ledge rather than jumping.
    private static void SimulateArc(Grid g, int startCol, int startY, int dir, int holdSteps, bool jump,
                                    List<Vector2Int> landings)
    {
        float x = startCol + 0.5f;
        float y = startY;

        float vy = jump ? JumpForce : 0f;
        // NOT dir * (MoveSpeed + JumpForce): the jump's horizontal impulse is wiped by the next
        // FixedUpdate's grounded branch before it can travel. See the note on the constants.
        float vx = dir * MoveSpeed;

        for (int step = 0; step < MaxSimSteps; step++)
        {
            bool holding = jump && step < holdSteps;
            vy += (vy > 0f ? RiseAccel(holding) : FallAccel()) * Dt;
            vx = Mathf.Lerp(vx, dir * MoveSpeed, AirControlPerStep);

            // --- horizontal, resolved separately so a wall doesn't kill the whole arc ---
            float nx = x + vx * Dt;
            int ncol = Mathf.FloorToInt(nx);
            int cy = Mathf.FloorToInt(y);
            if (g.Fits(ncol, cy)) x = nx;
            else vx = 0f;

            // --- vertical ---
            float ny = y + vy * Dt;
            int ncy = Mathf.FloorToInt(ny);
            int col = Mathf.FloorToInt(x);

            if (vy > 0f)
            {
                if (g.Solid(col, ncy + 1)) vy = 0f;          // bonked a ceiling
                else y = ny;
            }
            else
            {
                // Support, not Solid: falling onto a one-way platform lands you on it.
                if (g.Support(col, ncy))
                {
                    int landY = ncy + 1;
                    if (g.Grounded(col, landY)) landings.Add(new Vector2Int(col, landY));
                    return;
                }
                y = ny;
            }

            if (y < -2f) return;   // fell out of the room
        }
    }

    // Every grounded cell the player can get to from the spawn, using ONLY jumping and moving.
    private static HashSet<Vector2Int> Reachable(Grid g, int startCol, int startY)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        // Drop the spawn to the floor first — 'S' is authored in mid-air in several rooms.
        int sy = startY;
        while (sy > 0 && !g.Grounded(startCol, sy) && g.Fits(startCol, sy)) sy--;

        Vector2Int start = new Vector2Int(startCol, sy);
        seen.Add(start);
        queue.Enqueue(start);

        List<Vector2Int> landings = new List<Vector2Int>();
        int[] holdVariants = { 3, 8, 15, 25, 60 };   // tapped through to fully held

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();

            // Walking, including a one-tile step up or down.
            for (int d = -1; d <= 1; d += 2)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int n = new Vector2Int(cur.x + d, cur.y + dy);
                    if (g.Grounded(n.x, n.y) && g.Fits(cur.x + d, cur.y) && seen.Add(n)) queue.Enqueue(n);
                }

            landings.Clear();
            for (int dir = -1; dir <= 1; dir++)
            {
                SimulateArc(g, cur.x, cur.y, dir, 0, false, landings);          // walk off the edge
                foreach (int hold in holdVariants)
                    SimulateArc(g, cur.x, cur.y, dir, hold, true, landings);    // jumps of every length
            }

            foreach (Vector2Int l in landings)
                if (seen.Add(l)) queue.Enqueue(l);
        }

        return seen;
    }

    // ---- the checks -------------------------------------------------------------------------

    public static Report Validate(string path)
    {
        Report rep = new Report();
        StringBuilder sb = new StringBuilder();
        List<string> notes = new List<string>();
        string name = Path.GetFileNameWithoutExtension(path);

        Grid g = Parse(path, notes);
        if (g == null)
        {
            rep.Passed = false;
            rep.Text = $"[{name}] EMPTY — no grid rows found.\n";
            return rep;
        }

        List<string> fail = new List<string>();
        List<string> warn = new List<string>();

        int sCol, sY, xCol, xY;
        bool hasS = g.Find('S', out sCol, out sY);
        bool hasX = g.Find('X', out xCol, out xY);

        if (!hasS) fail.Add("no 'S' spawn");
        if (!hasX) fail.Add("no 'X' exit");

        HashSet<Vector2Int> reach = null;
        if (hasS)
        {
            reach = Reachable(g, sCol, sY);

            if (hasX)
            {
                // LAW 1: the exit must be reachable with jumping and moving alone.
                bool exitReached = false;
                for (int dy = -1; dy <= 2 && !exitReached; dy++)
                    for (int dx = -1; dx <= 1 && !exitReached; dx++)
                        if (reach.Contains(new Vector2Int(xCol + dx, xY + dy))) exitReached = true;

                if (!exitReached)
                    fail.Add($"LAW 1 — exit at ({xCol},{xY}) is NOT reachable by jumping and moving alone");

                // LAW 7: spawn and exit must be far apart, so Phase/Portal can't skip the level.
                int manhattan = Mathf.Abs(xCol - sCol) + Mathf.Abs(xY - sY);
                if (manhattan < 20)
                    warn.Add($"LAW 7 — spawn and exit are only {manhattan} tiles apart (want 20+)");
            }
        }

        // LAW 8 (designer 2026-08-07): THE SPAWN IS A SAFE BEACH.
        //
        // The player must be able to arrive, look around, open their deck and decide before
        // anything can touch them. So: no enemy standing on the platform they spawn on, and
        // nothing able to target them from across the room either.
        //
        // Ranged threats are checked by LINE OF SIGHT rather than distance, because a spitter
        // twenty tiles away down a clear corridor is aiming at the player, while one six tiles
        // away behind a wall is not.
        if (hasS)
        {
            const string melee = "mzZlM", ranged = "rst", flying = "b";
            const int RangedGuard = 26, MeleeGuard = 10;

            // The contiguous run of standable ground the spawn is on = "the first platform".
            int sFloor = sY;
            while (sFloor > 0 && !g.Grounded(sCol, sFloor) && g.Fits(sCol, sFloor)) sFloor--;
            int runL = sCol; while (g.Grounded(runL - 1, sFloor)) runL--;
            int runR = sCol; while (g.Grounded(runR + 1, sFloor)) runR++;

            for (int y = 0; y < g.h; y++)
                for (int c = 0; c < g.w; c++)
                {
                    char e = g.At(c, y);
                    bool isMelee = melee.IndexOf(e) >= 0, isRanged = ranged.IndexOf(e) >= 0, isFly = flying.IndexOf(e) >= 0;
                    if (!isMelee && !isRanged && !isFly) continue;

                    if (c >= runL && c <= runR && Mathf.Abs(y - sFloor) <= 1)
                        fail.Add($"LAW 8 — '{e}' at ({c},{y}) stands on the SPAWN PLATFORM; the first platform must be empty");
                    else if ((isRanged || isFly) && Dist(c, y, sCol, sFloor) <= RangedGuard && ClearLine(g, c, y, sCol, sFloor))
                        fail.Add($"LAW 8 — '{e}' at ({c},{y}) has line of sight to the spawn; it can target the player before they act");
                    else if (isMelee && Dist(c, y, sCol, sFloor) <= MeleeGuard && ClearLine(g, c, y, sCol, sFloor))
                        warn.Add($"LAW 8 — '{e}' at ({c},{y}) is {Dist(c, y, sCol, sFloor)} tiles from the spawn with a clear path");
                }
        }

        // LAW 4: no one-way platforms. LAW 5: no turrets in generated rooms.
        int oneWay = Count(g, '='), turrets = Count(g, 't'), crumbling = Count(g, 'c');
        if (oneWay > 0) fail.Add($"LAW 4 — {oneWay} one-way platform tile(s) '='; use solid strips");
        if (turrets > 0) fail.Add($"LAW 5 — {turrets} turret(s) 't'; the importer can only floor-mount them");
        if (crumbling > 0) warn.Add($"{crumbling} crumbling platform(s) 'c' — sprites are outdated, use 'T' trapdoors");

        // How much of the room is actually usable. A room that is mostly sealed rock or open void
        // is not a level, it is a box — GenLevel3 is a 60x30 room whose top third is empty air.
        int air = 0, standable = 0;
        for (int y = 0; y < g.h; y++)
            for (int c = 0; c < g.w; c++)
            {
                if (!g.Solid(c, y)) air++;
                if (g.Grounded(c, y)) standable++;
            }

        int reachCount = reach != null ? reach.Count : 0;
        float usedPct = standable > 0 ? 100f * reachCount / standable : 0f;
        if (standable > 0 && usedPct < 60f)
            warn.Add($"only {usedPct:0}% of standable ground is reachable — {standable - reachCount} orphaned cell(s)");

        float density = 100f * (1f - (float)air / (g.w * g.h));
        if (density < 30f)
            warn.Add($"rock density {density:0}% — mostly open void, the room has little to climb on");

        rep.Passed = fail.Count == 0;

        sb.AppendLine($"[{name}] {(rep.Passed ? "PASS" : "FAIL")}   {g.w}x{g.h}, " +
                      $"{standable} standable, {reachCount} reachable ({usedPct:0}%), rock {density:0}%");
        foreach (string f in fail) sb.AppendLine("   FAIL  " + f);
        foreach (string w in warn) sb.AppendLine("   warn  " + w);

        rep.Text = sb.ToString();
        return rep;
    }

    // Renders the room with every cell the player can STAND ON marked, so a failure can be seen
    // rather than argued about. This is the tool's most useful output when authoring: it answers
    // "where does the route actually stop?" directly, and it is how the validator itself gets
    // checked — a reachability claim you cannot eyeball is a reachability claim you cannot trust.
    //
    //   o = reachable standing cell      x = standable but ORPHANED (this is where levels break)
    //   S / X = spawn and exit           # = rock
    public static string Overlay(string path)
    {
        Grid g = Parse(path, new List<string>());
        if (g == null) return "(empty)";

        int sCol, sY;
        if (!g.Find('S', out sCol, out sY)) return "(no spawn)";
        HashSet<Vector2Int> reach = Reachable(g, sCol, sY);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Path.GetFileNameWithoutExtension(path) + "  o=reachable  x=orphaned");
        for (int y = g.h - 1; y >= 0; y--)
        {
            for (int c = 0; c < g.w; c++)
            {
                char raw = g.At(c, y);
                if (raw == 'S' || raw == 'X') { sb.Append(raw); continue; }
                if (raw == '#') { sb.Append('#'); continue; }
                if (g.Grounded(c, y)) sb.Append(reach.Contains(new Vector2Int(c, y)) ? 'o' : 'x');
                else sb.Append(raw == '.' ? ' ' : raw);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int Dist(int ax, int ay, int bx, int by) =>
        Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(ay - by));

    // Bresenham-ish walk: is there unobstructed sight between two cells?
    private static bool ClearLine(Grid g, int ax, int ay, int bx, int by)
    {
        int steps = Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay));
        if (steps == 0) return true;
        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(ay, by, t));
            if (g.Solid(x, y)) return false;
        }
        return true;
    }

    private static int Count(Grid g, char c)
    {
        int n = 0;
        for (int y = 0; y < g.h; y++)
            for (int x = 0; x < g.w; x++)
                if (g.At(x, y) == c) n++;
        return n;
    }
}

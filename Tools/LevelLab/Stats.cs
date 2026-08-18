namespace LevelLab;

/// <summary>
/// Shape metrics for a level grid. The point of these numbers is to make "my generated
/// rooms feel empty and rambling" measurable instead of a matter of taste: they compare a
/// candidate room against the hand-built rooms the designer already likes.
/// </summary>
public sealed class Stats
{
    public string Name;
    public int Width, Height, Cells;
    public int OpenCells;
    public double OpenPct;

    /// <summary>Solid cells with air directly above — everything the player can stand on.</summary>
    public int SurfaceCells;
    public double SurfacePer100Open;

    /// <summary>Horizontal runs of standable surface, bucketed by length.</summary>
    public int[] RunBuckets = new int[6];      // 1, 2-3, 4-6, 7-10, 11-16, 17+
    public static readonly string[] RunLabels = { "1", "2-3", "4-6", "7-10", "11-16", "17+" };
    public int LongestRun;

    /// <summary>How far an open cell falls before hitting something. High = void.</summary>
    public double MeanDrop;
    public double PctOpenDeepDrop;             // open cells with >8 tiles of nothing below
    public int LargestVoidArea;                // biggest all-open rectangle

    public static Stats Of(Grid g)
    {
        var s = new Stats { Name = g.Name, Width = g.Width, Height = g.Height, Cells = g.Width * g.Height };

        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (!g.Solid(x, y)) s.OpenCells++;
        s.OpenPct = 100.0 * s.OpenCells / s.Cells;

        // --- standable surfaces and their horizontal runs ---
        for (int y = 0; y < g.Height; y++)
        {
            int run = 0;
            for (int x = 0; x <= g.Width; x++)
            {
                bool surface = x < g.Width && g.Solid(x, y) && !g.Solid(x, y - 1) && y - 1 >= 0;
                if (surface) { run++; s.SurfaceCells++; }
                else if (run > 0) { s.Bucket(run); s.LongestRun = Math.Max(s.LongestRun, run); run = 0; }
            }
        }
        s.SurfacePer100Open = s.OpenCells == 0 ? 0 : 100.0 * s.SurfaceCells / s.OpenCells;

        // --- drop depth under every open cell ---
        long dropSum = 0; int deep = 0;
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                if (g.Solid(x, y)) continue;
                int d = 0;
                while (y + d + 1 < g.Height && !g.Solid(x, y + d + 1)) d++;
                dropSum += d;
                if (d > 8) deep++;
            }
        s.MeanDrop = s.OpenCells == 0 ? 0 : (double)dropSum / s.OpenCells;
        s.PctOpenDeepDrop = s.OpenCells == 0 ? 0 : 100.0 * deep / s.OpenCells;
        s.LargestVoidArea = LargestOpenRect(g);
        return s;
    }

    void Bucket(int run)
    {
        int i = run switch { 1 => 0, <= 3 => 1, <= 6 => 2, <= 10 => 3, <= 16 => 4, _ => 5 };
        RunBuckets[i]++;
    }

    /// <summary>Largest all-open axis-aligned rectangle (classic histogram scan).</summary>
    static int LargestOpenRect(Grid g)
    {
        var heights = new int[g.Width];
        int best = 0;
        for (int y = 0; y < g.Height; y++)
        {
            for (int x = 0; x < g.Width; x++)
                heights[x] = g.Solid(x, y) ? 0 : heights[x] + 1;
            best = Math.Max(best, LargestInHistogram(heights));
        }
        return best;
    }

    static int LargestInHistogram(int[] h)
    {
        var stack = new Stack<int>();
        int best = 0;
        for (int i = 0; i <= h.Length; i++)
        {
            int cur = i == h.Length ? 0 : h[i];
            while (stack.Count > 0 && h[stack.Peek()] >= cur)
            {
                int height = h[stack.Pop()];
                int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                best = Math.Max(best, height * (i - left));
            }
            stack.Push(i);
        }
        return best;
    }

    public int TotalRuns => RunBuckets.Sum();
    public double RunPct(int i) => TotalRuns == 0 ? 0 : 100.0 * RunBuckets[i] / TotalRuns;
}

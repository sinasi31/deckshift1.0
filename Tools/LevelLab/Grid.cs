namespace LevelLab;

/// <summary>
/// An ASCII level grid in the same language the Level Text Importer reads:
/// '#' solid, '.' air, plus entity markers. Row 0 is the TOP row, matching the .txt files
/// (tilemap Y grows upward, so extraction flips).
/// </summary>
public sealed class Grid
{
    public char[,] Cells;
    public int Width, Height;
    public string Name = "";

    public char At(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height ? '#' : Cells[y, x];

    public bool Solid(int x, int y) => At(x, y) == '#';

    public static Grid Blank(int w, int h)
    {
        var g = new Grid { Width = w, Height = h, Cells = new char[h, w] };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                g.Cells[y, x] = '.';
        return g;
    }

    public static Grid FromText(string path)
    {
        var lines = File.ReadAllLines(path)
                        .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("!"))
                        .SkipWhile(string.IsNullOrWhiteSpace)
                        .ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

        int w = lines.Max(l => l.Length);
        var g = Blank(w, lines.Count);
        g.Name = Path.GetFileNameWithoutExtension(path);
        for (int y = 0; y < lines.Count; y++)
            for (int x = 0; x < lines[y].Length; x++)
                g.Cells[y, x] = lines[y][x];
        return g;
    }

    /// <summary>Builds a grid from every collider-bearing tilemap in a hand-built level prefab.</summary>
    public static Grid FromPrefab(string prefabPath)
    {
        var solid = TilemapExtract.Layers(prefabPath).Where(l => l.HasCollider && l.Tiles.Count > 0).ToList();
        if (solid.Count == 0) throw new InvalidOperationException($"no solid tilemap layers in {prefabPath}");

        var all = solid.SelectMany(l => l.Tiles).ToList();
        int minX = all.Min(c => c.X), maxX = all.Max(c => c.X);
        int minY = all.Min(c => c.Y), maxY = all.Max(c => c.Y);

        var g = Blank(maxX - minX + 1, maxY - minY + 1);
        g.Name = Path.GetFileNameWithoutExtension(prefabPath);
        foreach (var c in all)
            g.Cells[maxY - c.Y, c.X - minX] = '#';   // flip Y: tilemap up-positive -> text top-down
        return g;
    }

    public string ToText()
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) sb.Append(Cells[y, x]);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

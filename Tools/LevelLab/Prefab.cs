using System.Text.RegularExpressions;

namespace LevelLab;

/// <summary>
/// A deliberately small Unity-YAML reader — just enough to pull tilemaps and prefab-instance
/// placements out of a level .prefab. Unity's serialised format is a stream of documents:
///
///   --- !u!&lt;classId&gt; &amp;&lt;fileId&gt;
///   TypeName:
///     m_Field: value
///
/// We key documents by their anchor fileId and their type name, which is all that's needed
/// to join a component back to the GameObject (and therefore the name) that owns it.
/// </summary>
public sealed class UnityDoc
{
    public long FileId;
    public string Type = "";
    public string Body = "";

    public string Field(string name)
    {
        var m = Regex.Match(Body, $@"(?m)^\s*{Regex.Escape(name)}:\s*(.*)$");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public long RefField(string name)
    {
        var v = Field(name);
        if (v == null) return 0;
        var m = Regex.Match(v, @"fileID:\s*(-?\d+)");
        return m.Success ? long.Parse(m.Groups[1].Value) : 0;
    }
}

public static class PrefabReader
{
    static readonly Regex DocHeader = new(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);

    public static List<UnityDoc> Read(string path)
    {
        var docs = new List<UnityDoc>();
        UnityDoc cur = null;
        var body = new System.Text.StringBuilder();
        bool typeSeen = false;

        foreach (var line in File.ReadLines(path))
        {
            var h = DocHeader.Match(line);
            if (h.Success)
            {
                if (cur != null) { cur.Body = body.ToString(); docs.Add(cur); }
                cur = new UnityDoc { FileId = long.Parse(h.Groups[2].Value) };
                body.Clear();
                typeSeen = false;
                continue;
            }
            if (cur == null) continue;

            if (!typeSeen && line.Length > 0 && line[0] != ' ' && line.EndsWith(":"))
            {
                cur.Type = line[..^1];
                typeSeen = true;
                continue;
            }
            body.AppendLine(line);
        }
        if (cur != null) { cur.Body = body.ToString(); docs.Add(cur); }
        return docs;
    }

    /// <summary>Maps every component fileId to the name of the GameObject that owns it.</summary>
    public static Dictionary<long, string> OwnerNames(List<UnityDoc> docs)
    {
        var goNames = docs.Where(d => d.Type == "GameObject")
                          .ToDictionary(d => d.FileId, d => d.Field("m_Name") ?? "");
        var map = new Dictionary<long, string>();
        foreach (var d in docs)
        {
            if (d.Type == "GameObject") continue;
            long go = d.RefField("m_GameObject");
            if (go != 0 && goNames.TryGetValue(go, out var n)) map[d.FileId] = n;
        }
        return map;
    }
}

public readonly record struct Cell(int X, int Y);

public sealed class TilemapLayer
{
    public string Name = "";
    public bool HasCollider;
    public readonly HashSet<Cell> Tiles = new();
}

public static class TilemapExtract
{
    static readonly Regex TileEntry = new(@"^\s*- first: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}", RegexOptions.Compiled);

    public static List<TilemapLayer> Layers(string prefabPath)
    {
        var docs = PrefabReader.Read(prefabPath);
        var owners = PrefabReader.OwnerNames(docs);

        // Which GameObjects carry a TilemapCollider2D — those are the solid layers.
        var solidOwners = new HashSet<long>(
            docs.Where(d => d.Type == "TilemapCollider2D").Select(d => d.RefField("m_GameObject")));

        var layers = new List<TilemapLayer>();
        foreach (var d in docs.Where(d => d.Type == "Tilemap"))
        {
            var layer = new TilemapLayer
            {
                Name = owners.TryGetValue(d.FileId, out var n) ? n : "(unnamed)",
                HasCollider = solidOwners.Contains(d.RefField("m_GameObject")),
            };

            bool inTiles = false;
            foreach (var line in d.Body.Split('\n'))
            {
                if (Regex.IsMatch(line, @"^\s{2}m_Tiles:\s*$")) { inTiles = true; continue; }
                if (inTiles && Regex.IsMatch(line, @"^\s{2}m_\w+:")) { inTiles = false; }
                if (!inTiles) continue;

                var m = TileEntry.Match(line);
                if (m.Success)
                    layer.Tiles.Add(new Cell(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
            }
            layers.Add(layer);
        }
        return layers;
    }
}

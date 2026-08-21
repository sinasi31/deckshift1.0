using System.Text.RegularExpressions;

namespace LevelLab;

/// <summary>
/// Reads the prefab INSTANCES placed inside a level — the props, platforms, decoration and
/// enemies that the tilemap extraction misses. A room's character comes from these as much as
/// from its geometry, so a generated room that only paints tiles will always look bare.
/// </summary>
public static class Objects
{
    static readonly Regex GuidLine = new(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

    /// <summary>Maps every prefab .meta guid to its asset path, so instances can be named.</summary>
    public static Dictionary<string, string> GuidIndex(string assetsRoot)
    {
        var map = new Dictionary<string, string>();
        foreach (var meta in Directory.EnumerateFiles(assetsRoot, "*.prefab.meta", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(meta))
            {
                var m = GuidLine.Match(line);
                if (!m.Success) continue;
                map[m.Groups[1].Value] = meta[..^5];   // strip ".meta"
                break;
            }
        }
        return map;
    }

    public readonly record struct Placement(string Guid, string Name, float X, float Y);

    public static List<Placement> Placements(string levelPrefab, Dictionary<string, string> guids)
    {
        var docs = PrefabReader.Read(levelPrefab);
        var list = new List<Placement>();

        foreach (var d in docs.Where(d => d.Type == "PrefabInstance"))
        {
            var srcLine = Regex.Match(d.Body, @"(?m)^\s*m_SourcePrefab:.*$");
            if (!srcLine.Success) continue;
            var gm = GuidLine.Match(srcLine.Value);
            if (!gm.Success) continue;

            string guid = gm.Groups[1].Value;
            string name = guids.TryGetValue(guid, out var path)
                ? Path.GetFileNameWithoutExtension(path)
                : "(unknown " + guid[..8] + ")";

            float x = 0, y = 0;
            foreach (Match m in Regex.Matches(d.Body,
                     @"propertyPath:\s*m_LocalPosition\.(x|y)\s*\r?\n\s*value:\s*(-?[\d.eE+]+)"))
            {
                if (float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float v))
                {
                    if (m.Groups[1].Value == "x") x = v; else y = v;
                }
            }
            list.Add(new Placement(guid, name, x, y));
        }
        return list;
    }
}

using UnityEngine;

/// <summary>
/// The real pixels, lifted out of the Cainos packs.
///
/// ⚠️ WHY THIS EXISTS AT ALL. Deckshift's UI is supposed to look like it shipped inside the asset
/// packs the rest of the game is built from. Every previous attempt failed the same way: a theme was
/// INVENTED (smoked glass, brass, frost), carefully lit, and rejected — because a colour reasoned
/// from a district's NAME is not the colour the artist actually painted. This asset removes the
/// invention step. Each ramp below is a set of colours sampled straight out of a pack PNG, sorted
/// dark-to-light, so a surface generated from it can only ever be in the pack's palette.
///
/// The FORM is authored (folds, hems, wear, layout); the COLOUR is never authored. That split is the
/// whole idea — see <see cref="Salvage"/> for the two laws that go with it.
///
/// Built by <c>Deckshift → Bake Salvage Art</c>. Loaded by name from Resources at runtime, the same
/// way GateArt is, so nothing has to be wired into a scene and nothing can be lost from one.
/// </summary>
public class SalvageArt : ScriptableObject
{
    public const string ResourcePath = "SalvageArt";

    /// <summary>
    /// A material's colours, sampled from the pack and sorted ASCENDING by luminance.
    /// Index it with a 0..1 noise value and you get pack-authentic mottling for free.
    /// </summary>
    [System.Serializable]
    public class Ramp
    {
        public string id;
        public string source;          // which sheet + sprite it came from, for provenance
        public Color[] steps;          // dark -> light

        public Color Sample(float t)
        {
            if (steps == null || steps.Length == 0) return Color.magenta;   // loud, never silent
            if (steps.Length == 1) return steps[0];
            t = Mathf.Clamp01(t) * (steps.Length - 1);
            int i = Mathf.FloorToInt(t);
            int j = Mathf.Min(i + 1, steps.Length - 1);
            return Color.Lerp(steps[i], steps[j], t - i);
        }

        /// <summary>The value most of the surface actually is.</summary>
        public Color Body => Sample(0.5f);
    }

    public Ramp linen;      // TX Village Props — Cloth 08. The dust sheet.
    public Ramp rope;       // TX Village Props — Clother Hanger Rope 01.
    public Ramp wood;       // TX Dungeon Props  — plank/beam browns.
    public Ramp stone;      // TX Tileable - Dungeon Wall.
    public Ramp iron;       // TX Dungeon Props  — Beam Metal.

    private static SalvageArt cached;

    public static SalvageArt Get()
    {
        if (cached == null) cached = Resources.Load<SalvageArt>(ResourcePath);
        return cached;
    }

    public Ramp ById(string id)
    {
        switch (id)
        {
            case "linen": return linen;
            case "rope": return rope;
            case "wood": return wood;
            case "stone": return stone;
            case "iron": return iron;
        }
        return null;
    }
}

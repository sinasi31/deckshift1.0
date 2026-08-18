# LevelLab

A small C# console tool that measures Deckshift levels. It lives **outside `Assets/`** on
purpose so Unity never tries to compile it.

Requires the .NET SDK (9.0 is installed on this machine). Nothing else — it reads the Unity
YAML directly, so Unity does not have to be open.

```bash
# What the player can really do — jump heights and distances in tiles, from live physics values
dotnet run --project Tools/LevelLab -- metrics

# Validate a level text before showing it to anyone
dotnet run --project Tools/LevelLab -- check Assets/LevelTexts/GenLevel6.txt --map

# Shape metrics for any mix of level texts and hand-built room prefabs
dotnet run --project Tools/LevelLab -- stats Assets/LevelTexts/*.txt Assets/LevelEfeS/efeslevel1.prefab

# Turn a hand-built room's tilemap back into an ASCII grid (reference material)
dotnet run --project Tools/LevelLab -- extract Assets/LevelEfeS/efeslevel3.prefab extracted/efeslevel3.txt

# List the tilemap layers inside a room prefab
dotnet run --project Tools/LevelLab -- layers Assets/LevelEfeVrl/EfeVrl6.prefab
```

`check` exits 0 when a level is clean and 2 when it is not, so it can gate a batch.
Add `--auto` to check a room that has no `S`/`X` markers (extracted hand-built rooms): it
tries every standing cell as a start and reports the best one.

The rules these numbers feed into are in `LevelDesignRules.md` at the project root.
`extracted/` holds ASCII conversions of the hand-built rooms — the reference texture.

## Files

| File | What it does |
|---|---|
| `Sim.cs` | The player's real physics: constants copied from Player.prefab + PlayerController |
| `Grid.cs` | ASCII level grid, from a `.txt` or from a room prefab's tilemaps |
| `Prefab.cs` | Minimal Unity-YAML reader (documents, components, tilemap tiles) |
| `Stats.cs` | Shape metrics: openness, foothold density, ledge run lengths, voids |
| `Check.cs` | Trajectory-based reachability flood fill + the hand-built style bands |
| `Program.cs` | Command line |

If the player gets retuned, update the constants at the top of `Sim.cs` and rerun `metrics`.

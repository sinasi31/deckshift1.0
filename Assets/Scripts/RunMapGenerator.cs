using System.Collections.Generic;
using UnityEngine;

// Builds a RunMap for one act.
//
// The shape is Slay-the-Spire's: a fixed number of routes are CARVED from the bottom of the act to
// the top, one column-step at a time, and wherever two routes land on the same slot they share a
// node and the paths merge. That is what produces a graph that genuinely branches and rejoins,
// rather than N parallel lanes that never interact.
//
// Deterministic for a given seed (System.Random, not UnityEngine.Random — generating a map must not
// disturb the global random sequence the rest of the game is drawing from, and a reproducible seed
// is what makes the generator testable at all).
[System.Serializable]
public class RunMapSettings
{
    [Tooltip("Total rows INCLUDING the hub row and the boss row. 8 = hub + 6 combat floors + boss.")]
    public int floors = 8;

    [Tooltip("Widest the act can get, in columns.")]
    public int width = 5;

    [Tooltip("How many routes are carved bottom-to-top. More routes = a denser, more connected act. " +
             "Below 3 the act stops feeling like a choice; above width it just re-treads slots. " +
             "Matching this to width puts one route in every column, which keeps the middle of the " +
             "act populated — at 4-on-5 no route ever starts in the centre column and the act draws " +
             "as two arcs around an empty middle.")]
    public int pathCount = 5;

    [Range(0f, 1f)]
    [Tooltip("Chance a Fight or Elite carries a recharge room. Skirmishes NEVER can — that " +
             "restriction is the run economy, not a tuning value.")]
    public float rechargeChance = 0.35f;

    [Tooltip("Guarantee at least one Foundry and one Market exist somewhere in the act. Note this " +
             "guarantees they EXIST, not that any single route reaches them — that tension is the point.")]
    public bool guaranteeCoreRecharges = true;

    // Which recharge types this run is allowed to place. RunMapManager narrows this to the ones
    // LevelManager actually has a room prefab for.
    //
    // THE MAP MUST NEVER PROMISE SOMETHING IT CANNOT DELIVER. A Foundry icon on a branch the player
    // routes three floors to reach, which then spawns nothing because the prefab slot is empty, is
    // worse than no Foundry at all — it spends the player's Shift on a lie. None of the three
    // recharge rooms are built yet, so today this list resolves to empty and no recharge icons are
    // drawn; each one starts appearing on its own the moment its prefab is assigned.
    [HideInInspector]
    public List<RechargeType> allowedRecharges = new List<RechargeType>
    {
        RechargeType.Foundry, RechargeType.Market, RechargeType.Well
    };
}

public static class RunMapGenerator
{
    private struct Edge { public int from, to; }

    public static RunMap Generate(RunMapSettings s, int seed)
    {
        if (s == null) s = new RunMapSettings();

        int floors = Mathf.Max(3, s.floors);
        int width = Mathf.Max(1, s.width);
        int paths = Mathf.Clamp(s.pathCount, 1, Mathf.Max(1, width));

        System.Random rng = new System.Random(seed);

        RunMap map = new RunMap { floors = floors, seed = seed };

        // Slot grid: which node id occupies (floor, column), or -1. Sharing a slot is how two
        // carved routes merge into one node.
        int[,] slot = new int[floors, width];
        for (int f = 0; f < floors; f++)
            for (int c = 0; c < width; c++)
                slot[f, c] = -1;

        int mid = width / 2;
        MapNode start = NewNode(map, slot, 0, mid, MapNodeType.Start);
        MapNode boss = NewNode(map, slot, floors - 1, mid, MapNodeType.Boss);

        // Edges already carved between each pair of floors, used for the anti-crossing rule.
        List<Edge>[] carved = new List<Edge>[floors];
        for (int f = 0; f < floors; f++) carved[f] = new List<Edge>();

        int topCombatFloor = floors - 2;

        // Carve each route across the combat floors. Starting columns are spread so the act opens
        // wide instead of every route beginning in the same slot.
        for (int p = 0; p < paths; p++)
        {
            int col = paths == 1 ? mid : Mathf.RoundToInt((float)p * (width - 1) / (paths - 1));
            col = Mathf.Clamp(col, 0, width - 1);

            EnsureNode(map, slot, 1, col, MapNodeType.Skirmish);

            for (int f = 1; f < topCombatFloor; f++)
            {
                int nextCol = PickNextColumn(rng, carved[f], col, width);
                EnsureNode(map, slot, f + 1, nextCol, MapNodeType.Skirmish);
                Connect(map, slot, f, col, f + 1, nextCol, carved[f]);
                col = nextCol;
            }
        }

        // The hub feeds every opening node, and every top combat node feeds the boss, so the act
        // always has exactly one entrance and one exit.
        foreach (MapNode n in map.NodesOnFloor(1)) Link(start, n);
        foreach (MapNode n in map.NodesOnFloor(topCombatFloor)) Link(n, boss);

        AssignCombatTypes(map, rng, topCombatFloor);
        AttachRechargeRooms(map, rng, s, topCombatFloor);

        return map;
    }

    // A route steps at most one column sideways per floor. The candidate is rejected if it would
    // CROSS an edge already carved between the same two floors: crossed lines are unreadable on the
    // map and imply a connection that isn't there.
    private static int PickNextColumn(System.Random rng, List<Edge> carvedHere, int col, int width)
    {
        List<int> candidates = new List<int>();
        for (int d = -1; d <= 1; d++)
        {
            int c = col + d;
            if (c < 0 || c >= width) continue;
            if (Crosses(carvedHere, col, c)) continue;
            candidates.Add(c);
        }

        // Every sideways option crossed something; going straight up never crosses anything.
        if (candidates.Count == 0) return col;
        return candidates[rng.Next(candidates.Count)];
    }

    private static bool Crosses(List<Edge> edges, int from, int to)
    {
        foreach (Edge e in edges)
        {
            if (e.from < from && e.to > to) return true;
            if (e.from > from && e.to < to) return true;
        }
        return false;
    }

    private static MapNode NewNode(RunMap map, int[,] slot, int floor, int col, MapNodeType type)
    {
        MapNode n = new MapNode { id = map.nodes.Count, floor = floor, column = col, type = type };
        map.nodes.Add(n);
        slot[floor, col] = n.id;
        return n;
    }

    private static MapNode EnsureNode(RunMap map, int[,] slot, int floor, int col, MapNodeType type)
    {
        int id = slot[floor, col];
        if (id >= 0) return map.Get(id);
        return NewNode(map, slot, floor, col, type);
    }

    private static void Connect(RunMap map, int[,] slot, int f0, int c0, int f1, int c1, List<Edge> carvedHere)
    {
        MapNode a = map.Get(slot[f0, c0]);
        MapNode b = map.Get(slot[f1, c1]);
        if (a == null || b == null) return;

        if (Link(a, b)) carvedHere.Add(new Edge { from = c0, to = c1 });
    }

    // Returns true only if this edge was new, so callers don't record a duplicate.
    private static bool Link(MapNode a, MapNode b)
    {
        if (a.next.Contains(b.id)) return false;
        a.next.Add(b.id);
        b.prev.Add(a.id);
        return true;
    }

    // Difficulty ramps with depth: the act opens on Skirmishes and Elites only become likely
    // later. Elite weight is zero on the first combat floor by construction (t = 0), so the player
    // is never asked to take an Elite before they have had a chance to build anything.
    private static void AssignCombatTypes(RunMap map, System.Random rng, int topCombatFloor)
    {
        int combatFloors = topCombatFloor;   // floors 1..topCombatFloor inclusive

        for (int f = 1; f <= topCombatFloor; f++)
        {
            float t = combatFloors <= 1 ? 0f : (float)(f - 1) / (combatFloors - 1);

            float wSkirmish = Mathf.Lerp(0.70f, 0.10f, t);
            float wFight = Mathf.Lerp(0.30f, 0.45f, t);
            float wElite = Mathf.Lerp(0.00f, 0.45f, t);

            List<MapNode> row = map.NodesOnFloor(f);
            foreach (MapNode n in row)
                n.type = WeightedType(rng, wSkirmish, wFight, wElite);

            BreakUniformFloor(rng, row);
        }
    }

    // A floor where every branch is the same type offers no decision — it is a toll, not a choice,
    // and on the late floors the weights produce all-Elite rows often. Since difficulty IS the node
    // type here, a uniform row defeats the whole reason the map exists.
    //
    // Nudging ONE node one step EASIER is the right correction rather than re-rolling the row: it
    // guarantees a way through that isn't the hardest option, without flattening the ramp or
    // touching the floors that are legitimately uniform because they only have one node.
    private static void BreakUniformFloor(System.Random rng, List<MapNode> row)
    {
        if (row.Count < 2) return;

        MapNodeType first = row[0].type;
        foreach (MapNode n in row) if (n.type != first) return;

        MapNode victim = row[rng.Next(row.Count)];
        switch (first)
        {
            case MapNodeType.Elite: victim.type = MapNodeType.Fight; break;
            case MapNodeType.Fight: victim.type = MapNodeType.Skirmish; break;
            // An all-Skirmish row is the one case where the outlier goes the other way: there is
            // nothing easier than a Skirmish, so the choice has to be an opt-IN to danger.
            default: victim.type = MapNodeType.Fight; break;
        }
    }

    private static MapNodeType WeightedType(System.Random rng, float wS, float wF, float wE)
    {
        float total = wS + wF + wE;
        if (total <= 0f) return MapNodeType.Skirmish;

        double roll = rng.NextDouble() * total;
        if (roll < wS) return MapNodeType.Skirmish;
        if (roll < wS + wF) return MapNodeType.Fight;
        return MapNodeType.Elite;
    }

    // Recharge rooms hang off Fight and Elite nodes only. Within a floor the generator avoids
    // handing out the same type twice, so a floor offers a CHOICE between different problems being
    // solved rather than the same one on two branches.
    private static void AttachRechargeRooms(RunMap map, System.Random rng, RunMapSettings s, int topCombatFloor)
    {
        // Only types this run can actually spawn — see RunMapSettings.allowedRecharges. With none
        // available the act simply carries no recharge rooms, which is honest, rather than drawing
        // icons that lead nowhere.
        List<RechargeType> all = new List<RechargeType>();
        if (s.allowedRecharges != null)
            foreach (RechargeType t in s.allowedRecharges)
                if (t != RechargeType.None && !all.Contains(t)) all.Add(t);

        if (all.Count == 0) return;

        for (int f = 1; f <= topCombatFloor; f++)
        {
            List<RechargeType> unusedThisFloor = new List<RechargeType>(all);

            foreach (MapNode n in map.NodesOnFloor(f))
            {
                if (!n.CanCarryRecharge) continue;
                if (rng.NextDouble() > s.rechargeChance) continue;

                if (unusedThisFloor.Count == 0) unusedThisFloor.AddRange(all);
                int pick = rng.Next(unusedThisFloor.Count);
                n.recharge = unusedThisFloor[pick];
                unusedThisFloor.RemoveAt(pick);
            }
        }

        if (!s.guaranteeCoreRecharges) return;

        // A run with nowhere to spend gold or repair a card is a dead run, so force those two in if
        // the rolls did not produce them — but only if they're spawnable at all. Deliberately NOT a
        // guarantee that any single route reaches one: choosing whether to detour for it is the
        // decision the map exists to pose.
        if (all.Contains(RechargeType.Foundry)) EnsureExists(map, rng, RechargeType.Foundry);
        if (all.Contains(RechargeType.Market)) EnsureExists(map, rng, RechargeType.Market);
    }

    private static void EnsureExists(RunMap map, System.Random rng, RechargeType want)
    {
        List<MapNode> eligible = new List<MapNode>();

        foreach (MapNode n in map.nodes)
        {
            if (!n.CanCarryRecharge) continue;
            if (n.recharge == want) return;                 // already present, nothing to do
            if (n.recharge == RechargeType.None) eligible.Add(n);
        }

        if (eligible.Count == 0) return;   // no Fight/Elite free to carry it; not worth forcing
        eligible[rng.Next(eligible.Count)].recharge = want;
    }
}

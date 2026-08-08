using System.Collections.Generic;
using UnityEngine;

// Every RelicData in the project, in one asset, so nothing has to maintain a relic list by hand.
//
// WHY THIS EXISTS: the shop and the chests each carried their own Inspector list of relics, and
// both had silently fallen behind the actual roster — 18 relics existed, the shop offered 3 and the
// chest prefab 5. Nothing was broken in code; the lists were just never updated when relics were
// added, and there is no way to notice that from inside the game. A hand-maintained duplicate of
// something the project already knows is a bug waiting on the calendar.
//
// The catalogue is rebuilt automatically whenever a RelicData asset is added, removed or renamed
// (see Editor/RelicCatalogueBuilder), so "the shop doesn't have all the relics" cannot recur.
//
// It lives in Resources/ ONLY so runtime code can reach it with no scene wiring — the same
// self-bootstrapping the HUDs use. Do not add other assets to Resources/ casually; everything in
// there ships in the build whether or not it is referenced.
public class RelicCatalogue : ScriptableObject
{
    public const string ResourcePath = "RelicCatalogue";

    [Tooltip("Auto-generated. Rebuilt by Deckshift → Rebuild Relic Catalogue and on any RelicData " +
             "asset change. Editing this by hand is pointless; it will be overwritten.")]
    public List<RelicData> all = new List<RelicData>();
}

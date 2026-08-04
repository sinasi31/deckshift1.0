using UnityEngine;

// Guarantees that the player and the enemies always draw IN FRONT of level decoration.
//
// WHY THIS IS NEEDED AT ALL — the Cainos "3D Lit" props and the Cainos character rigs both render
// with OPAQUE shaders (render queue < 2500). Opaque geometry sorts by camera DEPTH, so
// SpriteRenderer.sortingOrder is ignored for them and the only thing that decides who draws on top
// is world Z. Two opaque things at the same Z sort arbitrarily, which is why the symptom was
// "SOMETIMES the player looks like it's behind a prop".
//
// WHY IT KEPT HAPPENING — measured across all 11 pool rooms (2026-08-08), there was no play plane.
// Every room had invented its own depth:
//
//     efeslevel1   spawn z 0.00,  enemies 0.00,  frontmost prop -0.01   (prop in front of enemies)
//     EfeVrl4      spawn z 0.00,  enemies 0.00,  frontmost prop  0.00   (exactly coplanar)
//     EfeVrl7      spawn z 2.00,  enemies 2.00,  frontmost prop  0.00   (props in front of both)
//     efeslevel3   spawn z 2.56 … hub spawn z -1.06 …
//
// The player's Z comes from whatever that room's GirisNoktasi happens to sit at (LevelManager
// copies the full Vector3), enemies sat wherever they were dropped, and props ranged from -1.12 to
// +3.56. Sorting was therefore luck, per room. Fixing individual props — as was done once for the
// entry door — only ever fixes the one prop somebody noticed.
//
// THE RULE: actors live on this plane; everything else is behind it. Enforced at room spawn, so it
// holds no matter what Z a prop is authored at, in rooms that already exist and in rooms nobody has
// built yet.
//
// Z IS FREE TO CHANGE. The camera is orthographic, so moving something along Z does not move it on
// screen by even a pixel, and Physics2D ignores Z entirely. Only the draw order changes.
public static class PlayPlane
{
    // Camera looks along +Z (it sits at negative Z), so SMALLER Z draws in FRONT.
    // -2 is comfortably ahead of the frontmost thing found in any room (-1.12, the hub
    // shopkeeper's fork), leaving room for decoration to be authored anywhere from -1 backwards.
    public const float Z = -2f;

    // How far behind the plane a prop gets pushed when it is found at or in front of it. Small on
    // purpose: it only has to break the tie, and props keep their relative ordering with each other.
    private const float PushBack = 0.25f;

    // Put one actor on the plane. Safe to call every frame if ever needed, but once per spawn is
    // enough — nothing in the game writes Z afterwards.
    public static void Snap(Transform t)
    {
        if (t == null) return;
        Vector3 p = t.position;
        if (Mathf.Approximately(p.z, Z)) return;
        t.position = new Vector3(p.x, p.y, Z);
    }

    // Apply the rule to a freshly spawned room: every enemy onto the plane, and any opaque prop
    // sitting at or in front of the plane pushed behind it.
    public static void Apply(GameObject roomRoot)
    {
        if (roomRoot == null) return;

        // 1) Actors onto the plane. EnemyHealth is the honest test for "this is a combatant" —
        //    enemies are split across two layers (see the layer-convention note in CLAUDE.md), so a
        //    layer mask would miss half of them.
        foreach (EnemyHealth enemy in roomRoot.GetComponentsInChildren<EnemyHealth>(true))
            Snap(enemy.transform);

        // 2) Anything opaque still at or in front of the plane, that is NOT an actor, goes behind
        //    it. This is the catch-all that makes new rooms safe without anyone remembering.
        foreach (Renderer r in roomRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;

            Material mat = r.sharedMaterial;
            if (mat == null || mat.renderQueue >= 2500) continue;   // transparent: sorts by order, not depth

            if (r.GetComponentInParent<EnemyHealth>() != null) continue;
            if (r.GetComponentInParent<PlayerController>() != null) continue;

            Transform t = r.transform;
            if (t.position.z > Z + 0.001f) continue;                 // already behind the plane

            // Move the highest ancestor that is still inside the room, so a multi-part prop keeps
            // its pieces together instead of being taken apart one renderer at a time.
            Transform move = t;
            while (move.parent != null && move.parent != roomRoot.transform
                   && move.parent.GetComponentInParent<EnemyHealth>() == null)
                move = move.parent;

            Vector3 p = move.position;
            move.position = new Vector3(p.x, p.y, Z + PushBack);
        }
    }
}

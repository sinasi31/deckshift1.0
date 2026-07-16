using System.Collections.Generic;
using UnityEngine;

// House-style afterimage (no prefab, no art, self-building, self-destroying — same pattern as
// ShockwaveVFX / EnemyHealthBar). Snapshots every visible SpriteRenderer under a source
// transform into faded, tinted copies that hang in world space and fade out, leaving a
// motion-blur streak of the ACTUAL layered character. Used by the dash.
public class DashAfterimage : MonoBehaviour
{
    // Spawns one frozen snapshot of `source`'s sprites at their current world pose.
    public static void Spawn(Transform source, Color tint, float life = 0.28f, int sortingOffset = -1)
    {
        if (source == null) return;
        SpriteRenderer[] srcRenderers = source.GetComponentsInChildren<SpriteRenderer>();
        if (srcRenderers.Length == 0) return;

        var go = new GameObject("DashAfterimage");
        var img = go.AddComponent<DashAfterimage>();
        img.life = life;
        img.maxLife = life;
        img.startAlpha = tint.a;
        img.ghosts = new List<SpriteRenderer>();

        foreach (SpriteRenderer sr in srcRenderers)
        {
            if (sr.sprite == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;

            var child = new GameObject(sr.name + "_ghost");
            child.transform.SetParent(go.transform, false);
            child.transform.position = sr.transform.position;
            child.transform.rotation = sr.transform.rotation;
            child.transform.localScale = sr.transform.lossyScale; // carries the facing-flip sign

            var g = child.AddComponent<SpriteRenderer>();
            g.sprite = sr.sprite;
            g.flipX = sr.flipX;
            g.flipY = sr.flipY;
            g.sortingLayerID = sr.sortingLayerID;
            g.sortingOrder = sr.sortingOrder + sortingOffset;
            g.color = tint;
            img.ghosts.Add(g);
        }

        if (img.ghosts.Count == 0) Destroy(go);
    }

    private List<SpriteRenderer> ghosts;
    private float life, maxLife, startAlpha;

    private void Update()
    {
        life -= Time.deltaTime;
        float a = startAlpha * Mathf.Clamp01(life / maxLife);
        foreach (SpriteRenderer r in ghosts)
        {
            if (r == null) continue;
            Color c = r.color;
            c.a = a;
            r.color = c;
        }
        if (life <= 0f) Destroy(gameObject);
    }
}

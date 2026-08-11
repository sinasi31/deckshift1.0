using UnityEngine;

// Grit kicked off the wall while sliding down it.
//
// The Cainos pack has no wall-slide clip, so the pose is borrowed from its LADDER CLIMB layer,
// frozen on one frame (see PlayerController.UpdateWallSlideAnimation). A frozen pose alone reads as
// the character being STUCK to the wall rather than moving down it — nothing on screen is telling
// you which way you're travelling. This is what supplies that: motes scraped loose at the contact
// point, drifting UP relative to the player because the player is going down.
//
// House pattern — procedural sprite built once and cached, no prefab and no art file, same shape as
// DashAfterimage and ScrapPickup. Each mote owns its own lifetime and destroys itself.
public class WallScrapeVFX : MonoBehaviour
{
    private static Sprite dotSprite;

    private SpriteRenderer sr;
    private Vector2 velocity;
    private float age, life;
    private float startAlpha, startScale;

    // `dirX` points INTO the wall, so the grit is thrown back out away from it.
    public static void Spawn(Vector2 worldPos, float dirX, int sortingLayerID, int sortingOrder)
    {
        GameObject go = new GameObject("WallScrapeMote");
        go.transform.position = worldPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDot();
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;

        // Dusty stone, not sparks — this is rock, and a bright spark would read as metal on metal.
        //
        // ⚠️ Pitched much brighter than "dust" suggests, because these render through the scene's
        // 0.5-intensity global Light2D like every other world sprite: a plausible dust value came
        // out at half strength against an already-dark wall and read as dirt on the lens. Same
        // lesson as the deep-rock tint — multiply by the light FIRST, then pick.
        sr.color = new Color(0.95f, 0.90f, 0.80f, 1f);

        WallScrapeVFX m = go.AddComponent<WallScrapeVFX>();
        m.sr = sr;
        m.life = Random.Range(0.22f, 0.42f);
        m.startAlpha = Random.Range(0.65f, 1f);
        m.startScale = Random.Range(0.09f, 0.18f);

        // Mostly UPWARD in screen terms: the player is falling past this point, so grit left behind
        // appears to rise. A little kick away from the wall keeps it from drawing inside the tiles.
        m.velocity = new Vector2(-dirX * Random.Range(0.4f, 1.3f), Random.Range(1.4f, 3.2f));

        go.transform.localScale = Vector3.one * m.startScale;
        go.AddComponent<TemporaryObject>();   // never survive a room change
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }

        float t = age / life;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= 0.92f;                                   // air drag, so it settles rather than flies

        transform.localScale = Vector3.one * startScale * (1f - t * 0.55f);

        Color c = sr.color;
        c.a = startAlpha * (1f - t * t);                     // holds, then drops away late
        sr.color = c;
    }

    private static Sprite GetDot()
    {
        if (dotSprite != null) return dotSprite;

        const int S = 8;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;                   // pixel art — a soft dot would look foreign
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
            }
        tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 16f);
        return dotSprite;
    }
}

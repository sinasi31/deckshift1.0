using UnityEngine;

// A collectible scrap shard. Entirely self-built: the GameObject, its sprite and its collider are
// all created in code by SpawnBurst, so there is NO prefab to wire up and nothing that can go
// missing from a scene. Same house pattern as SpitGlob / DashAfterimage / EnemyHealthBar.
//
// Deliberately has NO Rigidbody2D. The pop-out arc is integrated by hand against a ground raycast
// taken at spawn. Real physics would need a solid collider to land on, and a solid collider on a
// pickup bumps the player's capsule around; a trigger-only rigidbody would fall through the floor
// forever. Hand-integrating sidesteps the whole physics-layer question, which in this project is
// exactly the kind of setup detail that silently breaks.
//
// Carries TemporaryObject, so LevelManager wipes uncollected shards when the room changes —
// scrap you didn't pick up is scrap you didn't earn.
public class ScrapPickup : MonoBehaviour
{
    private int amount = 1;

    // arc state
    private Vector2 velocity;
    private float floorY;
    private bool landed;
    private float bobPhase;
    private float landedScaleBase = 1f;

    private const float GRAVITY = -22f;
    private const float LIFETIME = 60f;   // safety net so a shard can never linger forever

    private SpriteRenderer sr;
    private static Sprite cachedShardSprite;

    // Splits `totalScrap` into a few shards and pops them out of `pos`. Small amounts stay as a
    // single shard; larger drops fan out so a big kill reads as a bigger payout.
    public static void SpawnBurst(Vector3 pos, int totalScrap)
    {
        if (totalScrap <= 0) return;

        int shards = Mathf.Clamp(totalScrap, 1, 5);
        int per = totalScrap / shards;
        int remainder = totalScrap - (per * shards);

        for (int i = 0; i < shards; i++)
        {
            int carried = per + (i < remainder ? 1 : 0);
            if (carried <= 0) continue;
            Spawn(pos, carried, shards);
        }
    }

    private static void Spawn(Vector3 pos, int carried, int siblingCount)
    {
        GameObject go = new GameObject("ScrapPickup");
        go.transform.position = pos;

        ScrapPickup p = go.AddComponent<ScrapPickup>();
        p.amount = carried;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetShardSprite();
        sr.color = Color.white;              // colour is baked into the sprite
        sr.sortingOrder = 50;                // above level tiles, below UI
        p.sr = sr;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.42f;                  // generous — collection should never feel finicky

        go.AddComponent<TemporaryObject>();

        // Fan the burst outward: more shards means a wider spread.
        float spread = siblingCount > 1 ? 3.6f : 1.6f;
        p.velocity = new Vector2(Random.Range(-spread, spread), Random.Range(5f, 8f));
        p.floorY = FindFloor(pos);
        p.bobPhase = Random.Range(0f, Mathf.PI * 2f);

        float s = Random.Range(0.85f, 1.1f);
        p.landedScaleBase = s;
        go.transform.localScale = Vector3.one * s;

        Destroy(go, LIFETIME);
    }

    // Where this shard will come to rest. Ground is layer 3; the name lookup is preferred so a
    // reordered layer list still works, with the literal as a fallback if it's ever renamed
    // (GetMask returns 0 for an unknown name, which would leave shards falling forever).
    private static float FindFloor(Vector3 from)
    {
        int mask = LayerMask.GetMask("Ground");
        if (mask == 0) mask = 1 << 3;

        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, 30f, mask);
        return hit.collider != null ? hit.point.y + 0.25f : from.y - 1.5f;
    }

    private void Update()
    {
        if (!landed)
        {
            velocity.y += GRAVITY * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, velocity.x * 40f * Time.deltaTime);

            if (transform.position.y <= floorY && velocity.y < 0f)
            {
                Vector3 p = transform.position;
                p.y = floorY;
                transform.position = p;

                // One small bounce, then settle.
                if (Mathf.Abs(velocity.y) > 2.5f)
                {
                    velocity.y *= -0.35f;
                    velocity.x *= 0.5f;
                }
                else
                {
                    landed = true;
                    transform.rotation = Quaternion.identity;
                }
            }
        }
        else
        {
            // Idle bob + a slow glint pulse so shards stay visible against busy tilework.
            bobPhase += Time.deltaTime * 3.2f;
            Vector3 p = transform.position;
            p.y = floorY + Mathf.Sin(bobPhase) * 0.09f;
            transform.position = p;

            if (sr != null)
            {
                float glint = 0.86f + 0.14f * Mathf.Sin(bobPhase * 1.7f);
                sr.color = new Color(glint, glint, glint, 1f);
            }
        }

        // Scrap Magnet relic pulls the shard toward the player when in range (no-op without it).
        ScrapMagnet.Attract(transform);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) player = other.GetComponentInParent<PlayerController>();
        if (player != null) player.AddScrap(amount);

        // Positional, like the gold pickup — a local "tink" at the shard, not a 2D global sound.
        Vector3 soundPos = transform.position;
        if (Camera.main != null) soundPos.z = Camera.main.transform.position.z;
        SfxManager.PlayAtPoint(ProcSfx.ScrapPickup, soundPos, 0.5f);

        Destroy(gameObject);
    }

    // The shard sprite, shared with the HUD counter so the number on screen and the thing on the
    // floor are visibly the same currency.
    public static Sprite ShardSprite => GetShardSprite();

    // A jagged little iron offcut, generated once and shared by every shard. Drawn as a rough
    // quadrilateral chunk with a lit top-left edge and a dark underside so it reads as metal
    // rather than a blob, at the ~20px it actually occupies on screen.
    private static Sprite GetShardSprite()
    {
        if (cachedShardSprite != null) return cachedShardSprite;

        const int S = 24;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color body = ScrapEconomy.ScrapColor;
        Color lit = Color.Lerp(body, Color.white, 0.45f);
        Color dark = Color.Lerp(body, Color.black, 0.45f);
        Color edge = Color.Lerp(body, Color.black, 0.7f);

        // Chunk outline: a lopsided hexagon, deliberately asymmetric so it looks torn.
        Vector2[] poly =
        {
            new Vector2(0.18f, 0.42f), new Vector2(0.36f, 0.80f), new Vector2(0.70f, 0.86f),
            new Vector2(0.86f, 0.56f), new Vector2(0.68f, 0.18f), new Vector2(0.30f, 0.14f)
        };

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                if (!InPolygon(uv, poly)) { tex.SetPixel(x, y, clear); continue; }

                // Shade by position: upper-left catches light, lower-right falls into shadow.
                float shade = Mathf.Clamp01((uv.x * -0.5f + uv.y * 1.0f) + 0.25f);
                Color c = shade > 0.72f ? lit : (shade < 0.34f ? dark : body);

                // Darken the rim so the silhouette stays crisp on light tiles.
                if (!InPolygon(uv + new Vector2(0.055f, 0f), poly) ||
                    !InPolygon(uv - new Vector2(0.055f, 0f), poly) ||
                    !InPolygon(uv + new Vector2(0f, 0.055f), poly) ||
                    !InPolygon(uv - new Vector2(0f, 0.055f), poly))
                    c = edge;

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        cachedShardSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S / 0.55f);
        return cachedShardSprite;
    }

    private static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }
}

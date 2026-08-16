using UnityEngine;

// The Wizard's innate projectile. House pattern: built ENTIRELY in code (sprite included), like
// ScrapPickup and the procedural UI — so there is no prefab to wire and nothing to lose out of a
// scene. Spawn it with ArcaneBolt.Spawn(...).
//
// The bolt does FLAT damage, so it has one look. It had a size/colour ramp while its damage scaled
// with hand emptiness; when the scaling was cut, the ramp went with it — a bolt that varied in
// appearance without varying in effect would be a telegraph pointing at nothing.
public class ArcaneBolt : MonoBehaviour
{
    private float damage;
    private bool hasHit;

    private static Sprite cachedSprite;

    // Cold arcane violet — deliberately nowhere near Fireball's orange. The innate must never be
    // mistaken for the card firing itself for free.
    private static readonly Color Tint = new Color(0.72f, 0.68f, 0.98f, 1f);
    private const float SIZE = 0.6f;

    public static ArcaneBolt Spawn(Vector3 pos, bool facingRight, float damage, float speed = 20f)
    {
        var go = new GameObject("ArcaneBolt");
        go.transform.position = pos;

        // PlayerProjectile (layer 10) — the same layer the Fireball uses, so it inherits whatever
        // the collision matrix already says about player shots.
        int layer = LayerMask.NameToLayer("PlayerProjectile");
        if (layer >= 0) go.layer = layer;

        // Swept out of the scene on a room change along with everything else spawned at runtime.
        // Without this the bolt is a scene-root object and outlives the room that fired it.
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoltSprite();
        sr.color = Tint;
        sr.sortingOrder = 5;

        go.transform.localScale = Vector3.one * SIZE;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.34f;      // in local units, so the hitbox grows with the charge too
        col.isTrigger = true;

        var bolt = go.AddComponent<ArcaneBolt>();
        bolt.damage = damage;

        float dir = facingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * speed, 0f);
        go.transform.rotation = facingRight ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

        bolt.BuildTrail(sr);
        Destroy(go, 2.2f);
        return bolt;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (other.GetComponent<Portal>() != null) return;
        if (other.GetComponentInParent<PlayerController>() != null) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            hasHit = true;

            // ⚠️ ATTRIBUTION IS EXPLICITLY CLEARED, NOT LEFT ALONE. No card fired this, so no card's
            // blessing may claim its damage or its kill. AttributedCard should already be null here,
            // but "should already be" is exactly how a stale attribution gets away with it.
            RuntimeCard prev = DeckManager.instance != null ? DeckManager.instance.AttributedCard : null;
            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = null;

            // Relics still apply (Whetstone, Midas Recoil, Glass Heart). The innate is the player's
            // attack, so a relic that sharpens their attacks should sharpen this one.
            float dealt = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damage, target as EnemyHealth)
                : damage;
            target.TakeDamage(dealt);

            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = prev;

            Impact();
            return;
        }

        // Terrain. Triggers (pickups, zones) are passed straight through.
        if (other.isTrigger) return;
        hasHit = true;
        Impact();
    }

    private void Impact()
    {
        SpawnFlash(transform.position, GetComponent<SpriteRenderer>().color, transform.localScale.x);
        Destroy(gameObject);
    }

    private void BuildTrail(SpriteRenderer sr)
    {
        var trailGO = new GameObject("BoltTrail");
        trailGO.transform.SetParent(transform, false);

        var trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.startWidth = 0.7f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.03f;
        trail.autodestruct = false;
        trail.numCapVertices = 4;
        trail.sortingOrder = sr.sortingOrder - 1;
        trail.material = new Material(Shader.Find("Sprites/Default"));

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(sr.color, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    // A brief expanding puff at the point of impact. Its own object, because the bolt is destroyed
    // on the same frame — the same rule EnemyHealth.Die's VFX has to follow.
    private static void SpawnFlash(Vector3 pos, Color colour, float scale)
    {
        var go = new GameObject("BoltImpact");
        go.transform.position = pos;
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoltSprite();
        sr.color = colour;
        sr.sortingOrder = 6;

        go.AddComponent<BoltImpactFade>().Init(scale);
    }

    // A hot white core inside a soft violet halo. One cached texture serves every bolt; the charge
    // is carried by SpriteRenderer.color and the transform scale.
    private static Sprite GetBoltSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                var d = new Vector2(x + 0.5f - half, y + 0.5f - half);

                // Slightly elongated along travel so it reads as a bolt rather than a ball.
                float dist = new Vector2(d.x / 1.45f, d.y).magnitude / half;

                float halo = Mathf.Clamp01(1f - dist / 0.95f);
                float core = Mathf.Clamp01(1f - dist / 0.42f);

                float a = halo * halo * 0.75f + core * core;
                if (a <= 0.002f) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }

                // Written white and tinted at runtime by SpriteRenderer.color. The centre is left
                // denser than the halo so a dimmed bolt still has a bright core to read against —
                // a flat blob loses all of its punch the moment the tint darkens it.
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a) * (0.55f + 0.45f * core)));
            }
        }
        tex.Apply();

        cachedSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return cachedSprite;
    }
}

// Expand-and-fade for the impact puff. Unscaled-time-agnostic on purpose: this happens in the
// world, so a HitStop freeze should hold it still like everything else.
public class BoltImpactFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private float t;
    private float from, to;
    private const float LIFE = 0.18f;

    public void Init(float scale)
    {
        from = scale * 0.9f;
        to = scale * 2.6f;
        transform.localScale = Vector3.one * from;
    }

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / LIFE);

        transform.localScale = Vector3.one * Mathf.Lerp(from, to, k * k);

        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f - k;
            sr.color = c;
        }

        if (k >= 1f) Destroy(gameObject);
    }
}

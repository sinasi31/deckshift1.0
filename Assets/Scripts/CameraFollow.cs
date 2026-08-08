using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow instance;

    [Header("Takip Ayarları")]
    public Transform target;
    public Vector2 offset;

    [Header("Cinematic Focus")]
    [Tooltip("Seconds for the camera to ease onto / off a focus target (boss awaken, etc.).")]
    public float focusSmoothTime = 0.28f;

    private BoxCollider2D[] zones;
    private BoxCollider2D activeZone;

    // Cinematic focus: temporarily pan toward another transform (e.g. the boss on awaken),
    // then release back to the player. Applied as a post-clamp offset like shake/peek so it
    // reads as a smooth pan without disturbing normal follow state.
    private Transform focusTarget;
    private float focusWeight;         // 0 = on player, 1 = fully on focusTarget
    private float focusTargetWeight;   // where focusWeight is easing toward
    private float focusVel;            // SmoothDamp velocity

    private void Awake()
    {
        instance = this;
    }

    // Pan the camera onto a target (holds until ReleaseFocus). Null-safe to call.
    public void FocusOn(Transform t)
    {
        focusTarget = t;
        focusTargetWeight = 1f;
    }

    // Ease the camera back to the player.
    public void ReleaseFocus()
    {
        focusTargetWeight = 0f;
    }

    public void SetZones(BoxCollider2D[] newZones)
    {
        zones = newZones;
        activeZone = null;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdateActiveZone();

        Vector3 pos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        if (activeZone != null)
        {
            Bounds b = activeZone.bounds;
            float halfH = Camera.main.orthographicSize;
            float halfW = halfH * Camera.main.aspect;

            pos.x = Mathf.Clamp(pos.x, b.min.x + halfW, b.max.x - halfW);
            pos.y = Mathf.Clamp(pos.y, b.min.y + halfH, b.max.y - halfH);
        }

        // Shake and peek offsets applied after clamping so zone edges don't suppress them
        Vector3 shakeOffset = CameraShake.instance != null ? (Vector3)CameraShake.instance.shakeOffset : Vector3.zero;
        Vector3 peekOffset = CameraPeek.instance != null ? (Vector3)CameraPeek.instance.peekOffset : Vector3.zero;

        // Cinematic focus offset (unscaled so it pans through hit-stop freezes).
        focusWeight = Mathf.SmoothDamp(focusWeight, focusTargetWeight, ref focusVel,
                                       Mathf.Max(0.01f, focusSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
        Vector3 focusOffset = Vector3.zero;
        if (focusTarget != null && focusWeight > 0.001f)
            focusOffset = (Vector3)(Vector2)(focusTarget.position - target.position) * focusWeight;

        transform.position = pos + shakeOffset + peekOffset + focusOffset;
    }

    private void UpdateActiveZone()
    {
        if (zones == null || zones.Length == 0) return;

        if (activeZone != null && activeZone.OverlapPoint(target.position))
            return;

        foreach (var zone in zones)
        {
            if (zone.OverlapPoint(target.position))
            {
                activeZone = zone;
                return;
            }
        }

        float minDist = float.MaxValue;
        foreach (var zone in zones)
        {
            Vector2 closest = zone.ClosestPoint(target.position);
            float dist = Vector2.Distance(closest, target.position);
            if (dist < minDist)
            {
                minDist = dist;
                activeZone = zone;
            }
        }
    }
}
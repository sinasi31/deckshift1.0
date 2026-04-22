using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Takip Ayarları")]
    public Transform target;
    public Vector2 offset;

    private BoxCollider2D[] zones;
    private BoxCollider2D activeZone;

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

        transform.position = pos;
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
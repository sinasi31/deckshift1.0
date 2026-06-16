using UnityEngine;

/// Attach to the Main Camera.
public class JaceCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3   offset      = new Vector3(0f, 1.5f, -10f);

    [Header("Smoothing")]
    public float smoothSpeed = 6f;

    [Header("Bounds (optional)")]
    public bool  useBounds;
    public float minX, maxX, minY, maxY;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
        }

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}

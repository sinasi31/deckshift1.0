using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;

    // Sýnýrlarý Editörde görelim diye public yaptým
    public BoxCollider2D currentBounds;

    private Camera cam;
    private float camHalfHeight;
    private float camHalfWidth;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        camHalfHeight = cam.orthographicSize;
        camHalfWidth = camHalfHeight * cam.aspect;
    }

    public void SetBounds(BoxCollider2D newBounds)
    {
        currentBounds = newBounds;

        // Debug için konsola yazalým
        if (currentBounds != null)
            Debug.Log($"Kamera Sýnýrlarý Atandý: {newBounds.name} - Boyut: {newBounds.bounds.size}");
        else
            Debug.LogError("Kamera Sýnýrý (Bounds) NULL geldi! Obje ismini kontrol et.");
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Hedef pozisyon (Z eksenini -10'da sabit tutuyoruz, ÇOK ÖNEMLÝ)
        Vector3 desiredPosition = new Vector3(target.position.x, target.position.y, -10f);

        if (currentBounds != null)
        {
            Bounds bounds = currentBounds.bounds;

            // --- MATEMATÝKSEL DÜZELTME ---
            // Eðer oda geniþliði kameradan küçükse, kamerayý odanýn ortasýna sabitle.
            // Deðilse, kenarlara çarpýnca durdur (Clamp).

            float minX, maxX, minY, maxY;

            // X EKSENÝ KONTROLÜ
            if (bounds.size.x < camHalfWidth * 2)
            {
                // Oda kameradan dar -> Ortala
                desiredPosition.x = bounds.center.x;
            }
            else
            {
                // Oda geniþ -> Sýnýrla
                minX = bounds.min.x + camHalfWidth;
                maxX = bounds.max.x - camHalfWidth;
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            }

            // Y EKSENÝ KONTROLÜ
            if (bounds.size.y < camHalfHeight * 2)
            {
                // Oda kameradan kýsa -> Ortala
                desiredPosition.y = bounds.center.y;
            }
            else
            {
                // Oda yüksek -> Sýnýrla
                minY = bounds.min.y + camHalfHeight;
                maxY = bounds.max.y - camHalfHeight;
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            }
        }

        // Yumuþak geçiþ
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    // --- BU KISIM ÇOK ÖNEMLÝ: GÖRSEL HATA AYIKLAMA ---
    // Scene ekranýnda sýnýrlarý Kýrmýzý Kutu olarak çizer.
    private void OnDrawGizmos()
    {
        if (currentBounds != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(currentBounds.bounds.center, currentBounds.bounds.size);
        }
    }
}
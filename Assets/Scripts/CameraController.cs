using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target; // Takip edilecek oyuncu
    public float smoothSpeed = 0.125f; // Yumuþak takip hýzý

    private BoxCollider2D currentBounds; // Odanýn sýnýrlarý
    private Camera cam;

    private float camHalfHeight;
    private float camHalfWidth;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        // Kameranýn dikey ve yatay yarýçapýný hesapla (Zoom oranýna göre)
        camHalfHeight = cam.orthographicSize;
        camHalfWidth = camHalfHeight * cam.aspect;
    }

    // LevelManager her yeni oda yarattýðýnda bu fonksiyonu çaðýrýp yeni sýnýrlarý verecek
    public void SetBounds(BoxCollider2D newBounds)
    {
        currentBounds = newBounds;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. Hedef pozisyon (Oyuncu)
        Vector3 desiredPosition = new Vector3(target.position.x, target.position.y, transform.position.z);

        // 2. Eðer bir sýnýr kutumuz varsa, kamerayý o kutunun içine hapset (Clamp)
        if (currentBounds != null)
        {
            // Kutunun en saðý, solu, tepesi ve altý
            float minX = currentBounds.bounds.min.x + camHalfWidth;
            float maxX = currentBounds.bounds.max.x - camHalfWidth;
            float minY = currentBounds.bounds.min.y + camHalfHeight;
            float maxY = currentBounds.bounds.max.y - camHalfHeight;

            // Kameranýn gidebileceði x ve y deðerlerini kýsýtla
            // (Mathf.Clamp: Deðer min'den küçükse min, max'tan büyükse max yapar)
            float clampedX = Mathf.Clamp(desiredPosition.x, minX, maxX);
            float clampedY = Mathf.Clamp(desiredPosition.y, minY, maxY);

            desiredPosition = new Vector3(clampedX, clampedY, transform.position.z);
        }

        // 3. Yumuþak geçiþ ile hareket et
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}
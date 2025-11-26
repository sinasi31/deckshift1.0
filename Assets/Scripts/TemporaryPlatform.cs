using UnityEngine;

public class TemporaryPlatform : MonoBehaviour
{
    public float lifeTime = 2f; // Platformun sahnede kalacaðý süre (saniye)

    void Start()
    {
        // 'lifeTime' saniye sonra DestroyPlatform fonksiyonunu çaðýr.
        Invoke("DestroyPlatform", lifeTime);
    }

    void DestroyPlatform()
    {
        // Bu objeyi yok et.
        Destroy(gameObject);
    }
}
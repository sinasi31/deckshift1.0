using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    public float lifeTime = 1f; // 1 saniye sonra yok ol

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}
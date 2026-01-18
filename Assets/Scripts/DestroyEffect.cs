using UnityEngine;

public class DestroyEffect : MonoBehaviour
{
    // Efekt kaç saniye ekranda kalsýn? (Yarým saniye ideal)
    public float lifetime = 0.5f;

    void Start()
    {
        // Belirtilen süre dolunca bu objeyi oyundan sil.
        Destroy(gameObject, lifetime);
    }
}

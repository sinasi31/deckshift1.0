using UnityEngine;


public class GhostTrail : MonoBehaviour
{
    // Hayaletin sahnede kalma süresi
    public float activeTime = 0.5f;

    public void Init(SpriteRenderer playerSr, Color color)
    {
        SpriteRenderer mySprite = GetComponent<SpriteRenderer>();

        // Oyuncunun o anki resmini ve yönünü kopyala
        mySprite.sprite = playerSr.sprite;
        transform.localScale = playerSr.transform.localScale;
        mySprite.color = color;

        // Oyuncunun bir týk arkasýnda görünsün
        mySprite.sortingLayerName = playerSr.sortingLayerName;
        mySprite.sortingOrder = playerSr.sortingOrder - 1;

        // Zamanla yok ol (Basit Unity koduyla)
        Destroy(gameObject, activeTime);
    }
}

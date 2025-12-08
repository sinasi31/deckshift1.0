using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;

    // Hareket Ayarlarý
    private Vector3 moveVector;

    // --- DÜZELTME 1: Bu deðiþkeni kullanacaðýz, deðerini 10 (eski hýz) yapalým ---
    private float moveSpeed = 10f;
    // ---------------------------------------------------------------------------

    private float disappearSpeed = 3f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(int damageAmount)
    {
        textMesh.text = damageAmount.ToString();
        textColor = textMesh.color;
        disappearTimer = 0.5f;

        // --- DÜZELTME 2: Elle '10f' yazmak yerine 'moveSpeed' deðiþkenini kullandýk ---
        moveVector = new Vector3(Random.Range(-0.5f, 0.5f), 1f) * moveSpeed;
        // -----------------------------------------------------------------------------
    }

    private void Update()
    {
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 8f * Time.deltaTime;

        if (disappearTimer > 0)
        {
            disappearTimer -= Time.deltaTime;
        }
        else
        {
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
using UnityEngine;
using TMPro; // TextMeshPro kullanacaðýz

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;

    // Hareket Ayarlarý
    private Vector3 moveVector;
    private float moveSpeed = 2f;
    private float disappearSpeed = 3f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(int damageAmount)
    {
        textMesh.text = damageAmount.ToString();

        // Yazý rengini al (Baþlangýçta tam görünür)
        textColor = textMesh.color;
        disappearTimer = 0.5f; // Yarým saniye bekle sonra silinmeye baþla

        // Hafifçe yukarý ve saða/sola rastgele fýrlasýn (Daha dinamik durur)
        moveVector = new Vector3(Random.Range(-0.5f, 0.5f), 1f) * 10f; // Ýlk fýrlama hýzý
    }

    private void Update()
    {
        // 1. Hareket (Yukarý Süzülme)
        transform.position += moveVector * Time.deltaTime;

        // Hýzý zamanla yavaþlat (Fiziksel sürtünme gibi)
        moveVector -= moveVector * 8f * Time.deltaTime;

        if (disappearTimer > 0)
        {
            disappearTimer -= Time.deltaTime;
        }
        else
        {
            // 2. Renk Solmasý (Fade Out)
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // Görünmez olunca yok et
            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
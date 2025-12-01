using UnityEngine;

public class AncientBook : MonoBehaviour
{
    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            isCollected = true;
            Debug.Log("KADÝM KÝTAP AÇILIYOR...");

            // Skill seçim ekranýný aç
            if (SkillRewardManager.instance != null)
            {
                SkillRewardManager.instance.OpenSkillSelection();
            }
            else
            {
                Debug.LogError("SkillRewardManager sahnede bulunamadý!");
            }

            Destroy(gameObject);
        }
    }
}
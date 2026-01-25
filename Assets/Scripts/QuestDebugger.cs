using UnityEngine;

public class QuestDebugger : MonoBehaviour
{
    void Update()
    {
        // K tuþuna basýnca bir düþman öldürmüþ gibi yap
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("DEBUG: Düþman öldürme simüle edildi.");
            if (QuestSystem.instance != null)
            {
                QuestSystem.instance.ReportEvent(QuestType.KillEnemy, 1);
            }
        }

        // G tuþuna basýnca 100 altýn bulmuþ gibi yap
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("DEBUG: Altýn bulma simüle edildi.");
            if (QuestSystem.instance != null)
            {
                // Altýn miktarýný artýrarak simüle ediyoruz
                QuestSystem.instance.ReportEvent(QuestType.GoldAccumulate, 100);
            }
        }

        // L tuþuna basýnca Havada Kill almýþ gibi yap
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("DEBUG: Air Kill simüle edildi.");
            if (QuestSystem.instance != null)
            {
                QuestSystem.instance.ReportEvent(QuestType.AirKill, 1);
            }
        }
    }
}
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI rewardText;
    public Button acceptButton;
    public TextMeshProUGUI buttonText; // Butonun içindeki yazý ("Accept" -> "Active")

    private QuestData myQuest;

    public void Setup(QuestData data)
    {
        myQuest = data;

        titleText.text = data.questName;
        descriptionText.text = data.description;
        rewardText.text = data.rewardText;

        // Butonu sýfýrla
        acceptButton.interactable = true;
        buttonText.text = "ACCEPT";

        // Eðer bu görevi zaten aldýysak butonu kilitle
        if (QuestManager.instance.activeQuests.Contains(data))
        {
            SetAcceptedVisuals();
        }
    }

    public void OnAcceptClicked()
    {
        if (myQuest == null) return;

        bool success = QuestManager.instance.AcceptQuest(myQuest);

        if (success)
        {
            SetAcceptedVisuals();
            // Buraya güzel bir "Mühür Basma" sesi veya efekti ekleyebiliriz
        }
    }

    private void SetAcceptedVisuals()
    {
        acceptButton.interactable = false;
        buttonText.text = "ACCEPTED";
        buttonText.color = Color.green; // veya sarý, temana göre
    }
}
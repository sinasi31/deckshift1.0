using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestPaper : MonoBehaviour
{
    [Header("UI Elemanlarý")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI rewardText;
    public Button acceptButton;

    private QuestData myData;

    public void Setup(QuestData data)
    {
        myData = data;
        titleText.text = data.questName;
        descText.text = data.description;
        rewardText.text = data.rewardText;

        // Butona týklanýnca ne olacaðýný burada kodla baðlýyoruz
        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnAccept);
    }

    void OnAccept()
    {
        Debug.Log($"Görev Kabul Edildi: {myData.questName}");

        acceptButton.interactable = false;
        acceptButton.GetComponentInChildren<TextMeshProUGUI>().text = "ACCEPTED";

        QuestSystem.instance.AcceptQuest(myData);
    }
}
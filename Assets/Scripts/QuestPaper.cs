using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestPaper : MonoBehaviour
{
    [Header("UI Elemanlar�")]
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

        // Butona t�klan�nca ne olaca��n� burada kodla ba�l�yoruz
        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnAccept);
    }

    void OnAccept()
    {
        Debug.Log($"G�rev Kabul Edildi: {myData.questName}");

        acceptButton.interactable = false;
        var label = acceptButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = "ACCEPTED";

        QuestSystem.instance.AcceptQuest(myData);
    }
}
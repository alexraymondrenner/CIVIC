using TMPro;
using UnityEngine;

public class PlayerRoomLabelFormatter : MonoBehaviour
{
    [SerializeField]
    private TMP_Text playerNameText;

    [SerializeField]
    private TMP_Text playerTypeText;

    public void SetPlayerInfo(string playerName, UserType playerType)
    {
        playerNameText.text = playerName;
        playerTypeText.text = playerType.ToString();
    }
}
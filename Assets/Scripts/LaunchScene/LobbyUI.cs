using UnityEngine;
using TMPro;

using Photon.Pun;
using Photon.Realtime;

public class LobbyUI : MonoBehaviour
{
    [Header("UI Panels")]

    [SerializeField]
    private GameObject connectUI;

    [SerializeField]
    private GameObject roomUI;

    [SerializeField]
    private GameObject loadingWheel;

    [SerializeField]
    private GameObject TutorialUI;


    [Header("Player List")]

    [SerializeField]
    private Transform playerListParent;

    [SerializeField]
    private GameObject playerRowPrefab;

    [Header("Start Button")]

    [SerializeField]
    private GameObject startSimulationButton, wait;

    public bool isFacilitator = false;
    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            ShowRoomUI();
            RefreshPlayerList();
        }
        else
        {
            ShowConnectionUI();
        }
    }

    // =========================================================
    // UI STATES
    // =========================================================

    public void ShowConnectionUI()
    {
        if (connectUI != null)
            connectUI.SetActive(true);

        if (roomUI != null)
            roomUI.SetActive(false);

        if (loadingWheel != null)
            loadingWheel.SetActive(false);
        if (TutorialUI != null)
            TutorialUI.SetActive(false);
    }

    public void ShowLoadingUI()
    {
        if (connectUI != null)
            connectUI.SetActive(false);

        if (roomUI != null)
            roomUI.SetActive(false);

        if (loadingWheel != null)
            loadingWheel.SetActive(true);
        if (TutorialUI != null)
            TutorialUI.SetActive(false);
    }

    public void ShowRoomUI()
    {
        if (connectUI != null)
            connectUI.SetActive(false);

        if (roomUI != null)
            roomUI.SetActive(true);

        if (loadingWheel != null)
            loadingWheel.SetActive(false);

        if (TutorialUI != null)
            TutorialUI.SetActive(true);
    }

    // =========================================================
    // PLAYER LIST
    // =========================================================

    public void RefreshPlayerList()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (playerListParent == null || playerRowPrefab == null)
        {
            Debug.LogWarning(
                "Player list parent or player row prefab is not assigned."
            );

            return;
        }

        ClearPlayerList();
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject playerRow = Instantiate(
                playerRowPrefab,
                playerListParent
            );

            PlayerRoomLabelFormatter formatter =
                playerRow.GetComponent<PlayerRoomLabelFormatter>();

            if (formatter == null)
            {
                Debug.LogError(
                    "Player row prefab does not contain " +
                    "PlayerRoomLabelFormatter."
                );

                Destroy(playerRow);
                continue;
            }
            formatter.SetPlayerInfo(player.NickName,GetPlayerType(player));
        }
        UpdateStartButton();
    }
    private UserType GetPlayerType(Player player)
    {
        if (player.CustomProperties.TryGetValue(
                "UserType",
                out object value))
        {
            return (UserType)System.Convert.ToInt32(value);
        }

        return UserType.Unassigned;
    }


    private void ClearPlayerList()
    {
        for (int i = playerListParent.childCount - 1; i >= 0; i--)
        {
            Destroy(
                playerListParent.GetChild(i).gameObject
            );
        }
    }

    // =========================================================
    // MASTER CLIENT UI
    // =========================================================

    public void UpdateStartButton()
    {
        if (startSimulationButton == null)
            return;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                "UserType",
                out object value))
        {
            UserType userType =
                (UserType)System.Convert.ToInt32(value);

            isFacilitator =
                userType == UserType.Facilitator; print((UserType)System.Convert.ToInt32(value));
        }
       
        //Temperary letting everyone to be able to start the simualtion 
        //isFacilitator = true;
        startSimulationButton.SetActive(isFacilitator);
        wait.SetActive(!isFacilitator);
    }
}
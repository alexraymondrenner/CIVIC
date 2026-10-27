using System;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class Launcher : MonoBehaviourPunCallbacks
{
    private const string PlayerTypeProperty = "UserType";

    [Header("Room Settings")]

    [SerializeField]
    private byte maxPlayersPerRoom = 4;

    [SerializeField]
    private string gameVersion = "1";

    private LobbyUI lobbyUI;

    [SerializeField]
    private TMP_Dropdown userTypeDropdown;
    [SerializeField]
    private TMP_InputField userName;

    private bool isConnecting;

    [SerializeField]
    private string simulationSceneName;

    [Header("Single Player Settings")]
    [SerializeField]
    private string singlePlayerSceneName = "Tutorial";

    private bool isLeavingForSinglePlayer = false;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        InitializeUserTypeDropdown();
    }

    private void Start()
    {
        lobbyUI = this.GetComponent<LobbyUI>();
    }

    // =========================================================
    // USER TYPE DROPDOWN
    // =========================================================

    private void InitializeUserTypeDropdown()
    {
        if (userTypeDropdown == null)
        {
            Debug.LogWarning(
                "User type dropdown is not assigned."
            );

            return;
        }

        userTypeDropdown.ClearOptions();

        List<string> typeNames = new List<string>(
            Enum.GetNames(typeof(UserType))
        );

        userTypeDropdown.AddOptions(typeNames);
    }

    // =========================================================
    // CONNECT
    // =========================================================
    private const string roomID = "MySharedRoom";
    
    public void ConnectRoom()
    {

        if (userTypeDropdown == null)
        {
            Debug.LogError("User type dropdown is not assigned.");
            return;
        }

        string playerName = userName.text;

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogError("Player name cannot be empty.");
            return;
        }

        if (string.IsNullOrEmpty(roomID))
        {
            Debug.LogError("Room ID cannot be empty.");
            return;
        }

        UserType selectedType =
            (UserType)userTypeDropdown.value;

        if (selectedType == UserType.Unassigned)
        {
            Debug.LogError("Please select a user type.");
            return;
        }


        // updating UserDataManager.
        UserDataManager.Instance.SetUserType(selectedType);

        PhotonNetwork.NickName = playerName;

        if (lobbyUI != null)
        {
            lobbyUI.ShowLoadingUI();
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinOrCreateRoom(roomID);
        }
        else
        {
            PhotonNetwork.GameVersion = gameVersion;
            isConnecting = true;

            bool connectionStarted =
                PhotonNetwork.ConnectUsingSettings();

            if (!connectionStarted)
            {
                isConnecting = false;

                Debug.LogError(
                    "Could not start the Photon connection."
                );

                if (lobbyUI != null)
                {
                    lobbyUI.ShowConnectionUI();
                }
            }
        }
    }


    public override void OnConnectedToMaster()
    {
        if (!isConnecting)
            return;

        isConnecting = false;

        //string roomID = roomID;

        JoinOrCreateRoom(roomID);
    }

    private void JoinOrCreateRoom(string roomID)
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom
        };

        Debug.Log(
            $"Joining or creating room: {roomID}"
        );

        PhotonNetwork.JoinOrCreateRoom(
            roomID,
            roomOptions,
            TypedLobby.Default
        );
    }

    // =========================================================
    // FAILURE CALLBACKS
    // =========================================================

    public override void OnJoinRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogError(
            $"Failed to join room: {message}"
        );

        if (lobbyUI != null)
        {
            lobbyUI.ShowConnectionUI();
        }
    }

    public override void OnCreateRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogError(
            $"Failed to create room: {message}"
        );

        if (lobbyUI != null)
        {
            lobbyUI.ShowConnectionUI();
        }
    }

    public override void OnDisconnected(
        DisconnectCause cause)
    {
        isConnecting = false;

        Debug.LogWarning(
            $"Disconnected from Photon: {cause}"
        );

        if (lobbyUI != null)
        {
            lobbyUI.ShowConnectionUI();
        }
    }

    // =========================================================
    // Launch Simulation
    // =========================================================


    public void LaunchSimulation()
    {
        if (!lobbyUI.isFacilitator)
        {
            Debug.LogWarning(
                "Only the facilitator can start the simulation."
            );
            return;
        }

        NetworkManager.Instance.LoadNetworkLevel(
            simulationSceneName
        );

    }


}
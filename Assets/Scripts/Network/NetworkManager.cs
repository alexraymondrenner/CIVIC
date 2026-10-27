using UnityEngine;
using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using ExitGames.Client.Photon;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [Header("Network State")]

    [SerializeField, ReadOnly]
    private bool enableSync = false;

    [SerializeField, ReadOnly]
    private string currentRoomID = "";

    [Header("Scene Settings")]

    [SerializeField]
    private string loginSceneName = "LogIn";

    [SerializeField]
    private GameObject launcher;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // PHOTON ROOM CALLBACKS
    // =========================================================

    public override void OnJoinedRoom()
    {
        Debug.Log(
            $"Joined room: {PhotonNetwork.CurrentRoom.Name}"
        );

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.RefreshRoomID();
        }


        if (launcher != null)
        {
            launcher.GetComponent<LobbyUI>().ShowRoomUI();
            PublishLocalUserType();
            launcher.GetComponent<LobbyUI>().RefreshPlayerList();
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("NetworkManager left the Photon room.");

        currentRoomID = "";
        enableSync = false;

        // Do not automatically disconnect here unless that is
        // specifically the behavior you want.
    }

   

    // =========================================================
    // PLAYER CALLBACKS
    // =========================================================

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(
            $"Player entered room: {newPlayer.NickName}"
        );

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("This player is the master client.");
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log(
            $"Player left room: {otherPlayer.NickName}"
        );

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log(
                "This player is currently the master client."
            );
        }
    }

    public override void OnMasterClientSwitched(
        Player newMasterClient)
    {
        Debug.Log(
            $"Master Client is now: {newMasterClient.NickName}"
        );

        if (launcher != null)
        {
            launcher.GetComponent<LobbyUI>().UpdateStartButton();
        }
    }

    // =========================================================
    // CONNECTION CALLBACKS
    // =========================================================

    public override void OnDisconnected(
        DisconnectCause cause)
    {
        Debug.LogWarning(
            $"Disconnected from Photon: {cause}"
        );

        currentRoomID = "";
        enableSync = false;

        SceneManager.LoadScene(loginSceneName);
    }

    // =========================================================
    // SYNC CONTROL
    // =========================================================

    public void StartSync()
    {
        enableSync = true;

        Debug.Log("Network synchronization enabled.");
    }

    public void StopSync()
    {
        enableSync = false;

        Debug.Log("Network synchronization disabled.");
    }

    public bool GetSyncState()
    {
        return enableSync;
    }

    public string GetCurrentRoomID()
    {
        return currentRoomID;
    }

    // =========================================================
    // NETWORK ELEMENT VISIBILITY
    // =========================================================

    public void UpdateNetworkElementVisibility(
        GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning(
                "Cannot update network element: target is null."
            );

            return;
        }

        PhotonView targetPhotonView =
            target.GetComponent<PhotonView>();

        if (targetPhotonView == null)
        {
            Debug.LogWarning(
                $"{target.name} does not have a PhotonView."
            );

            return;
        }

        if (!targetPhotonView.IsMine)
        {
            target.SetActive(enableSync);
        }
    }

    // Keep this method if other existing scripts already call it.
    public void TurnOffNetworkElement(GameObject target)
    {
        UpdateNetworkElementVisibility(target);
    }

    // =========================================================
    // LEAVE / DISCONNECT
    // =========================================================

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot leave room because this player " +
                "is not currently in a room."
            );

            return;
        }

        PhotonNetwork.LeaveRoom();
    }

    public void Disconnect()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning(
                "Photon is already disconnected."
            );

            return;
        }

        PhotonNetwork.Disconnect();
    }
    // =========================================================
    // LOAD LEVEL
    // =========================================================

    public void LoadNetworkLevel(string sceneName)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("Cannot load a network level while outside a room.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning(
                "Only the master client can load a synchronized level."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Scene name cannot be empty.");
            return;
        }

        Debug.Log($"Loading network level: {sceneName}");

        PhotonNetwork.LoadLevel(sceneName);
    }

    public override void OnPlayerPropertiesUpdate(
    Player targetPlayer,
    Hashtable changedProperties)
    {
        if (changedProperties.ContainsKey("UserType"))
        {
            Debug.Log(
                $"User type updated for: {targetPlayer.NickName}"
            );

            if (launcher.GetComponent<LobbyUI>() != null)
            {
                launcher.GetComponent<LobbyUI>().RefreshPlayerList();
            }
        }
    }
    private void PublishLocalUserType()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot publish user type before joining a room."
            );

            return;
        }

        if (UserDataManager.Instance == null)
        {
            Debug.LogError(
                "UserDataManager does not exist."
            );

            return;
        }

        UserType userType =
            UserDataManager.Instance.GetUserType();

        Hashtable properties = new Hashtable
    {
        { "UserType", (int)userType }
    };

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            properties
        );

        Debug.Log(
            $"Publishing user type to Photon: {userType}"
        );

        if (userType == UserType.Facilitator &&
        !PhotonNetwork.IsMasterClient)
        {
            bool requestSent = PhotonNetwork.SetMasterClient(
                PhotonNetwork.LocalPlayer
            );

            Debug.Log(
                $"Requested Facilitator as Master Client: {requestSent}"
            );
        }
        else
        {
            Debug.Log(
             $"Facilitator Already Exist"
         );
            //LeaveRoom();


        }
    }

}
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ConnectionButtons : MonoBehaviourPunCallbacks
{
    [SerializeField] private string gameVersion = "KanDirect";
    [SerializeField] private string devRegion = "asia";
    [SerializeField] private byte maxPlayersPerRoom = 4;
    [SerializeField] private string roomCodePrefix = "KAN";

    private enum PendingAction
    {
        None,
        Host,
        Client
    }

    private PendingAction pendingAction;
    private string pendingRoomCode;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;

        if (!string.IsNullOrWhiteSpace(devRegion))
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = devRegion;
        }
    }

    public void StartHost()
    {
        pendingAction = PendingAction.Host;
        pendingRoomCode = $"{roomCodePrefix}{Random.Range(100, 1000)}";
        EnsureConnected();
    }

    public void StartClient()
    {
        pendingAction = PendingAction.Client;
        EnsureConnected();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        ExecutePendingAction();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (pendingAction != PendingAction.Client)
        {
            return;
        }

        pendingAction = PendingAction.Host;
        pendingRoomCode = $"{roomCodePrefix}{Random.Range(100, 1000)}";
        ExecutePendingAction();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (pendingAction != PendingAction.Host)
        {
            return;
        }

        pendingRoomCode = $"{roomCodePrefix}{Random.Range(100, 1000)}";
        ExecutePendingAction();
    }

    private void EnsureConnected()
    {
        if (PhotonNetwork.InRoom)
        {
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InLobby)
            {
                ExecutePendingAction();
            }
            else
            {
                PhotonNetwork.JoinLobby();
            }

            return;
        }

        PhotonNetwork.ConnectUsingSettings();
    }

    private void ExecutePendingAction()
    {
        switch (pendingAction)
        {
            case PendingAction.Host:
                PhotonNetwork.CreateRoom(
                    pendingRoomCode,
                    new RoomOptions { MaxPlayers = maxPlayersPerRoom });
                break;

            case PendingAction.Client:
                PhotonNetwork.JoinRandomRoom();
                break;
        }
    }
}

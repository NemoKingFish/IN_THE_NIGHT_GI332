using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameStatusVisibility : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject statusPanel;

    private void Start()
    {
        ApplyDisconnectedState();
        RefreshUI();
    }

    public override void OnJoinedRoom() => RefreshUI();
    public override void OnLeftRoom() => RefreshUI();
    public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshUI();
    public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshUI();

    private bool IsConnectedToSession()
    {
        return PhotonNetwork.InRoom;
    }

    private void RefreshUI()
    {
        if (statusPanel == null) return;

        statusPanel.SetActive(IsConnectedToSession());
    }

    private void ApplyDisconnectedState()
    {
        if (statusPanel != null)
            statusPanel.SetActive(false);
    }
}

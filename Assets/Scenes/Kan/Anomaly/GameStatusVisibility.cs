using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameStatusVisibility : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject statusPanel;

    private void Start()
    {
        RefreshUI();
    }

    public override void OnJoinedRoom()
    {
        RefreshUI();
    }

    public override void OnLeftRoom()
    {
        RefreshUI();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (statusPanel != null)
        {
            statusPanel.SetActive(PhotonNetwork.InRoom);
        }
    }
}

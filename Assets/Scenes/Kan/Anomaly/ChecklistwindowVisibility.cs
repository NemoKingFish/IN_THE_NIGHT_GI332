using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ChecklistButtonVisibility : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject openChecklistButton;

    private void Start()
    {
        RefreshButtonState();
    }

    public override void OnJoinedRoom()
    {
        RefreshButtonState();
    }

    public override void OnLeftRoom()
    {
        RefreshButtonState();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshButtonState();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(PhotonNetwork.InRoom);
        }
    }
}

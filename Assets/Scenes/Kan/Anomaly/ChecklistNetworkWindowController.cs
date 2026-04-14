using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ChecklistNetworkWindowController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private GameObject openChecklistButton;

    private void Start()
    {
        ApplyDisconnectedState();
        RefreshUI();
    }

    public override void OnJoinedRoom()
    {
        RefreshUI();
    }

    public override void OnLeftRoom()
    {
        ApplyDisconnectedState();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshUI();
    }

    private static bool IsConnectedToSession()
    {
        return PhotonNetwork.InRoom;
    }

    private void RefreshUI()
    {
        if (!IsConnectedToSession())
        {
            ApplyDisconnectedState();
            return;
        }

        if (checklistWindow != null && checklistWindow.activeSelf)
        {
            if (openChecklistButton != null)
            {
                openChecklistButton.SetActive(false);
            }
        }
        else
        {
            if (openChecklistButton != null)
            {
                openChecklistButton.SetActive(true);
            }
        }
    }

    private void ApplyDisconnectedState()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(false);
        }
    }

    public void OpenWindow()
    {
        if (!IsConnectedToSession())
        {
            return;
        }

        if (checklistWindow != null)
        {
            checklistWindow.SetActive(true);
        }

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(false);
        }
    }

    public void CloseWindow()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(IsConnectedToSession());
        }
    }

    public void ToggleWindow()
    {
        if (!IsConnectedToSession() || checklistWindow == null)
        {
            return;
        }

        var willOpen = !checklistWindow.activeSelf;
        checklistWindow.SetActive(willOpen);

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(!willOpen);
        }
    }
}

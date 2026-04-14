using Photon.Pun;
using UnityEngine;

public class ChecklistWindowController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private GameObject openChecklistButton;

    private void Start()
    {
        ApplyDisconnectedState();
        RefreshUi();
    }

    public override void OnJoinedRoom()
    {
        RefreshUi();
    }

    public override void OnLeftRoom()
    {
        ApplyDisconnectedState();
    }

    public void OpenWindow()
    {
        if (!PhotonNetwork.InRoom)
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

        UnlockCursor();
    }

    public void CloseWindow()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(PhotonNetwork.InRoom);
        }

        if (PhotonNetwork.InRoom)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    public void ToggleWindow()
    {
        if (!PhotonNetwork.InRoom || checklistWindow == null)
        {
            return;
        }

        var isOpening = !checklistWindow.activeSelf;
        checklistWindow.SetActive(isOpening);

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(!isOpening);
        }

        if (isOpening)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
    }

    private void RefreshUi()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        if (openChecklistButton != null)
        {
            openChecklistButton.SetActive(PhotonNetwork.InRoom);
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

        UnlockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

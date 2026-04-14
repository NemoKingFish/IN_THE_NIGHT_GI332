using Photon.Pun;
using UnityEngine;

public class ChecklistWindowToggle : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private void Start()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }
    }

    public override void OnLeftRoom()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }
    }

    private void Update()
    {
        if (PhotonNetwork.InRoom && Input.GetKeyDown(toggleKey))
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        if (!PhotonNetwork.InRoom || checklistWindow == null)
        {
            return;
        }

        checklistWindow.SetActive(!checklistWindow.activeSelf);

        if (checklistWindow.activeSelf)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OpenWindow()
    {
        if (!PhotonNetwork.InRoom || checklistWindow == null)
        {
            return;
        }

        checklistWindow.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseWindow()
    {
        if (checklistWindow == null)
        {
            return;
        }

        checklistWindow.SetActive(false);
        if (PhotonNetwork.InRoom)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

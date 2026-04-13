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

    public override void OnJoinedRoom() => RefreshUI();
    public override void OnLeftRoom() => RefreshUI();
    public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshUI();
    public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshUI();

    private bool IsConnectedToSession()
    {
        return PhotonNetwork.InRoom;

        // จะนับว่าใช้งานได้เมื่อเป็น host หรือ client ที่เชื่อมต่อแล้ว

    }

    private void RefreshUI()
    {
        bool connected = IsConnectedToSession();

        if (!connected)
        {
            ApplyDisconnectedState();
            return;
        }

        // ถ้าเชื่อมต่อแล้ว แต่หน้าต่างยังไม่เปิด ให้โชว์ปุ่ม Open
        if (checklistWindow != null && checklistWindow.activeSelf)
        {
            if (openChecklistButton != null)
                openChecklistButton.SetActive(false);
        }
        else
        {
            if (openChecklistButton != null)
                openChecklistButton.SetActive(true);
        }
    }

    private void ApplyDisconnectedState()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(false);
    }

    public void OpenWindow()
    {
        if (!IsConnectedToSession()) return;

        if (checklistWindow != null)
            checklistWindow.SetActive(true);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(false);
    }

    public void CloseWindow()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(IsConnectedToSession());
    }

    public void ToggleWindow()
    {
        if (!IsConnectedToSession()) return;
        if (checklistWindow == null) return;

        bool willOpen = !checklistWindow.activeSelf;
        checklistWindow.SetActive(willOpen);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(!willOpen);
    }
}

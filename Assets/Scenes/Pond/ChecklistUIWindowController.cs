using Unity.Netcode;
using UnityEngine;

public class ChecklistUIWindowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChecklistUI checklistUI;
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private GameObject openChecklistButton;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private void Start()
    {
        ApplyDisconnectedState();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("ChecklistUIWindowController: NetworkManager.Singleton not found");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Update()
    {
        if (!IsConnectedToSession())
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleWindow();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        RefreshUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RefreshUI();
    }

    private bool IsConnectedToSession()
    {
        if (NetworkManager.Singleton == null)
            return false;

        return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient;
    }

    private void RefreshUI()
    {
        bool connected = IsConnectedToSession();

        if (!connected)
        {
            ApplyDisconnectedState();
            return;
        }

        bool isWindowOpen = checklistWindow != null && checklistWindow.activeSelf;

        if (openChecklistButton != null)
            openChecklistButton.SetActive(!isWindowOpen);
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
        if (!IsConnectedToSession())
            return;

        if (checklistUI != null)
            checklistUI.OpenWindow();
        else if (checklistWindow != null)
            checklistWindow.SetActive(true);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(false);
    }

    public void CloseWindow()
    {
        if (checklistUI != null)
            checklistUI.CloseWindow();
        else if (checklistWindow != null)
            checklistWindow.SetActive(false);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(IsConnectedToSession());
    }

    public void ToggleWindow()
    {
        if (!IsConnectedToSession())
            return;

        if (checklistUI != null)
            checklistUI.ToggleWindow();
        else if (checklistWindow != null)
            checklistWindow.SetActive(!checklistWindow.activeSelf);

        bool isWindowOpen = checklistWindow != null && checklistWindow.activeSelf;
        if (openChecklistButton != null)
            openChecklistButton.SetActive(!isWindowOpen);
    }
}
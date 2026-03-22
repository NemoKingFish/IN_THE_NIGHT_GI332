using Unity.Netcode;
using UnityEngine;

public class ChecklistButtonVisibility : MonoBehaviour
{
    [SerializeField] private GameObject openChecklistButton;

    private void Start()
    {
        if (openChecklistButton != null)
            openChecklistButton.SetActive(false);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton not found");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // กรณีเข้าเกมมาแล้ว network ทำงานอยู่
        RefreshButtonState();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        RefreshButtonState();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        if (openChecklistButton == null || NetworkManager.Singleton == null) return;

        bool isConnected =
            NetworkManager.Singleton.IsHost ||
            NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsServer;

        openChecklistButton.SetActive(isConnected);
    }
}
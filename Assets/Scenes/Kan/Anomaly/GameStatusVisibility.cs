using Unity.Netcode;
using UnityEngine;

public class GameStatusVisibility : MonoBehaviour
{
    [SerializeField] private GameObject statusPanel;

    private void Start()
    {
        ApplyDisconnectedState();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton not found");
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
        if (NetworkManager.Singleton == null) return false;

        return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient;
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
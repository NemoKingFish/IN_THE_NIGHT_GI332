using Unity.Netcode;
using UnityEngine;

public class ConnectionButtons : MonoBehaviour
{
    public void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("ConnectionButtons: null");
            return;
        }

        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("ConnectionButtons: null");
            return;
        }

        NetworkManager.Singleton.StartClient();
    }
}
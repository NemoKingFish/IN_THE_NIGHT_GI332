using Unity.Netcode;
using UnityEngine;

public class PlayerTrainInteractor : NetworkBehaviour
{
    [SerializeField] private NetworkPlayerSeatState seatState;

    private NetworkRailTrainController nearbyTrain;

    private void Awake()
    {
        if (seatState == null)
        {
            seatState = GetComponent<NetworkPlayerSeatState>();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[PLAYER] Press E | seated={seatState != null && seatState.IsSeated} | nearbyTrain={(nearbyTrain != null)}");

            if (seatState != null && seatState.IsSeated && seatState.CurrentTrain != null)
            {
                Debug.Log("[PLAYER] Request Exit");
                seatState.CurrentTrain.RequestExitServerRpc();
            }
            else if (nearbyTrain != null)
            {
                Debug.Log("[PLAYER] Request Enter");
                nearbyTrain.RequestEnterServerRpc();
            }
            else
            {
                Debug.Log("[PLAYER] No nearby train");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        NetworkRailTrainController train = other.GetComponentInParent<NetworkRailTrainController>();
        if (train != null)
        {
            nearbyTrain = train;
            Debug.Log("[PLAYER] Entered train trigger");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        NetworkRailTrainController train = other.GetComponentInParent<NetworkRailTrainController>();
        if (train != null && nearbyTrain == train)
        {
            nearbyTrain = null;
            Debug.Log("[PLAYER] Exited train trigger");
        }
    }
}
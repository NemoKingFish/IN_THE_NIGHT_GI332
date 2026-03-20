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

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[PLAYER] Press E | seated={seatState != null && seatState.IsSeated} | nearbyTrain={(nearbyTrain != null)}");

            if (seatState != null && seatState.IsSeated && seatState.CurrentTrain != null)
            {
                if (!seatState.CurrentTrain.IsSpawned)
                {
                    Debug.LogWarning("[PLAYER] CurrentTrain exists but is not spawned yet");
                    return;
                }

                Debug.Log("[PLAYER] Request Exit");
                seatState.CurrentTrain.RequestExitServerRpc();
                return;
            }

            if (nearbyTrain == null)
            {
                Debug.LogWarning("[PLAYER] No nearby train");
                return;
            }

            Debug.Log($"[PLAYER] nearbyTrain name={nearbyTrain.name} | IsSpawned={nearbyTrain.IsSpawned}");

            if (!nearbyTrain.IsSpawned)
            {
                Debug.LogWarning("[PLAYER] Train found, but NetworkObject is not spawned yet");
                return;
            }

            Debug.Log("[PLAYER] Request Enter");
            nearbyTrain.RequestEnterServerRpc();
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
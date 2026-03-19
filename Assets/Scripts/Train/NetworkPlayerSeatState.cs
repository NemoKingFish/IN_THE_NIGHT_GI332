using Unity.Netcode;
using UnityEngine;
using StarterAssets;

public class NetworkPlayerSeatState : NetworkBehaviour
{
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private CharacterController characterController;

    public bool IsSeated { get; private set; }
    public int CurrentSeatIndex { get; private set; } = -1;
    public NetworkRailTrainController CurrentTrain { get; private set; }

    private ulong currentTrainNetworkObjectId;

    private void Awake()
    {
        if (thirdPersonController == null)
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    public void SetSeatedServer(bool seated, ulong trainNetworkObjectId, int seatIndex)
    {
        IsSeated = seated;
        currentTrainNetworkObjectId = trainNetworkObjectId;
        CurrentSeatIndex = seatIndex;
    }

    public void SetSeatedLocal(bool seated, NetworkRailTrainController train, int seatIndex)
    {
        IsSeated = seated;
        CurrentTrain = train;
        CurrentSeatIndex = seatIndex;

        if (thirdPersonController != null)
        {
            thirdPersonController.enabled = !seated;
        }

        if (characterController != null)
        {
            characterController.enabled = !seated;
        }
    }
}
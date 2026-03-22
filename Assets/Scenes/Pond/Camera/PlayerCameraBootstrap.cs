using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraBootstrap : NetworkBehaviour
{
    [SerializeField] private Transform headBobTarget;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("Main Camera not found.", this);
            return;
        }

        CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            Debug.LogError("CinemachineBrain missing on Main Camera.", mainCam);
            return;
        }

        CinemachineCamera fpsCam = FindFirstObjectByType<CinemachineCamera>();
        if (fpsCam == null)
        {
            Debug.LogError("No CinemachineCamera found in scene.", this);
            return;
        }

        if (headBobTarget == null)
        {
            Debug.LogError("headBobTarget is null.", this);
            return;
        }

        fpsCam.Priority = 100;
        fpsCam.Target.TrackingTarget = headBobTarget;
        fpsCam.Target.LookAtTarget = headBobTarget;
    }
}
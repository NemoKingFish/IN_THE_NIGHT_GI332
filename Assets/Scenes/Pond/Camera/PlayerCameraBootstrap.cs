using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraBootstrap : NetworkBehaviour
{
    [SerializeField] private Transform headBobTarget;
    [SerializeField] private int cameraPriority = 100;

    private CinemachineCamera fpsCam;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (headBobTarget == null)
        {
            Debug.LogError("PlayerCameraBootstrap: headBobTarget is null.", this);
            enabled = false;
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("PlayerCameraBootstrap: Main Camera not found.", this);
            enabled = false;
            return;
        }

        CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            Debug.LogError("PlayerCameraBootstrap: CinemachineBrain missing on Main Camera.", mainCam);
            enabled = false;
            return;
        }

        fpsCam = FindFirstObjectByType<CinemachineCamera>();
        if (fpsCam == null)
        {
            Debug.LogError("PlayerCameraBootstrap: No CinemachineCamera found in scene.", this);
            enabled = false;
            return;
        }

        fpsCam.Priority = cameraPriority;
        fpsCam.Target.TrackingTarget = headBobTarget;
        fpsCam.Target.LookAtTarget = headBobTarget;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        if (fpsCam != null)
        {
            fpsCam.Target.TrackingTarget = null;
            fpsCam.Target.LookAtTarget = null;
        }
    }
}
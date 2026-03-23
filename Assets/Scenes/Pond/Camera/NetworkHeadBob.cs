using Unity.Netcode;
using UnityEngine;

public class NetworkHeadBob : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bobTarget;
    [SerializeField] private NetworkPlayerMotor motor;

    [Header("Bob")]
    [SerializeField] private float bobFrequency = 9f;
    [SerializeField] private float bobAmplitudeY = 0.045f;
    [SerializeField] private float bobSmoothSpeed = 10f;

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;
    private float bobTimer;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (bobTarget == null)
            bobTarget = transform;

        if (motor == null)
            motor = GetComponentInParent<NetworkPlayerMotor>();

        if (motor == null)
        {
            Debug.LogError("NetworkHeadBob: NetworkPlayerMotor not found.", this);
            enabled = false;
            return;
        }

        defaultLocalPos = bobTarget.localPosition;
        defaultLocalRot = bobTarget.localRotation;
    }

    private void LateUpdate()
    {
        if (motor == null)
            return;

        Vector3 planarVelocity = motor.HorizontalVelocity;
        planarVelocity.y = 0f;

        float speed = planarVelocity.magnitude;
        bool isMoving = speed > 0.1f;
        bool grounded = motor.IsGrounded;

        HandleHeadBob(isMoving, grounded, speed);
        ResetRotation();
    }

    private void HandleHeadBob(bool isMoving, bool grounded, float speed)
    {
        if (isMoving && grounded)
        {
            bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp(speed / 4f, 0.75f, 1.75f);

            float y = Mathf.Sin(bobTimer) * bobAmplitudeY;
            Vector3 target = defaultLocalPos + new Vector3(0f, y, 0f);

            bobTarget.localPosition = Vector3.Lerp(
                bobTarget.localPosition,
                target,
                bobSmoothSpeed * Time.deltaTime
            );
        }
        else
        {
            bobTimer = 0f;

            bobTarget.localPosition = Vector3.Lerp(
                bobTarget.localPosition,
                defaultLocalPos,
                bobSmoothSpeed * Time.deltaTime
            );
        }
    }

    private void ResetRotation()
    {
        bobTarget.localRotation = Quaternion.Slerp(
            bobTarget.localRotation,
            defaultLocalRot,
            bobSmoothSpeed * Time.deltaTime
        );
    }
}
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
    [SerializeField] private float bobAmplitudeX = 0.02f;
    [SerializeField] private float bobSmoothSpeed = 10f;

    [Header("Tilt")]
    [SerializeField] private float tiltAmount = 1.5f;
    [SerializeField] private float tiltSmoothSpeed = 10f;
    [SerializeField] private float maxTilt = 3f;

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;
    private float bobTimer;

    private void Start()
    {
        if (bobTarget == null)
            bobTarget = transform;

        defaultLocalPos = bobTarget.localPosition;
        defaultLocalRot = bobTarget.localRotation;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (motor == null)
            return;

        Vector3 planarVelocity = motor.HorizontalVelocity;
        planarVelocity.y = 0f;

        float speed = planarVelocity.magnitude;
        bool isMoving = speed > 0.1f;
        bool grounded = motor.IsGrounded;

        float moveX = Input.GetAxisRaw("Horizontal");

        HandleHeadBob(isMoving, grounded, speed);
        HandleTilt(moveX);
    }

    private void HandleHeadBob(bool isMoving, bool grounded, float speed)
    {
        if (isMoving && grounded)
        {
            bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp(speed / 4f, 0.75f, 1.75f);

            float x = Mathf.Cos(bobTimer * 0.5f) * bobAmplitudeX;
            float y = Mathf.Sin(bobTimer) * bobAmplitudeY;

            Vector3 target = defaultLocalPos + new Vector3(x, y, 0f);

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

    private void HandleTilt(float moveX)
    {
        float zTilt = Mathf.Clamp(-moveX * tiltAmount, -maxTilt, maxTilt);
        Quaternion targetRot = defaultLocalRot * Quaternion.Euler(0f, 0f, zTilt);

        bobTarget.localRotation = Quaternion.Slerp(
            bobTarget.localRotation,
            targetRot,
            tiltSmoothSpeed * Time.deltaTime
        );
    }
}
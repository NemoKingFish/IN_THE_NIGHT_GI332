using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerInput : NetworkBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public float LookYaw { get; private set; }

    [Header("Look")]
    [SerializeField] private float sensitivity = 120f;
    [SerializeField] private Transform playerYawRoot;
    [SerializeField] private Transform pitchRoot;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (playerYawRoot == null)
            playerYawRoot = transform;

        if (pitchRoot == null)
        {
            Debug.LogError("NetworkPlayerInput: pitchRoot is not assigned.", this);
            enabled = false;
            return;
        }

        LookYaw = playerYawRoot.eulerAngles.y;
        pitch = NormalizeAngle(pitchRoot.localEulerAngles.x);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        ReadLook();
        ReadMove();

        SubmitInputServerRpc(MoveInput, JumpPressed, SprintHeld, LookYaw);
    }

    private void ReadLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity * Time.deltaTime;

        // Yaw: local ตอบสนองทันที
        LookYaw += mouseX;
        playerYawRoot.rotation = Quaternion.Euler(0f, LookYaw, 0f);

        // Pitch: local only
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void ReadMove()
    {
        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        JumpPressed = Input.GetButtonDown("Jump");
        SprintHeld = Input.GetKey(KeyCode.LeftShift);
    }

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 move, bool jump, bool sprint, float yaw)
    {
        MoveInput = move;
        JumpPressed = jump;
        SprintHeld = sprint;
        LookYaw = yaw;
    }

    public void ConsumeJump()
    {
        JumpPressed = false;
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }
}
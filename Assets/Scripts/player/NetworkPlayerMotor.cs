using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkPlayerInput))]
public class NetworkPlayerMotor : NetworkBehaviour
{
    [Header("Move")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float airControl = 0.4f;

    public float VerticalVelocity => verticalVelocity;

    [Header("Jump / Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float jumpHeight = 1.2f;

    private CharacterController controller;
    private NetworkPlayerInput netInput;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    public Vector3 HorizontalVelocity => horizontalVelocity;
    public bool IsGrounded => controller != null && controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        netInput = GetComponent<NetworkPlayerInput>();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        TickMovement(Time.deltaTime);
    }

    private void TickMovement(float dt)
    {
        Vector2 input = netInput.MoveInput;
        bool sprint = netInput.SprintHeld;
        bool jump = netInput.JumpPressed;

        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y);
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        float targetSpeed = sprint ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = moveDir * targetSpeed;

        float control = controller.isGrounded ? 1f : airControl;
        horizontalVelocity = Vector3.Lerp(
            horizontalVelocity,
            targetVelocity,
            acceleration * control * dt
        );

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (jump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                netInput.ConsumeJump();
            }
        }

        verticalVelocity += gravity * dt;

        Vector3 finalMove = horizontalVelocity;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * dt);
    }

    public void OnFootstep(AnimationEvent evt)
    {
        //กัน error 
    }
}
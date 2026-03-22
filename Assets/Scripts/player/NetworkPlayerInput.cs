using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerInput : NetworkBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }

    private void Update()
    {
        if (!IsOwner)
            return;

        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        JumpPressed = Input.GetButtonDown("Jump");
        SprintHeld = Input.GetKey(KeyCode.LeftShift);

        SubmitInputServerRpc(MoveInput, JumpPressed, SprintHeld);
    }

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 move, bool jump, bool sprint)
    {
        MoveInput = move;
        JumpPressed = jump;
        SprintHeld = sprint;
    }

    public void ConsumeJump()
    {
        JumpPressed = false;
    }
}
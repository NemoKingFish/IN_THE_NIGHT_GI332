using StarterAssets;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
public class ClientPlayerMove : NetworkBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private ThirdPersonController thirdPersonController;
    private void Awake()
    {
        starterAssetsInputs.enabled = false;
        playerInput.enabled = false;
        thirdPersonController.enabled = false;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            starterAssetsInputs.enabled = true;
            playerInput.enabled = true;
        }
        if (IsServer)
        {
            thirdPersonController.enabled = true;
        }
    }
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint)
    {
        starterAssetsInputs.MoveInput(move);
        starterAssetsInputs.LookInput(look);
        starterAssetsInputs.JumpInput(jump);
        starterAssetsInputs.SprintInput(sprint);
    }
    private void LateUpdate()
    {
        if (!IsOwner)
        {
            return;
        }
        UpdateInputServerRpc(starterAssetsInputs.move, starterAssetsInputs.look,
            starterAssetsInputs.jump, starterAssetsInputs.sprint);
    }
}
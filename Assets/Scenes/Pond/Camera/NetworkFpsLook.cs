using Unity.Netcode;
using UnityEngine;

public class NetworkFpsLook : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerYawRoot;
    [SerializeField] private Transform pitchRoot;

    [Header("Settings")]
    [SerializeField] private float sensitivity = 180f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity * Time.deltaTime;

        playerYawRoot.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        SubmitYawServerRpc(playerYawRoot.eulerAngles.y);
    }

    [ServerRpc]
    private void SubmitYawServerRpc(float yaw)
    {
        Vector3 euler = transform.eulerAngles;
        euler.y = yaw;
        transform.eulerAngles = euler;
    }
}
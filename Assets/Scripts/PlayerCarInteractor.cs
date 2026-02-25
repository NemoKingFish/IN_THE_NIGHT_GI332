using Unity.Netcode;
using UnityEngine;

public class PlayerCarInteractor : NetworkBehaviour
{
    [Header("Keybind")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Driving Input")]
    public string verticalAxis = "Vertical";   // W/S
    public string horizontalAxis = "Horizontal"; // A/D

    // ใส่ ref ได้ 2 วิธี:
    // 1) ใช้ trigger หา "รถที่อยู่ใกล้"
    // 2) หรือใช้ Physics.OverlapSphere ในตอนกด E
    NetworkCarController nearbyCar;

    bool inCar;
    NetworkCarController currentCar;
    int seatIndex = -1;

    // ถ้ามีสคริปต์เดินของคุณ ให้ลากมาปิด/เปิดได้
    [Header("Optional: Your movement script")]
    public Behaviour movementToDisable;

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (!inCar)
            {
                if (nearbyCar != null)
                {
                    nearbyCar.RequestEnterServerRpc(NetworkObjectId);
                }
            }
            else
            {
                // ขอออก (Server จะเช็คว่ารถหยุดนิ่งไหม)
                if (currentCar != null)
                    currentCar.RequestExitServerRpc(NetworkObjectId);
            }
        }

        // ถ้าเป็นคนขับ ส่ง input ให้รถ
        if (inCar && currentCar != null && currentCar.IsMyDriverSeat(OwnerClientId))
        {
            float throttle = Input.GetAxisRaw(verticalAxis);
            float steer = Input.GetAxisRaw(horizontalAxis);
            currentCar.SubmitDriverInputServerRpc(throttle, steer);
        }
    }

    // ให้รถเรียก เพื่อปิด/เปิดการเดิน + เก็บสถานะ
    public void SetInCarState(bool value, NetworkCarController car, int seat)
    {
        inCar = value;
        currentCar = value ? car : null;
        seatIndex = seat;

        if (movementToDisable != null)
            movementToDisable.enabled = !value;

        // ถ้าใช้ CharacterController ก็ปิดได้
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = !value;
    }

    // --- หา "รถที่อยู่ใกล้" ด้วย Trigger ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;
        var car = other.GetComponentInParent<NetworkCarController>();
        if (car != null) nearbyCar = car;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        var car = other.GetComponentInParent<NetworkCarController>();
        if (car != null && car == nearbyCar) nearbyCar = null;
    }
}
using Unity.Netcode;
using UnityEngine;

public class NetworkCarController : NetworkBehaviour
{
    [Header("Seats (0 = Driver)")]
    public Transform[] seatPoints = new Transform[4];

    [Header("Enter Settings")]
    public float enterRadius = 2.0f;

    [Header("Exit Settings")]
    public float stopSpeedToExit = 0.15f; // รถต้องช้ากว่านี้ถึงลงได้

    [Header("Driving")]
    public float motorForce = 8000f;
    public float steerTorque = 2500f;
    public float maxSpeed = 18f;

    Rigidbody rb;

    // เก็บผู้เล่นที่นั่งแต่ละที่ (ClientId) - -1 = ว่าง
    private NetworkVariable<long>[] seatOwners;

    // input จากคนขับ (เก็บใน server)
    float throttleInput;
    float steerInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (seatOwners == null)
        {
            seatOwners = new NetworkVariable<long>[seatPoints.Length];
            for (int i = 0; i < seatOwners.Length; i++)
                seatOwners[i] = new NetworkVariable<long>(-1);
        }
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        long driverId = seatOwners[0].Value;
        bool hasDriver = driverId != -1;

        if (!hasDriver)
        {
            // ไม่มีคนขับก็ "ปล่อยไหล" ตามฟิสิกส์ได้ หรือจะหน่วงก็ได้
            return;
        }

        // จำกัดความเร็ว
        Vector3 flatVel = rb.linearVelocity; flatVel.y = 0;
        if (flatVel.magnitude < maxSpeed || Mathf.Sign(throttleInput) != Mathf.Sign(Vector3.Dot(transform.forward, flatVel.normalized)))
        {
            rb.AddForce(transform.forward * (throttleInput * motorForce) * Time.fixedDeltaTime, ForceMode.Force);
        }

        // เลี้ยว (ใช้ Torque)
        rb.AddTorque(Vector3.up * (steerInput * steerTorque) * Time.fixedDeltaTime, ForceMode.Force);
    }

    public bool IsStopped()
    {
        if (rb == null) return true;
        Vector3 v = rb.linearVelocity; v.y = 0;
        return v.magnitude <= stopSpeedToExit;
    }

    int FindFreeSeat()
    {
        for (int i = 0; i < seatOwners.Length; i++)
        {
            if (seatOwners[i].Value == -1) return i;
        }
        return -1;
    }

    int FindSeatByOwner(long clientId)
    {
        for (int i = 0; i < seatOwners.Length; i++)
        {
            if (seatOwners[i].Value == clientId) return i;
        }
        return -1;
    }

    bool IsWithinEnterRadius(Transform playerTf)
    {
        Vector3 p = playerTf.position;
        Vector3 c = transform.position;
        p.y = 0; c.y = 0;
        return Vector3.Distance(p, c) <= enterRadius;
    }

    // -------------------------
    // RPC: ขอขึ้นรถ / ลงรถ
    // -------------------------

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(ulong playerNetObjectId, ServerRpcParams rpcParams = default)
    {
        var senderClientId = (long)rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNo))
            return;

        // ถ้าผู้ส่งไม่ใช่เจ้าของ player ตัวนี้ -> ปฏิเสธ
        if (playerNo.OwnerClientId != (ulong)senderClientId) return;

        // ถ้านั่งอยู่แล้ว -> ไม่ให้ซ้ำ
        if (FindSeatByOwner(senderClientId) != -1) return;

        // ระยะ
        if (!IsWithinEnterRadius(playerNo.transform)) return;

        int seatIndex = FindFreeSeat();
        if (seatIndex == -1) return;

        seatOwners[seatIndex].Value = senderClientId;

        // สั่ง client ทุกคนให้จัดตำแหน่งผู้เล่นให้นั่งที่นั่งนี้
        AttachPlayerClientRpc(playerNetObjectId, seatIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitServerRpc(ulong playerNetObjectId, ServerRpcParams rpcParams = default)
    {
        var senderClientId = (long)rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNo))
            return;

        if (playerNo.OwnerClientId != (ulong)senderClientId) return;

        int seatIndex = FindSeatByOwner(senderClientId);
        if (seatIndex == -1) return;

        // ลงได้เฉพาะตอนรถหยุดนิ่ง
        if (!IsStopped()) return;

        seatOwners[seatIndex].Value = -1;

        DetachPlayerClientRpc(playerNetObjectId, seatIndex);
    }

    // คนขับส่ง input เข้ามา (server เก็บไว้ แล้วคุมฟิสิกส์)
    [ServerRpc(RequireOwnership = false)]
    public void SubmitDriverInputServerRpc(float throttle, float steer, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;

        // ต้องเป็นคนที่นั่ง Driver seat จริง
        if (seatOwners[0].Value != sender) return;

        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    // -------------------------
    // ClientRpc: จัดการนั่ง/ลง
    // -------------------------

    [ClientRpc]
    void AttachPlayerClientRpc(ulong playerNetObjectId, int seatIndex)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNo))
            return;

        var playerTf = playerNo.transform;
        playerTf.SetParent(seatPoints[seatIndex], worldPositionStays: false);
        playerTf.localPosition = Vector3.zero;
        playerTf.localRotation = Quaternion.identity;

        // ปิดการเดินของผู้เล่น (ถ้าคุณใช้ CharacterController/สคริปต์เดิน ให้ปิดตรงนี้)
        var mover = playerNo.GetComponent<PlayerCarInteractor>();
        if (mover != null) mover.SetInCarState(true, this, seatIndex);
    }

    [ClientRpc]
    void DetachPlayerClientRpc(ulong playerNetObjectId, int seatIndex)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNo))
            return;

        var playerTf = playerNo.transform;
        playerTf.SetParent(null, true);

        // วางคนลงข้างๆ รถ (ออกทางด้านข้างนิดนึง)
        Vector3 exitPos = transform.position + transform.right * 2.0f;
        exitPos.y = playerTf.position.y;
        playerTf.position = exitPos;

        var mover = playerNo.GetComponent<PlayerCarInteractor>();
        if (mover != null) mover.SetInCarState(false, this, -1);
    }

    // helper สำหรับ UI/เช็ค
    public bool IsMyDriverSeat(ulong clientId) => seatOwners[0].Value == (long)clientId;
    public bool IsAnySeatOwnedBy(ulong clientId) => FindSeatByOwner((long)clientId) != -1;
}
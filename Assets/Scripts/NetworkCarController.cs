using Unity.Netcode;
using UnityEngine;
/*
public class NetworkCarController : NetworkBehaviour
{
    [Header("Seats (0 = Driver)")]
    public Transform[] seatPoints = new Transform[4];

    [Header("Enter Settings")]
    public float enterRadius = 2.0f;

    [Header("Exit Settings")]
    public float stopSpeedToExit = 0.15f;

    [Header("Driving")]
    public float motorForce = 8000f;
    public float steerTorque = 2500f;
    public float maxSpeed = 18f;

    private Rigidbody rb;

    // ✅ ต้องเป็น field ที่สร้างตั้งแต่ก่อน Spawn
    private NetworkVariable<long> seat0 = new NetworkVariable<long>(-1); // driver
    private NetworkVariable<long> seat1 = new NetworkVariable<long>(-1);
    private NetworkVariable<long> seat2 = new NetworkVariable<long>(-1);
    private NetworkVariable<long> seat3 = new NetworkVariable<long>(-1);

    float throttleInput;
    float steerInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    long GetSeatOwner(int i) => i switch
    {
        0 => seat0.Value,
        1 => seat1.Value,
        2 => seat2.Value,
        3 => seat3.Value,
        _ => -1
    };

    void SetSeatOwner(int i, long v)
    {
        switch (i)
        {
            case 0: seat0.Value = v; break;
            case 1: seat1.Value = v; break;
            case 2: seat2.Value = v; break;
            case 3: seat3.Value = v; break;
        }
    }

    int FindFreeSeat()
    {
        for (int i = 0; i < 4; i++)
            if (GetSeatOwner(i) == -1) return i;
        return -1;
    }

    int FindSeatByOwner(long clientId)
    {
        for (int i = 0; i < 4; i++)
            if (GetSeatOwner(i) == clientId) return i;
        return -1;
    }

    bool IsWithinEnterRadius(Transform playerTf)
    {
        Vector3 p = playerTf.position; p.y = 0;
        Vector3 c = transform.position; c.y = 0;
        return Vector3.Distance(p, c) <= enterRadius;
    }

    public bool IsStopped()
    {
        Vector3 v = rb.linearVelocity; v.y = 0;
        return v.magnitude <= stopSpeedToExit;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        long driverId = seat0.Value;
        if (driverId == -1) return;

        Vector3 flatVel = rb.linearVelocity; flatVel.y = 0;
        if (flatVel.magnitude < maxSpeed ||
            Mathf.Sign(throttleInput) != Mathf.Sign(Vector3.Dot(transform.forward, flatVel.normalized)))
        {
            rb.AddForce(transform.forward * (throttleInput * motorForce) * Time.fixedDeltaTime, ForceMode.Force);
        }

        rb.AddTorque(Vector3.up * (steerInput * steerTorque) * Time.fixedDeltaTime, ForceMode.Force);
    }

    // ---------------- RPC ----------------

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(ulong playerNetObjectId, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO))
            return;

        if (playerNO.OwnerClientId != (ulong)sender) return;
        if (FindSeatByOwner(sender) != -1) return;
        if (!IsWithinEnterRadius(playerNO.transform)) return;

        int seatIndex = FindFreeSeat();
        if (seatIndex == -1) return;

        SetSeatOwner(seatIndex, sender);

        AttachPlayerClientRpc(playerNetObjectId, seatIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitServerRpc(ulong playerNetObjectId, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO))
            return;

        if (playerNO.OwnerClientId != (ulong)sender) return;

        int seatIndex = FindSeatByOwner(sender);
        if (seatIndex == -1) return;

        if (!IsStopped()) return;

        SetSeatOwner(seatIndex, -1);
        DetachPlayerClientRpc(playerNetObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitDriverInputServerRpc(float throttle, float steer, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;
        if (seat0.Value != sender) return;

        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    // -------------- ClientRpc --------------

    [ClientRpc]
    void AttachPlayerClientRpc(ulong playerNetObjectId, int seatIndex)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO))
            return;

        // ✅ ใช้ TrySetParent กับ "รถ (NetworkObject)" เท่านั้น
        // แล้วค่อยย้ายไปยัง seat ด้วย local transform
        if (playerNO.TrySetParent(GetComponent<NetworkObject>(), worldPositionStays: true))
        {
            // แปลง seat world -> car local แล้วใส่ให้ player
            Transform seat = seatPoints[seatIndex];
            Transform car = transform;

            Vector3 localPos = car.InverseTransformPoint(seat.position);
            Quaternion localRot = Quaternion.Inverse(car.rotation) * seat.rotation;

            playerNO.transform.localPosition = localPos;
            playerNO.transform.localRotation = localRot;
        }

        var interactor = playerNO.GetComponent<PlayerCarInteractor>();
        if (interactor != null) interactor.SetInCarState(true, this, seatIndex);
    }

    [ClientRpc]
    void DetachPlayerClientRpc(ulong playerNetObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO))
            return;

        playerNO.TrySetParent((NetworkObject)null, worldPositionStays: true);

        // วางลงข้างรถ
        Vector3 exitPos = transform.position + transform.right * 2.0f;
        exitPos.y = playerNO.transform.position.y;
        playerNO.transform.position = exitPos;

        var interactor = playerNO.GetComponent<PlayerCarInteractor>();
        if (interactor != null) interactor.SetInCarState(false, this, -1);
    }

    // helpers
    public bool IsMyDriverSeat(ulong clientId) => seat0.Value == (long)clientId;
} */
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class NetworkCarController : NetworkBehaviour
{
    [Header("Seats (0 = Driver)")]
    [Tooltip("Transforms for each seat. (0 = driver). Not NetworkObjects.")]
    public Transform[] seatPoints = new Transform[4];

    [Header("Enter / Exit")]
    [Min(0.1f)] public float enterRadius = 2.0f;
    [Min(0.01f)] public float stopSpeedToExit = 0.15f;
    [Tooltip("Local offset (car space) where player will be placed when exiting.")]
    public Vector3 exitOffsetLocal = new Vector3(2f, 0f, 0f);

    [Header("Chassis (Geometry)")]
    [Tooltip("Distance between front and rear axle (meters).")]
    [Min(0.2f)] public float wheelBase = 2.6f;

    [Tooltip("Distance between left and right wheels (meters). Used for Ackermann-ish.")]
    [Min(0.2f)] public float trackWidth = 1.6f;

    [Header("Speed (Arcade Longitudinal)")]
    [Min(0.1f)] public float maxForwardSpeed = 18f;  // m/s
    [Min(0.1f)] public float maxReverseSpeed = 7f;   // m/s
    [Min(0.1f)] public float acceleration = 12f;     // m/s^2
    [Min(0.1f)] public float braking = 20f;          // m/s^2
    [Min(0f)] public float coastDecel = 3.5f;      // m/s^2

    [Header("Steering (Ackermann-ish + Speed Based)")]
    [Range(1f, 60f)] public float maxSteerAngleDeg = 32f;

    [Tooltip("How quickly steering angle follows input (higher = snappier).")]
    [Min(0.1f)] public float steerResponse = 10f;

    [Tooltip("Steer vs speed curve. X=speed01 (0..1), Y=steer multiplier (0..1).")]
    public AnimationCurve steerBySpeed = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.4f, 0.8f),
        new Keyframe(1f, 0.35f)
    );

    [Header("Yaw / Spin Clamp")]
    [Tooltip("Clamp yaw rate when moving forward (deg/s).")]
    [Range(30f, 360f)] public float maxYawRateForwardDeg = 170f;

    [Tooltip("Clamp yaw rate when reversing (deg/s).")]
    [Range(30f, 360f)] public float maxYawRateReverseDeg = 110f;

    [Header("Grip / Slip Control (Slip Angle Clamp)")]
    [Tooltip("How much to remove sideways velocity each physics step (0=ice, 1=grippy).")]
    [Range(0f, 1.2f)] public float lateralGrip = 0.75f;

    [Tooltip("Max allowed slip angle (deg) between velocity direction and car forward. Lower = straighter.")]
    [Range(1f, 45f)] public float maxSlipAngleDeg = 14f;

    [Tooltip("How strongly we steer the velocity direction back toward forward when slip exceeds limit.")]
    [Range(0f, 10f)] public float slipClampStrength = 3.0f;

    [Tooltip("Extra yaw stability (damps angular velocity Y).")]
    [Range(0f, 5f)] public float yawStability = 1.2f;

    [Header("Rigidbody Tuning")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0.1f);

    Rigidbody rb;

    // Seats (NetworkVariables must be fields, not runtime-created arrays)
    NetworkVariable<long> seat0 = new(-1);
    NetworkVariable<long> seat1 = new(-1);
    NetworkVariable<long> seat2 = new(-1);
    NetworkVariable<long> seat3 = new(-1);

    // Server-side inputs
    float throttleInput; // -1..1
    float steerInput;    // -1..1
    float currentSteerDeg;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Only needs to be set once; applies on all instances
        rb.centerOfMass += centerOfMassOffset;
    }

    // ---------------- Seat helpers ----------------
    long GetSeatOwner(int i) => i switch { 0 => seat0.Value, 1 => seat1.Value, 2 => seat2.Value, 3 => seat3.Value, _ => -1 };
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
        for (int i = 0; i < 4; i++) if (GetSeatOwner(i) == -1) return i;
        return -1;
    }
    int FindSeatByOwner(long clientId)
    {
        for (int i = 0; i < 4; i++) if (GetSeatOwner(i) == clientId) return i;
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

    public bool IsMyDriverSeat(ulong clientId) => seat0.Value == (long)clientId;

    // ---------------- Driving (SERVER authoritative) ----------------
    void FixedUpdate()
    {
        if (!IsServer) return;

        bool hasDriver = seat0.Value != -1;
        if (!hasDriver)
        {
            throttleInput = 0f;
            steerInput = 0f;
        }

        float dt = Time.fixedDeltaTime;

        // planar basis
        Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0; right.Normalize();

        // planar velocity
        Vector3 vel = rb.linearVelocity; vel.y = 0;
        float speed = vel.magnitude;
        float forwardSpeed = Vector3.Dot(vel, fwd);

        // 1) Speed control (accelerate/brake/coast)
        float targetSpeed = 0f;
        if (throttleInput > 0f) targetSpeed = throttleInput * maxForwardSpeed;
        else if (throttleInput < 0f) targetSpeed = throttleInput * maxReverseSpeed;

        float accel;
        if (Mathf.Abs(throttleInput) < 0.01f) accel = coastDecel;              // ปล่อยคันเร่ง
        else if (Mathf.Sign(targetSpeed) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.5f)
            accel = braking;                                                   // กดสวนทิศ = เบรกแรง
        else
            accel = acceleration;

        float newForwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, accel * dt);

        // 2) Steering (speed based + smooth)
        // ยิ่งเร็ว ยิ่งเลี้ยวน้อย
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, maxForwardSpeed));
        float steerMul = Mathf.Clamp01(steerBySpeed.Evaluate(speed01));

        float targetSteerDeg = steerInput * maxSteerAngleDeg * steerMul;
        currentSteerDeg = Mathf.Lerp(currentSteerDeg, targetSteerDeg, steerResponse * dt);

        // 3) Turn by yaw (bicycle model-ish)
        // yawRate ≈ v / R, R = wheelBase / tan(delta)
        float deltaRad = currentSteerDeg * Mathf.Deg2Rad;
        float tan = Mathf.Tan(deltaRad);

        float yawRateRad = 0f;
        if (Mathf.Abs(tan) > 0.0001f && Mathf.Abs(newForwardSpeed) > 0.05f)
        {
            float radius = wheelBase / tan; // signed
            yawRateRad = newForwardSpeed / Mathf.Max(0.01f, radius);
        }

        // clamp yaw rate (กัน spin ตอนถอยหลัง)
        float maxYawRateRad = (newForwardSpeed >= 0f ? maxYawRateForwardDeg : maxYawRateReverseDeg) * Mathf.Deg2Rad;
        yawRateRad = Mathf.Clamp(yawRateRad, -maxYawRateRad, maxYawRateRad);

        Quaternion yawDelta = Quaternion.Euler(0f, yawRateRad * Mathf.Rad2Deg * dt, 0f);
        rb.MoveRotation(rb.rotation * yawDelta);

        // 4) Lateral grip (remove sideways velocity)
        float lateralSpeed = Vector3.Dot(vel, right);
        float newLateralSpeed = Mathf.Lerp(lateralSpeed, 0f, lateralGrip);

        Vector3 newPlanarVel = fwd * newForwardSpeed + right * newLateralSpeed;

        // 5) Slip-angle clamp (ช่วยให้ “วิ่งตรง” ไม่ปัดง่าย)
        newPlanarVel = ClampSlipAngle(newPlanarVel, fwd, maxSlipAngleDeg, slipClampStrength, dt);

        rb.linearVelocity = new Vector3(newPlanarVel.x, rb.linearVelocity.y, newPlanarVel.z);

        // 6) yaw stability (damp angular Y)
        Vector3 ang = rb.angularVelocity;
        ang.y = Mathf.Lerp(ang.y, 0f, yawStability * dt);
        rb.angularVelocity = ang;
    }

    static Vector3 ClampSlipAngle(Vector3 planarVel, Vector3 forward, float maxSlipDeg, float strength, float dt)
    {
        float speed = planarVel.magnitude;
        if (speed < 0.01f) return planarVel;

        Vector3 velDir = planarVel / speed;
        float slipDeg = Vector3.SignedAngle(forward, velDir, Vector3.up);
        float abs = Mathf.Abs(slipDeg);

        if (abs <= maxSlipDeg) return planarVel;

        // ดึงทิศทางความเร็วกลับเข้าใกล้ forward
        float t = Mathf.Clamp01((abs - maxSlipDeg) / Mathf.Max(0.01f, maxSlipDeg));
        float pull = Mathf.Clamp01(strength * t * dt);

        Vector3 desired = (Vector3.Dot(forward, planarVel) >= 0f) ? forward : -forward;
        Vector3 newDir = Vector3.RotateTowards(velDir, desired, pull, 0f);

        return newDir.normalized * speed;
    }
    /*
    static float ComputeEffectiveSteerDeg_Ackermann(float centerSteerDeg, float track, float wheelBaseMeters)
    {
        // For small angles, centerSteer is fine.
        // We compute turning radius from center angle, then compute inner & outer angles.
        // Finally approximate effective steer as average of inner & outer.
        float abs = Mathf.Abs(centerSteerDeg);
        if (abs < 0.01f) return 0f;

        float sign = Mathf.Sign(centerSteerDeg);
        float deltaRad = abs * Mathf.Deg2Rad;

        float tan = Mathf.Tan(deltaRad);
        if (tan < 0.0001f) return centerSteerDeg;

        float R = wheelBaseMeters / tan; // positive radius magnitude for center

        // inner/outer radii
        float Rin = Mathf.Max(0.01f, R - track * 0.5f);
        float Rout = Mathf.Max(0.01f, R + track * 0.5f);

        float deltaIn = Mathf.Atan(wheelBaseMeters / Rin) * Mathf.Rad2Deg;
        float deltaOut = Mathf.Atan(wheelBaseMeters / Rout) * Mathf.Rad2Deg;

        float effective = (deltaIn + deltaOut) * 0.5f;
        return effective * sign;
    } */

   /*
    static Vector3 ClampSlipAngle(Vector3 planarVel, Vector3 forward, float maxSlipDeg, float strength, float dt)
    {
        float speed = planarVel.magnitude;
        if (speed < 0.01f) return planarVel;

        Vector3 velDir = planarVel / speed;
        float slipDeg = Vector3.SignedAngle(forward, velDir, Vector3.up);
        float abs = Mathf.Abs(slipDeg);

        if (abs <= maxSlipDeg) return planarVel;

        // Pull velocity direction back toward forward direction
        float t = Mathf.Clamp01((abs - maxSlipDeg) / Mathf.Max(0.01f, maxSlipDeg));
        float pull = Mathf.Clamp01(strength * t * dt);

        Vector3 targetDir = Vector3.RotateTowards(velDir, forward * Mathf.Sign(Vector3.Dot(forward, planarVel.normalized)), pull, 0f);
        return targetDir.normalized * speed;
    } */

    // ---------------- RPCs ----------------
    [ServerRpc(RequireOwnership = false)]
    public void SubmitDriverInputServerRpc(float throttle, float steer, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;
        if (seat0.Value != sender) return;

        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(ulong playerNetObjectId, ServerRpcParams rpcParams = default)
    {
        long sender = (long)rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO)) return;
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

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO)) return;
        if (playerNO.OwnerClientId != (ulong)sender) return;

        int seatIndex = FindSeatByOwner(sender);
        if (seatIndex == -1) return;

        if (!IsStopped()) return;

        SetSeatOwner(seatIndex, -1);
        DetachPlayerClientRpc(playerNetObjectId);
    }

    // ---------------- Client RPC: seat attach/detach ----------------
    [ClientRpc]
    void AttachPlayerClientRpc(ulong playerNetObjectId, int seatIndex)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO)) return;

        // Parent to car NetworkObject (allowed by NGO)
        playerNO.TrySetParent(GetComponent<NetworkObject>(), worldPositionStays: true);

        // Snap to seat pose
        Transform seat = seatPoints[seatIndex];
        Transform car = transform;

        Vector3 localPos = car.InverseTransformPoint(seat.position);
        Quaternion localRot = Quaternion.Inverse(car.rotation) * seat.rotation;

        playerNO.transform.localPosition = localPos;
        playerNO.transform.localRotation = localRot;

        var interactor = playerNO.GetComponent<PlayerCarInteractor>();
        if (interactor != null) interactor.SetInCarState(true, this, seatIndex);
    }

    [ClientRpc]
    void DetachPlayerClientRpc(ulong playerNetObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNO)) return;

        playerNO.TrySetParent((NetworkObject)null, worldPositionStays: true);

        Vector3 worldExit = transform.TransformPoint(exitOffsetLocal);
        worldExit.y = playerNO.transform.position.y;
        playerNO.transform.position = worldExit;

        var interactor = playerNO.GetComponent<PlayerCarInteractor>();
        if (interactor != null) interactor.SetInCarState(false, this, -1);
    }
}
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NetworkCarSimple : NetworkBehaviour
{
    [Header("Tuning")]
    public float accel = 18f;            // แรงเร่ง
    public float maxSpeed = 16f;         // m/s
    public float reverseSpeed = 8f;      // m/s
    public float brake = 25f;            // แรงเบรก
    public float turnStrength = 75f;     // องศา/วินาที ตอนความเร็วต่ำ
    public AnimationCurve turnBySpeed =  // เลี้ยวน้อยลงเมื่อเร็ว
        AnimationCurve.EaseInOut(0, 1f, 1f, 0.25f);

    [Header("Refs")]
    public Transform visualRoot; // ใส่ไว้ได้ ถ้าอยากให้โมเดลเอียง/อนิเมชัน (ไม่ใส่ก็ได้)

    private Rigidbody rb;

    // ใครเป็นคนขับ (NetworkObjectId ของ player)
    public NetworkVariable<ulong> DriverId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // input ล่าสุด (เก็บที่ server)
    float throttle; // -1..1
    float steer;    // -1..1
    bool braking;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public bool HasDriver => DriverId.Value != 0;

    // เรียกจาก SeatManager ตอนขึ้น/ลง
    [ServerRpc(RequireOwnership = false)]
    public void SetDriverServerRpc(ulong playerId)
    {
        DriverId.Value = playerId; // 0 = ไม่มีคนขับ
        throttle = 0;
        steer = 0;
        braking = false;
    }

    // ผู้ขับส่ง input มาที่ server
    [ServerRpc(RequireOwnership = false)]
    public void SubmitInputServerRpc(float throttle01, float steer01, bool brakeBtn, ServerRpcParams rpc = default)
    {
        // เช็คว่า "คนส่ง" ต้องเป็นคนขับเท่านั้น
        if (rpc.Receive.SenderClientId != DriverId.Value) return;

        throttle = Mathf.Clamp(throttle01, -1f, 1f);
        steer = Mathf.Clamp(steer01, -1f, 1f);
        braking = brakeBtn;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        // ไม่มีคนขับ = ปล่อยไหล (หรือจะหยุดก็ได้)
        if (!HasDriver)
        {
            throttle = 0;
            steer = 0;
            braking = false;
        }

        var vel = rb.linearVelocity;
        float speed = new Vector3(vel.x, 0, vel.z).magnitude;
        float speed01 = Mathf.Clamp01(speed / maxSpeed);

        // ----- Forward vector ตามรถ -----
        Vector3 forward = transform.forward;
        Vector3 planarVel = new Vector3(vel.x, 0, vel.z);

        // ----- throttle / brake -----
        if (braking)
        {
            // เบรก: ลดความเร็วแบบตรง ๆ
            rb.AddForce(-planarVel.normalized * brake, ForceMode.Acceleration);
        }
        else
        {
            // จำกัดความเร็วเดินหน้า/ถอย
            float targetMax = (throttle >= 0) ? maxSpeed : reverseSpeed;
            if (speed < targetMax + 0.2f)
            {
                rb.AddForce(forward * (throttle * accel), ForceMode.Acceleration);
            }
        }

        // ----- steering (กันหักแรง) -----
        // เลี้ยวตามความเร็ว: เร็ว = เลี้ยวน้อย
        float turnMul = turnBySpeed.Evaluate(speed01);
        float yawPerSec = turnStrength * turnMul;

        // เลี้ยวเฉพาะตอนมีการ “เคลื่อนที่” พอสมควร (กันหมุนฟรีตอนนิ่ง)
        if (speed > 0.2f)
        {
            float yaw = steer * yawPerSec * Time.fixedDeltaTime;
            Quaternion delta = Quaternion.Euler(0f, yaw, 0f);
            rb.MoveRotation(rb.rotation * delta);

            // ทำให้ความเร็ว “หันตามหน้า” นิดหน่อย (arcade ให้คุมง่าย)
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * rb.linearVelocity.magnitude, 0.05f);
        }
    }
}
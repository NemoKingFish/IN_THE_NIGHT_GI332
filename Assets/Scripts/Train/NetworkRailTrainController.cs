using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class NetworkRailTrainController : NetworkBehaviour
{
    private const ulong EMPTY_SEAT = ulong.MaxValue;

    [Header("Path")]
    [SerializeField] private CinemachinePathBase railPath;
    [SerializeField] private bool loopPath = false;
    [SerializeField] private bool rotateToPath = true;

    [Header("Seats")]
    [SerializeField] private Transform[] seatPoints = new Transform[4];
    [SerializeField] private Transform exitPoint;
    [SerializeField] private int maxPassengers = 4;

    [Header("Movement")]
    [SerializeField] private float maxForwardSpeed = 8f;
    [SerializeField] private float maxReverseSpeed = 4f;
    [SerializeField] private float forwardAcceleration = 4f;
    [SerializeField] private float reverseAcceleration = 3f;
    [SerializeField] private float brakePower = 10f;
    [SerializeField] private float idleDeceleration = 2f;

    [Header("Orientation Offset")]
    [SerializeField] private Vector3 modelRotationOffsetEuler = new Vector3(0f, 0f, 0f);

    [Header("Start Positions")]
    [SerializeField] private bool usePlacedPositionAsStart = true;
    [SerializeField] private int pathSearchSamples = 200;
    [SerializeField] private float startDistance = 0f;

    [Header("Input Send")]
    [SerializeField] private float inputSendInterval = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [SerializeField] private Rigidbody rb;

    private NetworkList<ulong> seatOwners;

    private readonly NetworkVariable<float> currentDistance =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> currentSpeed =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float serverThrottle;
    private bool serverBrake;

    private float nextInputSendTime;
    private float lastSentThrottle;
    private bool lastSentBrake;

    // สำคัญ: เริ่มต้นอย่าย้ายรถเข้ารางทันที
    private bool hasStartedMovingOnRail = false;

    public bool IsFull => GetFirstEmptySeat() == -1;

    public ulong DriverClientId
    {
        get
        {
            if (seatOwners == null || seatOwners.Count == 0) return EMPTY_SEAT;
            return seatOwners[0];
        }
    }

    public bool LocalClientIsDriver
    {
        get
        {
            if (!IsSpawned || NetworkManager.Singleton == null) return false;
            if (seatOwners == null || seatOwners.Count == 0) return false;
            return seatOwners[0] == NetworkManager.Singleton.LocalClientId;
        }
    }

    private void Awake()
    {
        seatOwners = new NetworkList<ulong>();

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[TRAIN] OnNetworkSpawn called | name={name} | IsSpawned={IsSpawned} | IsServer={IsServer}");
        if (IsServer)
        {
            if (seatOwners.Count == 0)
            {
                int seatCount = Mathf.Clamp(maxPassengers, 1, 4);
                for (int i = 0; i < seatCount; i++)
                {
                    seatOwners.Add(EMPTY_SEAT);
                }
            }

            if (usePlacedPositionAsStart && railPath != null)
            {
                currentDistance.Value = FindClosestDistanceOnPath(transform.position);
            }
            else
            {
                currentDistance.Value = startDistance;
            }

            currentSpeed.Value = 0f;
            serverThrottle = 0f;
            serverBrake = false;
            hasStartedMovingOnRail = false;

            if (showDebugLog)
            {
                Debug.Log($"[TRAIN] OnNetworkSpawn keep placed position = {transform.position}");
                Debug.Log($"[TRAIN] Cached start distance = {currentDistance.Value}");
            }
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (LocalClientIsDriver)
        {
            ReadDriverInput();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (railPath == null) return;

        SimulateTrain(Time.fixedDeltaTime);
        SnapPassengersToSeats();
    }

    private void ReadDriverInput()
    {
        float throttle = 0f;
        bool brake = false;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard.wKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed) throttle -= 1f;
            brake = keyboard.spaceKey.isPressed;
        }
        else
#endif
        {
            if (Input.GetKey(KeyCode.W)) throttle += 1f;
            if (Input.GetKey(KeyCode.S)) throttle -= 1f;
            brake = Input.GetKey(KeyCode.Space);
        }

        if (IsServer)
        {
            serverThrottle = throttle;
            serverBrake = brake;
            return;
        }

        bool changed = !Mathf.Approximately(throttle, lastSentThrottle) || brake != lastSentBrake;
        bool reachedSendTime = Time.time >= nextInputSendTime;

        if (changed || reachedSendTime)
        {
            SubmitDriverInputServerRpc(throttle, brake);

            lastSentThrottle = throttle;
            lastSentBrake = brake;
            nextInputSendTime = Time.time + inputSendInterval;
        }
    }

    private void SimulateTrain(float deltaTime)
    {
        float speed = currentSpeed.Value;

        if (serverBrake)
        {
            speed = Mathf.MoveTowards(speed, 0f, brakePower * deltaTime);
        }
        else if (serverThrottle > 0f)
        {
            speed = Mathf.MoveTowards(speed, maxForwardSpeed, forwardAcceleration * deltaTime);
        }
        else if (serverThrottle < 0f)
        {
            speed = Mathf.MoveTowards(speed, -maxReverseSpeed, reverseAcceleration * deltaTime);
        }
        else
        {
            speed = Mathf.MoveTowards(speed, 0f, idleDeceleration * deltaTime);
        }

        float newDistance = currentDistance.Value + speed * deltaTime;
        float pathLength = Mathf.Max(railPath.PathLength, 0.001f);

        if (loopPath)
        {
            if (newDistance < 0f) newDistance += pathLength;
            if (newDistance > pathLength) newDistance -= pathLength;
        }
        else
        {
            newDistance = Mathf.Clamp(newDistance, 0f, pathLength);

            if ((Mathf.Approximately(newDistance, 0f) && speed < 0f) ||
                (Mathf.Approximately(newDistance, pathLength) && speed > 0f))
            {
                speed = 0f;
            }
        }

        currentSpeed.Value = speed;
        currentDistance.Value = newDistance;

        // เริ่มตามรางเฉพาะตอนมีการขับจริง
        bool shouldStartFollowingRail =
            !Mathf.Approximately(serverThrottle, 0f) ||
            !Mathf.Approximately(speed, 0f);

        if (shouldStartFollowingRail)
        {
            hasStartedMovingOnRail = true;
        }

        if (hasStartedMovingOnRail)
        {
            ApplyPoseFromPath(newDistance);
        }
    }

    private void ApplyPoseFromPath(float distance)
    {
        Vector3 localPos = railPath.EvaluatePositionAtUnit(
            distance,
            CinemachinePathBase.PositionUnits.Distance
        );

        Quaternion localRot = railPath.EvaluateOrientationAtUnit(
            distance,
            CinemachinePathBase.PositionUnits.Distance
        );

        Vector3 worldPos = railPath.transform.TransformPoint(localPos);
        Quaternion worldRot = railPath.transform.rotation * localRot;

        Quaternion modelOffset = Quaternion.Euler(modelRotationOffsetEuler);
        worldRot = worldRot * modelOffset;

        if (rotateToPath)
        {
            transform.SetPositionAndRotation(worldPos, worldRot);
        }
        else
        {
            transform.position = worldPos;
        }
    }

    private void SnapPassengersToSeats()
    {
        for (int i = 0; i < seatOwners.Count; i++)
        {
            ulong clientId = seatOwners[i];
            if (clientId == EMPTY_SEAT) continue;
            if (i >= seatPoints.Length || seatPoints[i] == null) continue;

            NetworkObject playerObject = GetPlayerObject(clientId);
            if (playerObject == null) continue;

            playerObject.transform.SetPositionAndRotation(seatPoints[i].position, seatPoints[i].rotation);
        }
    }

    private NetworkObject GetPlayerObject(ulong clientId)
    {
        if (NetworkManager == null) return null;
        if (!NetworkManager.ConnectedClients.ContainsKey(clientId)) return null;

        return NetworkManager.ConnectedClients[clientId].PlayerObject;
    }

    private int GetFirstEmptySeat()
    {
        if (seatOwners == null) return -1;

        for (int i = 0; i < seatOwners.Count; i++)
        {
            if (seatOwners[i] == EMPTY_SEAT)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetSeatIndexOf(ulong clientId)
    {
        if (seatOwners == null) return -1;

        for (int i = 0; i < seatOwners.Count; i++)
        {
            if (seatOwners[i] == clientId)
            {
                return i;
            }
        }

        return -1;
    }

    public bool IsClientSeated(ulong clientId)
    {
        return GetSeatIndexOf(clientId) != -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (showDebugLog)
        {
            Debug.Log($"[TRAIN] RequestEnter from client {senderId}");
        }

        if (IsClientSeated(senderId))
        {
            if (showDebugLog)
            {
                Debug.Log("[TRAIN] Client already seated");
            }
            return;
        }

        int seatIndex = GetFirstEmptySeat();
        if (seatIndex == -1)
        {
            if (showDebugLog)
            {
                Debug.Log("[TRAIN] No empty seat");
            }
            return;
        }

        NetworkObject playerObject = GetPlayerObject(senderId);
        if (playerObject == null)
        {
            if (showDebugLog)
            {
                Debug.Log("[TRAIN] PlayerObject not found");
            }
            return;
        }

        seatOwners[seatIndex] = senderId;

        if (showDebugLog)
        {
            Debug.Log($"[TRAIN] Client {senderId} seated at {seatIndex}");
        }

        if (seatIndex < seatPoints.Length && seatPoints[seatIndex] != null)
        {
            playerObject.transform.SetPositionAndRotation(seatPoints[seatIndex].position, seatPoints[seatIndex].rotation);
        }

        var playerSeatState = playerObject.GetComponent<NetworkPlayerSeatState>();
        if (playerSeatState != null)
        {
            playerSeatState.SetSeatedServer(true, this.NetworkObjectId, seatIndex);
        }

        NotifySeatStateClientRpc(
            new NetworkObjectReference(playerObject),
            true,
            seatIndex,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { senderId }
                }
            });
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        int seatIndex = GetSeatIndexOf(senderId);
        if (seatIndex == -1) return;

        seatOwners[seatIndex] = EMPTY_SEAT;

        NetworkObject playerObject = GetPlayerObject(senderId);
        if (playerObject == null) return;

        Vector3 exitPos = exitPoint != null
            ? exitPoint.position
            : transform.position + transform.right * 2f;

        Quaternion exitRot = exitPoint != null
            ? exitPoint.rotation
            : transform.rotation;

        playerObject.transform.SetPositionAndRotation(exitPos, exitRot);

        var playerSeatState = playerObject.GetComponent<NetworkPlayerSeatState>();
        if (playerSeatState != null)
        {
            playerSeatState.SetSeatedServer(false, 0, -1);
        }

        NotifySeatStateClientRpc(
            new NetworkObjectReference(playerObject),
            false,
            -1,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { senderId }
                }
            });

        if (seatIndex == 0)
        {
            serverThrottle = 0f;
            serverBrake = true;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitDriverInputServerRpc(float throttle, bool brake, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (DriverClientId != senderId) return;

        serverThrottle = Mathf.Clamp(throttle, -1f, 1f);
        serverBrake = brake;
    }

    [ClientRpc]
    private void NotifySeatStateClientRpc(NetworkObjectReference playerRef, bool seated, int seatIndex, ClientRpcParams clientRpcParams = default)
    {
        if (!playerRef.TryGet(out NetworkObject playerObject)) return;

        var seatState = playerObject.GetComponent<NetworkPlayerSeatState>();
        if (seatState != null)
        {
            seatState.SetSeatedLocal(seated, this, seatIndex);
        }
    }

    private float FindClosestDistanceOnPath(Vector3 targetWorldPos)
    {
        float pathLength = Mathf.Max(railPath.PathLength, 0.001f);
        float bestDistance = 0f;
        float bestSqrDist = float.MaxValue;

        for (int i = 0; i <= pathSearchSamples; i++)
        {
            float t = (i / (float)pathSearchSamples) * pathLength;

            Vector3 localPos = railPath.EvaluatePositionAtUnit(
                t,
                CinemachinePathBase.PositionUnits.Distance
            );

            Vector3 worldPos = railPath.transform.TransformPoint(localPos);
            float sqrDist = (worldPos - targetWorldPos).sqrMagnitude;

            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                bestDistance = t;
            }
        }

        return bestDistance;
    }
}
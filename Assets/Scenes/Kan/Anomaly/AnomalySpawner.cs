using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class AnomalySpawnPoint : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject anomalyPrefab;

    [Header("Anomaly Info")]
    [SerializeField] private int anomalyID;
    [SerializeField] private string anomalyName;
    [SerializeField] private AnomalyType assignedAnomalyType = AnomalyType.None;

    [Header("Spawn Chance")]
    [SerializeField, Range(0f, 100f)] private float anomalyChance = 30f;

    public NetworkVariable<int> currentAnomalyID = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<FixedString64Bytes> currentAnomalyName = new NetworkVariable<FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> currentAnomalyType = new NetworkVariable<int>(
        (int)AnomalyType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkObject currentSpawnedObject;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (currentSpawnedObject == null)
            SpawnNormal();
    }

    public void SpawnNormal()
    {
        if (!IsServer) return;
        ReplaceSpawnedObject(normalPrefab, false);
    }

    public void RollAndSpawn()
    {
        if (!IsServer) return;

        float randomValue = Random.Range(0f, 100f);
        bool spawnAnomaly = randomValue < anomalyChance;

        GameObject prefabToSpawn = spawnAnomaly ? anomalyPrefab : normalPrefab;
        ReplaceSpawnedObject(prefabToSpawn, spawnAnomaly);
    }

    private void ReplaceSpawnedObject(GameObject prefabToSpawn, bool asAnomaly)
    {
        if (currentSpawnedObject != null)
        {
            if (currentSpawnedObject.IsSpawned)
                currentSpawnedObject.Despawn(true);
            else
                Destroy(currentSpawnedObject.gameObject);

            currentSpawnedObject = null;
        }

        GameObject obj = Instantiate(prefabToSpawn, transform.position, transform.rotation);
        currentSpawnedObject = obj.GetComponent<NetworkObject>();

        if (currentSpawnedObject == null)
        {
            Debug.LogError($"Prefab {prefabToSpawn.name} has no NetworkObject");
            Destroy(obj);
            return;
        }

        if (asAnomaly)
        {
            currentAnomalyID.Value = anomalyID;
            currentAnomalyName.Value = anomalyName;
            currentAnomalyType.Value = (int)assignedAnomalyType;
        }
        else
        {
            currentAnomalyID.Value = -1;
            currentAnomalyName.Value = "Normal";
            currentAnomalyType.Value = (int)AnomalyType.None;
        }

        currentSpawnedObject.Spawn(true);
    }

    public bool HasAnomaly()
    {
        return GetCurrentAnomalyType() != AnomalyType.None;
    }

    public AnomalyType GetCurrentAnomalyType()
    {
        return (AnomalyType)currentAnomalyType.Value;
    }

    public AnomalyType GetAssignedAnomalyType()
    {
        return assignedAnomalyType;
    }

    public int GetAnomalyID()
    {
        return anomalyID;
    }

    public string GetAnomalyName()
    {
        return anomalyName;
    }

    public string GetAnomalyTypeName()
    {
        return assignedAnomalyType.ToString();
    }
}
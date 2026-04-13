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

    [Header("Prefab Pivot")]
    [SerializeField] private bool usePrefabPivot = true;
    [SerializeField] private string prefabPivotName = "SpawnPivot";

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

        GameObject obj = InstantiatePrefabAtSpawnPoint(prefabToSpawn);
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

    private GameObject InstantiatePrefabAtSpawnPoint(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);

        if (!usePrefabPivot)
        {
            obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
            return obj;
        }

        Transform pivot = FindChildRecursive(obj.transform, prefabPivotName);

        if (pivot == null)
        {
            Debug.LogWarning(
                $"Prefab {prefab.name} does not have a child named '{prefabPivotName}'. Using root transform instead.",
                obj
            );

            obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
            return obj;
        }

        // ทำให้ pivot ของ prefab มาตรงกับ AnomalySpawnPoint
        Quaternion rootRotation = transform.rotation * Quaternion.Inverse(pivot.localRotation);
        Vector3 rootPosition = transform.position - (rootRotation * pivot.localPosition);

        obj.transform.SetPositionAndRotation(rootPosition, rootRotation);
        return obj;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
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
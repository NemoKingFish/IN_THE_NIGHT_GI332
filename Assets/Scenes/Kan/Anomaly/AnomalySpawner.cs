using Unity.Netcode;
using UnityEngine;

public class AnomalySpawnPoint : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject anomalyPrefab;

    [Header("Spawn Chance")]
    [SerializeField, Range(0f, 100f)] private float anomalyChance;

    [Header("Debug")]
    [SerializeField] private bool spawnedAsAnomaly;

    private NetworkObject currentSpawnedObject;

    public override void OnNetworkSpawn()
    {
        // ให้ Server เท่านั้นที่ตัดสินใจสุ่ม
        if (!IsServer) return;

        SpawnObject();
    }

    private void SpawnObject()
    {
        if (currentSpawnedObject != null) return;

        float randomValue = Random.Range(0f, 100f);
        bool isAnomaly = randomValue < anomalyChance;

        spawnedAsAnomaly = isAnomaly;

        GameObject prefabToSpawn = isAnomaly ? anomalyPrefab : normalPrefab;

        GameObject obj = Instantiate(prefabToSpawn, transform.position, transform.rotation);

        currentSpawnedObject = obj.GetComponent<NetworkObject>();
        if (currentSpawnedObject == null)
        {
            Debug.LogError($"Prefab {prefabToSpawn.name} ไม่มี NetworkObject");
            Destroy(obj);
            return;
        }

        currentSpawnedObject.Spawn(true);

        Debug.Log($"[Server] Spawned {(isAnomaly ? "ANOMALY" : "NORMAL")} at {transform.position}, Roll = {randomValue}");
    }
}
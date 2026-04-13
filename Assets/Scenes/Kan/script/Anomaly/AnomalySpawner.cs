using System;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class AnomalySpawnPoint : MonoBehaviourPunCallbacks
{
    [Header("Normal State")]
    [SerializeField] private bool preferSceneObjectAsNormal = true;
    [SerializeField] private GameObject normalPrefab;

    [Header("Anomaly State")]
    [SerializeField] private GameObject anomalyPrefab;

    [Header("Anomaly Info")]
    [SerializeField] private int anomalyID;
    [SerializeField] private string anomalyName;
    [SerializeField] private AnomalyType assignedAnomalyType = AnomalyType.None;

    [Header("Spawn Chance")]
    [SerializeField, Range(0f, 100f)] private float anomalyChance = 30f;

    [Header("Moved Object")]
    [SerializeField] private Vector3 movedLocalPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 movedLocalEulerOffset = Vector3.zero;
    [HideInInspector, SerializeField] private bool usePrefabPivot = true;
    [HideInInspector, SerializeField] private string prefabPivotName = "SpawnPivot";

    public ObservableValue<int> currentAnomalyID = new ObservableValue<int>(-1);
    public ObservableValue<string> currentAnomalyName = new ObservableValue<string>("Normal");
    public ObservableValue<int> currentAnomalyType = new ObservableValue<int>((int)AnomalyType.None);

    private GameObject currentSpawnedObject;
    private string syncKeyPrefix;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private bool useSceneObjectAsNormal;
    private Renderer[] managedRenderers;
    private Collider[] managedColliders;
    private Collider2D[] managedColliders2D;
    private Canvas[] managedCanvases;
    private Light[] managedLights;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        CacheManagedComponents();
        useSceneObjectAsNormal = ShouldUseSceneObjectAsNormal();
        syncKeyPrefix = BuildSyncKeyPrefix();
        ApplyLocalState(-1, "Normal", AnomalyType.None);
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            SpawnNormal();
            return;
        }

        ApplyStateFromRoomProperties(PhotonNetwork.CurrentRoom.CustomProperties);

        if (PhotonNetwork.IsMasterClient && !HasSyncedState(PhotonNetwork.CurrentRoom.CustomProperties))
        {
            SpawnNormal();
        }
    }

    public override void OnJoinedRoom()
    {
        ApplyStateFromRoomProperties(PhotonNetwork.CurrentRoom.CustomProperties);

        if (PhotonNetwork.IsMasterClient && !HasSyncedState(PhotonNetwork.CurrentRoom.CustomProperties))
        {
            SpawnNormal();
        }
    }

    public void SpawnNormal()
    {
        if (!CanWriteState()) return;
        ApplySpawnState(false, true);
    }

    public void RollAndSpawn()
    {
        if (!CanWriteState()) return;

        float randomValue = Random.Range(0f, 100f);
        bool spawnAnomaly = randomValue < anomalyChance;
        ApplySpawnState(spawnAnomaly, true);
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        ApplyStateFromRoomProperties(propertiesThatChanged);
    }

    private void ApplySpawnState(bool asAnomaly, bool syncToRoom)
    {
        var nextType = asAnomaly ? assignedAnomalyType : AnomalyType.None;
        var nextId = asAnomaly ? anomalyID : -1;
        var nextName = asAnomaly ? anomalyName : "Normal";

        ApplyLocalState(nextId, nextName, nextType);

        if (syncToRoom && PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { GetPropertyKey("Id"), nextId },
                { GetPropertyKey("Name"), nextName },
                { GetPropertyKey("Type"), (int)nextType }
            });
        }
    }

    private void ApplyStateFromRoomProperties(PhotonHashtable properties)
    {
        if (!HasSyncedState(properties))
        {
            return;
        }

        var nextId = ReadInt(properties, GetPropertyKey("Id"), -1);
        var nextName = ReadString(properties, GetPropertyKey("Name"), "Normal");
        var nextType = (AnomalyType)ReadInt(properties, GetPropertyKey("Type"), (int)AnomalyType.None);

        ApplyLocalState(nextId, nextName, nextType);
    }

    private void ApplyLocalState(int nextId, string nextName, AnomalyType nextType)
    {
        ApplyPresentation(nextType);
        currentAnomalyID.Value = nextId;
        currentAnomalyName.Value = nextName;
        currentAnomalyType.Value = (int)nextType;
    }

    private void ApplyPresentation(AnomalyType nextType)
    {
        RestoreSceneObjectTransform();

        if (nextType == AnomalyType.None)
        {
            ShowNormalPresentation();
            return;
        }

        ShowAnomalyPresentation(nextType);
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

    private void ShowNormalPresentation()
    {
        DestroySpawnedObject();

        if (useSceneObjectAsNormal)
        {
            SetSceneObjectVisible(true);
            return;
        }

        SetSceneObjectVisible(false);

        if (normalPrefab != null)
        {
            currentSpawnedObject = InstantiateManagedPrefab(normalPrefab, originalLocalPosition, originalLocalRotation);
        }
    }

    private void ShowAnomalyPresentation(AnomalyType nextType)
    {
        DestroySpawnedObject();
        RestoreSceneObjectTransform();

        var anomalyLocalPosition = GetAnomalyLocalPosition(nextType);
        var anomalyLocalRotation = GetAnomalyLocalRotation(nextType);

        if (nextType == AnomalyType.MissingObject)
        {
            SetSceneObjectVisible(false);
            return;
        }

        var prefabToSpawn = ResolveAnomalyPrefab(nextType);

        if (useSceneObjectAsNormal && prefabToSpawn == null && nextType == AnomalyType.MovedObject)
        {
            SetSceneObjectVisible(true);
            ApplySceneObjectTransform(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        SetSceneObjectVisible(false);

        if (prefabToSpawn != null)
        {
            currentSpawnedObject = InstantiateManagedPrefab(prefabToSpawn, anomalyLocalPosition, anomalyLocalRotation);
        }
    }

    private GameObject ResolveAnomalyPrefab(AnomalyType nextType)
    {
        if (anomalyPrefab != null)
        {
            return anomalyPrefab;
        }

        return nextType == AnomalyType.MovedObject ? normalPrefab : null;
    }

    private GameObject InstantiateManagedPrefab(GameObject prefab, Vector3 localPosition, Quaternion localRotation)
    {
        if (prefab == null)
        {
            return null;
        }

        var parent = transform.parent;
        var instance = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);

        if (parent != null)
        {
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = originalLocalScale;
        }
        else
        {
            instance.transform.position = localPosition;
            instance.transform.rotation = localRotation;
            instance.transform.localScale = originalLocalScale;
        }

        return instance;
    }

    private void DestroySpawnedObject()
    {
        if (currentSpawnedObject == null)
        {
            return;
        }

        Destroy(currentSpawnedObject);
        currentSpawnedObject = null;
    }

    private void CacheManagedComponents()
    {
        managedRenderers = GetComponentsInChildren<Renderer>(true);
        managedColliders = GetComponentsInChildren<Collider>(true);
        managedColliders2D = GetComponentsInChildren<Collider2D>(true);
        managedCanvases = GetComponentsInChildren<Canvas>(true);
        managedLights = GetComponentsInChildren<Light>(true);
    }

    private bool ShouldUseSceneObjectAsNormal()
    {
        if (!preferSceneObjectAsNormal)
        {
            return false;
        }

        return HasManagedSceneContent();
    }

    private bool HasManagedSceneContent()
    {
        return managedRenderers.Length > 0 ||
               managedColliders.Length > 0 ||
               managedColliders2D.Length > 0 ||
               managedCanvases.Length > 0 ||
               managedLights.Length > 0;
    }

    private void SetSceneObjectVisible(bool visible)
    {
        for (var i = 0; i < managedRenderers.Length; i++)
        {
            if (managedRenderers[i] != null)
            {
                managedRenderers[i].enabled = visible;
            }
        }

        for (var i = 0; i < managedColliders.Length; i++)
        {
            if (managedColliders[i] != null)
            {
                managedColliders[i].enabled = visible;
            }
        }

        for (var i = 0; i < managedColliders2D.Length; i++)
        {
            if (managedColliders2D[i] != null)
            {
                managedColliders2D[i].enabled = visible;
            }
        }

        for (var i = 0; i < managedCanvases.Length; i++)
        {
            if (managedCanvases[i] != null)
            {
                managedCanvases[i].enabled = visible;
            }
        }

        for (var i = 0; i < managedLights.Length; i++)
        {
            if (managedLights[i] != null)
            {
                managedLights[i].enabled = visible;
            }
        }
    }

    private void RestoreSceneObjectTransform()
    {
        ApplySceneObjectTransform(originalLocalPosition, originalLocalRotation);
        transform.localScale = originalLocalScale;
    }

    private void ApplySceneObjectTransform(Vector3 localPosition, Quaternion localRotation)
    {
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
    }

    private Vector3 GetAnomalyLocalPosition(AnomalyType nextType)
    {
        if (nextType != AnomalyType.MovedObject)
        {
            return originalLocalPosition;
        }

        return originalLocalPosition + (originalLocalRotation * movedLocalPositionOffset);
    }

    private Quaternion GetAnomalyLocalRotation(AnomalyType nextType)
    {
        if (nextType != AnomalyType.MovedObject)
        {
            return originalLocalRotation;
        }

        return originalLocalRotation * Quaternion.Euler(movedLocalEulerOffset);
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

    private bool CanWriteState()
    {
        return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    }

    private bool HasSyncedState(PhotonHashtable properties)
    {
        return properties != null && properties.ContainsKey(GetPropertyKey("Type"));
    }

    private string GetPropertyKey(string suffix)
    {
        return $"{syncKeyPrefix}_{suffix}";
    }

    private string BuildSyncKeyPrefix()
    {
        var parts = new StringBuilder("AnomalyPoint");
        var current = transform;

        while (current != null)
        {
            parts.Insert(0, $"{current.GetSiblingIndex()}_{current.name}_");
            current = current.parent;
        }

        return parts.ToString()
            .Replace(" ", "_")
            .Replace("(", "_")
            .Replace(")", "_")
            .Replace(".", "_");
    }

    private static int ReadInt(PhotonHashtable properties, string key, int fallback)
    {
        if (properties != null && properties.TryGetValue(key, out var value))
        {
            if (value is int intValue)
            {
                return intValue;
            }
        }

        return fallback;
    }

    private static string ReadString(PhotonHashtable properties, string key, string fallback)
    {
        if (properties != null && properties.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }

        return fallback;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (assignedAnomalyType != AnomalyType.MovedObject)
        {
            return;
        }

        var from = transform.position;
        var to = transform.position + (transform.rotation * movedLocalPositionOffset);

        Gizmos.color = new Color(0.1f, 0.95f, 0.15f, 0.95f);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.18f);
        Gizmos.DrawSphere(to, 0.06f);
    }
#endif
}

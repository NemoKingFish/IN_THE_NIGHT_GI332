using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

public class AnomalySpawnPoint : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    private class SpatialAudioSettings
    {
        public bool enabled;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float minDistance = 1f;
        [Min(0.01f)] public float maxDistance = 12f;
        public bool loop = true;
    }

    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    [Header("Normal State")]
    [SerializeField] private bool preferSceneObjectAsNormal = true;
    [SerializeField] private GameObject normalPrefab;

    [Header("Anomaly State")]
    [SerializeField] private GameObject anomalyPrefab;

    [Header("Anomaly Info")]
    [SerializeField] private int anomalyID;
    [SerializeField] private string anomalyName;
    [SerializeField] private AnomalyType assignedAnomalyType = AnomalyType.None;
    [SerializeField, Range(1, 3)] private int anomalyPhase = 1;

    [Header("Spawn Chance")]
    [SerializeField, Range(0f, 100f)] private float anomalyChance = 30f;

    [Header("Moved Object")]
    [SerializeField] private Vector3 movedLocalPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 movedLocalEulerOffset = Vector3.zero;

    [Header("Changed Object Overrides")]
    [SerializeField] private bool overrideChangedObjectScale;
    [SerializeField] private Vector3 changedObjectScaleMultiplier = Vector3.one;
    [SerializeField] private bool overrideChangedObjectColor;
    [SerializeField] private Color changedObjectColor = Color.white;

    [Header("Audio")]
    [SerializeField] private SpatialAudioSettings normalAudioSettings = new SpatialAudioSettings();
    [SerializeField] private SpatialAudioSettings anomalyAudioSettings = new SpatialAudioSettings();

    [Header("Editor Preview")]
    [SerializeField] private bool previewAnomalyInEditMode;

    public ObservableValue<int> currentAnomalyID = new ObservableValue<int>(-1);
    public ObservableValue<string> currentAnomalyName = new ObservableValue<string>("Normal");
    public ObservableValue<int> currentAnomalyType = new ObservableValue<int>((int)AnomalyType.None);

    private GameObject currentSpawnedObject;
    private string syncKeyPrefix;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private bool hasCachedOriginalTransform;
    private bool useSceneObjectAsNormal;
    private Renderer[] managedRenderers;
    private Collider[] managedColliders;
    private Collider2D[] managedColliders2D;
    private Canvas[] managedCanvases;
    private Light[] managedLights;
    private AudioSource managedAudioSource;
#if UNITY_EDITOR
    private bool editorPreviewRefreshQueued;
    private bool suppressEditorOriginalTransformCapture;
#endif

    private void Awake()
    {
        if (Application.isPlaying && previewAnomalyInEditMode)
        {
            previewAnomalyInEditMode = false;
        }

        CaptureOriginalTransformSnapshot();
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
        if (!CanWriteState())
        {
            return;
        }

        ApplySpawnState(false, true);
    }

    public void RollAndSpawn()
    {
        RollAndSpawn(3);
    }

    public void RollAndSpawn(int activeProgressionPhase)
    {
        if (!CanWriteState())
        {
            return;
        }

        if (!IsAvailableInProgressionPhase(activeProgressionPhase))
        {
            ApplySpawnState(false, true);
            return;
        }

        var randomValue = Random.Range(0f, 100f);
        var spawnAnomaly = randomValue < anomalyChance;
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
        ApplyAudioPresentation(nextType);
        currentAnomalyID.Value = nextId;
        currentAnomalyName.Value = nextName;
        currentAnomalyType.Value = (int)nextType;
    }

    private void ApplyPresentation(AnomalyType nextType)
    {
        RestoreSceneObjectTransform();
        RestoreSceneObjectAppearance();

        if (nextType == AnomalyType.None)
        {
            ShowNormalPresentation();
            return;
        }

        ShowAnomalyPresentation(nextType);
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
            currentSpawnedObject = InstantiateManagedPrefab(normalPrefab, originalLocalPosition, originalLocalRotation, true);
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

        if (nextType == AnomalyType.MovedObject)
        {
            ApplyMovedObjectPresentation(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        if (nextType == AnomalyType.ChangedObject && (HasChangedObjectOverrides() || anomalyPrefab == null))
        {
            ApplyChangedObjectPresentation(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        if (nextType == AnomalyType.StrangeSound && anomalyPrefab == null)
        {
            ApplyStrangeSoundPresentation(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        var prefabToSpawn = ResolveAnomalyPrefab(nextType);

        SetSceneObjectVisible(false);

        if (prefabToSpawn != null)
        {
            currentSpawnedObject = InstantiateManagedPrefab(prefabToSpawn, anomalyLocalPosition, anomalyLocalRotation, false);
        }
    }

    private void ApplyStrangeSoundPresentation(Vector3 anomalyLocalPosition, Quaternion anomalyLocalRotation)
    {
        if (useSceneObjectAsNormal)
        {
            SetSceneObjectVisible(true);
            ApplySceneObjectTransform(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        SetSceneObjectVisible(false);

        if (normalPrefab != null)
        {
            currentSpawnedObject = InstantiateManagedPrefab(normalPrefab, anomalyLocalPosition, anomalyLocalRotation, true);
        }
    }

    private bool HasChangedObjectOverrides()
    {
        return overrideChangedObjectScale || overrideChangedObjectColor;
    }

    private GameObject ResolveAnomalyPrefab(AnomalyType nextType)
    {
        if (nextType == AnomalyType.MissingObject || nextType == AnomalyType.MovedObject)
        {
            return null;
        }

        if (anomalyPrefab != null)
        {
            return anomalyPrefab;
        }

        return null;
    }

    private void ApplyMovedObjectPresentation(Vector3 anomalyLocalPosition, Quaternion anomalyLocalRotation)
    {
        if (useSceneObjectAsNormal)
        {
            SetSceneObjectVisible(true);
            ApplySceneObjectTransform(anomalyLocalPosition, anomalyLocalRotation);
            return;
        }

        SetSceneObjectVisible(false);

        if (normalPrefab != null)
        {
            currentSpawnedObject = InstantiateManagedPrefab(normalPrefab, anomalyLocalPosition, anomalyLocalRotation, true);
        }
    }

    private void ApplyChangedObjectPresentation(Vector3 anomalyLocalPosition, Quaternion anomalyLocalRotation)
    {
        if (useSceneObjectAsNormal)
        {
            SetSceneObjectVisible(true);
            ApplySceneObjectTransform(anomalyLocalPosition, anomalyLocalRotation);
            ApplyChangedObjectOverrides(transform, managedRenderers, true);
            return;
        }

        SetSceneObjectVisible(false);

        var changedObjectBasePrefab = normalPrefab != null ? normalPrefab : anomalyPrefab;
        if (changedObjectBasePrefab == null)
        {
            return;
        }

        var useOriginalScaleBase = normalPrefab != null;
        currentSpawnedObject = InstantiateManagedPrefab(
            changedObjectBasePrefab,
            anomalyLocalPosition,
            anomalyLocalRotation,
            useOriginalScaleBase);

        if (currentSpawnedObject == null)
        {
            return;
        }

        var instanceRenderers = currentSpawnedObject.GetComponentsInChildren<Renderer>(true);
        ApplyChangedObjectOverrides(currentSpawnedObject.transform, instanceRenderers, useOriginalScaleBase);
    }

    private void ApplyChangedObjectOverrides(Transform targetTransform, Renderer[] targetRenderers, bool useOriginalScaleBase)
    {
        if (targetTransform == null)
        {
            return;
        }

        if (overrideChangedObjectScale)
        {
            var baseScale = useOriginalScaleBase ? originalLocalScale : targetTransform.localScale;
            targetTransform.localScale = Vector3.Scale(baseScale, changedObjectScaleMultiplier);
        }

        if (overrideChangedObjectColor)
        {
            ApplyRendererColorOverride(targetRenderers, changedObjectColor);
        }
    }

    private GameObject InstantiateManagedPrefab(GameObject prefab, Vector3 localPosition, Quaternion localRotation, bool forceOriginalScale)
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
            instance.transform.localScale = forceOriginalScale ? originalLocalScale : prefab.transform.localScale;
        }
        else
        {
            instance.transform.position = localPosition;
            instance.transform.rotation = localRotation;
            instance.transform.localScale = forceOriginalScale ? originalLocalScale : prefab.transform.localScale;
        }

        return instance;
    }

    private void DestroySpawnedObject()
    {
        if (currentSpawnedObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(currentSpawnedObject);
        }
        else
        {
            DestroyImmediate(currentSpawnedObject);
        }

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

    private void ApplyAudioPresentation(AnomalyType nextType)
    {
        var settings = nextType == AnomalyType.None ? normalAudioSettings : anomalyAudioSettings;
        if (!HasAudioSettings(settings))
        {
            StopManagedAudio();
            return;
        }

        var targetAnchor = currentSpawnedObject != null ? currentSpawnedObject.transform : transform;
        if (targetAnchor == null)
        {
            StopManagedAudio();
            return;
        }

        var audioSource = EnsureManagedAudioSource(targetAnchor);
        if (audioSource == null)
        {
            return;
        }

        ConfigureAudioSource(audioSource, settings);
        var soundEmitter = SoundCategoryEmitter.Ensure(audioSource, SoundCategory.Sfx);
        if (soundEmitter != null)
        {
            soundEmitter.CaptureCurrentVolumeAsBase();
        }

        if (audioSource.clip != settings.clip)
        {
            audioSource.Stop();
            audioSource.clip = settings.clip;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private static bool HasAudioSettings(SpatialAudioSettings settings)
    {
        return settings != null && settings.enabled && settings.clip != null;
    }

    private AudioSource EnsureManagedAudioSource(Transform targetAnchor)
    {
        if (targetAnchor == null)
        {
            return null;
        }

        if (managedAudioSource == null)
        {
            managedAudioSource = GetComponentInChildren<AudioSource>(true);

            if (managedAudioSource == null || managedAudioSource.gameObject.name != "__AnomalyManagedAudio")
            {
                var audioObject = new GameObject("__AnomalyManagedAudio", typeof(AudioSource));
                managedAudioSource = audioObject.GetComponent<AudioSource>();
            }
        }

        if (managedAudioSource == null)
        {
            return null;
        }

        var audioTransform = managedAudioSource.transform;
        if (audioTransform.parent != targetAnchor)
        {
            audioTransform.SetParent(targetAnchor, false);
        }

        audioTransform.localPosition = Vector3.zero;
        audioTransform.localRotation = Quaternion.identity;

        return managedAudioSource;
    }

    private static void ConfigureAudioSource(AudioSource audioSource, SpatialAudioSettings settings)
    {
        if (audioSource == null || settings == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel = 0f;
        audioSource.loop = settings.loop;
        audioSource.volume = Mathf.Clamp01(settings.volume);
        audioSource.minDistance = Mathf.Max(0f, settings.minDistance);
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.01f, settings.maxDistance);
    }

    private void StopManagedAudio()
    {
        if (managedAudioSource == null)
        {
            return;
        }

        managedAudioSource.Stop();
        managedAudioSource.clip = null;
    }

    private void RestoreSceneObjectAppearance()
    {
        ClearRendererColorOverride(managedRenderers);
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
        EnsureOriginalTransformSnapshot();
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

    private static void ApplyRendererColorOverride(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        var propertyBlock = new MaterialPropertyBlock();

        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                continue;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                var canWriteColor = false;
                propertyBlock.Clear();

                if (material.HasProperty(BaseColorPropertyId))
                {
                    propertyBlock.SetColor(BaseColorPropertyId, color);
                    canWriteColor = true;
                }

                if (material.HasProperty(ColorPropertyId))
                {
                    propertyBlock.SetColor(ColorPropertyId, color);
                    canWriteColor = true;
                }

                if (canWriteColor)
                {
                    renderer.SetPropertyBlock(propertyBlock, materialIndex);
                }
            }
        }
    }

    private static void ClearRendererColorOverride(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                renderer.SetPropertyBlock(null, materialIndex);
            }
        }
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

    public int GetAnomalyPhase()
    {
        return Mathf.Clamp(anomalyPhase, 1, 3);
    }

    public bool IsAvailableInProgressionPhase(int activeProgressionPhase)
    {
        return GetAnomalyPhase() <= Mathf.Max(1, activeProgressionPhase);
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
        if (properties != null && properties.TryGetValue(key, out var value) && value is int intValue)
        {
            return intValue;
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

    private void CaptureOriginalTransformSnapshot()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        hasCachedOriginalTransform = true;
    }

    private void EnsureOriginalTransformSnapshot()
    {
        if (!hasCachedOriginalTransform)
        {
            CaptureOriginalTransformSnapshot();
        }
    }

    private void SanitizeAudioSettings()
    {
        SanitizeAudioSettings(normalAudioSettings);
        SanitizeAudioSettings(anomalyAudioSettings);
    }

    private static void SanitizeAudioSettings(SpatialAudioSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.volume = Mathf.Clamp01(settings.volume);
        settings.minDistance = Mathf.Max(0f, settings.minDistance);
        settings.maxDistance = Mathf.Max(settings.minDistance + 0.01f, settings.maxDistance);
    }

    private Vector3 GetAudioGizmoWorldPosition(AnomalyType previewType)
    {
        var localPosition = GetAnomalyLocalPosition(previewType);
        var parent = transform.parent;
        return parent != null ? parent.TransformPoint(localPosition) : localPosition;
    }

    private void DrawAudioRangeGizmo(SpatialAudioSettings settings, AnomalyType previewType, Color color)
    {
        if (!HasAudioSettings(settings))
        {
            return;
        }

        var worldPosition = GetAudioGizmoWorldPosition(previewType);

        Gizmos.color = color;
        Gizmos.DrawWireSphere(worldPosition, settings.maxDistance);

        if (settings.minDistance > 0f)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.55f));
            Gizmos.DrawWireSphere(worldPosition, settings.minDistance);
        }
    }

#if UNITY_EDITOR
    public bool SyncEditorGeneratedFieldsNow()
    {
        return SyncEditorGeneratedFields();
    }

    public void ApplyEditorGeneratedIdentity(int nextId, string nextName)
    {
        anomalyID = Mathf.Max(1, nextId);
        anomalyName = nextName ?? string.Empty;
    }

    public bool IsPreviewAnomalyInEditMode()
    {
        return previewAnomalyInEditMode;
    }

    public void SetPreviewAnomalyInEditMode(bool shouldPreview)
    {
        if (shouldPreview)
        {
            CaptureOriginalTransformSnapshot();
        }

        suppressEditorOriginalTransformCapture = true;
        previewAnomalyInEditMode = shouldPreview;

        if (Application.isPlaying)
        {
            if (previewAnomalyInEditMode)
            {
                previewAnomalyInEditMode = false;
            }

            return;
        }

        RefreshEditorPreview();
    }

    private void OnValidate()
    {
        SyncEditorGeneratedFields();
        anomalyPhase = Mathf.Clamp(anomalyPhase, 1, 3);
        SanitizeAudioSettings();

        if (!previewAnomalyInEditMode && !suppressEditorOriginalTransformCapture)
        {
            CaptureOriginalTransformSnapshot();
        }
        else
        {
            EnsureOriginalTransformSnapshot();
        }

        suppressEditorOriginalTransformCapture = false;

        CacheManagedComponents();
        useSceneObjectAsNormal = ShouldUseSceneObjectAsNormal();

        if (!Application.isPlaying)
        {
            QueueEditorPreviewRefresh();
        }
    }

    private void RefreshEditorPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        EnsureOriginalTransformSnapshot();

        if (previewAnomalyInEditMode && assignedAnomalyType != AnomalyType.None)
        {
            ApplyLocalState(anomalyID, anomalyName, assignedAnomalyType);
            return;
        }

        ApplyLocalState(-1, "Normal", AnomalyType.None);
    }

    private void QueueEditorPreviewRefresh()
    {
        if (editorPreviewRefreshQueued)
        {
            return;
        }

        editorPreviewRefreshQueued = true;
        EditorApplication.delayCall += RefreshEditorPreviewDelayed;
    }

    private void RefreshEditorPreviewDelayed()
    {
        editorPreviewRefreshQueued = false;

        if (this == null || gameObject == null || Application.isPlaying)
        {
            return;
        }

        RefreshEditorPreview();
        EditorUtility.SetDirty(this);
    }

    private bool SyncEditorGeneratedFields()
    {
        if (Application.isPlaying)
        {
            return false;
        }

        var changed = false;
        var nextName = gameObject != null ? gameObject.name : string.Empty;
        if (anomalyName != nextName)
        {
            anomalyName = nextName;
            changed = true;
        }

        var nextId = anomalyID;

        if (anomalyID < 1 || IsDuplicateEditorAnomalyId(anomalyID))
        {
            nextId = GetNextAvailableEditorAnomalyId();
        }

        if (anomalyID != nextId)
        {
            anomalyID = nextId;
            changed = true;
        }

        return changed;
    }

    private bool IsDuplicateEditorAnomalyId(int candidateId)
    {
        if (candidateId < 1)
        {
            return true;
        }

        var allPoints = FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allPoints.Length; i++)
        {
            var point = allPoints[i];
            if (point == null || point == this)
            {
                continue;
            }

            if (point.anomalyID == candidateId)
            {
                return true;
            }
        }

        return false;
    }

    private int GetNextAvailableEditorAnomalyId()
    {
        var usedIds = new HashSet<int>();
        var allPoints = FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allPoints.Length; i++)
        {
            var point = allPoints[i];
            if (point == null || point == this || point.anomalyID < 1)
            {
                continue;
            }

            usedIds.Add(point.anomalyID);
        }

        var nextId = 1;
        while (usedIds.Contains(nextId))
        {
            nextId++;
        }

        return nextId;
    }

    private void OnDrawGizmosSelected()
    {
        if (assignedAnomalyType == AnomalyType.MovedObject)
        {
            var from = transform.position;
            var to = transform.position + (transform.rotation * movedLocalPositionOffset);

            Gizmos.color = new Color(0.1f, 0.95f, 0.15f, 0.95f);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(to, 0.18f);
            Gizmos.DrawSphere(to, 0.06f);
        }

        var normalAudioColor = new Color(0.1f, 0.75f, 1f, 0.75f);
        var anomalyAudioColor = new Color(1f, 0.4f, 0.15f, 0.8f);

        DrawAudioRangeGizmo(normalAudioSettings, AnomalyType.None, normalAudioColor);
        DrawAudioRangeGizmo(anomalyAudioSettings, assignedAnomalyType, anomalyAudioColor);
    }
#endif
}

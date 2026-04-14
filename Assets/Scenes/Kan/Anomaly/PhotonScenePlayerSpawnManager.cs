using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PhotonScenePlayerSpawnManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte PlayerStateEventCode = 91;
    private static PhotonScenePlayerSpawnManager instance;

    [Serializable]
    public struct PlayerStateData
    {
        public int actorNumber;
        public Vector3 position;
        public Quaternion rotation;
        public float pitch;
        public string displayName;
    }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string spawnPadName = "SpawnPad";
    [SerializeField] private string fallbackSpawnPadName = "SpawnPoint";
    [SerializeField] private float sendInterval = 0.05f;
    [SerializeField] private bool preferKanPlayerPrefab = true;

    private readonly Dictionary<int, PhotonScenePlayerAvatar> avatarsByActorNumber = new Dictionary<int, PhotonScenePlayerAvatar>();
    private readonly Dictionary<int, int> spawnPadIndexByActorNumber = new Dictionary<int, int>();
    private readonly List<Transform> spawnPads = new List<Transform>();
    private float nextSendTime;
    private bool warnedMissingPrefab;
    private bool warnedMissingSpawnPads;
    private bool loggedFallbackSpawnPads;
    private bool warnedInsufficientSpawnPads;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<PhotonScenePlayerSpawnManager>() != null)
        {
            return;
        }

        if (FindFirstObjectByType<GameRoundManager>() == null)
        {
            return;
        }

        var bootstrapObject = new GameObject("PhotonScenePlayerSpawnManager");
        bootstrapObject.AddComponent<PhotonScenePlayerSpawnManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        RefreshSpawnPads();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            EnsurePlayerAvatars();
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        EnsurePlayerAvatars();
        SendLocalPlayerState();
    }

    public override void OnJoinedRoom()
    {
        EnsurePlayerAvatars();
    }

    public override void OnLeftRoom()
    {
        ClearAvatars();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        EnsurePlayerAvatars();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer == null)
        {
            return;
        }

        if (avatarsByActorNumber.TryGetValue(otherPlayer.ActorNumber, out var avatar) && avatar != null)
        {
            Destroy(avatar.gameObject);
        }

        avatarsByActorNumber.Remove(otherPlayer.ActorNumber);
        spawnPadIndexByActorNumber.Remove(otherPlayer.ActorNumber);
        EnsurePlayerAvatars();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != PlayerStateEventCode || photonEvent.CustomData is not object[] payload || payload.Length < 5)
        {
            return;
        }

        var actorNumber = (int)payload[0];
        if (PhotonNetwork.LocalPlayer != null && actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return;
        }

        if (!avatarsByActorNumber.TryGetValue(actorNumber, out var avatar) || avatar == null)
        {
            EnsurePlayerAvatars();
            if (!avatarsByActorNumber.TryGetValue(actorNumber, out avatar) || avatar == null)
            {
                return;
            }
        }

        avatar.ApplyRemoteState(
            (Vector3)payload[1],
            (Quaternion)payload[2],
            (float)payload[3],
            payload[4] as string);
    }

    private void EnsurePlayerAvatars()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        RefreshSpawnPads();
        if (spawnPads.Count == 0)
        {
            if (!warnedMissingSpawnPads)
            {
                Debug.LogWarning("[PhotonScenePlayerSpawnManager] No 'SpawnPad' or 'SpawnPoint' objects were found in the target scene.");
                warnedMissingSpawnPads = true;
            }

            return;
        }

        warnedMissingSpawnPads = false;

        var players = PhotonNetwork.PlayerList;
        Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        RebuildSpawnAssignments(players);
        if (players.Length <= spawnPads.Count)
        {
            warnedInsufficientSpawnPads = false;
        }

        for (var i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null)
            {
                continue;
            }

            if (avatarsByActorNumber.TryGetValue(player.ActorNumber, out var existingAvatar) && existingAvatar != null)
            {
                continue;
            }

            var spawnIndex = GetAssignedSpawnPadIndex(player.ActorNumber);
            var spawnPad = GetSpawnPadForAssignedIndex(spawnIndex);
            if (spawnPad == null)
            {
                if (!warnedInsufficientSpawnPads)
                {
                    Debug.LogWarning("[PhotonScenePlayerSpawnManager] Not enough spawn pads for all players in the room.");
                    warnedInsufficientSpawnPads = true;
                }

                continue;
            }

            var avatarObject = CreatePlayerInstance(spawnPad);
            if (avatarObject == null)
            {
                if (!warnedMissingPrefab)
                {
                    Debug.LogWarning("[PhotonScenePlayerSpawnManager] Player prefab is missing and no fallback player could be created.");
                    warnedMissingPrefab = true;
                }

                return;
            }

            warnedMissingPrefab = false;
            var avatar = avatarObject.GetComponent<PhotonScenePlayerAvatar>();
            if (avatar == null)
            {
                avatar = avatarObject.AddComponent<PhotonScenePlayerAvatar>();
            }

            var isLocal = PhotonNetwork.LocalPlayer != null && player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
            avatar.Initialize(player.ActorNumber, isLocal, GetPlayerDisplayName(player));
            avatarsByActorNumber[player.ActorNumber] = avatar;
        }
    }

    private void SendLocalPlayerState()
    {
        if (PhotonNetwork.LocalPlayer == null || Time.time < nextSendTime)
        {
            return;
        }

        if (!avatarsByActorNumber.TryGetValue(PhotonNetwork.LocalPlayer.ActorNumber, out var avatar) || avatar == null || !avatar.IsLocalPlayer)
        {
            return;
        }

        nextSendTime = Time.time + sendInterval;

        var state = avatar.BuildState(GetPlayerDisplayName(PhotonNetwork.LocalPlayer));
        PhotonNetwork.RaiseEvent(
            PlayerStateEventCode,
            new object[] { state.actorNumber, state.position, state.rotation, state.pitch, state.displayName },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendUnreliable);
    }

    private void RefreshSpawnPads()
    {
        spawnPads.Clear();

        var preferredPads = FindSpawnPadsByName(spawnPadName);
        if (preferredPads.Count > 0)
        {
            spawnPads.AddRange(preferredPads);
            loggedFallbackSpawnPads = false;
        }
        else
        {
            var fallbackPads = FindSpawnPadsByName(fallbackSpawnPadName);
            if (fallbackPads.Count > 0)
            {
                spawnPads.AddRange(fallbackPads);
                if (!loggedFallbackSpawnPads)
                {
                    Debug.Log("[PhotonScenePlayerSpawnManager] Using fallback spawn pads named 'SpawnPoint'.");
                    loggedFallbackSpawnPads = true;
                }
            }
            else
            {
                loggedFallbackSpawnPads = false;
            }
        }

        ShuffleSpawnPadsDeterministically();
    }

    private List<Transform> FindSpawnPadsByName(string targetName)
    {
        var results = new List<Transform>();
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i] != null && allTransforms[i].name == targetName)
            {
                results.Add(allTransforms[i]);
            }
        }

        results.Sort((a, b) => string.CompareOrdinal(GetTransformHierarchyPath(a), GetTransformHierarchyPath(b)));
        return results;
    }

    private void RebuildSpawnAssignments(Player[] players)
    {
        spawnPadIndexByActorNumber.Clear();

        if (spawnPads.Count == 0 || players == null || players.Length == 0)
        {
            return;
        }

        var assignableCount = Mathf.Min(players.Length, spawnPads.Count);
        for (var i = 0; i < assignableCount; i++)
        {
            var player = players[i];
            if (player == null)
            {
                continue;
            }

            spawnPadIndexByActorNumber[player.ActorNumber] = i;
        }
    }

    private int GetAssignedSpawnPadIndex(int actorNumber)
    {
        if (spawnPadIndexByActorNumber.TryGetValue(actorNumber, out var existingIndex))
        {
            return existingIndex;
        }

        return -1;
    }

    private Transform GetSpawnPadForAssignedIndex(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= spawnPads.Count)
        {
            return null;
        }

        return spawnPads[spawnIndex];
    }

    private void ClearAvatars()
    {
        foreach (var pair in avatarsByActorNumber)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        avatarsByActorNumber.Clear();
        spawnPadIndexByActorNumber.Clear();
    }

    private static string GetPlayerDisplayName(Player player)
    {
        if (player == null)
        {
            return "Player";
        }

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue("PlayerName", out var nameValue) &&
            nameValue is string playerName &&
            !string.IsNullOrWhiteSpace(playerName))
        {
            return playerName;
        }

        return $"Player{player.ActorNumber:000}";
    }

    private GameObject CreatePlayerInstance(Transform spawnPad)
    {
        if (playerPrefab == null)
        {
            if (preferKanPlayerPrefab)
            {
                playerPrefab = TryResolveKanPlayerPrefab();
            }

            if (playerPrefab == null)
            {
                playerPrefab = TryResolveLegacyPlayerPrefab();
            }
        }

        GameObject instance;
        if (playerPrefab != null)
        {
            instance = Instantiate(playerPrefab, spawnPad.position, spawnPad.rotation);
        }
        else
        {
            instance = CreateFallbackPlayerObject(spawnPad.position, spawnPad.rotation);
        }

        PrepareInstantiatedPlayerObject(instance);
        return instance;
    }

    private void ShuffleSpawnPadsDeterministically()
    {
        if (spawnPads.Count <= 1)
        {
            return;
        }

        var seed = GetCurrentRoomSeed();
        var random = new System.Random(seed);
        for (var i = spawnPads.Count - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (spawnPads[i], spawnPads[swapIndex]) = (spawnPads[swapIndex], spawnPads[i]);
        }
    }

    private int GetCurrentRoomSeed()
    {
        unchecked
        {
            var seed = 17;
            var roomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty;
            seed = (seed * 31) + GetStableHash(roomName);
            seed = (seed * 31) + spawnPads.Count;
            return seed;
        }
    }

    private static int GetStableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }

            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }

    private static string GetTransformHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        var path = target.name;
        var current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = FindChildRecursive(parent.GetChild(i), childName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static GameObject TryResolveKanPlayerPrefab()
    {
#if UNITY_EDITOR
        var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Kan/Player.prefab");
        if (editorPrefab != null)
        {
            Debug.Log("[PhotonScenePlayerSpawnManager] Using Assets/Scenes/Kan/Player.prefab for Photon spawn.");
            return editorPrefab;
        }
#endif

        var loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < loadedObjects.Length; i++)
        {
            var candidate = loadedObjects[i];
            if (candidate == null || candidate.name != "Player")
            {
                continue;
            }

            if (candidate.GetComponent<CharacterController>() == null)
            {
                continue;
            }

            if (FindChildRecursive(candidate.transform, "CameraTarget") == null)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static void PrepareInstantiatedPlayerObject(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (var i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        var cameras = instance.GetComponentsInChildren<Camera>(true);
        for (var i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = false;
            cameras[i].gameObject.SetActive(false);
        }

        var audioListeners = instance.GetComponentsInChildren<AudioListener>(true);
        for (var i = 0; i < audioListeners.Length; i++)
        {
            audioListeners[i].enabled = false;
        }

        var behaviours = instance.GetComponentsInChildren<Behaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null || ShouldKeepBehaviourEnabled(behaviour))
            {
                continue;
            }

            behaviour.enabled = false;
        }

        var animator = instance.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (instance.GetComponent<CharacterController>() == null)
        {
            var controller = instance.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
        }

        var avatar = instance.GetComponent<PhotonScenePlayerAvatar>();
        if (avatar == null)
        {
            avatar = instance.AddComponent<PhotonScenePlayerAvatar>();
        }

        var cameraTarget = FindChildRecursive(instance.transform, "CameraTarget");
        if (cameraTarget == null)
        {
            var cameraTargetObject = new GameObject("CameraTarget");
            cameraTargetObject.transform.SetParent(instance.transform, false);
            cameraTargetObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        }
    }

    private static bool ShouldKeepBehaviourEnabled(Behaviour behaviour)
    {
        if (behaviour is CharacterController || behaviour is Animator)
        {
            return true;
        }

        if (behaviour is PhotonScenePlayerAvatar)
        {
            return true;
        }

        if (behaviour is TextMeshPro || behaviour is TextMeshProUGUI)
        {
            return true;
        }

        var typeName = behaviour.GetType().Name;
        return typeName == nameof(PhotonScenePlayerAvatar);
    }

    private static GameObject TryResolveLegacyPlayerPrefab()
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            var behaviourType = behaviour.GetType();
            if (behaviourType.FullName != "Unity.Netcode.NetworkManager")
            {
                continue;
            }

            var networkConfig = ReadMemberValue(behaviour, behaviourType, "NetworkConfig");
            if (networkConfig == null)
            {
                continue;
            }

            var legacyPlayerPrefab = ReadMemberValue(networkConfig, networkConfig.GetType(), "PlayerPrefab") as GameObject;
            if (legacyPlayerPrefab != null)
            {
                Debug.Log("[PhotonScenePlayerSpawnManager] Using legacy NetworkManager PlayerPrefab as Photon fallback.");
                return legacyPlayerPrefab;
            }
        }

        return null;
    }

    private static object ReadMemberValue(object source, Type declaringType, string memberName)
    {
        if (source == null || declaringType == null)
        {
            return null;
        }

        const BindingFlags BindingOptions = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var property = declaringType.GetProperty(memberName, BindingOptions);
        if (property != null)
        {
            return property.GetValue(source);
        }

        var field = declaringType.GetField(memberName, BindingOptions);
        if (field != null)
        {
            return field.GetValue(source);
        }

        return null;
    }

    private static GameObject CreateFallbackPlayerObject(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("PhotonRuntimePlayer");
        root.transform.SetPositionAndRotation(position, rotation);

        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);

        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            UnityEngine.Object.Destroy(visualCollider);
        }

        var cameraTarget = new GameObject("CameraTarget");
        cameraTarget.transform.SetParent(root.transform, false);
        cameraTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var nameLabelObject = new GameObject("NameLabel");
        nameLabelObject.transform.SetParent(root.transform, false);
        nameLabelObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        var nameLabel = nameLabelObject.AddComponent<TextMeshPro>();
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.fontSize = 3f;
        nameLabel.color = Color.white;

        return root;
    }
}

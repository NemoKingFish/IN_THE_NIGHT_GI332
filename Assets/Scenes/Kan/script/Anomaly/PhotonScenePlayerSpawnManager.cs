using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonScenePlayerSpawnManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte PlayerTransformEventCode = 61;

    [Header("Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string primarySpawnPadName = "SpawnPad";
    [SerializeField] private string fallbackSpawnPadName = "SpawnPoint";
    [SerializeField] private string cameraTargetName = "CameraTarget";
    [SerializeField] private float spawnHeightOffset = 0.15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.75f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.35f;
    [SerializeField] private float lookSensitivity = 2.25f;
    [SerializeField] private float groundedProbeDistance = 0.18f;
    [SerializeField] private float spawnGroundSearchHeight = 8f;

    [Header("Camera")]
    [SerializeField] private float cameraDistance = 3.75f;
    [SerializeField] private float cameraPitchMin = -35f;
    [SerializeField] private float cameraPitchMax = 65f;
    [SerializeField] private bool deactivateSceneMainCamera = true;

    [Header("Network")]
    [SerializeField] private float networkSendRate = 12f;
    [SerializeField] private float remoteLerpSpeed = 12f;

    private readonly Dictionary<int, PhotonScenePlayerAvatar> spawnedAvatars = new Dictionary<int, PhotonScenePlayerAvatar>();
    private readonly List<Transform> spawnPads = new List<Transform>();

    private bool hasInitializedScenePlayers;
    public float MoveSpeed => moveSpeed;
    public float Gravity => gravity;
    public float JumpHeight => jumpHeight;
    public float LookSensitivity => lookSensitivity;
    public float GroundedProbeDistance => groundedProbeDistance;
    public float SpawnGroundSearchHeight => spawnGroundSearchHeight;
    public float CameraDistance => cameraDistance;
    public float CameraPitchMin => cameraPitchMin;
    public float CameraPitchMax => cameraPitchMax;
    public float NetworkSendRate => networkSendRate;
    public float RemoteLerpSpeed => remoteLerpSpeed;

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

    private void Start()
    {
        TryInitializeScenePlayers();
    }

    public override void OnJoinedRoom()
    {
        hasInitializedScenePlayers = false;
        TryInitializeScenePlayers();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        TryInitializeScenePlayers();
        SpawnMissingRoomPlayers();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RemoveAvatar(otherPlayer.ActorNumber);
    }

    public override void OnLeftRoom()
    {
        hasInitializedScenePlayers = false;
        ClearAllAvatars();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TryInitializeScenePlayers();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != PlayerTransformEventCode)
        {
            return;
        }

        if (!(photonEvent.CustomData is object[] payload) || payload.Length < 6)
        {
            return;
        }

        var actorNumber = ConvertToInt(payload[0]);
        if (actorNumber == PhotonNetwork.LocalPlayer?.ActorNumber)
        {
            return;
        }

        if (!spawnedAvatars.TryGetValue(actorNumber, out var avatar) || avatar == null)
        {
            TrySpawnAvatarForActor(actorNumber);
            if (!spawnedAvatars.TryGetValue(actorNumber, out avatar) || avatar == null)
            {
                return;
            }
        }

        var position = new Vector3(
            ConvertToFloat(payload[1]),
            ConvertToFloat(payload[2]),
            ConvertToFloat(payload[3]));
        var yaw = ConvertToFloat(payload[4]);
        var pitch = ConvertToFloat(payload[5]);

        avatar.ApplyRemoteState(position, Quaternion.Euler(0f, yaw, 0f), pitch);
    }

    public void SendLocalAvatarState(Vector3 position, Quaternion rotation, float pitch)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        var payload = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            position.x,
            position.y,
            position.z,
            rotation.eulerAngles.y,
            pitch
        };

        PhotonNetwork.RaiseEvent(
            PlayerTransformEventCode,
            payload,
            new RaiseEventOptions
            {
                Receivers = ReceiverGroup.Others,
                CachingOption = EventCaching.DoNotCache
            },
            new SendOptions
            {
                Reliability = false
            });
    }

    public Transform FindCameraTarget(Transform rootTransform)
    {
        if (rootTransform == null)
        {
            return null;
        }

        foreach (var child in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == cameraTargetName)
            {
                return child;
            }
        }

        return rootTransform;
    }

    public Vector3 ResolveSpawnPosition(Vector3 requestedPosition, CharacterController controller)
    {
        var searchOrigin = requestedPosition + Vector3.up * spawnGroundSearchHeight;
        var searchDistance = spawnGroundSearchHeight * 2f + 2f;

        if (Physics.Raycast(searchOrigin, Vector3.down, out var hit, searchDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var bottomOffset = 0f;
            if (controller != null)
            {
                bottomOffset = controller.center.y - (controller.height * 0.5f) + controller.skinWidth + 0.02f;
            }

            return hit.point + Vector3.up * Mathf.Max(0.02f, -bottomOffset);
        }

        return requestedPosition;
    }

    public void DisableSceneMainCameras(Camera localCamera)
    {
        if (!deactivateSceneMainCamera)
        {
            return;
        }

        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera == null)
            {
                continue;
            }

            var go = camera.gameObject;
            if (!go.scene.IsValid() || go.scene != gameObject.scene)
            {
                continue;
            }

            if (localCamera != null && camera == localCamera)
            {
                continue;
            }

            camera.enabled = false;
        }

        foreach (var listener in Resources.FindObjectsOfTypeAll<AudioListener>())
        {
            if (listener == null)
            {
                continue;
            }

            var go = listener.gameObject;
            if (!go.scene.IsValid() || go.scene != gameObject.scene)
            {
                continue;
            }

            if (localCamera != null && listener.gameObject == localCamera.gameObject)
            {
                continue;
            }

            listener.enabled = false;
        }
    }

    private void TryInitializeScenePlayers()
    {
        if (hasInitializedScenePlayers || !PhotonNetwork.InRoom)
        {
            return;
        }

        CacheSpawnPads();
        SpawnMissingRoomPlayers();
        hasInitializedScenePlayers = true;
    }

    private void CacheSpawnPads()
    {
        spawnPads.Clear();

        AddPadsMatching(primarySpawnPadName);
        if (spawnPads.Count > 0)
        {
            return;
        }

        AddPadsMatching(fallbackSpawnPadName);
        if (spawnPads.Count > 0)
        {
            Debug.Log($"[PhotonScenePlayerSpawnManager] Using fallback spawn pads named '{fallbackSpawnPadName}'.");
        }
        else
        {
            Debug.LogWarning($"[PhotonScenePlayerSpawnManager] No '{primarySpawnPadName}' or '{fallbackSpawnPadName}' found in scene '{gameObject.scene.name}'.");
        }
    }

    private void AddPadsMatching(string targetName)
    {
        foreach (var sceneTransform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (sceneTransform == null)
            {
                continue;
            }

            var go = sceneTransform.gameObject;
            if (!go.scene.IsValid() || go.scene != gameObject.scene)
            {
                continue;
            }

            if (sceneTransform.name != targetName && !sceneTransform.name.StartsWith(targetName + " "))
            {
                continue;
            }

            spawnPads.Add(sceneTransform);
        }

        spawnPads.Sort((left, right) =>
        {
            var zCompare = left.position.z.CompareTo(right.position.z);
            if (zCompare != 0)
            {
                return zCompare;
            }

            return left.position.x.CompareTo(right.position.x);
        });
    }

    private void SpawnMissingRoomPlayers()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogWarning("[PhotonScenePlayerSpawnManager] Player prefab is missing.");
            return;
        }

        if (spawnPads.Count == 0)
        {
            return;
        }

        var activeActorNumbers = new HashSet<int>(PhotonNetwork.PlayerList.Select(player => player.ActorNumber));
        var actorNumbersToRemove = spawnedAvatars.Keys.Where(actorNumber => !activeActorNumbers.Contains(actorNumber)).ToArray();
        for (var i = 0; i < actorNumbersToRemove.Length; i++)
        {
            RemoveAvatar(actorNumbersToRemove[i]);
        }

        var orderedPlayers = PhotonNetwork.PlayerList.OrderBy(player => player.ActorNumber).ToArray();
        for (var i = 0; i < orderedPlayers.Length; i++)
        {
            var player = orderedPlayers[i];
            if (spawnedAvatars.ContainsKey(player.ActorNumber))
            {
                continue;
            }

            var spawnPad = spawnPads[i % spawnPads.Count];
            CreateAvatar(player, spawnPad);
        }
    }

    private void TrySpawnAvatarForActor(int actorNumber)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out var player))
        {
            return;
        }

        var orderedPlayers = PhotonNetwork.PlayerList.OrderBy(roomPlayer => roomPlayer.ActorNumber).ToArray();
        var spawnIndex = 0;
        for (var i = 0; i < orderedPlayers.Length; i++)
        {
            if (orderedPlayers[i].ActorNumber == actorNumber)
            {
                spawnIndex = i;
                break;
            }
        }

        if (spawnPads.Count == 0)
        {
            CacheSpawnPads();
        }

        if (spawnPads.Count == 0)
        {
            return;
        }

        CreateAvatar(player, spawnPads[spawnIndex % spawnPads.Count]);
    }

    private void CreateAvatar(Player player, Transform spawnPad)
    {
        if (playerPrefab == null || spawnPad == null)
        {
            return;
        }

        var spawnPosition = spawnPad.position + Vector3.up * spawnHeightOffset;
        var avatarObject = Instantiate(playerPrefab, spawnPosition, spawnPad.rotation);
        avatarObject.name = $"Photon Player {player.ActorNumber}";

        var avatar = avatarObject.GetComponent<PhotonScenePlayerAvatar>();
        if (avatar == null)
        {
            avatar = avatarObject.AddComponent<PhotonScenePlayerAvatar>();
        }

        avatar.Initialize(
            this,
            player.ActorNumber,
            GetPlayerDisplayName(player),
            player.ActorNumber == PhotonNetwork.LocalPlayer?.ActorNumber,
            FindCameraTarget(avatarObject.transform));

        spawnedAvatars[player.ActorNumber] = avatar;
    }

    private void RemoveAvatar(int actorNumber)
    {
        if (!spawnedAvatars.TryGetValue(actorNumber, out var avatar))
        {
            return;
        }

        spawnedAvatars.Remove(actorNumber);
        if (avatar != null)
        {
            Destroy(avatar.gameObject);
        }
    }

    private void ClearAllAvatars()
    {
        foreach (var avatar in spawnedAvatars.Values)
        {
            if (avatar != null)
            {
                Destroy(avatar.gameObject);
            }
        }

        spawnedAvatars.Clear();
    }

    private static string GetPlayerDisplayName(Player player)
    {
        if (player == null)
        {
            return "Player";
        }

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue("PlayerDisplayName", out var displayNameObject) &&
            displayNameObject is string displayName &&
            !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(player.NickName))
        {
            return player.NickName;
        }

        return $"Player{player.ActorNumber}";
    }

    private static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return 0f;
    }

    private static int ConvertToInt(object value)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        return 0;
    }
}

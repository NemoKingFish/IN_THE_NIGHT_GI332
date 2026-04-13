using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class LobbyTestPhotonController : MonoBehaviourPunCallbacks
{
    private const string RoomDisplayNameKey = "RoomDisplayName";
    private const string LeaderNameKey = "LeaderName";
    private const string PasswordRequiredKey = "PasswordRequired";
    private const string PasswordCodeKey = "PasswordCode";
    private const string PlayerDisplayNameKey = "PlayerDisplayName";
    private const string PlayerReadyKey = "PlayerReady";
    private const string RoomClosingKey = "RoomClosing";
    private const string RoomCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string PasswordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int RoomCodeLength = 5;
    [Header("Photon")]
    [SerializeField] private string gameVersion = "kan-lobby";
    [SerializeField] private string targetSceneName = "Anomaly Test System Belike";
    [SerializeField] private LobbySceneTargetReference targetSceneReference;
    [SerializeField] private byte maxPlayersPerRoom = 4;
    [SerializeField] private bool autoConnectOnPlay = true;

    private const float PanelWidth = 1080f;
    private const float PanelHeight = 720f;

    private readonly Dictionary<string, RoomInfo> roomCache = new Dictionary<string, RoomInfo>();
    private readonly List<RoomInfo> roomDisplayList = new List<RoomInfo>();
    private PendingAction pendingAction;
    private string pendingCreateCode;
    private string pendingJoinRoomCode;
    private string statusMessage = "Connecting...";
    private string createLobbyNameInput = string.Empty;
    private string createLeaderNameInput = string.Empty;
    private bool createRequiresPassword;
    private string createPasswordCode = string.Empty;
    private string selectedRoomCode = string.Empty;
    private string passwordAttemptInput = string.Empty;
    private string localPlayerNameInput = string.Empty;
    private string localSubmittedName = string.Empty;
    private Vector2 roomScrollPosition;
    private Vector2 playerScrollPosition;
    private LobbyViewState lobbyViewState;
    private float currentPanelWidth;
    private float currentPanelHeight;

    private enum PendingAction
    {
        None,
        CreateRoom,
        JoinRoom
    }

    private enum LobbyViewState
    {
        Lobby,
        CreateRoom,
        PasswordPrompt
    }

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        localPlayerNameInput = GenerateMemberName();
    }

    private void Start()
    {
        if (autoConnectOnPlay)
        {
            ConnectToPhoton();
        }
    }

    private void OnGUI()
    {
        GUI.backgroundColor = Color.white;
        currentPanelWidth = Mathf.Clamp(Screen.width - 24f, 320f, PanelWidth);
        currentPanelHeight = Mathf.Clamp(Screen.height - 24f, 320f, PanelHeight);
        var panelRect = new Rect(
            (Screen.width - currentPanelWidth) * 0.5f,
            (Screen.height - currentPanelHeight) * 0.5f,
            currentPanelWidth,
            currentPanelHeight);

        GUILayout.BeginArea(panelRect, GUI.skin.window);
        GUILayout.Label(PhotonNetwork.InRoom ? "Room" : "Lobby");
        GUILayout.Space(8f);

        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Label($"Photon State: {PhotonNetwork.NetworkClientState}");
        GUILayout.Label($"Connected: {(PhotonNetwork.IsConnectedAndReady ? "Yes" : "No")}");
        GUILayout.Label($"Current Room: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "-")}");

        if (PhotonNetwork.InRoom)
        {
            DrawInRoomUI();
        }
        else
        {
            if (lobbyViewState == LobbyViewState.CreateRoom)
            {
                DrawCreateRoomUI();
            }
            else if (lobbyViewState == LobbyViewState.PasswordPrompt)
            {
                DrawPasswordPromptUI();
            }
            else
            {
                DrawLobbySetupUI();
            }
        }
        GUILayout.EndArea();
    }

    public void ConnectToPhoton()
    {
        localPlayerNameInput = SanitizePlayerName(localPlayerNameInput);
        if (string.IsNullOrWhiteSpace(localPlayerNameInput))
        {
            localPlayerNameInput = GenerateMemberName();
        }

        PhotonNetwork.NickName = localPlayerNameInput;
        PhotonNetwork.GameVersion = gameVersion;

        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
            {
                PhotonNetwork.JoinLobby();
            }

            SetStatus("Already connected to Photon.");
            return;
        }

        SetStatus("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void CreateRoom()
    {
        if (!IsCreateRoomValid())
        {
            SetStatus("Enter Lobby Name and Leader Name first.");
            return;
        }

        pendingCreateCode = GenerateRoomCode();

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            pendingAction = PendingAction.CreateRoom;
            SetStatus("Connecting first, then creating room...");
            ConnectToPhoton();
            return;
        }

        CreateRoomInternal();
    }

    public void ConnectToSelectedRoom()
    {
        var room = GetSelectedRoomInfo();
        if (room == null)
        {
            SetStatus("Select a room first.");
            return;
        }

        if (RoomRequiresPassword(room))
        {
            lobbyViewState = LobbyViewState.PasswordPrompt;
            passwordAttemptInput = string.Empty;
            return;
        }

        BeginJoinSelectedRoom(room.Name);
    }

    public void JoinSelectedRoomWithPassword()
    {
        var room = GetSelectedRoomInfo();
        if (room == null)
        {
            SetStatus("Selected room no longer exists.");
            lobbyViewState = LobbyViewState.Lobby;
            return;
        }

        if (GetRoomPassword(room) != passwordAttemptInput.Trim())
        {
            SetStatus("Incorrect password.");
            return;
        }

        lobbyViewState = LobbyViewState.Lobby;
        BeginJoinSelectedRoom(room.Name);
    }

    private void BeginJoinSelectedRoom(string roomCode)
    {
        pendingJoinRoomCode = roomCode;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            pendingAction = PendingAction.JoinRoom;
            SetStatus("Connecting first, then joining room...");
            ConnectToPhoton();
            return;
        }

        JoinRoomInternal();
    }

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
        {
            SetStatus("You are not in a room.");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { RoomClosingKey, true }
            });
        }

        SetStatus("Leaving room...");
        PhotonNetwork.LeaveRoom();
    }

    public void LoadTargetScene()
    {
        var sceneToLoad = GetTargetSceneName();
        if (!PhotonNetwork.InRoom)
        {
            SetStatus("Join a room before loading the test scene.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            SetStatus("Only the room host can load the test scene.");
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            SetStatus("Assign a target scene first.");
            return;
        }

        SetStatus($"Loading {sceneToLoad} for everyone...");
        PhotonNetwork.LoadLevel(sceneToLoad);
    }

    public override void OnConnectedToMaster()
    {
        SetStatus($"Connected as {PhotonNetwork.NickName}.");

        if (pendingAction == PendingAction.CreateRoom)
        {
            CreateRoomInternal();
        }
        else if (pendingAction == PendingAction.JoinRoom)
        {
            JoinRoomInternal();
        }
        else
        {
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        SetStatus("Lobby ready.");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        for (var i = 0; i < roomList.Count; i++)
        {
            var room = roomList[i];
            if (room.RemovedFromList || !room.IsVisible)
            {
                roomCache.Remove(room.Name);
            }
            else
            {
                roomCache[room.Name] = room;
            }
        }

        roomDisplayList.Clear();
        foreach (var room in roomCache.Values)
        {
            roomDisplayList.Add(room);
        }

        roomDisplayList.Sort((left, right) => string.CompareOrdinal(GetRoomDisplayName(left), GetRoomDisplayName(right)));

        if (!string.IsNullOrWhiteSpace(selectedRoomCode) && !roomCache.ContainsKey(selectedRoomCode))
        {
            selectedRoomCode = string.Empty;
        }
    }

    public override void OnJoinedRoom()
    {
        lobbyViewState = LobbyViewState.Lobby;
        passwordAttemptInput = string.Empty;

        if (PhotonNetwork.IsMasterClient)
        {
            localPlayerNameInput = SanitizePlayerName(createLeaderNameInput);
        }
        else
        {
            localPlayerNameInput = GenerateUniqueDefaultPlayerName();
        }

        localSubmittedName = string.Empty;
        SubmitLocalPlayerName(localPlayerNameInput);
        SetLocalReadyState(false);
        SetStatus($"Joined room: {GetCurrentRoomDisplayName()}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        pendingAction = PendingAction.None;
        pendingCreateCode = string.Empty;
        SetStatus($"Create room failed ({returnCode}): {message}");
        lobbyViewState = LobbyViewState.CreateRoom;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        pendingAction = PendingAction.None;
        SetStatus($"Disconnected: {cause}");
        roomCache.Clear();
        roomDisplayList.Clear();
        selectedRoomCode = string.Empty;
        lobbyViewState = LobbyViewState.Lobby;
    }

    public override void OnLeftRoom()
    {
        lobbyViewState = LobbyViewState.Lobby;
        passwordAttemptInput = string.Empty;
        SetStatus("Left room.");

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        pendingAction = PendingAction.None;
        SetStatus($"Join room failed ({returnCode}): {message}");
        lobbyViewState = LobbyViewState.Lobby;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetStatus($"{GetPlayerDisplayName(newPlayer)} joined the room.");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetStatus($"{GetPlayerDisplayName(otherPlayer)} left the room.");
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (propertiesThatChanged.ContainsKey(RoomClosingKey) && !PhotonNetwork.IsMasterClient)
        {
            SetStatus("Leader closed the room.");
            PhotonNetwork.LeaveRoom();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer == PhotonNetwork.LocalPlayer && changedProps.ContainsKey(PlayerDisplayNameKey))
        {
            localSubmittedName = GetPlayerDisplayName(targetPlayer);
            localPlayerNameInput = localSubmittedName;
        }

        if (changedProps.ContainsKey(PlayerReadyKey))
        {
            var readyText = GetPlayerReady(targetPlayer) ? "ready" : "not ready";
            SetStatus($"{GetPlayerDisplayName(targetPlayer)} is {readyText}.");
        }
    }

    private void CreateRoomInternal()
    {
        pendingAction = PendingAction.None;
        createLobbyNameInput = SanitizeLobbyName(createLobbyNameInput);
        createLeaderNameInput = SanitizePlayerName(createLeaderNameInput);
        if (createRequiresPassword && string.IsNullOrWhiteSpace(createPasswordCode))
        {
            createPasswordCode = GeneratePasswordCode();
        }

        PhotonNetwork.NickName = createLeaderNameInput;
        localPlayerNameInput = createLeaderNameInput;
        localSubmittedName = string.Empty;

        var options = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            CleanupCacheOnLeave = true,
            PublishUserId = true,
            CustomRoomProperties = new Hashtable
            {
                { RoomDisplayNameKey, createLobbyNameInput },
                { LeaderNameKey, createLeaderNameInput },
                { PasswordRequiredKey, createRequiresPassword },
                { PasswordCodeKey, createRequiresPassword ? createPasswordCode : string.Empty },
                { RoomClosingKey, false }
            },
            CustomRoomPropertiesForLobby = new[]
            {
                RoomDisplayNameKey,
                LeaderNameKey,
                PasswordRequiredKey,
                PasswordCodeKey
            }
        };

        SetStatus($"Creating room: {createLobbyNameInput} ({pendingCreateCode})");
        PhotonNetwork.CreateRoom(pendingCreateCode, options);
    }

    private void JoinRoomInternal()
    {
        pendingAction = PendingAction.None;
        SetStatus($"Joining room with code: {pendingJoinRoomCode}");
        PhotonNetwork.JoinRoom(pendingJoinRoomCode);
    }

    private void EnsureDefaultInputs()
    {
        if (string.IsNullOrWhiteSpace(localPlayerNameInput))
        {
            localPlayerNameInput = GenerateMemberName();
        }
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        Debug.Log($"[LobbyTestPhotonController] {message}");
    }

    private string GetTargetSceneName()
    {
        if (targetSceneReference != null && !string.IsNullOrWhiteSpace(targetSceneReference.SceneName))
        {
            return targetSceneReference.SceneName;
        }

        return targetSceneName;
    }

    private void DrawLobbySetupUI()
    {
        var roomListHeight = Mathf.Clamp(currentPanelHeight - 300f, 140f, 360f);

        GUILayout.Space(18f);
        GUILayout.Label("Lobby List");
        roomScrollPosition = GUILayout.BeginScrollView(roomScrollPosition, GUILayout.Height(roomListHeight));
        if (roomDisplayList.Count == 0)
        {
            GUILayout.Label("No room available.");
        }

        for (var i = 0; i < roomDisplayList.Count; i++)
        {
            DrawRoomCard(roomDisplayList[i]);
            GUILayout.Space(8f);
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10f);
        using (new GUILayout.HorizontalScope())
        {
            var oldColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(1f, 0.7f, 0.2f, 1f);
            if (LeftClickButton("Create Room", GUILayout.Height(42f)))
            {
                lobbyViewState = LobbyViewState.CreateRoom;
                createLobbyNameInput = string.Empty;
                createLeaderNameInput = string.Empty;
                createRequiresPassword = false;
                createPasswordCode = string.Empty;
            }

            GUI.backgroundColor = GetSelectedRoomInfo() == null ? Color.red : Color.green;
            if (LeftClickButton("Connect", GUILayout.Height(42f)))
            {
                ConnectToSelectedRoom();
            }

            GUI.backgroundColor = oldColor;
        }
    }

    private void DrawInRoomUI()
    {
        var room = PhotonNetwork.CurrentRoom;
        var playerListHeight = Mathf.Clamp(currentPanelHeight - 360f, 120f, 300f);
        var allMembersReady = AreAllNonLeaderPlayersReady();

        GUILayout.Space(16f);
        GUILayout.Label(GetCurrentRoomDisplayName());
        GUILayout.Label($"Password : {GetCurrentRoomPasswordLabel()}");
        GUILayout.Label($"Player : {room.PlayerCount} / {room.MaxPlayers}");

        GUILayout.Space(12f);
        GUILayout.BeginVertical("box", GUILayout.Height(playerListHeight));
        playerScrollPosition = GUILayout.BeginScrollView(playerScrollPosition, GUILayout.Height(playerListHeight - 12f));
        DrawPlayerSlots();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8f);
        using (new GUILayout.HorizontalScope())
        {
            var oldColor = GUI.backgroundColor;
            var oldEnabled = GUI.enabled;

            GUI.backgroundColor = PhotonNetwork.IsMasterClient
                ? (allMembersReady ? Color.green : Color.gray)
                : (GetPlayerReady(PhotonNetwork.LocalPlayer) ? Color.green : new Color(0.85f, 0.85f, 0.2f, 1f));
            GUI.enabled = !PhotonNetwork.IsMasterClient || allMembersReady;

            var primaryLabel = PhotonNetwork.IsMasterClient ? "Start" : "Ready";
            if (LeftClickButton(primaryLabel, GUILayout.Height(40f)))
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    if (allMembersReady)
                    {
                        LoadTargetScene();
                    }
                    else
                    {
                        SetStatus("All members must be ready before starting.");
                    }
                }
                else
                {
                    SetLocalReadyState(!GetPlayerReady(PhotonNetwork.LocalPlayer));
                }
            }

            GUI.enabled = oldEnabled;
            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f, 1f);
            if (LeftClickButton("Leave", GUILayout.Height(40f)))
            {
                LeaveRoom();
            }

            GUI.backgroundColor = oldColor;
            GUI.enabled = oldEnabled;
        }
    }

    private string GetCurrentRoomDisplayName()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            return string.Empty;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomDisplayNameKey, out var value))
        {
            return value?.ToString();
        }

        return PhotonNetwork.CurrentRoom.Name;
    }

    private string GetCurrentRoomPasswordLabel()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            return string.Empty;
        }

        if (!TryReadBool(PhotonNetwork.CurrentRoom.CustomProperties, PasswordRequiredKey))
        {
            return "Open";
        }

        return TryReadString(PhotonNetwork.CurrentRoom.CustomProperties, PasswordCodeKey, string.Empty);
    }

    private void DrawCreateRoomUI()
    {
        GUILayout.Space(16f);
        GUILayout.Label("Create Lobby");
        GUILayout.Space(8f);
        GUILayout.Label("Lobby Name");
        createLobbyNameInput = GUILayout.TextField(createLobbyNameInput, 24);
        GUILayout.Space(8f);
        GUILayout.Label("Leader Name");
        createLeaderNameInput = GUILayout.TextField(createLeaderNameInput, 24);
        GUILayout.Space(12f);
        GUILayout.Label($"Max Player : {maxPlayersPerRoom}");

        var nextToggle = GUILayout.Toggle(createRequiresPassword, "Password");
        if (nextToggle != createRequiresPassword)
        {
            createRequiresPassword = nextToggle;
            createPasswordCode = createRequiresPassword ? GeneratePasswordCode() : string.Empty;
        }

        GUI.enabled = false;
        GUILayout.TextField(createRequiresPassword ? createPasswordCode : string.Empty, 16);
        GUI.enabled = true;

        GUILayout.Space(18f);
        using (new GUILayout.HorizontalScope())
        {
            var oldColor = GUI.backgroundColor;

            GUI.backgroundColor = IsCreateRoomValid() ? Color.green : Color.red;
            if (LeftClickButton("Create Room", GUILayout.Height(46f)))
            {
                CreateRoom();
            }

            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f, 1f);
            if (LeftClickButton("Cancel", GUILayout.Height(46f)))
            {
                lobbyViewState = LobbyViewState.Lobby;
            }

            GUI.backgroundColor = oldColor;
        }
    }

    private void DrawPasswordPromptUI()
    {
        var room = GetSelectedRoomInfo();
        GUILayout.Space(16f);
        GUILayout.Label("Enter Password");
        GUILayout.Label(room == null ? "Selected room no longer exists." : GetRoomDisplayName(room));
        passwordAttemptInput = GUILayout.TextField(passwordAttemptInput, 16);
        GUILayout.Space(12f);

        using (new GUILayout.HorizontalScope())
        {
            var oldColor = GUI.backgroundColor;

            GUI.backgroundColor = string.IsNullOrWhiteSpace(passwordAttemptInput) ? Color.red : Color.green;
            if (LeftClickButton("Join Room", GUILayout.Height(44f)))
            {
                JoinSelectedRoomWithPassword();
            }

            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f, 1f);
            if (LeftClickButton("Back", GUILayout.Height(44f)))
            {
                lobbyViewState = LobbyViewState.Lobby;
            }

            GUI.backgroundColor = oldColor;
        }
    }

    private void DrawRoomCard(RoomInfo room)
    {
        var oldColor = GUI.backgroundColor;
        GUI.backgroundColor = room.Name == selectedRoomCode ? Color.yellow : Color.white;

        GUILayout.BeginVertical("box");
        if (LeftClickButton($"Lobby Name : {GetRoomDisplayName(room)}\nMax Player : {room.PlayerCount} / {room.MaxPlayers}\nLeader : {GetRoomLeaderName(room)}\nPassword : {(RoomRequiresPassword(room) ? "Require" : "Open")}", GUILayout.Height(110f)))
        {
            selectedRoomCode = room.Name;
        }
        GUILayout.EndVertical();
        GUI.backgroundColor = oldColor;
    }

    private void DrawPlayerSlots()
    {
        var players = GetPlayersInDisplayOrder();
        for (var i = 0; i < maxPlayersPerRoom; i++)
        {
            var oldColor = GUI.backgroundColor;
            if (i < players.Count && GetPlayerReady(players[i]))
            {
                GUI.backgroundColor = Color.green;
            }

            GUILayout.BeginVertical("box");
            if (i < players.Count)
            {
                DrawPlayerSlot(players[i]);
            }
            else
            {
                GUILayout.Label("Player Name :");
                GUILayout.Label(string.Empty);
            }

            GUILayout.EndVertical();
            GUI.backgroundColor = oldColor;
            GUILayout.Space(6f);
        }
    }

    private void DrawPlayerSlot(Player player)
    {
        GUILayout.Label("Player Name :");
        if (player == PhotonNetwork.LocalPlayer && !player.IsMasterClient)
        {
            var nextName = GUILayout.TextField(localPlayerNameInput, 24);
            nextName = SanitizePlayerName(nextName);
            if (nextName != localPlayerNameInput)
            {
                localPlayerNameInput = nextName;
                SubmitLocalPlayerName(localPlayerNameInput);
            }
        }
        else
        {
            GUILayout.Label(GetPlayerDisplayName(player));
        }

        GUILayout.Label(player.IsMasterClient ? "Leader" : "Member");
        if (!player.IsMasterClient)
        {
            GUILayout.Label(GetPlayerReady(player) ? "Ready" : "Not Ready");
        }
    }

    private string GenerateRoomCode()
    {
        var codeChars = new char[RoomCodeLength];
        for (var i = 0; i < RoomCodeLength; i++)
        {
            codeChars[i] = RoomCodeCharacters[Random.Range(0, RoomCodeCharacters.Length)];
        }

        return new string(codeChars);
    }

    private string GeneratePasswordCode()
    {
        var codeChars = new char[RoomCodeLength];
        for (var i = 0; i < RoomCodeLength; i++)
        {
            codeChars[i] = PasswordCharacters[Random.Range(0, PasswordCharacters.Length)];
        }

        return new string(codeChars);
    }

    private string GetRoomDisplayName(RoomInfo room)
    {
        return TryReadString(room.CustomProperties, RoomDisplayNameKey, room.Name);
    }

    private string GetRoomLeaderName(RoomInfo room)
    {
        return TryReadString(room.CustomProperties, LeaderNameKey, "Leader");
    }

    private bool RoomRequiresPassword(RoomInfo room)
    {
        return TryReadBool(room.CustomProperties, PasswordRequiredKey);
    }

    private string GetRoomPassword(RoomInfo room)
    {
        return TryReadString(room.CustomProperties, PasswordCodeKey, string.Empty);
    }

    private RoomInfo GetSelectedRoomInfo()
    {
        if (string.IsNullOrWhiteSpace(selectedRoomCode))
        {
            return null;
        }

        roomCache.TryGetValue(selectedRoomCode, out var room);
        return room;
    }

    private List<Player> GetPlayersInDisplayOrder()
    {
        var players = new List<Player>();
        foreach (var entry in PhotonNetwork.CurrentRoom.Players)
        {
            players.Add(entry.Value);
        }

        players.Sort((left, right) =>
        {
            if (left.IsMasterClient && !right.IsMasterClient)
            {
                return -1;
            }

            if (!left.IsMasterClient && right.IsMasterClient)
            {
                return 1;
            }

            return left.ActorNumber.CompareTo(right.ActorNumber);
        });

        return players;
    }

    private string GetPlayerDisplayName(Player player)
    {
        return TryReadString(player.CustomProperties, PlayerDisplayNameKey, player.NickName);
    }

    private bool GetPlayerReady(Player player)
    {
        return TryReadBool(player.CustomProperties, PlayerReadyKey);
    }

    private void SubmitLocalPlayerName(string playerName)
    {
        var sanitizedName = SanitizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(sanitizedName) || sanitizedName == localSubmittedName)
        {
            return;
        }

        localSubmittedName = sanitizedName;
        PhotonNetwork.NickName = sanitizedName;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PlayerDisplayNameKey, sanitizedName }
        });
    }

    private void SetLocalReadyState(bool isReady)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PlayerReadyKey, isReady }
        });
    }

    private bool AreAllNonLeaderPlayersReady()
    {
        foreach (var entry in PhotonNetwork.CurrentRoom.Players)
        {
            var player = entry.Value;
            if (player.IsMasterClient)
            {
                continue;
            }

            if (!GetPlayerReady(player))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCreateRoomValid()
    {
        return !string.IsNullOrWhiteSpace(SanitizeLobbyName(createLobbyNameInput)) &&
               !string.IsNullOrWhiteSpace(SanitizePlayerName(createLeaderNameInput));
    }

    private bool LeftClickButton(string label, params GUILayoutOption[] options)
    {
        var clicked = GUILayout.Button(label, options);
        return clicked && Event.current.button == 0;
    }

    private string SanitizeLobbyName(string lobbyName)
    {
        return string.IsNullOrWhiteSpace(lobbyName) ? string.Empty : lobbyName.Trim();
    }

    private string SanitizePlayerName(string playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim();
    }

    private string GenerateMemberName()
    {
        return $"Player{Random.Range(100, 1000)}";
    }

    private string GenerateUniqueDefaultPlayerName()
    {
        var usedNames = new HashSet<string>();
        foreach (var entry in PhotonNetwork.CurrentRoom.Players)
        {
            var player = entry.Value;
            if (player == PhotonNetwork.LocalPlayer)
            {
                continue;
            }

            var displayName = GetPlayerDisplayName(player);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                usedNames.Add(displayName);
            }
        }

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var candidate = GenerateMemberName();
            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }
        }

        for (var suffix = 1000; suffix < 10000; suffix++)
        {
            var candidate = $"Player{suffix}";
            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"Player{PhotonNetwork.LocalPlayer.ActorNumber}";
    }

    private static string TryReadString(Hashtable table, string key, string fallback)
    {
        if (table != null && table.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }

        return fallback;
    }

    private static bool TryReadBool(Hashtable table, string key)
    {
        if (table != null && table.TryGetValue(key, out var value) && value is bool boolValue)
        {
            return boolValue;
        }

        return false;
    }
}

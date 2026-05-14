using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyTestPhotonController : MonoBehaviourPunCallbacks
{
    private static readonly TypedLobby SharedLobby = TypedLobby.Default;
    private const string LobbyNameKey = "LobbyName";
    private const string LeaderNameKey = "LeaderName";
    private const string PasswordRequiredKey = "PasswordRequired";
    private const string PasswordCodeKey = "PasswordCode";
    private const string PlayerNameKey = "PlayerName";
    private const string PlayerReadyKey = "PlayerReady";
    private const string RoomClosingKey = "RoomClosing";

    [Header("Connect")]
    [SerializeField] private string gameVersion = "KanLobby";
    [SerializeField] private string devRegion = "asia";
    [SerializeField] private byte maxPlayersPerRoom = 4;
    [SerializeField] private LobbySceneTargetReference sceneTargetReference;
    [SerializeField] private string mainMenuSceneName = "Menu";

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField createLobbyNameInput;
    [SerializeField] private TMP_InputField createLeaderNameInput;
    [SerializeField] private Toggle createPasswordToggle;
    [SerializeField] private TMP_InputField generatedPasswordInput;
    [SerializeField] private Button openCreateRoomButton;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button cancelCreateButton;
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private Button roomListEntryButtonPrefab;
    [SerializeField] private TextMeshProUGUI screenHeaderText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI photonStateText;
    [SerializeField] private TextMeshProUGUI connectedText;
    [SerializeField] private TextMeshProUGUI currentRoomText;
    [SerializeField] private TextMeshProUGUI lobbyListTitleText;
    [SerializeField] private TextMeshProUGUI emptyLobbyText;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Room UI")]
    [SerializeField] private TextMeshProUGUI roomTitleText;
    [SerializeField] private TextMeshProUGUI roomPasswordText;
    [SerializeField] private TextMeshProUGUI roomPlayerCountText;
    [SerializeField] private TMP_InputField[] playerNameInputs = Array.Empty<TMP_InputField>();
    [SerializeField] private TextMeshProUGUI[] playerRoleTexts = Array.Empty<TextMeshProUGUI>();
    [SerializeField] private Image[] playerSlotBorders = Array.Empty<Image>();
    [SerializeField] private Button readyOrStartButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private GameObject roomPanel;

    private readonly Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();
    private readonly List<Button> roomListButtons = new List<Button>();
    private const float RoomListEntryHeight = 138f;
    private const float RoomListEntrySpacing = 16f;
    private const float RoomListPaddingTop = 14f;
    private const float RoomListPaddingBottom = 14f;

    private RoomInfo selectedRoom;
    private string generatedPasswordCode = "";
    private GameObject joinPasswordPanel;
    private TMP_InputField joinPasswordInput;
    private Button joinPasswordConfirmButton;
    private Button joinPasswordCancelButton;
    private bool pendingReturnToMainMenu;
    private bool pendingLobbyRefresh;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;

        if (!string.IsNullOrWhiteSpace(devRegion))
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = devRegion;
        }

        WireUi();
        ConfigureRuntimeListLayouts();
        EnsureJoinPasswordPanel();
        ConnectToPhoton();
        RefreshAllUi();
    }

    private void Update()
    {
        HandleMainMenuEscapeInput();
        UpdateCreateButtonState();
        UpdateConnectButtonState();
        UpdateLobbyForegroundVisibility();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby(SharedLobby);
        SetStatus("Connected to Photon.");
        RefreshConnectionSummaryUi();
    }

    public override void OnJoinedLobby()
    {
        pendingLobbyRefresh = false;
        SetStatus("Lobby ready.");
        RefreshConnectionSummaryUi();
        RefreshRoomList();
    }

    public override void OnLeftLobby()
    {
        if (pendingLobbyRefresh && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom)
        {
            PhotonNetwork.JoinLobby(SharedLobby);
        }
    }

    public override void OnCreatedRoom()
    {
        SetStatus($"Created room: {(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "-")}");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"[LobbyTestPhotonController] Room list update received: {roomList.Count}");
        for (var i = 0; i < roomList.Count; i++)
        {
            var info = roomList[i];
            Debug.Log($"[LobbyTestPhotonController] Room update -> name:{info.Name}, removed:{info.RemovedFromList}, visible:{info.IsVisible}, open:{info.IsOpen}, players:{info.PlayerCount}/{info.MaxPlayers}");
            if (info.RemovedFromList)
            {
                cachedRooms.Remove(info.Name);
                continue;
            }

            cachedRooms[info.Name] = info;
        }

        RefreshRoomList();
    }

    public override void OnJoinedRoom()
    {
        AssignUniqueDefaultNameIfNeeded();
        SetLocalReady(false);
        CloseJoinPasswordPanel();
        ResetCreateRoomForm();
        if (PhotonNetwork.IsMasterClient)
        {
            RepublishCurrentRoomListing();
        }
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(false);
        }
        UpdateRoomUi();
        RefreshAllUi();
    }

    public override void OnLeftRoom()
    {
        if (pendingReturnToMainMenu)
        {
            pendingReturnToMainMenu = false;
            TryLoadMainMenuScene();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby(SharedLobby);
        }

        RefreshAllUi();
    }

    private void HandleMainMenuEscapeInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        CloseJoinPasswordPanel();
        CloseCreateRoomPanel();

        if (PhotonNetwork.InRoom)
        {
            pendingReturnToMainMenu = true;
            PhotonNetwork.LeaveRoom(false);
            return;
        }

        TryLoadMainMenuScene();
    }

    private void TryLoadMainMenuScene()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("[LobbyTestPhotonController] No main menu scene assigned for Escape return.");
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        Debug.LogWarning($"[LobbyTestPhotonController] Scene '{mainMenuSceneName}' is not available to load.");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"Create room failed: {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetStatus($"Join room failed: {message}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus($"Disconnected: {cause}");
        cachedRooms.Clear();
        selectedRoom = null;
        RefreshAllUi();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            RepublishCurrentRoomListing();
        }

        UpdateRoomUi();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            RepublishCurrentRoomListing();
        }

        UpdateRoomUi();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        UpdateRoomUi();
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        if (ReadBool(propertiesThatChanged, RoomClosingKey, false) && PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        UpdateRoomUi();
    }

    public void OpenCreateRoomPanel()
    {
        if (PhotonNetwork.InRoom)
        {
            return;
        }

        ResetCreateRoomForm();

        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(true);
        }

        GeneratePasswordIfNeeded();
        UpdateCreateButtonState();
        UpdateLobbyForegroundVisibility();
    }

    public void CloseCreateRoomPanel()
    {
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(false);
        }

        ResetCreateRoomForm();
        UpdateLobbyForegroundVisibility();
    }

    public void CreateRoom()
    {
        if (!CanCreateRoom())
        {
            return;
        }

        generatedPasswordCode = createPasswordToggle != null && createPasswordToggle.isOn ? GenerateCode(5) : "";
        var roomCode = GenerateCode(5);
        var lobbyName = createLobbyNameInput.text.Trim();
        var leaderName = createLeaderNameInput.text.Trim();

        SetLocalPlayerName(leaderName);
        SetLocalReady(false);

        var roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true,
            PublishUserId = true,
            CleanupCacheOnLeave = true,
            CustomRoomProperties = new PhotonHashtable
            {
                { LobbyNameKey, lobbyName },
                { LeaderNameKey, leaderName },
                { PasswordRequiredKey, createPasswordToggle != null && createPasswordToggle.isOn },
                { PasswordCodeKey, generatedPasswordCode }
            },
            CustomRoomPropertiesForLobby = new[] { LobbyNameKey, LeaderNameKey, PasswordRequiredKey, PasswordCodeKey }
        };

        SetStatus($"Creating room: {lobbyName} ({roomCode})");
        PhotonNetwork.CreateRoom(roomCode, roomOptions, SharedLobby);
    }

    public void JoinSelectedRoom()
    {
        if (selectedRoom == null)
        {
            return;
        }

        if (ReadBool(selectedRoom.CustomProperties, PasswordRequiredKey, false))
        {
            OpenJoinPasswordPanel();
            return;
        }

        JoinSelectedRoomInternal();
    }

    public void ConfirmJoinSelectedRoomPassword()
    {
        if (selectedRoom == null)
        {
            CloseJoinPasswordPanel();
            return;
        }

        var expectedPassword = ReadString(selectedRoom.CustomProperties, PasswordCodeKey, string.Empty);
        var enteredPassword = joinPasswordInput != null ? joinPasswordInput.text.Trim() : string.Empty;
        if (!string.Equals(expectedPassword, enteredPassword, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Wrong room password.");
            return;
        }

        CloseJoinPasswordPanel();
        JoinSelectedRoomInternal();
    }

    public void CancelJoinSelectedRoomPassword()
    {
        CloseJoinPasswordPanel();
    }

    private void JoinSelectedRoomInternal()
    {
        SetLocalPlayerName(GetDefaultLocalPlayerName());
        SetLocalReady(false);
        PhotonNetwork.JoinRoom(selectedRoom.Name);
    }

    public void ToggleReadyOrStart()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            if (!AreAllMembersReady())
            {
                return;
            }

            var targetSceneName = sceneTargetReference != null ? sceneTargetReference.GetTargetSceneName() : "";
            if (!string.IsNullOrWhiteSpace(targetSceneName))
            {
                PhotonNetwork.LoadLevel(targetSceneName);
            }

            return;
        }

        SetLocalReady(!GetPlayerReady(PhotonNetwork.LocalPlayer));
        UpdateRoomUi();
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
                {
                    { RoomClosingKey, true }
                });
                StartCoroutine(LeaveRoomAfterClosingFlag());
                return;
            }

            PhotonNetwork.LeaveRoom();
        }
    }

    public void SetSelectedRoom(string roomName)
    {
        selectedRoom = cachedRooms.TryGetValue(roomName, out var roomInfo) ? roomInfo : null;
        UpdateConnectButtonState();
        RefreshRoomList();
    }

    public void OnLocalPlayerNameEdited(string newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return;
        }

        SetLocalPlayerName(newValue.Trim());
    }

    private void WireUi()
    {
        if (openCreateRoomButton != null)
        {
            openCreateRoomButton.onClick.RemoveAllListeners();
            openCreateRoomButton.onClick.AddListener(OpenCreateRoomPanel);
        }

        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveAllListeners();
            createRoomButton.onClick.AddListener(CreateRoom);
        }

        if (connectButton != null)
        {
            connectButton.onClick.RemoveAllListeners();
            connectButton.onClick.AddListener(JoinSelectedRoom);
        }

        if (cancelCreateButton != null)
        {
            cancelCreateButton.onClick.RemoveAllListeners();
            cancelCreateButton.onClick.AddListener(CloseCreateRoomPanel);
        }

        if (createPasswordToggle != null)
        {
            createPasswordToggle.onValueChanged.RemoveAllListeners();
            createPasswordToggle.onValueChanged.AddListener(_ => GeneratePasswordIfNeeded());
        }

        if (readyOrStartButton != null)
        {
            readyOrStartButton.onClick.RemoveAllListeners();
            readyOrStartButton.onClick.AddListener(ToggleReadyOrStart);
        }

        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveAllListeners();
            leaveRoomButton.onClick.AddListener(LeaveRoom);
        }

        for (var i = 0; i < playerNameInputs.Length; i++)
        {
            if (playerNameInputs[i] == null)
            {
                continue;
            }

            playerNameInputs[i].onValueChanged.RemoveAllListeners();
            playerNameInputs[i].onValueChanged.AddListener(OnLocalPlayerNameEdited);
            playerNameInputs[i].onEndEdit.RemoveAllListeners();
            playerNameInputs[i].onEndEdit.AddListener(OnLocalPlayerNameEdited);
        }
    }

    private void ConnectToPhoton()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
            {
                pendingLobbyRefresh = true;
                cachedRooms.Clear();
                selectedRoom = null;
                RefreshRoomList();
                PhotonNetwork.LeaveLobby();
                return;
            }

            if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
            {
                PhotonNetwork.JoinLobby(SharedLobby);
            }

            RefreshConnectionSummaryUi();
            return;
        }

        SetStatus("Connecting to Photon...");
        PhotonNetwork.NickName = GetDefaultLocalPlayerName();
        PhotonNetwork.ConnectUsingSettings();
    }

    private void RefreshAllUi()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(!PhotonNetwork.InRoom);
        }

        if (roomPanel != null)
        {
            roomPanel.SetActive(PhotonNetwork.InRoom);
        }

        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(!PhotonNetwork.InRoom && createRoomPanel.activeSelf);
        }

        UpdateLobbyForegroundVisibility();
        RefreshConnectionSummaryUi();
        UpdateRoomUi();
        UpdateConnectButtonState();
        UpdateCreateButtonState();
        RefreshRoomList();
    }

    private void ConfigureRuntimeListLayouts()
    {
        NormalizeRoomListContent();
        ConfigureRoomPlayerListPanelLayout();
        NormalizeRoomSlotLayout();
    }

    private void RefreshRoomList()
    {
        if (roomListContent == null)
        {
            RefreshEmptyLobbyState();
            return;
        }

        if (roomListEntryButtonPrefab != null)
        {
            roomListEntryButtonPrefab.gameObject.SetActive(false);
        }

        for (var i = 0; i < roomListButtons.Count; i++)
        {
            if (roomListButtons[i] != null)
            {
                Destroy(roomListButtons[i].gameObject);
            }
        }

        roomListButtons.Clear();

        foreach (var pair in cachedRooms)
        {
            var roomInfo = pair.Value;
            var button = CreateRuntimeRoomListEntry(roomInfo, roomListButtons.Count);
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = BuildRoomSummary(roomInfo);
            }

            var roomName = roomInfo.Name;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SetSelectedRoom(roomName));

            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selectedRoom != null && selectedRoom.Name == roomInfo.Name
                    ? new Color(1f, 0.9f, 0.2f, 1f)
                    : Color.black;
            }

            roomListButtons.Add(button);
        }

        if (roomListContent is RectTransform roomListRect)
        {
            roomListRect.anchorMin = new Vector2(0f, 1f);
            roomListRect.anchorMax = new Vector2(1f, 1f);
            roomListRect.pivot = new Vector2(0.5f, 1f);
            roomListRect.anchoredPosition = Vector2.zero;
            var contentHeight = roomListButtons.Count <= 0
                ? 0f
                : RoomListPaddingTop + RoomListPaddingBottom + (roomListButtons.Count * RoomListEntryHeight) + ((roomListButtons.Count - 1) * RoomListEntrySpacing);
            roomListRect.sizeDelta = new Vector2(0f, contentHeight);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(roomListRect);
        }

        RefreshEmptyLobbyState();
    }

    private void UpdateCreateButtonState()
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = CanCreateRoom();
            ApplyButtonBorder(createRoomButton, CanCreateRoom() ? new Color(0.23f, 0.95f, 0.18f, 1f) : new Color(0.92f, 0.24f, 0.18f, 1f));
        }
    }

    private void UpdateConnectButtonState()
    {
        if (selectedRoom != null && !cachedRooms.ContainsKey(selectedRoom.Name))
        {
            selectedRoom = null;
        }

        if (connectButton != null)
        {
            connectButton.interactable = selectedRoom != null;
            ApplyButtonBorder(connectButton, selectedRoom != null ? new Color(0.23f, 0.95f, 0.18f, 1f) : new Color(0.92f, 0.24f, 0.18f, 1f));
        }

        if (openCreateRoomButton != null)
        {
            ApplyButtonBorder(openCreateRoomButton, new Color(0.79f, 0.48f, 0.08f, 1f));
        }

        if (cancelCreateButton != null)
        {
            ApplyButtonBorder(cancelCreateButton, new Color(0.95f, 0.48f, 0.08f, 1f));
        }

        if (leaveRoomButton != null)
        {
            ApplyButtonBorder(leaveRoomButton, new Color(0.79f, 0.48f, 0.08f, 1f));
        }

        if (readyOrStartButton != null)
        {
            var canStartOrReady = !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || AreAllMembersReady();
            ApplyButtonBorder(readyOrStartButton, canStartOrReady ? new Color(0.23f, 0.95f, 0.18f, 1f) : new Color(0.92f, 0.24f, 0.18f, 1f));
        }
    }

    private void UpdateRoomUi()
    {
        if (roomPanel != null)
        {
            roomPanel.SetActive(PhotonNetwork.InRoom);
        }

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            ClearRoomUi();
            RefreshConnectionSummaryUi();
            return;
        }

        var roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
        var lobbyName = ReadString(roomProperties, LobbyNameKey, PhotonNetwork.CurrentRoom.Name);
        var roomPassword = ReadRoomPassword(roomProperties);
        if (roomTitleText != null)
        {
            roomTitleText.text = lobbyName;
        }

        if (roomPasswordText != null)
        {
            roomPasswordText.text = $"Password : {roomPassword}";
        }

        if (roomPlayerCountText != null)
        {
            roomPlayerCountText.text = $"Player : {PhotonNetwork.CurrentRoom.PlayerCount} / {PhotonNetwork.CurrentRoom.MaxPlayers}";
        }

        var players = PhotonNetwork.PlayerList;
        Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

        for (var i = 0; i < playerNameInputs.Length; i++)
        {
            var hasPlayer = i < players.Length;
            var player = hasPlayer ? players[i] : null;
            var isLeader = hasPlayer && player == PhotonNetwork.MasterClient;
            var isReady = hasPlayer && GetPlayerReady(player);
            var slotRoot = GetPlayerSlotRoot(i);

            if (slotRoot != null)
            {
                slotRoot.SetActive(hasPlayer);
            }

            if (playerNameInputs[i] != null)
            {
                var isLocalEditingField = hasPlayer &&
                                          PhotonNetwork.LocalPlayer == player &&
                                          playerNameInputs[i].isFocused;

                if (!isLocalEditingField)
                {
                    playerNameInputs[i].SetTextWithoutNotify(hasPlayer ? GetPlayerName(player) : string.Empty);
                }

                playerNameInputs[i].interactable = hasPlayer && PhotonNetwork.LocalPlayer == player && !isLeader;
            }

            if (i < playerRoleTexts.Length && playerRoleTexts[i] != null)
            {
                playerRoleTexts[i].text = hasPlayer ? (isLeader ? "Leader" : "Member") : "";
            }

            if (i < playerSlotBorders.Length && playerSlotBorders[i] != null)
            {
                playerSlotBorders[i].color = !hasPlayer
                    ? new Color(0.45f, 0.45f, 0.45f, 1f)
                    : (isReady && !isLeader ? new Color(0.23f, 0.95f, 0.18f, 1f) : Color.black);
            }
        }

        var readyText = readyOrStartButton != null ? readyOrStartButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (readyText != null)
        {
            readyText.text = PhotonNetwork.IsMasterClient ? "Start" : "Ready";
        }

        if (readyOrStartButton != null)
        {
            readyOrStartButton.interactable = PhotonNetwork.IsMasterClient ? AreAllMembersReady() : true;
        }

        RefreshConnectionSummaryUi();
        UpdateConnectButtonState();
    }

    private bool CanCreateRoom()
    {
        return createLobbyNameInput != null &&
               createLeaderNameInput != null &&
               !string.IsNullOrWhiteSpace(createLobbyNameInput.text) &&
               !string.IsNullOrWhiteSpace(createLeaderNameInput.text);
    }

    private void GeneratePasswordIfNeeded()
    {
        generatedPasswordCode = createPasswordToggle != null && createPasswordToggle.isOn ? GenerateCode(5) : "";

        if (generatedPasswordInput != null)
        {
            generatedPasswordInput.text = string.Empty;
            generatedPasswordInput.interactable = false;
            generatedPasswordInput.gameObject.SetActive(false);
        }
    }

    private void AssignUniqueDefaultNameIfNeeded()
    {
        if (PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        var currentName = GetPlayerName(PhotonNetwork.LocalPlayer);
        if (!string.IsNullOrWhiteSpace(currentName) && !currentName.StartsWith("Player", StringComparison.Ordinal))
        {
            return;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var players = PhotonNetwork.PlayerList;
        for (var i = 0; i < players.Length; i++)
        {
            if (players[i] != PhotonNetwork.LocalPlayer)
            {
                usedNames.Add(GetPlayerName(players[i]));
            }
        }

        string candidate;
        do
        {
            candidate = $"Player{UnityEngine.Random.Range(100, 1000)}";
        } while (usedNames.Contains(candidate));

        SetLocalPlayerName(candidate);
    }

    private bool AreAllMembersReady()
    {
        var players = PhotonNetwork.PlayerList;
        if (players.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < players.Length; i++)
        {
            if (players[i] == PhotonNetwork.MasterClient)
            {
                continue;
            }

            if (!GetPlayerReady(players[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetDefaultLocalPlayerName()
    {
        if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            return PhotonNetwork.NickName;
        }

        return $"Player{UnityEngine.Random.Range(100, 1000)}";
    }

    private void SetLocalPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        PhotonNetwork.NickName = playerName;

        if (PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { PlayerNameKey, playerName }
        });
    }

    private void SetLocalReady(bool ready)
    {
        if (PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { PlayerReadyKey, ready }
        });
    }

    private static bool GetPlayerReady(Player player)
    {
        return player != null &&
               player.CustomProperties != null &&
               player.CustomProperties.TryGetValue(PlayerReadyKey, out var readyValue) &&
               readyValue is bool ready &&
               ready;
    }

    private static string GetPlayerName(Player player)
    {
        if (player == null)
        {
            return "";
        }

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerNameKey, out var playerNameValue) &&
            playerNameValue is string playerName &&
            !string.IsNullOrWhiteSpace(playerName))
        {
            return playerName;
        }

        return $"Player{player.ActorNumber:000}";
    }

    private static string GenerateCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        }

        return builder.ToString();
    }

    private static string BuildRoomSummary(RoomInfo roomInfo)
    {
        var properties = roomInfo.CustomProperties;
        var lobbyName = ReadString(properties, LobbyNameKey, roomInfo.Name);
        var leaderName = ReadString(properties, LeaderNameKey, "Leader");
        var passwordRequired = ReadBool(properties, PasswordRequiredKey, false);
        return $"Lobby Name : {lobbyName}\nMax Player : {roomInfo.PlayerCount} / {roomInfo.MaxPlayers}\nLeader : {leaderName}\nPassword : {(passwordRequired ? "Require" : "Open")}";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"Status: {message}";
        }

        Debug.Log($"[LobbyTestPhotonController] {message}");
    }

    private static string ReadString(PhotonHashtable properties, string key, string fallback)
    {
        if (properties != null && properties.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }

        return fallback;
    }

    private static bool ReadBool(PhotonHashtable properties, string key, bool fallback)
    {
        if (properties != null && properties.TryGetValue(key, out var value) && value is bool boolValue)
        {
            return boolValue;
        }

        return fallback;
    }

    private static string ReadRoomPassword(PhotonHashtable properties)
    {
        var passwordCode = ReadString(properties, PasswordCodeKey, string.Empty);
        return string.IsNullOrWhiteSpace(passwordCode) ? "None" : passwordCode;
    }

    private void ApplyButtonBorder(Button button, Color borderColor)
    {
        // UI styling is now controlled from the scene/prefab so runtime no longer overwrites button borders.
    }

    private void ClearRoomUi()
    {
        if (roomTitleText != null)
        {
            roomTitleText.text = "";
        }

        if (roomPasswordText != null)
        {
            roomPasswordText.text = "Password : None";
        }

        if (roomPlayerCountText != null)
        {
            roomPlayerCountText.text = "Player : 0 / 4";
        }

        for (var i = 0; i < playerNameInputs.Length; i++)
        {
            var slotRoot = GetPlayerSlotRoot(i);
            if (slotRoot != null)
            {
                slotRoot.SetActive(false);
            }

            if (playerNameInputs[i] != null)
            {
                playerNameInputs[i].SetTextWithoutNotify(string.Empty);
                playerNameInputs[i].interactable = false;
            }

            if (i < playerRoleTexts.Length && playerRoleTexts[i] != null)
            {
                playerRoleTexts[i].text = "";
            }

            if (i < playerSlotBorders.Length && playerSlotBorders[i] != null)
            {
                playerSlotBorders[i].color = new Color(0.45f, 0.45f, 0.45f, 1f);
            }
        }
    }

    private IEnumerator LeaveRoomAfterClosingFlag()
    {
        yield return null;
        yield return null;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    private void RefreshConnectionSummaryUi()
    {
        var showLobbySummary = !PhotonNetwork.InRoom;
        SetUiElementVisible(screenHeaderText, showLobbySummary);
        SetUiElementVisible(statusText, false);
        SetUiElementVisible(photonStateText, false);
        SetUiElementVisible(connectedText, false);
        SetUiElementVisible(currentRoomText, false);
        SetUiElementVisible(lobbyListTitleText, false);

        if (screenHeaderText != null)
        {
            screenHeaderText.text = PhotonNetwork.InRoom ? "Room" : "Lobby";
        }

        if (photonStateText != null)
        {
            photonStateText.text = $"Photon State: {PhotonNetwork.NetworkClientState}";
        }

        if (connectedText != null)
        {
            connectedText.text = $"Connected: {(PhotonNetwork.IsConnected ? "Yes" : "No")}";
        }

        if (currentRoomText != null)
        {
            currentRoomText.text = $"Current Room: {(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "-")}";
        }

        if (lobbyListTitleText != null)
        {
            lobbyListTitleText.text = PhotonNetwork.InRoom ? string.Empty : "Lobby List";
        }
    }

    private void RefreshEmptyLobbyState()
    {
        if (emptyLobbyText == null)
        {
            return;
        }

        var hasRooms = roomListButtons.Count > 0;
        emptyLobbyText.gameObject.SetActive(!PhotonNetwork.InRoom && !hasRooms);
        emptyLobbyText.text = hasRooms ? string.Empty : "No room available.";
    }

    private Button CreateRuntimeRoomListEntry(RoomInfo roomInfo, int index)
    {
        if (roomListEntryButtonPrefab != null)
        {
            var prefabButton = Instantiate(roomListEntryButtonPrefab, roomListContent);
            prefabButton.gameObject.name = $"Room Entry {roomInfo.Name}";
            prefabButton.gameObject.SetActive(true);
            prefabButton.transform.SetAsLastSibling();
            ConfigureRoomListEntryButton(prefabButton, roomInfo, index);
            return prefabButton;
        }

        var entryObject = new GameObject($"Room Entry {roomInfo.Name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button), typeof(LayoutElement));
        entryObject.transform.SetParent(roomListContent, false);
        entryObject.transform.SetAsLastSibling();

        var rectTransform = entryObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -(RoomListPaddingTop + (index * (RoomListEntryHeight + RoomListEntrySpacing))));
        rectTransform.sizeDelta = new Vector2(0f, RoomListEntryHeight);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;

        var layoutElement = entryObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = RoomListEntryHeight;
        layoutElement.preferredHeight = RoomListEntryHeight;
        layoutElement.flexibleHeight = -1f;

        var image = entryObject.GetComponent<Image>();
        image.enabled = true;
        image.color = new Color(0.83f, 0.83f, 0.83f, 1f);
        image.raycastTarget = true;

        var outline = entryObject.GetComponent<Outline>();
        outline.effectColor = selectedRoom != null && selectedRoom.Name == roomInfo.Name
            ? new Color(1f, 0.9f, 0.2f, 1f)
            : Color.black;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        var button = entryObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.pressedColor = new Color(0.74f, 0.74f, 0.74f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(entryObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(20f, 16f);
        textRect.offsetMax = new Vector2(-20f, -16f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.color = Color.black;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.margin = Vector4.zero;

        return button;
    }

    private void ConfigureRoomListEntryButton(Button button, RoomInfo roomInfo, int index)
    {
        if (button == null)
        {
            return;
        }

        var rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -(RoomListPaddingTop + (index * (RoomListEntryHeight + RoomListEntrySpacing))));
            rectTransform.sizeDelta = new Vector2(0f, RoomListEntryHeight);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        var layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = button.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = RoomListEntryHeight;
        layoutElement.preferredHeight = RoomListEntryHeight;
        layoutElement.flexibleHeight = -1f;

        var outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = selectedRoom != null && selectedRoom.Name == roomInfo.Name
                ? new Color(1f, 0.9f, 0.2f, 1f)
                : Color.black;
        }

        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(button.transform, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
        }

        var textRect = text.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(20f, 16f);
            textRect.offsetMax = new Vector2(-20f, -16f);
        }

        text.gameObject.SetActive(true);
        text.enabled = true;
        text.raycastTarget = false;
    }

    private void RepublishCurrentRoomListing()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        var currentRoom = PhotonNetwork.CurrentRoom;
        currentRoom.IsVisible = true;
        currentRoom.IsOpen = true;

        var updatedProperties = new PhotonHashtable
        {
            { LobbyNameKey, ReadString(currentRoom.CustomProperties, LobbyNameKey, currentRoom.Name) },
            { LeaderNameKey, ReadString(currentRoom.CustomProperties, LeaderNameKey, GetPlayerName(PhotonNetwork.MasterClient)) },
            { PasswordRequiredKey, ReadBool(currentRoom.CustomProperties, PasswordRequiredKey, false) },
            { PasswordCodeKey, ReadString(currentRoom.CustomProperties, PasswordCodeKey, string.Empty) }
        };

        currentRoom.SetCustomProperties(updatedProperties);
        Debug.Log($"[LobbyTestPhotonController] Republished room listing: {currentRoom.Name}");
    }

    private void ConfigureRoomPlayerListPanelLayout()
    {
        if (roomPanel == null)
        {
            return;
        }

        var playerListPanelRect = FindChildRect(roomPanel.transform, "Player List Panel");
        SetRectTransform(playerListPanelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(760f, 500f));

        if (playerListPanelRect == null)
        {
            return;
        }

        var listGroup = playerListPanelRect.GetComponent<VerticalLayoutGroup>();
        if (listGroup != null)
        {
            listGroup.padding = new RectOffset(18, 18, 18, 18);
            listGroup.spacing = 14f;
            listGroup.childAlignment = TextAnchor.UpperCenter;
            listGroup.childControlWidth = true;
            listGroup.childControlHeight = true;
            listGroup.childForceExpandWidth = true;
            listGroup.childForceExpandHeight = false;
        }
    }

    private void NormalizeUiLayout()
    {
        NormalizeWidePanel(lobbyPanel, 70f, 70f, 110f, 70f);
        NormalizeWidePanel(roomPanel, 70f, 70f, 70f, 70f);
        NormalizeRoomListContent();
        NormalizeCreateRoomPanelLayout();
        NormalizeRoomPanelLayout();

        if (roomPanel != null)
        {
            var roomPanelRect = roomPanel.GetComponent<RectTransform>();
            if (roomPanelRect != null)
            {
                roomPanelRect.offsetMax = new Vector2(-70f, -52f);
            }
        }

        NormalizeRoomSlotLayout();
    }

    private void NormalizeWidePanel(GameObject panel, float left, float right, float top, float bottom)
    {
        if (panel == null)
        {
            return;
        }

        var rectTransform = panel.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private void NormalizeCreateRoomPanelLayout()
    {
        if (createRoomPanel == null)
        {
            return;
        }

        SetRectTransform(createRoomPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 560f));

        SetRectTransform(FindChildRect(createRoomPanel.transform, "Create Panel Title"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(420f, 72f));
        SetRectTransform(FindChildRect(createRoomPanel.transform, "Lobby Name Label"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -108f), new Vector2(240f, 38f));
        SetRectTransform(createLobbyNameInput != null ? createLobbyNameInput.GetComponent<RectTransform>() : null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -154f), new Vector2(520f, 54f));
        SetRectTransform(FindChildRect(createRoomPanel.transform, "Leader Name Label"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -226f), new Vector2(240f, 38f));
        SetRectTransform(createLeaderNameInput != null ? createLeaderNameInput.GetComponent<RectTransform>() : null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -272f), new Vector2(520f, 54f));
        SetRectTransform(FindChildRect(createRoomPanel.transform, "Max Player Label"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -350f), new Vector2(240f, 38f));
        SetRectTransform(FindChildRect(createRoomPanel.transform, "Password Label"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -402f), new Vector2(160f, 38f));
        SetRectTransform(createPasswordToggle != null ? createPasswordToggle.GetComponent<RectTransform>() : null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(176f, -404f), new Vector2(28f, 28f));
        SetRectTransform(generatedPasswordInput != null ? generatedPasswordInput.GetComponent<RectTransform>() : null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -450f), new Vector2(320f, 50f));
        SetRectTransform(createRoomButton != null ? createRoomButton.GetComponent<RectTransform>() : null, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-42f, -360f), new Vector2(246f, 62f));
        SetRectTransform(cancelCreateButton != null ? cancelCreateButton.GetComponent<RectTransform>() : null, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-42f, -436f), new Vector2(246f, 54f));

        NormalizeInputVisual(createLobbyNameInput, 22f);
        NormalizeInputVisual(createLeaderNameInput, 22f);
        NormalizeInputVisual(generatedPasswordInput, 20f);
        NormalizeButtonVisual(createRoomButton, 20f);
        NormalizeButtonVisual(cancelCreateButton, 18f);
    }

    private void NormalizeRoomPanelLayout()
    {
        if (roomPanel == null)
        {
            return;
        }

        SetRectTransform(roomTitleText != null ? roomTitleText.GetComponent<RectTransform>() : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 52f));
        SetRectTransform(roomPasswordText != null ? roomPasswordText.GetComponent<RectTransform>() : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(420f, 32f));
        SetRectTransform(roomPlayerCountText != null ? roomPlayerCountText.GetComponent<RectTransform>() : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(360f, 32f));

        if (roomTitleText != null)
        {
            roomTitleText.color = Color.white;
        }

        if (roomPasswordText != null)
        {
            roomPasswordText.color = Color.white;
        }

        if (roomPlayerCountText != null)
        {
            roomPlayerCountText.color = Color.white;
        }

        var playerListPanelRect = FindChildRect(roomPanel.transform, "Player List Panel");
        SetRectTransform(playerListPanelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(760f, 500f));

        if (playerListPanelRect != null)
        {
            var listGroup = playerListPanelRect.GetComponent<VerticalLayoutGroup>();
            if (listGroup != null)
            {
                listGroup.padding = new RectOffset(18, 18, 18, 18);
                listGroup.spacing = 14f;
                listGroup.childAlignment = TextAnchor.UpperCenter;
                listGroup.childControlWidth = true;
                listGroup.childControlHeight = true;
                listGroup.childForceExpandWidth = true;
                listGroup.childForceExpandHeight = false;
            }
        }

        SetRectTransform(readyOrStartButton != null ? readyOrStartButton.GetComponent<RectTransform>() : null, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 28f), new Vector2(190f, 58f));
        SetRectTransform(leaveRoomButton != null ? leaveRoomButton.GetComponent<RectTransform>() : null, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 28f), new Vector2(190f, 58f));

        NormalizeButtonVisual(readyOrStartButton, 20f);
        NormalizeButtonVisual(leaveRoomButton, 20f);
    }

    private void NormalizeRoomSlotLayout()
    {
        for (var i = 0; i < playerNameInputs.Length; i++)
        {
            var slotRoot = GetPlayerSlotRoot(i);
            if (slotRoot == null)
            {
                continue;
            }

            var slotRect = slotRoot.GetComponent<RectTransform>();
            if (slotRect != null)
            {
                slotRect.anchorMin = new Vector2(0f, 1f);
                slotRect.anchorMax = new Vector2(1f, 1f);
                slotRect.pivot = new Vector2(0.5f, 1f);
                slotRect.sizeDelta = new Vector2(0f, 94f);
            }

            var layoutElement = slotRoot.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = 94f;
                layoutElement.minHeight = 94f;
            }

            var slotImage = slotRoot.GetComponent<Image>();

            var labelRect = FindChildRect(slotRoot.transform, $"Player Label {i + 1}");
            SetRectTransform(labelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(136f, 24f));

            if (playerNameInputs[i] != null)
            {
                var inputRect = playerNameInputs[i].GetComponent<RectTransform>();
                SetRectTransform(inputRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(156f, -10f), new Vector2(520f, 42f));
            }

            if (i < playerRoleTexts.Length && playerRoleTexts[i] != null)
            {
                var roleRect = playerRoleTexts[i].GetComponent<RectTransform>();
                SetRectTransform(roleRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(260f, 26f));
            }
        }
    }

    private void NormalizeRoomListContent()
    {
        if (!(roomListContent is RectTransform roomListRect))
        {
            return;
        }

        roomListRect.anchorMin = new Vector2(0f, 1f);
        roomListRect.anchorMax = new Vector2(1f, 1f);
        roomListRect.pivot = new Vector2(0.5f, 1f);
        roomListRect.anchoredPosition = Vector2.zero;
        roomListRect.sizeDelta = Vector2.zero;

        var verticalLayoutGroup = roomListContent.GetComponent<VerticalLayoutGroup>();
        if (verticalLayoutGroup != null)
        {
            verticalLayoutGroup.enabled = false;
        }

        var contentSizeFitter = roomListContent.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.enabled = false;
        }
    }

    private void UpdateLobbyForegroundVisibility()
    {
        var showLobbyButtons = !PhotonNetwork.InRoom &&
                               (createRoomPanel == null || !createRoomPanel.activeSelf) &&
                               (joinPasswordPanel == null || !joinPasswordPanel.activeSelf);
        SetUiElementVisible(openCreateRoomButton, showLobbyButtons);
        SetUiElementVisible(connectButton, showLobbyButtons);
    }

    private void NormalizeInputVisual(TMP_InputField inputField, float fontSize)
    {
        if (inputField == null)
        {
            return;
        }

        var image = inputField.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.93f, 0.93f, 0.93f, 1f);
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.fontSize = fontSize;
            inputField.textComponent.alignment = TextAlignmentOptions.Left;
            inputField.textComponent.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        }

        if (inputField.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.fontSize = fontSize;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.color = new Color(0.35f, 0.35f, 0.35f, 0.65f);
        }
    }

    private void NormalizeButtonVisual(Button button, float fontSize)
    {
        if (button == null)
        {
            return;
        }

        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(10f, 4f, 10f, 4f);
        }
    }

    private void ResetCreateRoomForm()
    {
        if (createLobbyNameInput != null)
        {
            createLobbyNameInput.SetTextWithoutNotify(string.Empty);
        }

        if (createLeaderNameInput != null)
        {
            createLeaderNameInput.SetTextWithoutNotify(string.Empty);
        }

        if (createPasswordToggle != null)
        {
            createPasswordToggle.SetIsOnWithoutNotify(false);
        }

        generatedPasswordCode = string.Empty;
        if (generatedPasswordInput != null)
        {
            generatedPasswordInput.SetTextWithoutNotify(string.Empty);
            generatedPasswordInput.gameObject.SetActive(false);
        }

        UpdateCreateButtonState();
    }

    private void EnsureJoinPasswordPanel()
    {
        if (joinPasswordPanel != null || roomListContent == null)
        {
            return;
        }

        var canvasTransform = roomListContent.root;
        joinPasswordPanel = new GameObject("Join Password Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        joinPasswordPanel.transform.SetParent(canvasTransform, false);
        var panelRect = joinPasswordPanel.GetComponent<RectTransform>();
        SetRectTransform(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 260f));
        var panelImage = joinPasswordPanel.GetComponent<Image>();
        panelImage.color = new Color(0.22f, 0.23f, 0.29f, 0.98f);
        joinPasswordPanel.AddComponent<Outline>().effectColor = Color.black;
        joinPasswordPanel.GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);

        var title = CreateRuntimeLabel(joinPasswordPanel.transform, "Join Password", 26f, Color.white);
        SetRectTransform(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(340f, 42f));

        var hint = CreateRuntimeLabel(joinPasswordPanel.transform, "Enter room password", 18f, Color.white);
        SetRectTransform(hint.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(320f, 28f));

        joinPasswordInput = CreateRuntimeInputField(joinPasswordPanel.transform, "Join Password Input");
        SetRectTransform(joinPasswordInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(320f, 48f));
        NormalizeInputVisual(joinPasswordInput, 20f);

        joinPasswordConfirmButton = CreateRuntimeButton(joinPasswordPanel.transform, "Join Room", new Color(0.23f, 0.75f, 0.32f, 1f));
        SetRectTransform(joinPasswordConfirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-88f, 26f), new Vector2(150f, 46f));
        joinPasswordConfirmButton.onClick.AddListener(ConfirmJoinSelectedRoomPassword);

        joinPasswordCancelButton = CreateRuntimeButton(joinPasswordPanel.transform, "Cancel", new Color(0.87f, 0.55f, 0.12f, 1f));
        SetRectTransform(joinPasswordCancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(88f, 26f), new Vector2(150f, 46f));
        joinPasswordCancelButton.onClick.AddListener(CancelJoinSelectedRoomPassword);

        joinPasswordPanel.SetActive(false);
    }

    private void OpenJoinPasswordPanel()
    {
        EnsureJoinPasswordPanel();
        if (joinPasswordPanel != null)
        {
            joinPasswordPanel.SetActive(true);
        }

        if (joinPasswordInput != null)
        {
            joinPasswordInput.SetTextWithoutNotify(string.Empty);
            joinPasswordInput.ActivateInputField();
        }

        UpdateLobbyForegroundVisibility();
    }

    private void CloseJoinPasswordPanel()
    {
        if (joinPasswordPanel != null)
        {
            joinPasswordPanel.SetActive(false);
        }

        if (joinPasswordInput != null)
        {
            joinPasswordInput.SetTextWithoutNotify(string.Empty);
        }

        UpdateLobbyForegroundVisibility();
    }

    private static TextMeshProUGUI CreateRuntimeLabel(Transform parent, string textValue, float fontSize, Color color)
    {
        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = textValue;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_InputField CreateRuntimeInputField(Transform parent, string objectName)
    {
        var root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>();
        image.color = new Color(0.93f, 0.93f, 0.93f, 1f);

        var inputField = root.GetComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;

        var textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(root.transform, false);
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(14f, 8f);
        textAreaRect.offsetMax = new Vector2(-14f, -8f);

        var placeholder = CreateRuntimeLabel(textArea.transform, "Password", 20f, new Color(0.35f, 0.35f, 0.35f, 0.65f));
        placeholder.alignment = TextAlignmentOptions.Left;
        SetRectTransform(placeholder.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var text = CreateRuntimeLabel(textArea.transform, string.Empty, 20f, new Color(0.10f, 0.10f, 0.10f, 1f));
        text.alignment = TextAlignmentOptions.Left;
        SetRectTransform(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        inputField.textViewport = textAreaRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        return inputField;
    }

    private static Button CreateRuntimeButton(Transform parent, string labelText, Color backgroundColor)
    {
        var buttonObject = new GameObject(labelText, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;
        var outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.05f;
        colors.pressedColor = backgroundColor * 0.92f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var label = CreateRuntimeLabel(buttonObject.transform, labelText, 18f, Color.black);
        SetRectTransform(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private static RectTransform FindChildRect(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        var child = parent.Find(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private static void SetRectTransform(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private GameObject GetPlayerSlotRoot(int index)
    {
        if (index < 0 || index >= playerSlotBorders.Length || playerSlotBorders[index] == null)
        {
            return null;
        }

        return playerSlotBorders[index].gameObject;
    }

    private static void SetUiElementVisible(Component component, bool visible)
    {
        if (component != null)
        {
            component.gameObject.SetActive(visible);
        }
    }
}

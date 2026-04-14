using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LobbyTestSceneBuilder
{
    private const string LobbyScenePath = "Assets/Scenes/Kan/Lobby.unity";
    private const string LegacyLobbyScenePath = "Assets/Scenes/Kan/Lobby Test.unity";
    private const string DefaultTargetSceneName = "Anomaly Test System Belike";

    static LobbyTestSceneBuilder()
    {
        EditorApplication.delayCall += EnsureScenesExist;
    }

    public static void RebuildLobbyScene()
    {
        BuildScene(LobbyScenePath, true);
    }

    public static void RebuildLobbyTestScene()
    {
        BuildScene(LegacyLobbyScenePath, true);
    }

    public static void RebuildAllLobbyScenes()
    {
        BuildScene(LobbyScenePath, true);
        BuildScene(LegacyLobbyScenePath, true);
    }

    private static void EnsureScenesExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        BuildScene(LobbyScenePath, false);
        BuildScene(LegacyLobbyScenePath, false);
    }

    private static void BuildScene(string scenePath, bool overwriteExisting)
    {
        if (File.Exists(scenePath) && !overwriteExisting)
        {
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            var cameraObject = CreateSceneObject("Main Camera", null);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.76f, 0.84f, 0.95f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var eventSystemObject = CreateSceneObject("EventSystem", null);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();

            var canvasObject = CreateSceneObject("Canvas", null);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvasObject.AddComponent<GraphicRaycaster>();

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            CreatePanel(canvasObject.transform, "Background", new Color(0.76f, 0.84f, 0.95f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false, Color.clear, 0f);

            var targetReferenceObject = CreateSceneObject("Lobby Scene Target", null);
            var targetReference = targetReferenceObject.AddComponent<LobbySceneTargetReference>();
            var targetSerialized = new SerializedObject(targetReference);
            targetSerialized.FindProperty("targetSceneName").stringValue = DefaultTargetSceneName;
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerObject = CreateSceneObject("Lobby Controller", canvasObject.transform);
            var controller = controllerObject.AddComponent<LobbyTestPhotonController>();

            var screenHeaderText = CreateText(
                canvasObject.transform,
                "Screen Header Text",
                "Lobby",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -42f),
                new Vector2(360f, 40f),
                28f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var statusText = CreateText(
                canvasObject.transform,
                "Status Text",
                "Status: Lobby ready.",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -92f),
                new Vector2(900f, 34f),
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var photonStateText = CreateText(
                canvasObject.transform,
                "Photon State Text",
                "Photon State: JoinedLobby",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -130f),
                new Vector2(900f, 34f),
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var connectedText = CreateText(
                canvasObject.transform,
                "Connected Text",
                "Connected: Yes",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -168f),
                new Vector2(900f, 34f),
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var currentRoomText = CreateText(
                canvasObject.transform,
                "Current Room Text",
                "Current Room: -",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -206f),
                new Vector2(900f, 34f),
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var lobbyPanel = CreatePanel(
                canvasObject.transform,
                "Lobby Panel",
                new Color(0.44f, 0.50f, 0.60f, 0.94f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1830f, 960f),
                true,
                new Color(0.10f, 0.10f, 0.10f, 1f),
                4f);

            var roomListRoot = CreatePanel(
                lobbyPanel.transform,
                "Room List Root",
                new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                false,
                Color.clear,
                0f);
            var roomListRootRect = roomListRoot.GetComponent<RectTransform>();
            roomListRootRect.offsetMin = new Vector2(12f, 84f);
            roomListRootRect.offsetMax = new Vector2(-12f, -314f);

            var lobbyListTitleText = CreateText(
                lobbyPanel.transform,
                "Lobby List Title",
                "Lobby List",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -256f),
                new Vector2(320f, 32f),
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var emptyLobbyText = CreateText(
                lobbyPanel.transform,
                "Empty Lobby Text",
                "No room available.",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(86f, -294f),
                new Vector2(360f, 32f),
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                Color.white);

            var scrollFrame = CreatePanel(
                roomListRoot.transform,
                "Scroll Frame",
                new Color(0.18f, 0.18f, 0.18f, 1f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                true,
                Color.black,
                5f);

            var scrollFrameRect = scrollFrame.GetComponent<RectTransform>();
            scrollFrameRect.offsetMin = new Vector2(12f, 12f);
            scrollFrameRect.offsetMax = new Vector2(-12f, -12f);

            var scrollRect = scrollFrame.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var viewport = CreatePanel(
                scrollFrame.transform,
                "Viewport",
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                false,
                Color.clear,
                0f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-34f, -8f);

            var contentObject = CreateSceneObject("Content", viewport.transform);
            var contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(16, 16, 14, 14);
            contentLayout.spacing = 16f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            var scrollbar = CreateScrollbar(scrollFrame.transform);
            var scrollbarRect = scrollbar.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 1f);
            scrollbarRect.anchoredPosition = new Vector2(-6f, -6f);
            scrollbarRect.sizeDelta = new Vector2(20f, -12f);
            scrollRect.verticalScrollbar = scrollbar;

            var roomEntryTemplate = CreateRoomEntryButton(contentObject.transform);
            roomEntryTemplate.gameObject.SetActive(false);

            var openCreateRoomButton = CreateStyledButton(
                lobbyPanel.transform,
                "Create Room Button",
                "Create Room",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.18f, 0.18f, 0.18f, 0.58f),
                new Color(0.94f, 0.94f, 0.94f, 1f),
                new Color(0.77f, 0.47f, 0.08f, 1f),
                24f,
                TextAlignmentOptions.Center);
            var openCreateRect = openCreateRoomButton.GetComponent<RectTransform>();
            openCreateRect.offsetMin = new Vector2(12f, 12f);
            openCreateRect.offsetMax = new Vector2(-6f, 56f);

            var connectButton = CreateStyledButton(
                lobbyPanel.transform,
                "Connect Button",
                "Connect",
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.18f, 0.18f, 0.18f, 0.58f),
                new Color(0.94f, 0.94f, 0.94f, 1f),
                new Color(0.92f, 0.24f, 0.18f, 1f),
                24f,
                TextAlignmentOptions.Center);
            var connectRect = connectButton.GetComponent<RectTransform>();
            connectRect.offsetMin = new Vector2(6f, 12f);
            connectRect.offsetMax = new Vector2(-12f, 56f);

            var createRoomPanel = CreatePanel(
                canvasObject.transform,
                "Create Room Panel",
                new Color(0.28f, 0.28f, 0.28f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1280f, 760f),
                true,
                Color.black,
                5f);
            createRoomPanel.SetActive(false);

            CreateText(createRoomPanel.transform, "Create Panel Title", "Create Lobby", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(500f, 72f), 52f, FontStyles.Bold, TextAlignmentOptions.Center, Color.black);
            CreateText(createRoomPanel.transform, "Lobby Name Label", "Lobby Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -116f), new Vector2(320f, 52f), 34f, FontStyles.Normal, TextAlignmentOptions.Left, Color.black);
            var createLobbyNameInput = CreateInputField(createRoomPanel.transform, "Lobby Name Input", new Vector2(66f, -188f), new Vector2(980f, 72f), "");
            CreateText(createRoomPanel.transform, "Leader Name Label", "Leader Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -292f), new Vector2(320f, 52f), 34f, FontStyles.Normal, TextAlignmentOptions.Left, Color.black);
            var createLeaderNameInput = CreateInputField(createRoomPanel.transform, "Leader Name Input", new Vector2(66f, -364f), new Vector2(980f, 72f), "");
            CreateText(createRoomPanel.transform, "Max Player Label", "Max Player : 4", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -470f), new Vector2(320f, 52f), 34f, FontStyles.Normal, TextAlignmentOptions.Left, Color.black);
            CreateText(createRoomPanel.transform, "Password Label", "Password", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -560f), new Vector2(220f, 52f), 34f, FontStyles.Normal, TextAlignmentOptions.Left, Color.black);

            var passwordToggle = CreateToggle(createRoomPanel.transform, "Password Toggle", new Vector2(296f, -564f), new Vector2(58f, 58f));
            var generatedPasswordInput = CreateInputField(createRoomPanel.transform, "Generated Password Input", new Vector2(66f, -652f), new Vector2(500f, 66f), "");

            var confirmCreateButton = CreateStyledButton(
                createRoomPanel.transform,
                "Create Room Confirm Button",
                "Create Room",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-72f, -494f),
                new Vector2(440f, 94f),
                new Color(0.92f, 0.92f, 0.92f, 1f),
                Color.black,
                new Color(0.92f, 0.24f, 0.18f, 1f),
                30f,
                TextAlignmentOptions.Center);

            var cancelCreateButton = CreateStyledButton(
                createRoomPanel.transform,
                "Cancel Create Button",
                "Cancel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-72f, -626f),
                new Vector2(440f, 88f),
                new Color(0.92f, 0.92f, 0.92f, 1f),
                Color.black,
                new Color(0.95f, 0.48f, 0.08f, 1f),
                28f,
                TextAlignmentOptions.Center);

            var roomPanel = CreatePanel(
                canvasObject.transform,
                "Room Panel",
                new Color(0.44f, 0.50f, 0.60f, 0.94f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1830f, 960f),
                true,
                new Color(0.10f, 0.10f, 0.10f, 1f),
                4f);
            roomPanel.SetActive(false);

            var roomTitleText = CreateText(roomPanel.transform, "Room Title Text", "GG", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -256f), new Vector2(620f, 34f), 28f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
            var roomPasswordText = CreateText(roomPanel.transform, "Room Password Text", "Password : Open", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -294f), new Vector2(620f, 32f), 22f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
            var roomPlayerCountText = CreateText(roomPanel.transform, "Room Player Count Text", "Player : 1 / 4", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -332f), new Vector2(620f, 32f), 22f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);

            var playerListPanel = CreatePanel(
                roomPanel.transform,
                "Player List Panel",
                new Color(0.24f, 0.22f, 0.22f, 0.92f),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                true,
                new Color(0.10f, 0.10f, 0.10f, 1f),
                4f);
            var playerListRect = playerListPanel.GetComponent<RectTransform>();
            playerListRect.offsetMin = new Vector2(12f, 84f);
            playerListRect.offsetMax = new Vector2(-12f, -368f);

            var playerListLayout = playerListPanel.AddComponent<VerticalLayoutGroup>();
            playerListLayout.padding = new RectOffset(18, 18, 18, 18);
            playerListLayout.spacing = 18f;
            playerListLayout.childAlignment = TextAnchor.UpperCenter;
            playerListLayout.childControlWidth = true;
            playerListLayout.childControlHeight = true;
            playerListLayout.childForceExpandWidth = true;
            playerListLayout.childForceExpandHeight = false;

            var slotInputs = new TMP_InputField[4];
            var slotRoles = new TextMeshProUGUI[4];
            var slotBorders = new Image[4];

            for (var i = 0; i < 4; i++)
            {
                var slot = CreatePanel(
                    playerListPanel.transform,
                    $"Player Slot {i + 1}",
                    new Color(0.14f, 0.14f, 0.14f, 0.90f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(0f, 94f),
                    true,
                    Color.black,
                    3f);

                var slotLayout = slot.AddComponent<LayoutElement>();
                slotLayout.preferredHeight = 94f;
                slotLayout.minHeight = 86f;
                slotBorders[i] = slot.GetComponent<Image>();

                CreateText(slot.transform, $"Player Label {i + 1}", "Player Name :", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(170f, 24f), 16f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
                slotInputs[i] = CreateInputField(slot.transform, $"Player Name Input {i + 1}", new Vector2(10f, -38f), new Vector2(280f, 26f), "");
                slotRoles[i] = CreateText(slot.transform, $"Player Role Text {i + 1}", i == 0 ? "Leader" : "Member", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(180f, 22f), 16f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
            }

            var readyOrStartButton = CreateStyledButton(
                roomPanel.transform,
                "Ready Or Start Button",
                "Start",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.18f, 0.18f, 0.18f, 0.58f),
                new Color(0.94f, 0.94f, 0.94f, 1f),
                new Color(0.23f, 0.95f, 0.18f, 1f),
                24f,
                TextAlignmentOptions.Center);
            var readyRect = readyOrStartButton.GetComponent<RectTransform>();
            readyRect.offsetMin = new Vector2(12f, 12f);
            readyRect.offsetMax = new Vector2(-6f, 58f);

            var leaveRoomButton = CreateStyledButton(
                roomPanel.transform,
                "Leave Room Button",
                "Leave",
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.18f, 0.18f, 0.18f, 0.58f),
                new Color(0.94f, 0.94f, 0.94f, 1f),
                new Color(0.77f, 0.47f, 0.08f, 1f),
                24f,
                TextAlignmentOptions.Center);
            var leaveRect = leaveRoomButton.GetComponent<RectTransform>();
            leaveRect.offsetMin = new Vector2(6f, 12f);
            leaveRect.offsetMax = new Vector2(-12f, 58f);

            var controllerSerialized = new SerializedObject(controller);
            SetObjectReference(controllerSerialized, "sceneTargetReference", targetReference);
            SetObjectReference(controllerSerialized, "createLobbyNameInput", createLobbyNameInput);
            SetObjectReference(controllerSerialized, "createLeaderNameInput", createLeaderNameInput);
            SetObjectReference(controllerSerialized, "createPasswordToggle", passwordToggle);
            SetObjectReference(controllerSerialized, "generatedPasswordInput", generatedPasswordInput);
            SetObjectReference(controllerSerialized, "openCreateRoomButton", openCreateRoomButton);
            SetObjectReference(controllerSerialized, "createRoomButton", confirmCreateButton);
            SetObjectReference(controllerSerialized, "connectButton", connectButton);
            SetObjectReference(controllerSerialized, "cancelCreateButton", cancelCreateButton);
            SetObjectReference(controllerSerialized, "createRoomPanel", createRoomPanel);
            SetObjectReference(controllerSerialized, "roomListContent", contentObject.transform);
            SetObjectReference(controllerSerialized, "roomListEntryButtonPrefab", roomEntryTemplate);
            SetObjectReference(controllerSerialized, "screenHeaderText", screenHeaderText);
            SetObjectReference(controllerSerialized, "statusText", statusText);
            SetObjectReference(controllerSerialized, "photonStateText", photonStateText);
            SetObjectReference(controllerSerialized, "connectedText", connectedText);
            SetObjectReference(controllerSerialized, "currentRoomText", currentRoomText);
            SetObjectReference(controllerSerialized, "lobbyListTitleText", lobbyListTitleText);
            SetObjectReference(controllerSerialized, "emptyLobbyText", emptyLobbyText);
            SetObjectReference(controllerSerialized, "lobbyPanel", lobbyPanel);
            SetObjectReference(controllerSerialized, "roomTitleText", roomTitleText);
            SetObjectReference(controllerSerialized, "roomPasswordText", roomPasswordText);
            SetObjectReference(controllerSerialized, "roomPlayerCountText", roomPlayerCountText);
            SetArrayReferenceValues(controllerSerialized, "playerNameInputs", slotInputs);
            SetArrayReferenceValues(controllerSerialized, "playerRoleTexts", slotRoles);
            SetArrayReferenceValues(controllerSerialized, "playerSlotBorders", slotBorders);
            SetObjectReference(controllerSerialized, "readyOrStartButton", readyOrStartButton);
            SetObjectReference(controllerSerialized, "leaveRoomButton", leaveRoomButton);
            SetObjectReference(controllerSerialized, "roomPanel", roomPanel);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[LobbyTestSceneBuilder] Created scene at {scenePath}");
        }
        finally
        {
            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }

            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject CreateSceneObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, bool withOutline, Color outlineColor, float outlineSize)
    {
        var panel = CreateSceneObject(name, parent);
        var rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        var image = panel.AddComponent<Image>();
        image.color = color;

        if (withOutline)
        {
            AddOutline(panel, outlineColor, outlineSize);
        }

        return panel;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        var textObject = CreateSceneObject(name, parent);
        var rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateStyledButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color backgroundColor, Color textColor, Color borderColor, float fontSize, TextAlignmentOptions alignment)
    {
        var buttonObject = CreatePanel(parent, name, backgroundColor, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, true, borderColor, 4f);
        var button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor;
        colors.pressedColor = new Color(backgroundColor.r * 0.92f, backgroundColor.g * 0.92f, backgroundColor.b * 0.92f, backgroundColor.a);
        colors.selectedColor = backgroundColor;
        colors.disabledColor = new Color(backgroundColor.r * 0.85f, backgroundColor.g * 0.85f, backgroundColor.b * 0.85f, backgroundColor.a);
        button.colors = colors;

        var labelText = CreateText(buttonObject.transform, "Text", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, FontStyles.Normal, alignment, textColor);
        labelText.margin = new Vector4(16f, 8f, 16f, 8f);

        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = sizeDelta.y;

        return button;
    }

    private static Button CreateRoomEntryButton(Transform parent)
    {
        var button = CreateStyledButton(
            parent,
            "Room Entry Template",
            "Lobby Name : Test Server\nMax Player : 1 / 4\nLeader : Zaza3362\nPassword : Require",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 138f),
            new Color(0.83f, 0.83f, 0.83f, 1f),
            Color.black,
            Color.black,
            24f,
            TextAlignmentOptions.TopLeft);

        if (button.transform.Find("Text") is RectTransform labelRect)
        {
            labelRect.offsetMin = new Vector2(20f, 16f);
            labelRect.offsetMax = new Vector2(-20f, -16f);
        }

        var buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.alignment = TextAlignmentOptions.TopLeft;
        }

        return button;
    }

    private static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, string placeholder)
    {
        var root = CreatePanel(parent, name, new Color(0.92f, 0.92f, 0.92f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, sizeDelta, true, Color.black, 3f);
        var inputField = root.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 24;

        var textArea = CreateSceneObject("Text Area", root.transform);
        var textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.pivot = new Vector2(0.5f, 0.5f);
        textAreaRect.offsetMin = new Vector2(18f, 10f);
        textAreaRect.offsetMax = new Vector2(-18f, -10f);

        var placeholderText = CreateText(textArea.transform, "Placeholder", placeholder, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 28f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.45f, 0.45f, 0.45f, 0.65f));
        var inputText = CreateText(textArea.transform, "Text", "", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 28f, FontStyles.Normal, TextAlignmentOptions.Left, Color.black);

        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;

        var layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = sizeDelta.y;
        return inputField;
    }

    private static Toggle CreateToggle(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var root = CreateSceneObject(name, parent);
        var rectTransform = root.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        var background = root.AddComponent<Image>();
        background.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        AddOutline(root, Color.black, 3f);

        var toggle = root.AddComponent<Toggle>();
        toggle.targetGraphic = background;

        var checkmark = CreateSceneObject("Checkmark", root.transform);
        var checkmarkRect = checkmark.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(sizeDelta.x - 18f, sizeDelta.y - 18f);
        checkmarkRect.anchoredPosition = Vector2.zero;

        var checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.23f, 0.72f, 0.31f, 1f);
        toggle.graphic = checkmarkImage;
        return toggle;
    }

    private static Scrollbar CreateScrollbar(Transform parent)
    {
        var root = CreateSceneObject("Scrollbar Vertical", parent);
        var rectTransform = root.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(22f, -12f);

        var background = root.AddComponent<Image>();
        background.color = new Color(0.16f, 0.16f, 0.16f, 1f);

        var slidingArea = CreateSceneObject("Sliding Area", root.transform);
        var slidingRect = slidingArea.AddComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(4f, 4f);
        slidingRect.offsetMax = new Vector2(-4f, -4f);

        var handle = CreateSceneObject("Handle", slidingArea.transform);
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.86f, 0.86f, 0.86f, 1f);

        var scrollbar = root.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private static void AddOutline(GameObject gameObject, Color color, float size)
    {
        var outline = gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(size, -size);
        outline.useGraphicAlpha = false;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetArrayReferenceValues<T>(SerializedObject serializedObject, string propertyName, T[] values) where T : Object
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}

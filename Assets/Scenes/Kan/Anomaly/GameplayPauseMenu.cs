using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class GameplayPauseMenu : MonoBehaviourPunCallbacks
{
    private static GameplayPauseMenu instance;
    private static bool hasRegisteredSceneLoadedHook;

    [Header("Scene Flow")]
    [SerializeField] private string exitTargetSceneName = "Lobby";
#if UNITY_EDITOR
    [SerializeField] private SceneAsset exitTargetSceneAsset;
#endif

    [Header("Audio")]
    [SerializeField] private SoundManager soundManager;

    [Header("Hybrid Setup")]
    [SerializeField] private GameObject pauseMenuPrefab;

    [Header("UI References")]
    [SerializeField] private GameObject menuOverlay;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private GameObject[] transientWindowsToHide = Array.Empty<GameObject>();

    private bool isMenuOpen;
    private bool isExitPending;
    private bool suppressSliderCallbacks;
    private readonly List<GameObject> resolvedTransientWindows = new List<GameObject>();
    private readonly Dictionary<GameObject, bool> transientWindowStates = new Dictionary<GameObject, bool>();

    public static bool IsLocalPauseMenuOpen => instance != null && instance.isMenuOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!hasRegisteredSceneLoadedHook)
        {
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            hasRegisteredSceneLoadedHook = true;
        }

        if (FindFirstObjectByType<GameplayPauseMenu>() != null)
        {
            return;
        }

        if (FindFirstObjectByType<GameRoundManager>() == null)
        {
            return;
        }

        var rootObject = new GameObject(
            "GameplayPauseMenu",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(GameplayPauseMenu));

        var canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;

        var scaler = rootObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var rectTransform = rootObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void OnAnySceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (FindFirstObjectByType<GameRoundManager>() != null)
        {
            return;
        }

        UnlockCursor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (exitTargetSceneAsset != null)
        {
            exitTargetSceneName = exitTargetSceneAsset.name;
        }
    }
#endif

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureOverlayCanvas();
        EnsureEventSystem();
    }

    private void Start()
    {
        ResolveSoundManager();
        ResolveSceneReferences();
        BuildRuntimeUiIfNeeded();
        ResolveTransientWindows();
        WireUiEvents();
        NormalizeSettingsPanelTextColors();
        SetMenuVisible(false);
        SyncSliderValuesFromManager();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isMenuOpen)
            {
                CloseMenu();
                return;
            }

            OpenMenu();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public override void OnLeftRoom()
    {
        if (!isExitPending)
        {
            return;
        }

        LoadExitScene();
    }

    private void OpenMenu()
    {
        if (isExitPending)
        {
            return;
        }

        isMenuOpen = true;
        CacheAndHideTransientWindows();
        SetMenuVisible(true);
        ShowPauseButtons();
        SyncSliderValuesFromManager();
        UnlockCursor();
        ResetLocalPlayerMotion();
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
        SetMenuVisible(false);
        var restoredInteractiveWindow = RestoreTransientWindows();
        if (restoredInteractiveWindow)
        {
            UnlockCursor();
            return;
        }

        LockCursor();
    }

    private void OpenSettings()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }

        SyncSliderValuesFromManager();
    }

    private void ShowPauseButtons()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void ExitToLobby()
    {
        if (isExitPending)
        {
            return;
        }

        isExitPending = true;
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        LoadExitScene();
    }

    private void LoadExitScene()
    {
        var targetSceneName = string.IsNullOrWhiteSpace(exitTargetSceneName) ? "Lobby" : exitTargetSceneName;
        UnlockCursor();
        isMenuOpen = false;
        SetMenuVisible(false);
        SceneManager.LoadScene(targetSceneName);
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (suppressSliderCallbacks || soundManager == null)
        {
            return;
        }

        soundManager.SetMasterVolume(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (suppressSliderCallbacks || soundManager == null)
        {
            return;
        }

        soundManager.SetMusicVolume(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (suppressSliderCallbacks || soundManager == null)
        {
            return;
        }

        soundManager.SetSfxVolume(value);
    }

    private void ResolveSoundManager()
    {
        if (soundManager != null)
        {
            return;
        }

        soundManager = FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
        if (soundManager != null)
        {
            return;
        }

        var managerObject = new GameObject("SoundManager");
        soundManager = managerObject.AddComponent<SoundManager>();
    }

    private void EnsureOverlayCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private void BuildRuntimeUiIfNeeded()
    {
        if (HasAllRequiredReferences())
        {
            return;
        }

        if (pauseMenuPrefab != null)
        {
            Instantiate(pauseMenuPrefab, transform, false);
            ResolveSceneReferences();
            if (HasAllRequiredReferences())
            {
                return;
            }
        }

        var root = transform as RectTransform;
        if (root == null)
        {
            return;
        }

        menuOverlay = CreatePanel(root, "PauseOverlay", new Color(0f, 0f, 0f, 0.52f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pausePanel = CreatePanel(menuOverlay.transform, "PausePanel", Color.white, new Vector2(0.5f, 0.5f), new Vector2(760f, 820f));
        settingsPanel = CreatePanel(menuOverlay.transform, "SettingsPanel", Color.white, new Vector2(0.5f, 0.5f), new Vector2(1040f, 760f));

        CreateHeader(pausePanel.transform, "PauseTitle", "Pause", new Vector2(0f, 270f), 54f);
        resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0f, 120f), new Vector2(460f, 120f));
        settingsButton = CreateButton(pausePanel.transform, "SettingsButton", "Setting", new Vector2(0f, -40f), new Vector2(460f, 120f));
        exitButton = CreateButton(pausePanel.transform, "ExitButton", "Exit", new Vector2(0f, -200f), new Vector2(460f, 120f));

        CreateHeader(settingsPanel.transform, "SettingsTitle", "Settings", new Vector2(0f, 280f), 48f);
        masterVolumeSlider = CreateSliderRow(settingsPanel.transform, "Master Volume", new Vector2(0f, 130f));
        musicVolumeSlider = CreateSliderRow(settingsPanel.transform, "Music", new Vector2(0f, 30f));
        sfxVolumeSlider = CreateSliderRow(settingsPanel.transform, "SFX Volume", new Vector2(0f, -70f));
        settingsBackButton = CreateButton(settingsPanel.transform, "BackButton", "Back", new Vector2(0f, -220f), new Vector2(340f, 96f));
    }

    private bool HasAllRequiredReferences()
    {
        return menuOverlay != null &&
               pausePanel != null &&
               settingsPanel != null &&
               resumeButton != null &&
               settingsButton != null &&
               exitButton != null &&
               settingsBackButton != null &&
               masterVolumeSlider != null &&
               musicVolumeSlider != null &&
               sfxVolumeSlider != null;
    }

    private void ResolveSceneReferences()
    {
        var root = transform;
        if (root == null)
        {
            return;
        }

        if (menuOverlay == null)
        {
            menuOverlay = FindNamedChild(root, "PauseOverlay") ?? FindNamedChild(root, "MenuOverlay");
        }

        if (pausePanel == null)
        {
            pausePanel = FindNamedChild(root, "PausePanel");
        }

        if (settingsPanel == null)
        {
            settingsPanel = FindNamedChild(root, "SettingsPanel");
        }

        if (resumeButton == null)
        {
            resumeButton = FindNamedChildComponent<Button>(root, "ResumeButton");
        }

        if (settingsButton == null)
        {
            settingsButton = FindNamedChildComponent<Button>(root, "SettingsButton");
        }

        if (exitButton == null)
        {
            exitButton = FindNamedChildComponent<Button>(root, "ExitButton");
        }

        if (settingsBackButton == null)
        {
            settingsBackButton = FindNamedChildComponent<Button>(root, "SettingsBackButton") ??
                                 FindNamedChildComponent<Button>(root, "BackButton");
        }

        if (masterVolumeSlider == null)
        {
            masterVolumeSlider = FindNamedChildComponent<Slider>(root, "MasterVolumeSlider") ??
                                 FindNamedChildComponent<Slider>(root, "MasterVolume");
        }

        if (musicVolumeSlider == null)
        {
            musicVolumeSlider = FindNamedChildComponent<Slider>(root, "MusicVolumeSlider") ??
                                FindNamedChildComponent<Slider>(root, "Music");
        }

        if (sfxVolumeSlider == null)
        {
            sfxVolumeSlider = FindNamedChildComponent<Slider>(root, "SfxVolumeSlider") ??
                              FindNamedChildComponent<Slider>(root, "SFXVolumeSlider") ??
                              FindNamedChildComponent<Slider>(root, "SFXVolume");
        }
    }

    private void ResolveTransientWindows()
    {
        resolvedTransientWindows.Clear();
        transientWindowStates.Clear();

        for (var i = 0; i < transientWindowsToHide.Length; i++)
        {
            TryAddTransientWindow(transientWindowsToHide[i]);
        }

        TryAddTransientWindow(FindNamedSceneObject("CheckListWindow"));
        TryAddTransientWindow(FindNamedSceneObject("ChecklistWindow"));
        TryAddTransientWindow(FindNamedSceneObject("GameEndPanel"));
        TryAddTransientWindow(FindNamedSceneObject("GameEndPanelUI"));
    }

    private void WireUiEvents()
    {
        BindButton(resumeButton, CloseMenu);
        BindButton(settingsButton, OpenSettings);
        BindButton(exitButton, ExitToLobby);
        BindButton(settingsBackButton, ShowPauseButtons);
        BindSlider(masterVolumeSlider, OnMasterVolumeChanged);
        BindSlider(musicVolumeSlider, OnMusicVolumeChanged);
        BindSlider(sfxVolumeSlider, OnSfxVolumeChanged);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }

    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(callback);
    }

    private void SyncSliderValuesFromManager()
    {
        if (soundManager == null)
        {
            return;
        }

        suppressSliderCallbacks = true;
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(soundManager.MasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(soundManager.MusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(soundManager.SfxVolume);
        }

        suppressSliderCallbacks = false;
    }

    private void NormalizeSettingsPanelTextColors()
    {
        if (settingsPanel == null)
        {
            return;
        }

        var texts = settingsPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null)
            {
                continue;
            }

            var color = text.color;
            var isWhiteLike = color.a > 0.01f && color.r >= 0.8f && color.g >= 0.8f && color.b >= 0.8f;
            if (!isWhiteLike)
            {
                continue;
            }

            text.color = new Color(0.1f, 0.1f, 0.1f, color.a);
        }
    }

    private void CacheAndHideTransientWindows()
    {
        transientWindowStates.Clear();
        for (var i = 0; i < resolvedTransientWindows.Count; i++)
        {
            var target = resolvedTransientWindows[i];
            if (target == null || target == menuOverlay || target == pausePanel || target == settingsPanel)
            {
                continue;
            }

            transientWindowStates[target] = target.activeSelf;
            if (target.activeSelf)
            {
                target.SetActive(false);
            }
        }
    }

    private bool RestoreTransientWindows()
    {
        var restoredInteractiveWindow = false;
        foreach (var pair in transientWindowStates)
        {
            var target = pair.Key;
            if (target == null)
            {
                continue;
            }

            target.SetActive(pair.Value);
            restoredInteractiveWindow |= pair.Value;
        }

        transientWindowStates.Clear();
        return restoredInteractiveWindow;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void ResetLocalPlayerMotion()
    {
        var avatars = FindObjectsByType<PhotonScenePlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < avatars.Length; i++)
        {
            var avatar = avatars[i];
            if (avatar != null && avatar.IsLocalPlayer)
            {
                avatar.ResetMotionState();
                return;
            }
        }
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuOverlay != null)
        {
            menuOverlay.SetActive(visible);
        }
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchor, Vector2 size)
    {
        var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private static TextMeshProUGUI CreateHeader(Transform parent, string name, string text, Vector2 anchoredPosition, float fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(540f, 90f);
        rect.anchoredPosition = anchoredPosition;

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchoredPosition, Vector2 size)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.58f, 0.58f, 0.58f, 1f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.pressedColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 36f;
        label.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        label.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private static Slider CreateSliderRow(Transform parent, string labelText, Vector2 anchoredPosition)
    {
        var rowObject = new GameObject(labelText.Replace(" ", string.Empty) + "Row", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        var rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(780f, 80f);
        rowRect.anchoredPosition = anchoredPosition;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rowObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(260f, 72f);
        labelRect.anchoredPosition = new Vector2(-360f, 0f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 32f;
        label.color = new Color(0.14f, 0.14f, 0.14f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;

        var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(rowObject.transform, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(400f, 36f);
        sliderRect.anchoredPosition = new Vector2(110f, 0f);

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 18f);
        backgroundRect.anchoredPosition = Vector2.zero;
        var backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.78f, 0.78f, 0.78f, 1f);

        var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(0f, 0f);
        fillAreaRect.offsetMax = new Vector2(-20f, 0f);

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(0f, 0f);
        fillRect.offsetMax = new Vector2(0f, 0f);
        var fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.32f, 0.32f, 0.32f, 1f);

        var handleSlideAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideAreaObject.transform.SetParent(sliderObject.transform, false);
        var handleSlideAreaRect = handleSlideAreaObject.GetComponent<RectTransform>();
        handleSlideAreaRect.anchorMin = new Vector2(0f, 0f);
        handleSlideAreaRect.anchorMax = new Vector2(1f, 1f);
        handleSlideAreaRect.offsetMin = new Vector2(0f, 0f);
        handleSlideAreaRect.offsetMax = new Vector2(0f, 0f);

        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObject.transform.SetParent(handleSlideAreaObject.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(38f, 38f);
        var handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;

        return slider;
    }

    private static GameObject FindNamedChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        var child = FindChildRecursive(root, targetName);
        return child != null ? child.gameObject : null;
    }

    private static T FindNamedChildComponent<T>(Transform root, string targetName) where T : Component
    {
        var child = FindChildRecursive(root, targetName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var result = FindChildRecursive(parent.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void TryAddTransientWindow(GameObject target)
    {
        if (target == null || resolvedTransientWindows.Contains(target))
        {
            return;
        }

        resolvedTransientWindows.Add(target);
    }

    private static GameObject FindNamedSceneObject(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < transforms.Length; i++)
        {
            var current = transforms[i];
            if (current != null && current.name == targetName)
            {
                return current.gameObject;
            }
        }

        return null;
    }
}

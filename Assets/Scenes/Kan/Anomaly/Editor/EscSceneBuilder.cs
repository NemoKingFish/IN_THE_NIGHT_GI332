using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EscSceneBuilder
{
    private const string EscScenePath = "Assets/Scenes/Kan/ESC.unity";

    static EscSceneBuilder()
    {
        EditorApplication.delayCall += EnsureEscSceneExists;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    public static void RebuildEscScene()
    {
        if (TryGetOpenEscScene(out var openEscScene))
        {
            BuildScene(openEscScene);
            return;
        }

        BuildScene();
    }

    [MenuItem("Tools/Kan/Rebuild ESC Scene")]
    private static void RebuildEscSceneMenuItem()
    {
        RebuildEscScene();
    }

    private static void EnsureEscSceneExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(EscScenePath))
        {
            BuildScene();
            return;
        }

        if (TryGetOpenEscScene(out var alreadyOpenEscScene))
        {
            if (!HasCanvas(alreadyOpenEscScene))
            {
                BuildScene(alreadyOpenEscScene);
            }

            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        var escScene = EditorSceneManager.OpenScene(EscScenePath, OpenSceneMode.Additive);

        try
        {
            if (!HasCanvas(escScene))
            {
                EditorSceneManager.CloseScene(escScene, true);
                BuildScene();
                return;
            }
        }
        finally
        {
            if (escScene.IsValid() && escScene.isLoaded)
            {
                EditorSceneManager.CloseScene(escScene, true);
            }

            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != EscScenePath)
        {
            return;
        }

        if (HasCanvas(scene))
        {
            return;
        }

        EditorApplication.delayCall += () => BuildScene(scene);
    }

    private static bool TryGetOpenEscScene(out Scene scene)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var candidate = SceneManager.GetSceneAt(i);
            if (candidate.path == EscScenePath)
            {
                scene = candidate;
                return true;
            }
        }

        scene = default;
        return false;
    }

    private static bool HasCanvas(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<GameplayPauseMenu>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildScene()
    {
        BuildScene(default);
    }

    private static void BuildScene(Scene targetScene)
    {
        var activeScene = SceneManager.GetActiveScene();
        var useExistingScene = targetScene.IsValid() && targetScene.isLoaded;
        var scene = useExistingScene
            ? targetScene
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        if (useExistingScene)
        {
            ClearScene(scene);
        }

        SceneManager.SetActiveScene(scene);

        try
        {
            var cameraObject = CreateSceneObject("Main Camera", null);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.19f, 0.19f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = CreateSceneObject("Directional Light", null);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var eventSystemObject = CreateSceneObject("EventSystem", null);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();

            var canvasObject = CreateSceneObject("PauseMenuCanvas", null);
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

            var controller = canvasObject.AddComponent<GameplayPauseMenu>();

            var overlay = CreatePanel(
                canvasObject.transform,
                "PauseOverlay",
                new Color(0f, 0f, 0f, 0.55f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            var pausePanel = CreatePanel(
                overlay.transform,
                "PausePanel",
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(760f, 820f));

            var settingsPanel = CreatePanel(
                overlay.transform,
                "SettingsPanel",
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(1040f, 760f));

            CreateHeader(pausePanel.transform, "PauseTitle", "Pause", new Vector2(0f, 270f), 54f);
            var resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0f, 120f), new Vector2(460f, 120f));
            var settingsButton = CreateButton(pausePanel.transform, "SettingsButton", "Setting", new Vector2(0f, -40f), new Vector2(460f, 120f));
            var exitButton = CreateButton(pausePanel.transform, "ExitButton", "Exit", new Vector2(0f, -200f), new Vector2(460f, 120f));

            CreateHeader(settingsPanel.transform, "SettingsTitle", "Settings", new Vector2(0f, 280f), 48f);
            var masterSlider = CreateSliderRow(settingsPanel.transform, "Master Volume", "MasterVolumeSlider", new Vector2(0f, 130f));
            var musicSlider = CreateSliderRow(settingsPanel.transform, "Music", "MusicVolumeSlider", new Vector2(0f, 30f));
            var sfxSlider = CreateSliderRow(settingsPanel.transform, "SFX Volume", "SfxVolumeSlider", new Vector2(0f, -70f));
            var backButton = CreateButton(settingsPanel.transform, "SettingsBackButton", "Back", new Vector2(0f, -220f), new Vector2(340f, 96f));

            settingsPanel.SetActive(false);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("menuOverlay").objectReferenceValue = overlay;
            serializedController.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            serializedController.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            serializedController.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serializedController.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serializedController.FindProperty("exitButton").objectReferenceValue = exitButton;
            serializedController.FindProperty("settingsBackButton").objectReferenceValue = backButton;
            serializedController.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            serializedController.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            serializedController.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            if (!useExistingScene)
            {
                EditorSceneManager.SaveScene(scene, EscScenePath);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }
    }

    private static GameObject CreateSceneObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static void ClearScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            Object.DestroyImmediate(roots[i]);
        }
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchor, Vector2 size)
    {
        var panel = CreatePanel(parent, name, color, anchor, anchor, Vector2.zero, size);
        var rect = panel.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return panel;
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

        return buttonObject.GetComponent<Button>();
    }

    private static Slider CreateSliderRow(Transform parent, string labelText, string sliderName, Vector2 anchoredPosition)
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

        var sliderObject = new GameObject(sliderName, typeof(RectTransform), typeof(Slider));
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
        backgroundObject.GetComponent<Image>().color = new Color(0.78f, 0.78f, 0.78f, 1f);

        var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = new Vector2(-20f, 0f);

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillObject.GetComponent<Image>().color = new Color(0.32f, 0.32f, 0.32f, 1f);

        var handleSlideAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideAreaObject.transform.SetParent(sliderObject.transform, false);
        var handleSlideAreaRect = handleSlideAreaObject.GetComponent<RectTransform>();
        handleSlideAreaRect.anchorMin = Vector2.zero;
        handleSlideAreaRect.anchorMax = Vector2.one;
        handleSlideAreaRect.offsetMin = Vector2.zero;
        handleSlideAreaRect.offsetMax = Vector2.zero;

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
}

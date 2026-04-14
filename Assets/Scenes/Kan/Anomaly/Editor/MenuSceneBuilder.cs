using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Kan/Menu.unity";
    private const string DefaultTargetScenePath = "Assets/Scenes/Kan/Lobby Test.unity";
    private const string DefaultTargetSceneName = "Lobby Test";

    static MenuSceneBuilder()
    {
        EditorApplication.delayCall += EnsureMenuSceneExists;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    public static void RebuildMenuScene()
    {
        if (TryGetOpenMenuScene(out var openMenuScene))
        {
            BuildScene(openMenuScene);
            return;
        }

        BuildScene();
    }

    private static void EnsureMenuSceneExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(MenuScenePath))
        {
            BuildScene();
            return;
        }

        if (TryGetOpenMenuScene(out var alreadyOpenMenuScene))
        {
            if (!HasController(alreadyOpenMenuScene))
            {
                BuildScene(alreadyOpenMenuScene);
            }

            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        var menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        try
        {
            if (!HasController(menuScene))
            {
                EditorSceneManager.CloseScene(menuScene, true);
                BuildScene();
                return;
            }
        }
        finally
        {
            if (menuScene.IsValid() && menuScene.isLoaded)
            {
                EditorSceneManager.CloseScene(menuScene, true);
            }

            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != MenuScenePath)
        {
            return;
        }

        if (HasController(scene))
        {
            return;
        }

        EditorApplication.delayCall += () => BuildScene(scene);
    }

    private static bool HasController(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<MenuSceneController>(true) != null)
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
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.18f, 1f);
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

            CreatePanel(
                canvasObject.transform,
                "Background",
                new Color(0.09f, 0.11f, 0.18f, 1f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                false,
                Color.clear,
                0f);

            var glowPanel = CreatePanel(
                canvasObject.transform,
                "Glow Panel",
                new Color(0.18f, 0.24f, 0.38f, 0.42f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f),
                new Vector2(1240f, 760f),
                true,
                new Color(0.82f, 0.72f, 0.38f, 1f),
                3f);
            glowPanel.GetComponent<Image>().raycastTarget = false;

            var targetReferenceObject = CreateSceneObject("Menu Scene Target", null);
            var targetReference = targetReferenceObject.AddComponent<LobbySceneTargetReference>();
            var targetSerialized = new SerializedObject(targetReference);
            targetSerialized.FindProperty("targetSceneName").stringValue = DefaultTargetSceneName;

            var defaultSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultTargetScenePath);
            var sceneAssetProperty = targetSerialized.FindProperty("targetSceneAsset");
            if (sceneAssetProperty != null)
            {
                sceneAssetProperty.objectReferenceValue = defaultSceneAsset;
            }

            targetSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerObject = CreateSceneObject("Menu Controller", canvasObject.transform);
            var controller = controllerObject.AddComponent<MenuSceneController>();

            var titleText = CreateText(
                canvasObject.transform,
                "Title Text",
                "IN THE NIGHT",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -148f),
                new Vector2(940f, 120f),
                84f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white);

            var buttonPanel = CreatePanel(
                canvasObject.transform,
                "Button Panel",
                new Color(0.11f, 0.13f, 0.20f, 0.84f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 100f),
                new Vector2(720f, 360f),
                true,
                new Color(0.90f, 0.72f, 0.24f, 1f),
                4f);

            var playButton = CreateStyledButton(
                buttonPanel.transform,
                "Play Button",
                "Play",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(420f, 88f),
                new Color(0.22f, 0.75f, 0.35f, 1f),
                Color.white,
                new Color(0.88f, 0.94f, 0.89f, 1f),
                34f,
                TextAlignmentOptions.Center);

            var exitButton = CreateStyledButton(
                buttonPanel.transform,
                "Exit Button",
                "Exit",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -236f),
                new Vector2(420f, 88f),
                new Color(0.72f, 0.22f, 0.22f, 1f),
                Color.white,
                new Color(0.95f, 0.88f, 0.88f, 1f),
                34f,
                TextAlignmentOptions.Center);

            var controllerSerialized = new SerializedObject(controller);
            SetObjectReference(controllerSerialized, "sceneTargetReference", targetReference);
            SetObjectReference(controllerSerialized, "playButton", playButton);
            SetObjectReference(controllerSerialized, "exitButton", exitButton);
            SetObjectReference(controllerSerialized, "titleText", titleText);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (useExistingScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                EditorSceneManager.SaveScene(scene, MenuScenePath);
            }

            AssetDatabase.Refresh();
        }
        finally
        {
            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }

            if (!useExistingScene)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static bool TryGetOpenMenuScene(out Scene menuScene)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.path == MenuScenePath)
            {
                menuScene = scene;
                return true;
            }
        }

        menuScene = default;
        return false;
    }

    private static void ClearScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                Object.DestroyImmediate(roots[i]);
            }
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
        var highlightedColor = backgroundColor * 1.05f;
        highlightedColor.a = backgroundColor.a;
        var pressedColor = backgroundColor * 0.92f;
        pressedColor.a = backgroundColor.a;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = backgroundColor;
        colors.disabledColor = new Color(backgroundColor.r * 0.75f, backgroundColor.g * 0.75f, backgroundColor.b * 0.75f, backgroundColor.a);
        button.colors = colors;

        var labelText = CreateText(buttonObject.transform, "Text", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, FontStyles.Bold, alignment, textColor);
        labelText.margin = new Vector4(12f, 8f, 12f, 8f);
        return button;
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
}

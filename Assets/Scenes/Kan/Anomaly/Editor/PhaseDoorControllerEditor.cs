using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PhaseDoorController))]
public class PhaseDoorControllerEditor : Editor
{
    private SerializedProperty doorTransformProperty;
    private SerializedProperty closedLocalPositionProperty;
    private SerializedProperty closedLocalEulerAnglesProperty;
    private SerializedProperty openLocalPositionProperty;
    private SerializedProperty openLocalEulerAnglesProperty;
    private SerializedProperty disableCollidersWhenOpenProperty;

    private void OnEnable()
    {
        doorTransformProperty = serializedObject.FindProperty("doorTransform");
        closedLocalPositionProperty = serializedObject.FindProperty("closedLocalPosition");
        closedLocalEulerAnglesProperty = serializedObject.FindProperty("closedLocalEulerAngles");
        openLocalPositionProperty = serializedObject.FindProperty("openLocalPosition");
        openLocalEulerAnglesProperty = serializedObject.FindProperty("openLocalEulerAngles");
        disableCollidersWhenOpenProperty = serializedObject.FindProperty("disableCollidersWhenOpen");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(doorTransformProperty, new GUIContent("Door Transform"));
        EditorGUILayout.PropertyField(closedLocalPositionProperty, new GUIContent("Closed Local Position"));
        EditorGUILayout.PropertyField(closedLocalEulerAnglesProperty, new GUIContent("Closed Local Rotation"));
        EditorGUILayout.PropertyField(openLocalPositionProperty, new GUIContent("Open Local Position"));
        EditorGUILayout.PropertyField(openLocalEulerAnglesProperty, new GUIContent("Open Local Rotation"));
        EditorGUILayout.PropertyField(disableCollidersWhenOpenProperty, new GUIContent("Disable Colliders When Open"));

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (target is PhaseDoorController controller)
            {
                var nextPreviewState = EditorGUILayout.Toggle(
                    new GUIContent("Preview Open In Edit Mode"),
                    controller.IsPreviewOpenInEditMode());

                if (nextPreviewState != controller.IsPreviewOpenInEditMode())
                {
                    Undo.RecordObject(controller, "Toggle Door Preview");
                    controller.SetPreviewOpenInEditMode(nextPreviewState);
                    EditorUtility.SetDirty(controller);

                    if (controller.gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    }
                }
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Edit-mode door preview is disabled while the game is running.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Enable preview to see the opened door result directly in Edit Mode. Disable it to return to the closed state before testing.", MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[InitializeOnLoad]
public static class PhaseDoorControllerPlayModePreviewReset
{
    static PhaseDoorControllerPlayModePreviewReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var controllers = Object.FindObjectsByType<PhaseDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null || !controller.IsPreviewOpenInEditMode())
            {
                continue;
            }

            Undo.RecordObject(controller, "Disable Door Edit Preview");
            controller.SetPreviewOpenInEditMode(false);
            EditorUtility.SetDirty(controller);

            if (controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
    }
}

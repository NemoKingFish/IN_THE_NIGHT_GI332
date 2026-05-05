using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AnomalySpawnPoint))]
public class AnomalySpawnPointEditor : Editor
{
    private SerializedProperty preferSceneObjectAsNormalProperty;
    private SerializedProperty normalPrefabProperty;
    private SerializedProperty anomalyPrefabProperty;
    private SerializedProperty anomalyIdProperty;
    private SerializedProperty anomalyNameProperty;
    private SerializedProperty assignedAnomalyTypeProperty;
    private SerializedProperty anomalyPhaseProperty;
    private SerializedProperty anomalyChanceProperty;
    private SerializedProperty movedLocalPositionOffsetProperty;
    private SerializedProperty movedLocalEulerOffsetProperty;
    private SerializedProperty overrideChangedObjectScaleProperty;
    private SerializedProperty changedObjectScaleMultiplierProperty;
    private SerializedProperty overrideChangedObjectColorProperty;
    private SerializedProperty changedObjectColorProperty;

    private void OnEnable()
    {
        preferSceneObjectAsNormalProperty = serializedObject.FindProperty("preferSceneObjectAsNormal");
        normalPrefabProperty = serializedObject.FindProperty("normalPrefab");
        anomalyPrefabProperty = serializedObject.FindProperty("anomalyPrefab");
        anomalyIdProperty = serializedObject.FindProperty("anomalyID");
        anomalyNameProperty = serializedObject.FindProperty("anomalyName");
        assignedAnomalyTypeProperty = serializedObject.FindProperty("assignedAnomalyType");
        anomalyPhaseProperty = serializedObject.FindProperty("anomalyPhase");
        anomalyChanceProperty = serializedObject.FindProperty("anomalyChance");
        movedLocalPositionOffsetProperty = serializedObject.FindProperty("movedLocalPositionOffset");
        movedLocalEulerOffsetProperty = serializedObject.FindProperty("movedLocalEulerOffset");
        overrideChangedObjectScaleProperty = serializedObject.FindProperty("overrideChangedObjectScale");
        changedObjectScaleMultiplierProperty = serializedObject.FindProperty("changedObjectScaleMultiplier");
        overrideChangedObjectColorProperty = serializedObject.FindProperty("overrideChangedObjectColor");
        changedObjectColorProperty = serializedObject.FindProperty("changedObjectColor");
    }

    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying && target is AnomalySpawnPoint point)
        {
            if (point.SyncEditorGeneratedFieldsNow())
            {
                EditorUtility.SetDirty(point);

                if (point.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
                }
            }
        }

        serializedObject.Update();

        var anomalyType = (AnomalyType)assignedAnomalyTypeProperty.enumValueIndex;
        var usesSceneObjectAsNormal = preferSceneObjectAsNormalProperty.boolValue;

        EditorGUILayout.PropertyField(preferSceneObjectAsNormalProperty, new GUIContent("Use Scene Object As Normal"));
        EditorGUILayout.PropertyField(normalPrefabProperty, new GUIContent("Fallback Normal Prefab"));
        EditorGUILayout.HelpBox(
            "Enable scene object mode when this component is attached directly to the normal object in the scene. The fallback normal prefab is only used for old empty spawn-point setups.",
            MessageType.Info);

        if (usesSceneObjectAsNormal)
        {
            EditorGUILayout.HelpBox("When Use Scene Object As Normal is enabled, the object in the scene is treated as the normal state. Fallback Normal Prefab is only a backup for old empty spawn-point setups.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(anomalyType == AnomalyType.MissingObject || anomalyType == AnomalyType.MovedObject))
        {
            EditorGUILayout.PropertyField(anomalyPrefabProperty, new GUIContent("Anomaly Prefab"));
        }

        if (anomalyType == AnomalyType.MissingObject)
        {
            anomalyPrefabProperty.objectReferenceValue = null;
            EditorGUILayout.HelpBox("Missing Object does not use Anomaly Prefab. The normal object will simply be hidden when this anomaly is active.", MessageType.None);
        }
        else if (anomalyType == AnomalyType.MovedObject)
        {
            anomalyPrefabProperty.objectReferenceValue = null;
            EditorGUILayout.HelpBox("Moved Object does not use Anomaly Prefab. It moves the normal object by the offsets below. If this is an old empty spawn-point setup, Fallback Normal Prefab is used instead.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(anomalyIdProperty, new GUIContent("Anomaly ID"));
            EditorGUILayout.PropertyField(anomalyNameProperty, new GUIContent("Anomaly Name"));
        }

        EditorGUILayout.HelpBox("Anomaly ID runs automatically from 1 and avoids duplicates in Edit Mode. Anomaly Name follows the current object name automatically.", MessageType.None);
        EditorGUILayout.PropertyField(assignedAnomalyTypeProperty, new GUIContent("Assigned Anomaly Type"));
        EditorGUILayout.IntSlider(anomalyPhaseProperty, 1, 3, new GUIContent("Anomaly Phase"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.Slider(anomalyChanceProperty, 0f, 100f, new GUIContent("Anomaly Chance"));

        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(anomalyType != AnomalyType.MovedObject))
        {
            EditorGUILayout.PropertyField(movedLocalPositionOffsetProperty, new GUIContent("Moved Local Position Offset"));
            EditorGUILayout.PropertyField(movedLocalEulerOffsetProperty, new GUIContent("Moved Local Euler Offset"));
        }

        if (anomalyType != AnomalyType.MovedObject)
        {
            EditorGUILayout.HelpBox("Moved offsets are active only when Assigned Anomaly Type is set to MovedObject.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(anomalyType != AnomalyType.ChangedObject))
        {
            EditorGUILayout.PropertyField(overrideChangedObjectScaleProperty, new GUIContent("Override Changed Scale"));
            if (overrideChangedObjectScaleProperty.boolValue)
            {
                EditorGUILayout.PropertyField(changedObjectScaleMultiplierProperty, new GUIContent("Changed Scale Multiplier"));
            }

            EditorGUILayout.PropertyField(overrideChangedObjectColorProperty, new GUIContent("Override Changed Color"));
            if (overrideChangedObjectColorProperty.boolValue)
            {
                EditorGUILayout.PropertyField(changedObjectColorProperty, new GUIContent("Changed Color"));
            }
        }

        if (anomalyType == AnomalyType.ChangedObject)
        {
            EditorGUILayout.HelpBox("Changed Object can now reuse the normal object and apply optional scale/color overrides directly from the Inspector. Anomaly Prefab is optional for this type.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Changed Object overrides are active only when Assigned Anomaly Type is set to ChangedObject.", MessageType.None);
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (target is AnomalySpawnPoint previewPoint)
            {
                var nextPreviewState = EditorGUILayout.Toggle(
                    new GUIContent("Preview Anomaly In Edit Mode"),
                    previewPoint.IsPreviewAnomalyInEditMode());

                if (nextPreviewState != previewPoint.IsPreviewAnomalyInEditMode())
                {
                    Undo.RecordObject(previewPoint, "Toggle Anomaly Preview");
                    previewPoint.SetPreviewAnomalyInEditMode(nextPreviewState);
                    EditorUtility.SetDirty(previewPoint);

                    if (previewPoint.gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(previewPoint.gameObject.scene);
                    }

                    serializedObject.Update();
                }
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Edit-mode anomaly preview is disabled automatically while the game is playing.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Enable this only to preview how the anomaly will look in Edit Mode. It is reset back to normal automatically when entering Play Mode.", MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[InitializeOnLoad]
public static class AnomalySpawnPointPlayModePreviewReset
{
    private static bool hierarchySyncQueued;

    static AnomalySpawnPointPlayModePreviewReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += QueueHierarchySync;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var points = Object.FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (point == null || !point.IsPreviewAnomalyInEditMode())
            {
                continue;
            }

            Undo.RecordObject(point, "Disable Anomaly Edit Preview");
            point.SetPreviewAnomalyInEditMode(false);
            EditorUtility.SetDirty(point);

            if (point.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
            }
        }
    }

    private static void QueueHierarchySync()
    {
        if (Application.isPlaying || hierarchySyncQueued)
        {
            return;
        }

        hierarchySyncQueued = true;
        EditorApplication.delayCall += SyncHierarchyGeneratedFields;
    }

    private static void SyncHierarchyGeneratedFields()
    {
        hierarchySyncQueued = false;

        if (Application.isPlaying)
        {
            return;
        }

        var points = Object.FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(points, CompareByHierarchyOrder);

        var nextId = 1;
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (point == null)
            {
                continue;
            }

            var desiredName = point.gameObject != null ? point.gameObject.name : string.Empty;
            var changed = point.SyncEditorGeneratedFieldsNow();

            if (point.GetAnomalyID() != nextId || point.GetAnomalyName() != desiredName)
            {
                point.ApplyEditorGeneratedIdentity(nextId, desiredName);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(point);

                if (point.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
                }
            }

            nextId++;
        }
    }

    private static int CompareByHierarchyOrder(AnomalySpawnPoint left, AnomalySpawnPoint right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        var leftPath = GetHierarchyOrderingPath(left.transform);
        var rightPath = GetHierarchyOrderingPath(right.transform);
        return string.CompareOrdinal(leftPath, rightPath);
    }

    private static string GetHierarchyOrderingPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        var path = target.GetSiblingIndex().ToString("D4");
        var current = target.parent;
        while (current != null)
        {
            path = current.GetSiblingIndex().ToString("D4") + "/" + path;
            current = current.parent;
        }

        return target.gameObject.scene.name + "/" + path;
    }
}

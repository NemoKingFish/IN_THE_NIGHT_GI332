using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(AnomalySpawnPoint))]
public class AnomalySpawnPointEditor : Editor
{
    private SerializedProperty preferSceneObjectAsNormalProperty;
    private SerializedProperty normalPrefabProperty;
    private SerializedProperty anomalyPrefabProperty;
    private SerializedProperty anomalyIdProperty;
    private SerializedProperty anomalyNameProperty;
    private SerializedProperty assignedAnomalyTypesProperty;
    private SerializedProperty anomalyPhaseProperty;
    private SerializedProperty anomalyChanceProperty;
    private SerializedProperty movedLocalPositionOffsetProperty;
    private SerializedProperty movedLocalEulerOffsetProperty;
    private SerializedProperty overrideChangedObjectScaleProperty;
    private SerializedProperty changedObjectScaleMultiplierProperty;
    private SerializedProperty overrideChangedObjectColorProperty;
    private SerializedProperty changedObjectColorProperty;
    private SerializedProperty normalAudioSettingsProperty;
    private SerializedProperty anomalyAudioSettingsProperty;
    private ReorderableList assignedAnomalyTypesList;

    private void OnEnable()
    {
        if (target == null || targets == null || targets.Length == 0)
        {
            return;
        }

        preferSceneObjectAsNormalProperty = serializedObject.FindProperty("preferSceneObjectAsNormal");
        normalPrefabProperty = serializedObject.FindProperty("normalPrefab");
        anomalyPrefabProperty = serializedObject.FindProperty("anomalyPrefab");
        anomalyIdProperty = serializedObject.FindProperty("anomalyID");
        anomalyNameProperty = serializedObject.FindProperty("anomalyName");
        assignedAnomalyTypesProperty = serializedObject.FindProperty("assignedAnomalyTypes");
        anomalyPhaseProperty = serializedObject.FindProperty("anomalyPhase");
        anomalyChanceProperty = serializedObject.FindProperty("anomalyChance");
        movedLocalPositionOffsetProperty = serializedObject.FindProperty("movedLocalPositionOffset");
        movedLocalEulerOffsetProperty = serializedObject.FindProperty("movedLocalEulerOffset");
        overrideChangedObjectScaleProperty = serializedObject.FindProperty("overrideChangedObjectScale");
        changedObjectScaleMultiplierProperty = serializedObject.FindProperty("changedObjectScaleMultiplier");
        overrideChangedObjectColorProperty = serializedObject.FindProperty("overrideChangedObjectColor");
        changedObjectColorProperty = serializedObject.FindProperty("changedObjectColor");
        normalAudioSettingsProperty = serializedObject.FindProperty("normalAudioSettings");
        anomalyAudioSettingsProperty = serializedObject.FindProperty("anomalyAudioSettings");

        assignedAnomalyTypesList = new ReorderableList(serializedObject, assignedAnomalyTypesProperty, true, true, true, true);
        assignedAnomalyTypesList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Possible Anomaly Types");
        };
        assignedAnomalyTypesList.drawElementCallback = (rect, index, active, focused) =>
        {
            if (index < 0 || index >= assignedAnomalyTypesProperty.arraySize)
            {
                return;
            }

            var element = assignedAnomalyTypesProperty.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            element.enumValueIndex = (int)DrawAnomalyTypePopup(rect, new GUIContent($"Type {index + 1}"), (AnomalyType)element.enumValueIndex);
        };
        assignedAnomalyTypesList.onAddDropdownCallback = (rect, list) =>
        {
            var menu = new GenericMenu();
            var existingTypes = GetAssignedTypesFromProperty();
            var addedAny = false;

            foreach (AnomalyType anomalyType in System.Enum.GetValues(typeof(AnomalyType)))
            {
                if (anomalyType == AnomalyType.None || existingTypes.Contains(anomalyType))
                {
                    continue;
                }

                addedAny = true;
                menu.AddItem(new GUIContent(anomalyType.ToString()), false, () =>
                {
                    var nextIndex = assignedAnomalyTypesProperty.arraySize;
                    assignedAnomalyTypesProperty.InsertArrayElementAtIndex(nextIndex);
                    assignedAnomalyTypesProperty.GetArrayElementAtIndex(nextIndex).enumValueIndex = (int)anomalyType;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                });
            }

            if (!addedAny)
            {
                menu.AddDisabledItem(new GUIContent("All anomaly types already added"));
            }

            menu.DropDown(rect);
        };
        assignedAnomalyTypesList.onCanRemoveCallback = list => assignedAnomalyTypesProperty.arraySize > 1;
    }

    public override void OnInspectorGUI()
    {
        if (target == null || serializedObject == null)
        {
            EditorGUILayout.HelpBox("Selected anomaly object is no longer available.", MessageType.Info);
            return;
        }

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

        if (assignedAnomalyTypesProperty == null)
        {
            EditorGUILayout.HelpBox("Inspector data is not available for this anomaly object.", MessageType.Warning);
            return;
        }

        serializedObject.Update();

        var usesSceneObjectAsNormal = preferSceneObjectAsNormalProperty.boolValue;
        var usesMovedObject = PropertyContainsType(AnomalyType.MovedObject);
        var usesChangedObject = PropertyContainsType(AnomalyType.ChangedObject);
        var usesMissingObject = PropertyContainsType(AnomalyType.MissingObject);
        var usesStrangeSound = PropertyContainsType(AnomalyType.StrangeSound);
        var hasMultipleTypes = assignedAnomalyTypesProperty.arraySize > 1;

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
        EditorGUILayout.PropertyField(anomalyPrefabProperty, new GUIContent("Anomaly Prefab"));

        if (usesMissingObject)
        {
            EditorGUILayout.HelpBox("Missing Object does not use Anomaly Prefab. The normal object will simply be hidden when this anomaly is active.", MessageType.None);
        }

        if (usesMovedObject)
        {
            EditorGUILayout.HelpBox("Moved Object does not use Anomaly Prefab. It moves the normal object by the offsets below. If this is an old empty spawn-point setup, Fallback Normal Prefab is used instead.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(anomalyIdProperty, new GUIContent("Anomaly ID"));
            EditorGUILayout.PropertyField(anomalyNameProperty, new GUIContent("Anomaly Name"));
        }

        EditorGUILayout.HelpBox("Anomaly ID runs automatically from 1 and avoids duplicates in Edit Mode. Anomaly Name follows the current object name automatically.", MessageType.None);
        assignedAnomalyTypesList.DoLayoutList();
        if (assignedAnomalyTypesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Add at least 1 anomaly type. If this list is empty, the object will always stay normal.", MessageType.Warning);
        }
        else if (hasMultipleTypes)
        {
            EditorGUILayout.HelpBox("This object can now roll multiple anomaly types, but only 1 type is chosen when the anomaly spawns. The selected types share the same prefab, moved offsets, changed-object overrides, and audio settings on this spawn point.", MessageType.Info);
        }

        EditorGUILayout.IntSlider(anomalyPhaseProperty, 1, 3, new GUIContent("Anomaly Phase"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.Slider(anomalyChanceProperty, 0f, 50f, new GUIContent("Anomaly Chance"));

        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(!usesMovedObject))
        {
            EditorGUILayout.PropertyField(movedLocalPositionOffsetProperty, new GUIContent("Moved Local Position Offset"));
            EditorGUILayout.PropertyField(movedLocalEulerOffsetProperty, new GUIContent("Moved Local Euler Offset"));
        }

        if (!usesMovedObject)
        {
            EditorGUILayout.HelpBox("Moved offsets are active only when one of the possible anomaly types is set to MovedObject.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(!usesChangedObject))
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

        if (usesChangedObject)
        {
            EditorGUILayout.HelpBox("Changed Object can now reuse the normal object and apply optional scale/color overrides directly from the Inspector. Anomaly Prefab is optional for this type.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Changed Object overrides are active only when one of the possible anomaly types is set to ChangedObject.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(normalAudioSettingsProperty, new GUIContent("Normal Audio"), true);
        EditorGUILayout.PropertyField(anomalyAudioSettingsProperty, new GUIContent("Anomaly Audio"), true);

        if (usesStrangeSound)
        {
            EditorGUILayout.HelpBox("StrangeSound can keep the same object visible and swap from the normal clip to the anomaly clip. If Anomaly Prefab is empty, the normal visual object stays in place and only the sound changes.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Audio settings can be used to give the normal state and anomaly state different 3D sounds. Gizmos will show both sound ranges when this object is selected.", MessageType.None);
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (target is AnomalySpawnPoint previewPoint)
            {
                if (assignedAnomalyTypesProperty.arraySize > 1)
                {
                    var assignedTypes = previewPoint.GetAssignedAnomalyTypes();
                    var previewOptions = new string[assignedTypes.Count];
                    for (var index = 0; index < assignedTypes.Count; index++)
                    {
                        previewOptions[index] = assignedTypes[index].ToString();
                    }

                    var nextPreviewIndex = EditorGUILayout.Popup(
                        new GUIContent("Preview Type"),
                        previewPoint.GetPreviewAssignedAnomalyTypeIndex(),
                        previewOptions);

                    if (nextPreviewIndex != previewPoint.GetPreviewAssignedAnomalyTypeIndex())
                    {
                        Undo.RecordObject(previewPoint, "Change Preview Anomaly Type");
                        previewPoint.SetPreviewAssignedAnomalyTypeIndex(nextPreviewIndex);
                        EditorUtility.SetDirty(previewPoint);

                        if (previewPoint.gameObject.scene.IsValid())
                        {
                            EditorSceneManager.MarkSceneDirty(previewPoint.gameObject.scene);
                        }
                    }
                }

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

    private bool PropertyContainsType(AnomalyType anomalyType)
    {
        for (var index = 0; index < assignedAnomalyTypesProperty.arraySize; index++)
        {
            var element = assignedAnomalyTypesProperty.GetArrayElementAtIndex(index);
            if ((AnomalyType)element.enumValueIndex == anomalyType)
            {
                return true;
            }
        }

        return false;
    }

    private HashSet<AnomalyType> GetAssignedTypesFromProperty()
    {
        var assignedTypes = new HashSet<AnomalyType>();
        for (var index = 0; index < assignedAnomalyTypesProperty.arraySize; index++)
        {
            var element = assignedAnomalyTypesProperty.GetArrayElementAtIndex(index);
            var anomalyType = (AnomalyType)element.enumValueIndex;
            if (anomalyType != AnomalyType.None)
            {
                assignedTypes.Add(anomalyType);
            }
        }

        return assignedTypes;
    }

    private static AnomalyType DrawAnomalyTypePopup(Rect rect, GUIContent label, AnomalyType currentType)
    {
        var options = GetSelectableAnomalyTypeValues();
        var optionLabels = GetSelectableAnomalyTypeNames();
        var selectedIndex = 0;

        for (var index = 0; index < options.Length; index++)
        {
            if (options[index] == currentType)
            {
                selectedIndex = index;
                break;
            }
        }

        var nextIndex = EditorGUI.Popup(rect, label.text, selectedIndex, optionLabels);
        return options[Mathf.Clamp(nextIndex, 0, options.Length - 1)];
    }

    private static AnomalyType[] GetSelectableAnomalyTypeValues()
    {
        return new[]
        {
            AnomalyType.MissingObject,
            AnomalyType.MovedObject,
            AnomalyType.ExtraObject,
            AnomalyType.ChangedObject,
            AnomalyType.StrangeLight,
            AnomalyType.StrangeSound,
            AnomalyType.PictureChanged,
            AnomalyType.MultiplyingObject,
            AnomalyType.Creature,
            AnomalyType.Other
        };
    }

    private static string[] GetSelectableAnomalyTypeNames()
    {
        var values = GetSelectableAnomalyTypeValues();
        var names = new string[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            names[index] = values[index].ToString();
        }

        return names;
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

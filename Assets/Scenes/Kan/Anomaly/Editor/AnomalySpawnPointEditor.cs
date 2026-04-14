using UnityEditor;
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
    private SerializedProperty anomalyChanceProperty;
    private SerializedProperty movedLocalPositionOffsetProperty;
    private SerializedProperty movedLocalEulerOffsetProperty;

    private void OnEnable()
    {
        preferSceneObjectAsNormalProperty = serializedObject.FindProperty("preferSceneObjectAsNormal");
        normalPrefabProperty = serializedObject.FindProperty("normalPrefab");
        anomalyPrefabProperty = serializedObject.FindProperty("anomalyPrefab");
        anomalyIdProperty = serializedObject.FindProperty("anomalyID");
        anomalyNameProperty = serializedObject.FindProperty("anomalyName");
        assignedAnomalyTypeProperty = serializedObject.FindProperty("assignedAnomalyType");
        anomalyChanceProperty = serializedObject.FindProperty("anomalyChance");
        movedLocalPositionOffsetProperty = serializedObject.FindProperty("movedLocalPositionOffset");
        movedLocalEulerOffsetProperty = serializedObject.FindProperty("movedLocalEulerOffset");
    }

    public override void OnInspectorGUI()
    {
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
        EditorGUILayout.PropertyField(anomalyIdProperty, new GUIContent("Anomaly ID"));
        EditorGUILayout.PropertyField(anomalyNameProperty, new GUIContent("Anomaly Name"));
        EditorGUILayout.PropertyField(assignedAnomalyTypeProperty, new GUIContent("Assigned Anomaly Type"));

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

        serializedObject.ApplyModifiedProperties();
    }
}

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AnomalyFinder))]
public class AnomalyFinderEditor : Editor
{
    private const double HighlightDuration = 1.5d;

    private SerializedProperty searchIdInputProperty;
    private SerializedProperty searchNameInputProperty;
    private SerializedProperty foundTypeProperty;
    private SerializedProperty resultMessageProperty;
    private SerializedProperty fieldsLockedProperty;

    private static AnomalySpawnPoint highlightedPoint;
    private static double highlightEndTime;

    private void OnEnable()
    {
        searchIdInputProperty = serializedObject.FindProperty("searchIdInput");
        searchNameInputProperty = serializedObject.FindProperty("searchNameInput");
        foundTypeProperty = serializedObject.FindProperty("foundType");
        resultMessageProperty = serializedObject.FindProperty("resultMessage");
        fieldsLockedProperty = serializedObject.FindProperty("fieldsLocked");

        SceneView.duringSceneGui -= DuringSceneGui;
        SceneView.duringSceneGui += DuringSceneGui;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        EditorApplication.update -= OnEditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var finder = (AnomalyFinder)target;

        EditorGUILayout.LabelField("Anomaly Search", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(fieldsLockedProperty.boolValue))
        {
            EditorGUILayout.PropertyField(searchIdInputProperty, new GUIContent("ID"));
            EditorGUILayout.PropertyField(searchNameInputProperty, new GUIContent("Name"));
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(foundTypeProperty, new GUIContent("Type"));
            EditorGUILayout.PropertyField(resultMessageProperty, new GUIContent("Status"));
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(Application.isPlaying || !finder.CanSearch()))
        {
            if (GUILayout.Button("Search", GUILayout.Height(32f)))
            {
                ExecuteSearch(finder);
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying || !finder.CanGo()))
        {
            if (GUILayout.Button("Go", GUILayout.Height(32f)))
            {
                GoToFoundPoint(finder);
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Clear", GUILayout.Height(32f)))
            {
                finder.ClearSearch();
                ClearHighlight();
                MarkFinderDirty(finder);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void ExecuteSearch(AnomalyFinder finder)
    {
        if (finder == null)
        {
            return;
        }

        var idText = finder.SearchIdInput?.Trim() ?? string.Empty;
        var nameText = finder.SearchNameInput?.Trim() ?? string.Empty;

        AnomalySpawnPoint foundPoint = null;
        if (!string.IsNullOrWhiteSpace(idText) && int.TryParse(idText, out var targetId))
        {
            foundPoint = FindById(targetId);
        }
        else if (!string.IsNullOrWhiteSpace(nameText))
        {
            foundPoint = FindByName(nameText);
        }

        finder.SetSearchLocked(true);
        finder.SetSearchResult(foundPoint);
        MarkFinderDirty(finder);
    }

    private static void GoToFoundPoint(AnomalyFinder finder)
    {
        if (finder == null || !finder.CanGo())
        {
            return;
        }

        var foundPoint = finder.FoundPoint;
        if (foundPoint == null)
        {
            return;
        }

        Selection.activeGameObject = foundPoint.gameObject;
        EditorGUIUtility.PingObject(foundPoint.gameObject);

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Frame(new Bounds(foundPoint.transform.position, Vector3.one * 1.75f), false);
            SceneView.lastActiveSceneView.Repaint();
        }

        highlightedPoint = foundPoint;
        highlightEndTime = EditorApplication.timeSinceStartup + HighlightDuration;
        SceneView.RepaintAll();
    }

    private static AnomalySpawnPoint FindById(int targetId)
    {
        var points = FindAllPoints();
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].GetAnomalyID() == targetId)
            {
                return points[i];
            }
        }

        return null;
    }

    private static AnomalySpawnPoint FindByName(string targetName)
    {
        var points = FindAllPoints();
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
            {
                continue;
            }

            if (string.Equals(points[i].GetAnomalyName(), targetName, StringComparison.OrdinalIgnoreCase))
            {
                return points[i];
            }
        }

        return null;
    }

    private static AnomalySpawnPoint[] FindAllPoints()
    {
        return UnityEngine.Object.FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static void MarkFinderDirty(AnomalyFinder finder)
    {
        EditorUtility.SetDirty(finder);
        if (finder.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(finder.gameObject.scene);
        }
    }

    private static void OnEditorUpdate()
    {
        if (highlightedPoint == null)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup <= highlightEndTime)
        {
            return;
        }

        ClearHighlight();
    }

    private static void ClearHighlight()
    {
        highlightedPoint = null;
        highlightEndTime = 0d;
        SceneView.RepaintAll();
    }

    private static void DuringSceneGui(SceneView sceneView)
    {
        if (highlightedPoint == null)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup > highlightEndTime)
        {
            ClearHighlight();
            return;
        }

        var targetTransform = highlightedPoint.transform;
        if (targetTransform == null)
        {
            ClearHighlight();
            return;
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        var pulse = 1f + Mathf.PingPong((float)EditorApplication.timeSinceStartup * 2f, 0.25f);
        var size = HandleUtility.GetHandleSize(targetTransform.position) * pulse;

        Handles.color = new Color(1f, 0.95f, 0.15f, 0.95f);
        Handles.DrawWireDisc(targetTransform.position, Vector3.up, size);
        Handles.DrawWireDisc(targetTransform.position, Vector3.right, size * 0.8f);
        Handles.DrawWireDisc(targetTransform.position, Vector3.forward, size * 0.8f);
        Handles.Label(targetTransform.position + Vector3.up * size, "ANOMALY");
    }
}

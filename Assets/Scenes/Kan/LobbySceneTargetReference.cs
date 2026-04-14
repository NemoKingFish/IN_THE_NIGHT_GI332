using UnityEngine;

public class LobbySceneTargetReference : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "";

#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset targetSceneAsset;

    private void OnValidate()
    {
        if (targetSceneAsset != null)
        {
            targetSceneName = targetSceneAsset.name;
        }
    }

    public string GetEditorTargetScenePath()
    {
        return targetSceneAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(targetSceneAsset) : string.Empty;
    }
#endif

    public string GetTargetSceneName()
    {
        return targetSceneName;
    }
}

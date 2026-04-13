using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LobbySceneTargetReference : MonoBehaviour
{
    [SerializeField] private string sceneName = string.Empty;

#if UNITY_EDITOR
    [SerializeField] private SceneAsset sceneAsset;

    private void OnValidate()
    {
        sceneName = sceneAsset != null ? sceneAsset.name : string.Empty;
    }
#endif

    public string SceneName => sceneName;
}

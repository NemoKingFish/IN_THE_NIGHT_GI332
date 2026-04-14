using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSceneController : MonoBehaviour
{
    [SerializeField] private LobbySceneTargetReference sceneTargetReference;
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TextMeshProUGUI titleText;

    private void Awake()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonPressed);
            playButton.onClick.AddListener(OnPlayButtonPressed);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitButtonPressed);
            exitButton.onClick.AddListener(OnExitButtonPressed);
        }

        if (titleText != null)
        {
            titleText.text = "IN THE NIGHT";
        }
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonPressed);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitButtonPressed);
        }
    }

    private void OnPlayButtonPressed()
    {
        var targetSceneName = sceneTargetReference != null ? sceneTargetReference.GetTargetSceneName() : string.Empty;
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[MenuSceneController] No target scene assigned.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
            return;
        }

#if UNITY_EDITOR
        var targetScenePath = sceneTargetReference != null ? sceneTargetReference.GetEditorTargetScenePath() : string.Empty;
        if (!string.IsNullOrWhiteSpace(targetScenePath))
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                targetScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif

        Debug.LogWarning($"[MenuSceneController] Scene '{targetSceneName}' is not in Build Profiles and no editor scene asset fallback was found.");

    }

    private static void OnExitButtonPressed()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            UnityEditor.EditorApplication.isPlaying = false;
            return;
        }
#endif

        Application.Quit();
    }
}

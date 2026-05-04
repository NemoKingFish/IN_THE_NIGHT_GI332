using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndGameSceneController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private TextMeshProUGUI titleText;

    private void Awake()
    {
        ResolveReferences();
        EnsureButtonsExist();

        if (titleText != null)
        {
            titleText.text = "You Win";
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(BackToMenu);
        }

        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(BackToLobby);
        }
    }

    private void ResolveReferences()
    {
        if (backToMenuButton == null)
        {
            backToMenuButton = FindButton("BackToMenu") ?? FindButton("Main Menu");
        }

        if (backToLobbyButton == null)
        {
            backToLobbyButton = FindButton("BackToLobby") ?? FindButton("Lobby");
        }

        if (titleText == null)
        {
            titleText = FindText("END GAME") ?? FindFirstObjectByType<TextMeshProUGUI>();
        }
    }

    public void BackToMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(false);
        }

        LoadScene(menuSceneName);
    }

    public void BackToLobby()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(false);
        }

        LoadScene(lobbySceneName);
    }

    private static Button FindButton(string containsText)
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name.Contains(containsText))
            {
                return buttons[i];
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindText(string containsText)
    {
        var texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].text.Contains(containsText))
            {
                return texts[i];
            }
        }

        return null;
    }

    private void EnsureButtonsExist()
    {
        var root = transform as RectTransform;
        if (root == null)
        {
            return;
        }

        if (backToMenuButton == null)
        {
            backToMenuButton = CreateButton(root, "BackToMenu", "Back To Menu", new Vector2(0f, -150f));
        }

        if (backToLobbyButton == null)
        {
            backToLobbyButton = CreateButton(root, "BackToLobby", "Back To Lobby", new Vector2(0f, -240f));
        }
    }

    private static Button CreateButton(RectTransform parent, string objectName, string labelText, Vector2 anchoredPosition)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(420f, 64f);

        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.16f, 0.16f, 0.16f, 0.94f);

        var button = buttonObject.GetComponent<Button>();

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
        label.fontSize = 38f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return button;
    }

    private static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}

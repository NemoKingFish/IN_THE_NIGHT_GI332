using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameEndPanelUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private string lobbySceneName = "Lobby";
    [Header("Prefab UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button backToLobbyButton;

    private string pendingSceneName = string.Empty;

    private void Start()
    {
        ResolveReferences();
        WireButtonListeners();
        EnsurePanelExists();
        RefreshPanelVisibility();
    }

    private void Update()
    {
        if (gameRoundManager == null)
        {
            ResolveReferences();
        }

        RefreshPanelVisibility();
    }

    public override void OnLeftRoom()
    {
        if (!string.IsNullOrWhiteSpace(pendingSceneName))
        {
            LoadScene(pendingSceneName);
            pendingSceneName = string.Empty;
        }
    }

    private void ResolveReferences()
    {
        if (gameRoundManager == null)
        {
            gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        }

        if (panelRoot == null)
        {
            var panelTransform = transform.Find("GameEndPanel");
            panelRoot = panelTransform != null ? panelTransform.gameObject : null;
        }

        if (panelRoot != null)
        {
            if (titleText == null)
            {
                titleText = panelRoot.transform.Find("GameEndTitle")?.GetComponent<TextMeshProUGUI>();
            }

            if (subtitleText == null)
            {
                subtitleText = panelRoot.transform.Find("GameEndSubtitle")?.GetComponent<TextMeshProUGUI>();
            }

            if (backToMenuButton == null)
            {
                backToMenuButton = panelRoot.transform.Find("BackToMenuButton")?.GetComponent<Button>();
            }

            if (backToLobbyButton == null)
            {
                backToLobbyButton = panelRoot.transform.Find("BackToLobbyButton")?.GetComponent<Button>();
            }
        }
    }

    private void WireButtonListeners()
    {
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveListener(OnBackToMenuPressed);
            backToMenuButton.onClick.AddListener(OnBackToMenuPressed);
        }

        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveListener(OnBackToLobbyPressed);
            backToLobbyButton.onClick.AddListener(OnBackToLobbyPressed);
        }
    }

    private void EnsurePanelExists()
    {
        if (panelRoot != null)
        {
            WireButtonListeners();
            return;
        }

        if (transform is not RectTransform canvasRect)
        {
            return;
        }

        panelRoot = new GameObject("GameEndPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRoot.transform.SetParent(canvasRect, false);

        var panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.86f);

        titleText = CreateLabel(panelRoot.transform, "GameEndTitle", "YOU WIN", 96f, new Vector2(0f, 180f), new Vector2(900f, 120f));
        subtitleText = CreateLabel(panelRoot.transform, "GameEndSubtitle", "Return to the menu or lobby", 34f, new Vector2(0f, 90f), new Vector2(900f, 64f));
        backToMenuButton = CreateButton(panelRoot.transform, "BackToMenuButton", "Back To Menu", new Vector2(0f, -10f));
        backToLobbyButton = CreateButton(panelRoot.transform, "BackToLobbyButton", "Back To Lobby", new Vector2(0f, -100f));
        WireButtonListeners();

        panelRoot.SetActive(false);
    }

    private void RefreshPanelVisibility()
    {
        if (panelRoot == null || gameRoundManager == null)
        {
            return;
        }

        var shouldShow = gameRoundManager.gamePhase.Value == (int)GameRoundManager.GamePhase.Victory;
        if (panelRoot.activeSelf != shouldShow)
        {
            panelRoot.SetActive(shouldShow);
        }

        if (shouldShow)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnBackToMenuPressed()
    {
        ReturnToScene(menuSceneName);
    }

    private void OnBackToLobbyPressed()
    {
        ReturnToScene(lobbySceneName);
    }

    private void ReturnToScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            pendingSceneName = sceneName;
            PhotonNetwork.LeaveRoom(false);
            return;
        }

        LoadScene(sceneName);
    }

    private static void LoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string objectName, string textValue, float fontSize, Vector2 anchoredPosition, Vector2 size)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = labelObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = textValue;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string objectName, string labelText, Vector2 anchoredPosition)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(360f, 60f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 0.96f);

        var button = buttonObject.GetComponent<Button>();

        CreateLabel(buttonObject.transform, "Label", labelText, 28f, Vector2.zero, Vector2.zero);
        return button;
    }
}

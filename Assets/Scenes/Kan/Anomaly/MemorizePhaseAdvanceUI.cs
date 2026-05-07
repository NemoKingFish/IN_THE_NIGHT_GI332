using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemorizePhaseAdvanceUI : MonoBehaviour
{
    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private Button advanceButton;
    [SerializeField] private string startRememberLabel = "Start Remember";
    [SerializeField] private string startInvestigationLabel = "Start Investigation";
    [SerializeField] private KeyCode advanceKey = KeyCode.Return;

    private TextMeshProUGUI buttonText;

    private void Start()
    {
        ResolveReferences();
        EnsureButtonExists();
        WireButton();
        RefreshButtonState();
    }

    private void Update()
    {
        if (gameRoundManager == null)
        {
            ResolveReferences();
        }

        RefreshButtonState();

        if (ShouldAllowAdvanceHotkey() && Input.GetKeyDown(advanceKey))
        {
            OnAdvancePressed();
        }
    }

    private void ResolveReferences()
    {
        if (gameRoundManager == null)
        {
            gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        }
    }

    private void EnsureButtonExists()
    {
        if (advanceButton != null)
        {
            buttonText = advanceButton.GetComponentInChildren<TextMeshProUGUI>(true);
            return;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        var buttonObject = new GameObject("AdvanceMemorizeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 32f);
        buttonRect.sizeDelta = new Vector2(320f, 54f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.52f, 0.16f, 0.94f);

        advanceButton = buttonObject.GetComponent<Button>();

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        buttonText = labelObject.GetComponent<TextMeshProUGUI>();
        buttonText.font = TMP_Settings.defaultFontAsset;
        buttonText.fontSize = 24f;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.raycastTarget = false;
    }

    private void WireButton()
    {
        if (advanceButton == null)
        {
            return;
        }

        advanceButton.onClick.RemoveListener(OnAdvancePressed);
        advanceButton.onClick.AddListener(OnAdvancePressed);

        if (buttonText == null)
        {
            buttonText = advanceButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (buttonText != null)
        {
            buttonText.text = GetCurrentButtonLabel();
        }
    }

    private void RefreshButtonState()
    {
        if (advanceButton == null || gameRoundManager == null)
        {
            return;
        }

        var shouldShow = gameRoundManager.IsMemorizePhase() && !gameRoundManager.HasMemorizeTimer();
        if (advanceButton.gameObject.activeSelf != shouldShow)
        {
            advanceButton.gameObject.SetActive(shouldShow);
        }

        if (buttonText != null)
        {
            buttonText.text = GetCurrentButtonLabel();
        }
    }

    private bool ShouldAllowAdvanceHotkey()
    {
        return gameRoundManager != null &&
               gameRoundManager.IsMemorizePhase() &&
               !gameRoundManager.HasMemorizeTimer() &&
               !GameplayPauseMenu.IsLocalPauseMenuOpen;
    }

    private void OnAdvancePressed()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        if (!gameRoundManager.IsRememberStarted())
        {
            gameRoundManager.StartRememberPhase();
            return;
        }

        gameRoundManager.AdvanceMemorizePhase();
    }

    private string GetCurrentButtonLabel()
    {
        if (gameRoundManager == null || !gameRoundManager.IsRememberStarted())
        {
            return startRememberLabel;
        }

        return startInvestigationLabel;
    }
}

using TMPro;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class GameStatusUI : MonoBehaviour
{
    private const string PasswordCodeKey = "PasswordCode";

    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private TextMeshProUGUI roomPasswordText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI phaseText;
    [Header("Investigation Warning")]
    [SerializeField] private TextMeshProUGUI investigationWarningText;
    [SerializeField] private float investigationWarningThresholdSeconds = 15f;
    [SerializeField] private string investigationWarningMessage = "ถ้าไม่ submit ระบบจะใช้ checklist ปัจจุบันทันที";
    [SerializeField] private AudioClip investigationWarningClip;
    [Range(0f, 1f)] [SerializeField] private float investigationWarningVolume = 1f;
    [Header("Phase Unlock Popup")]
    [SerializeField] private GameObject phaseUnlockPanel;
    [SerializeField] private Image phaseUnlockImage;
    [SerializeField] private TextMeshProUGUI phaseUnlockText;
    [SerializeField] private Sprite phaseUnlockSprite;
    [SerializeField] private AudioClip phaseUnlockClip;
    [Range(0f, 1f)] [SerializeField] private float phaseUnlockVolume = 1f;
    [SerializeField] private float phaseUnlockDisplayDuration = 3f;
    [SerializeField] private Vector2 phaseUnlockImageSize = new Vector2(180f, 180f);

    private bool subscribed;
    private bool createdRoomPasswordTextAtRuntime;
    private bool createdInvestigationWarningAtRuntime;
    private bool createdPhaseUnlockAtRuntime;
    private bool hasShownInvestigationWarningThisPhase;
    private int lastObservedProgressionPhase = 1;
    private AudioSource uiAudioSource;
    private Coroutine phaseUnlockRoutine;

    private void Start()
    {
        ResolveReferences();
        TrySubscribe();
        RefreshUI();
        RefreshInvestigationWarning();
        HidePhaseUnlockPanelImmediate();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (gameRoundManager == null)
        {
            ResolveReferences();
            TrySubscribe();
        }

        if (gameRoundManager != null)
        {
            RefreshUI();
        }
    }

    private void ResolveReferences()
    {
        if (gameRoundManager == null)
        {
            gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        }

        if (scoreText == null)
        {
            scoreText = FindNamedText("ScoreText");
        }

        if (roomPasswordText == null)
        {
            roomPasswordText = FindNamedText("RoomPasswordText");
        }

        if (roomPasswordText == null)
        {
            roomPasswordText = CreateRoomPasswordText();
            createdRoomPasswordTextAtRuntime = roomPasswordText != null;
        }

        if (createdRoomPasswordTextAtRuntime)
        {
            ConfigureRoomPasswordTextLayout();
        }

        if (roundText == null)
        {
            roundText = FindNamedText("RoundText");
        }

        if (phaseText == null)
        {
            phaseText = FindNamedText("PhaseText");
        }

        if (investigationWarningText == null)
        {
            investigationWarningText = FindNamedText("InvestigationWarningText");
        }

        if (investigationWarningText == null)
        {
            investigationWarningText = CreateCenterWarningText();
            createdInvestigationWarningAtRuntime = investigationWarningText != null;
        }

        if (createdInvestigationWarningAtRuntime)
        {
            ConfigureInvestigationWarningTextLayout();
        }

        if (phaseUnlockPanel == null)
        {
            var panelTransform = FindNamedTransform("PhaseUnlockPanel");
            phaseUnlockPanel = panelTransform != null ? panelTransform.gameObject : null;
        }

        if (phaseUnlockPanel == null)
        {
            CreatePhaseUnlockPanel();
            createdPhaseUnlockAtRuntime = phaseUnlockPanel != null;
        }

        if (phaseUnlockPanel != null)
        {
            if (phaseUnlockImage == null)
            {
                phaseUnlockImage = phaseUnlockPanel.transform.Find("PhaseUnlockImage")?.GetComponent<Image>();
            }

            if (phaseUnlockText == null)
            {
                phaseUnlockText = phaseUnlockPanel.transform.Find("PhaseUnlockText")?.GetComponent<TextMeshProUGUI>();
            }
        }

        if (phaseUnlockSprite == null)
        {
            phaseUnlockSprite = Resources.Load<Sprite>("UI/phase_unlock_padlock");
        }

        if (phaseUnlockImage != null && phaseUnlockSprite != null)
        {
            phaseUnlockImage.sprite = phaseUnlockSprite;
            phaseUnlockImage.preserveAspect = true;
        }

        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
        }

        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || gameRoundManager == null)
        {
            return;
        }

        gameRoundManager.currentScore.OnValueChanged += OnScoreChanged;
        gameRoundManager.currentRound.OnValueChanged += OnRoundChanged;
        gameRoundManager.currentProgressionPhase.OnValueChanged += OnProgressionPhaseChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnPhaseChanged;
        gameRoundManager.currentPhaseEndTime.OnValueChanged += OnPhaseEndTimeChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || gameRoundManager == null)
        {
            return;
        }

        gameRoundManager.currentScore.OnValueChanged -= OnScoreChanged;
        gameRoundManager.currentRound.OnValueChanged -= OnRoundChanged;
        gameRoundManager.currentProgressionPhase.OnValueChanged -= OnProgressionPhaseChanged;
        gameRoundManager.gamePhase.OnValueChanged -= OnPhaseChanged;
        gameRoundManager.currentPhaseEndTime.OnValueChanged -= OnPhaseEndTimeChanged;
        subscribed = false;
    }

    private void OnScoreChanged(int oldValue, int newValue) => RefreshUI();
    private void OnRoundChanged(int oldValue, int newValue) => RefreshUI();
    private void OnProgressionPhaseChanged(int oldValue, int newValue)
    {
        RefreshUI();
        ShowPhaseUnlockIfNeeded(oldValue, newValue);
    }
    private void OnPhaseChanged(int oldValue, int newValue)
    {
        RefreshUI();
        HandleInvestigationWarningState(oldValue, newValue);
    }
    private void OnPhaseEndTimeChanged(double oldValue, double newValue) => RefreshUI();

    private void RefreshUI()
    {
        if (roomPasswordText != null)
        {
            roomPasswordText.text = $"Password : {GetCurrentRoomPassword()}";
        }

        if (gameRoundManager == null)
        {
            return;
        }

        if (scoreText != null)
            scoreText.text = $"Area Phase: {gameRoundManager.GetCurrentProgressionPhase()}/3";

        if (roundText != null)
            roundText.text = $"Round: {gameRoundManager.currentRound.Value}/{gameRoundManager.GetTotalRounds()}";

        if (phaseText != null)
        {
            var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;

            switch (phase)
            {
                case GameRoundManager.GamePhase.Memorize:
                    phaseText.text = gameRoundManager.HasMemorizeTimer()
                        ? $"Phase: Memorize ({gameRoundManager.GetMemorizeSecondsRemaining()})"
                        : "Phase: Memorize";
                    break;
                case GameRoundManager.GamePhase.SpawnLockdown:
                    phaseText.text = "Phase: Spawn Lockdown";
                    break;
                case GameRoundManager.GamePhase.Investigation:
                    phaseText.text = $"Phase: Investigation ({gameRoundManager.GetInvestigationSecondsRemaining()})";
                    break;
                case GameRoundManager.GamePhase.RoundTransition:
                    phaseText.text = "Phase: Transition";
                    break;
                case GameRoundManager.GamePhase.Victory:
                    phaseText.text = "Phase: Victory";
                    break;
            }
        }

        RefreshInvestigationWarning();
    }

    private TextMeshProUGUI FindNamedText(string objectName)
    {
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text != null && text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private string GetCurrentRoomPassword()
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
        {
            return "None";
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PasswordCodeKey, out var passwordValue) &&
            passwordValue is string passwordText &&
            !string.IsNullOrWhiteSpace(passwordText))
        {
            return passwordText;
        }

        return "None";
    }

    private TextMeshProUGUI CreateRoomPasswordText()
    {
        var textObject = new GameObject("RoomPasswordText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        var parentTransform = rootCanvas != null ? rootCanvas.transform : (scoreText != null ? scoreText.transform.parent : transform);
        textObject.transform.SetParent(parentTransform, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "Password : None";
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.raycastTarget = false;

        ConfigureRoomPasswordTextLayout(text);

        return text;
    }

    private void ConfigureRoomPasswordTextLayout()
    {
        ConfigureRoomPasswordTextLayout(roomPasswordText);
    }

    private void ConfigureRoomPasswordTextLayout(TextMeshProUGUI text)
    {
        if (text == null || text.rectTransform == null)
        {
            return;
        }

        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null && text.transform.parent != rootCanvas.transform)
        {
            text.transform.SetParent(rootCanvas.transform, false);
        }

        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = new Vector2(8f, -8f);
        text.rectTransform.sizeDelta = new Vector2(420f, 40f);
    }

    private TextMeshProUGUI CreateCenterWarningText()
    {
        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null)
        {
            return null;
        }

        var textObject = new GameObject("InvestigationWarningText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rootCanvas.transform, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.85f, 0.4f, 1f);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.text = string.Empty;
        textObject.SetActive(false);
        return text;
    }

    private void ConfigureInvestigationWarningTextLayout()
    {
        if (investigationWarningText == null)
        {
            return;
        }

        var rect = investigationWarningText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -96f);
        rect.sizeDelta = new Vector2(980f, 110f);
    }

    private Transform FindNamedTransform(string objectName)
    {
        foreach (var rect in GetComponentsInChildren<RectTransform>(true))
        {
            if (rect != null && rect.name == objectName)
            {
                return rect;
            }
        }

        return null;
    }

    private void RefreshInvestigationWarning()
    {
        if (investigationWarningText == null || gameRoundManager == null)
        {
            return;
        }

        var shouldWarn = gameRoundManager.IsInvestigationPhase() &&
                         gameRoundManager.GetInvestigationSecondsRemaining() > 0 &&
                         gameRoundManager.GetInvestigationSecondsRemaining() <= Mathf.CeilToInt(investigationWarningThresholdSeconds);

        if (!shouldWarn)
        {
            if (investigationWarningText.gameObject.activeSelf)
            {
                investigationWarningText.gameObject.SetActive(false);
            }

            return;
        }

        var secondsRemaining = gameRoundManager.GetInvestigationSecondsRemaining();
        investigationWarningText.text = $"เหลือ {secondsRemaining} วิ\n{investigationWarningMessage}";
        if (!investigationWarningText.gameObject.activeSelf)
        {
            investigationWarningText.gameObject.SetActive(true);
        }

        if (!hasShownInvestigationWarningThisPhase)
        {
            hasShownInvestigationWarningThisPhase = true;
            PlayUiClip(investigationWarningClip, investigationWarningVolume);
        }
    }

    private void HandleInvestigationWarningState(int oldPhase, int newPhase)
    {
        if (newPhase == (int)GameRoundManager.GamePhase.Investigation)
        {
            hasShownInvestigationWarningThisPhase = false;
            RefreshInvestigationWarning();
            return;
        }

        hasShownInvestigationWarningThisPhase = false;
        if (investigationWarningText != null)
        {
            investigationWarningText.gameObject.SetActive(false);
        }
    }

    private void CreatePhaseUnlockPanel()
    {
        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null)
        {
            return;
        }

        phaseUnlockPanel = new GameObject("PhaseUnlockPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        phaseUnlockPanel.transform.SetParent(rootCanvas.transform, false);

        var panelRect = phaseUnlockPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(520f, 340f);

        var panelImage = phaseUnlockPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);
        panelImage.raycastTarget = false;

        var iconObject = new GameObject("PhaseUnlockImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(phaseUnlockPanel.transform, false);
        phaseUnlockImage = iconObject.GetComponent<Image>();
        phaseUnlockImage.raycastTarget = false;
        phaseUnlockImage.preserveAspect = true;
        var iconRect = phaseUnlockImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 48f);
        iconRect.sizeDelta = phaseUnlockImageSize;

        var textObject = new GameObject("PhaseUnlockText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(phaseUnlockPanel.transform, false);
        phaseUnlockText = textObject.GetComponent<TextMeshProUGUI>();
        phaseUnlockText.font = TMP_Settings.defaultFontAsset;
        phaseUnlockText.fontSize = 34f;
        phaseUnlockText.alignment = TextAlignmentOptions.Center;
        phaseUnlockText.color = Color.white;
        phaseUnlockText.raycastTarget = false;
        var textRect = phaseUnlockText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, -92f);
        textRect.sizeDelta = new Vector2(460f, 100f);

        phaseUnlockPanel.SetActive(false);
    }

    private void ShowPhaseUnlockIfNeeded(int oldValue, int newValue)
    {
        if (gameRoundManager == null || phaseUnlockPanel == null)
        {
            return;
        }

        if (gameRoundManager.currentRound.Value <= 0)
        {
            lastObservedProgressionPhase = newValue;
            return;
        }

        if (newValue <= oldValue || newValue <= 1)
        {
            lastObservedProgressionPhase = newValue;
            return;
        }

        lastObservedProgressionPhase = newValue;

        if (phaseUnlockText != null)
        {
            phaseUnlockText.text = GetPhaseUnlockMessage(newValue);
        }

        if (phaseUnlockImage != null && phaseUnlockSprite != null)
        {
            phaseUnlockImage.sprite = phaseUnlockSprite;
        }

        if (phaseUnlockRoutine != null)
        {
            StopCoroutine(phaseUnlockRoutine);
        }

        phaseUnlockRoutine = StartCoroutine(ShowPhaseUnlockRoutine());
        PlayUiClip(phaseUnlockClip, phaseUnlockVolume);
    }

    private System.Collections.IEnumerator ShowPhaseUnlockRoutine()
    {
        if (phaseUnlockPanel == null)
        {
            yield break;
        }

        phaseUnlockPanel.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(0.5f, phaseUnlockDisplayDuration));
        HidePhaseUnlockPanelImmediate();
        phaseUnlockRoutine = null;
    }

    private void HidePhaseUnlockPanelImmediate()
    {
        if (phaseUnlockPanel != null)
        {
            phaseUnlockPanel.SetActive(false);
        }
    }

    private static string GetPhaseUnlockMessage(int progressionPhase)
    {
        return progressionPhase switch
        {
            2 => "มีโซนใหม่ปลดล็อก\nโซนฝั่งซ้ายเปิดแล้ว",
            3 => "มีโซนใหม่ปลดล็อก\nโซนฝั่งขวาเปิดแล้ว",
            _ => "มีโซนใหม่ปลดล็อก"
        };
    }

    private void PlayUiClip(AudioClip clip, float volume)
    {
        if (uiAudioSource == null || clip == null)
        {
            return;
        }

        uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}

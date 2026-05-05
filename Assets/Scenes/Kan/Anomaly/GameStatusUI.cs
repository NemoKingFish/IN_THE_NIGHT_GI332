using TMPro;
using Photon.Pun;
using UnityEngine;

public class GameStatusUI : MonoBehaviour
{
    private const string PasswordCodeKey = "PasswordCode";

    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private TextMeshProUGUI roomPasswordText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI phaseText;

    private bool subscribed;

    private void Start()
    {
        ResolveReferences();
        TrySubscribe();
        RefreshUI();
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
        }

        ConfigureRoomPasswordTextLayout();

        if (roundText == null)
        {
            roundText = FindNamedText("RoundText");
        }

        if (phaseText == null)
        {
            phaseText = FindNamedText("PhaseText");
        }
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
    private void OnProgressionPhaseChanged(int oldValue, int newValue) => RefreshUI();
    private void OnPhaseChanged(int oldValue, int newValue) => RefreshUI();
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
}

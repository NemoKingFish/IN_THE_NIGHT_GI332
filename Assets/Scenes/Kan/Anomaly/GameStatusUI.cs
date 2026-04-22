using TMPro;
using UnityEngine;

public class GameStatusUI : MonoBehaviour
{
    [SerializeField] private GameRoundManager gameRoundManager;
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
        gameRoundManager.gamePhase.OnValueChanged -= OnPhaseChanged;
        gameRoundManager.currentPhaseEndTime.OnValueChanged -= OnPhaseEndTimeChanged;
        subscribed = false;
    }

    private void OnScoreChanged(int oldValue, int newValue) => RefreshUI();
    private void OnRoundChanged(int oldValue, int newValue) => RefreshUI();
    private void OnPhaseChanged(int oldValue, int newValue) => RefreshUI();
    private void OnPhaseEndTimeChanged(double oldValue, double newValue) => RefreshUI();

    private void RefreshUI()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        if (scoreText != null)
            scoreText.text = $"Score: {gameRoundManager.currentScore.Value}/{gameRoundManager.GetScoreToWin()}";

        if (roundText != null)
            roundText.text = $"Round: {gameRoundManager.currentRound.Value}";

        if (phaseText != null)
        {
            var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;

            switch (phase)
            {
                case GameRoundManager.GamePhase.Memorize:
                    phaseText.text = $"Phase: Memorize ({gameRoundManager.GetMemorizeSecondsRemaining()})";
                    break;
                case GameRoundManager.GamePhase.Investigation:
                    phaseText.text = "Phase: Investigation";
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
}

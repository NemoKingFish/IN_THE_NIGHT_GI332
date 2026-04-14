using TMPro;
using UnityEngine;

public class GameStatusUI : MonoBehaviour
{
    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI phaseText;

    private void Start()
    {
        if (gameRoundManager == null)
        {
            Debug.LogError("GameStatusUI: GameRoundManager is missing.", this);
            enabled = false;
            return;
        }

        RefreshUI();

        gameRoundManager.currentScore.OnValueChanged += OnScoreChanged;
        gameRoundManager.currentRound.OnValueChanged += OnRoundChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnPhaseChanged;
        gameRoundManager.currentPhaseEndTime.OnValueChanged += OnPhaseEndTimeChanged;
    }

    private void OnDestroy()
    {
        if (gameRoundManager == null) return;

        gameRoundManager.currentScore.OnValueChanged -= OnScoreChanged;
        gameRoundManager.currentRound.OnValueChanged -= OnRoundChanged;
        gameRoundManager.gamePhase.OnValueChanged -= OnPhaseChanged;
        gameRoundManager.currentPhaseEndTime.OnValueChanged -= OnPhaseEndTimeChanged;
    }

    private void OnScoreChanged(int oldValue, int newValue) => RefreshUI();
    private void OnRoundChanged(int oldValue, int newValue) => RefreshUI();
    private void OnPhaseChanged(int oldValue, int newValue) => RefreshUI();
    private void OnPhaseEndTimeChanged(double oldValue, double newValue) => RefreshUI();

    private void Update()
    {
        if (gameRoundManager != null && gameRoundManager.IsMemorizePhase())
        {
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
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
}

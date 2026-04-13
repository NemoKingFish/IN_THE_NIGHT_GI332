using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameRoundManager : NetworkBehaviour
{
    public enum GamePhase
    {
        Memorize = 0,
        Investigation = 1,
        RoundTransition = 2,
        Victory = 3
    }

    private enum RoundOutcome
    {
        None = 0,
        Correct = 1,
        Wrong = 2
    }

    [Header("References")]
    [SerializeField] private ChecklistManager checklistManager;

    [Header("Game Settings")]
    [SerializeField] private int scoreToWin = 3;
    [SerializeField] private float memorizeDuration = 8f;
    [SerializeField] private float transitionDelay = 2f;

    public NetworkVariable<int> currentScore = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> currentRound = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> gamePhase = new NetworkVariable<int>(
        (int)GamePhase.Memorize,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Coroutine gameLoopRoutine;
    private bool needMemorize = true;
    private RoundOutcome lastRoundOutcome = RoundOutcome.None;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        StartGameLoop();
    }

    public void StartGameLoop()
    {
        if (!IsServer) return;

        if (gameLoopRoutine != null)
            StopCoroutine(gameLoopRoutine);

        gameLoopRoutine = StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        currentScore.Value = 0;
        currentRound.Value = 0;
        needMemorize = true;
        lastRoundOutcome = RoundOutcome.None;

        while (currentScore.Value < scoreToWin)
        {
            if (needMemorize)
            {
                SetAllSpawnPointsToNormal();
                ResetChecklistOnly();

                gamePhase.Value = (int)GamePhase.Memorize;
                yield return new WaitForSeconds(memorizeDuration);
            }

            currentRound.Value += 1;
            SpawnRoundAnomalies();
            PrepareChecklistForRound();
            gamePhase.Value = (int)GamePhase.Investigation;

            yield return new WaitUntil(() =>
                gamePhase.Value == (int)GamePhase.RoundTransition ||
                gamePhase.Value == (int)GamePhase.Victory
            );

            if (gamePhase.Value == (int)GamePhase.Victory)
                yield break;

            yield return new WaitForSeconds(transitionDelay);

            if (lastRoundOutcome == RoundOutcome.Correct)
            {
                needMemorize = false;
            }
            else if (lastRoundOutcome == RoundOutcome.Wrong)
            {
                needMemorize = true;
            }

            lastRoundOutcome = RoundOutcome.None;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitChecklistServerRpc()
    {
        if (!IsServer) return;
        if (gamePhase.Value != (int)GamePhase.Investigation) return;
        if (checklistManager == null) return;

        bool correct = checklistManager.EvaluateSubmission();

        if (correct)
        {
            currentScore.Value += 1;
            lastRoundOutcome = RoundOutcome.Correct;

            if (currentScore.Value >= scoreToWin)
            {
                gamePhase.Value = (int)GamePhase.Victory;
                return;
            }
        }
        else
        {
            currentScore.Value = 0;
            currentRound.Value = 0;
            lastRoundOutcome = RoundOutcome.Wrong;
        }

        gamePhase.Value = (int)GamePhase.RoundTransition;
    }

    private void SetAllSpawnPointsToNormal()
    {
        if (checklistManager == null) return;

        int count = checklistManager.GetItemCount();

        for (int i = 0; i < count; i++)
        {
            AnomalySpawnPoint point = checklistManager.GetPoint(i);
            if (point != null)
                point.SpawnNormal();
        }
    }

    private void SpawnRoundAnomalies()
    {
        if (checklistManager == null) return;

        int count = checklistManager.GetItemCount();

        for (int i = 0; i < count; i++)
        {
            AnomalySpawnPoint point = checklistManager.GetPoint(i);
            if (point != null)
                point.RollAndSpawn();
        }
    }

    private void PrepareChecklistForRound()
    {
        if (checklistManager == null) return;
        checklistManager.PrepareForNewRound();
    }

    private void ResetChecklistOnly()
    {
        if (checklistManager == null) return;
        checklistManager.ResetOnlySelections();
    }

    public bool IsInvestigationPhase()
    {
        return gamePhase.Value == (int)GamePhase.Investigation;
    }

    public int GetScoreToWin()
    {
        return scoreToWin;
    }
}
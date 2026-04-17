using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class GameRoundManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const string ScoreKey = "Game_CurrentScore";
    private const string RoundKey = "Game_CurrentRound";
    private const string PhaseKey = "Game_Phase";
    private const string PhaseEndTimeKey = "Game_PhaseEndTime";
    private const string PlayerSubmittedKey = "PlayerSubmitted";
    private const byte SubmitChecklistEventCode = 42;

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
    [SerializeField] private string victoryTargetSceneName = "";
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset victoryTargetSceneAsset;
#endif

    public ObservableValue<int> currentScore = new ObservableValue<int>(0);
    public ObservableValue<int> currentRound = new ObservableValue<int>(0);
    public ObservableValue<int> gamePhase = new ObservableValue<int>((int)GamePhase.Memorize);
    public ObservableValue<double> currentPhaseEndTime = new ObservableValue<double>(0d);

    private Coroutine gameLoopRoutine;
    private bool needMemorize = true;
    private RoundOutcome lastRoundOutcome = RoundOutcome.None;
    private int lastLocalSubmissionResetRound = -1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (victoryTargetSceneAsset != null)
        {
            victoryTargetSceneName = victoryTargetSceneAsset.name;
        }
    }
#endif

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        base.OnDisable();
    }

    private void Start()
    {
        EnsurePhotonSpawnManagerExists();

        if (PhotonNetwork.InRoom)
        {
            ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
            SyncLocalSubmissionFlagForCurrentPhase();
            TryStartAsMaster();
        }
    }

    public override void OnJoinedRoom()
    {
        EnsurePhotonSpawnManagerExists();
        ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
        SyncLocalSubmissionFlagForCurrentPhase();
        TryStartAsMaster();
    }

    public override void OnLeftRoom()
    {
        StopGameLoop();
        currentPhaseEndTime.Value = 0d;
        lastLocalSubmissionResetRound = -1;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (newMasterClient == PhotonNetwork.LocalPlayer)
        {
            TryStartAsMaster();
        }
        else
        {
            StopGameLoop();
        }
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        ApplyRoomState(propertiesThatChanged);
        SyncLocalSubmissionFlagForCurrentPhase();
    }

    public void StartGameLoop()
    {
        if (!CanWriteState())
        {
            return;
        }

        StopGameLoop();
        gameLoopRoutine = StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        SetCurrentScore(0);
        SetCurrentRound(0);
        needMemorize = true;
        lastRoundOutcome = RoundOutcome.None;

        while (currentScore.Value < scoreToWin)
        {
            if (needMemorize)
            {
                SetAllSpawnPointsToNormal();
                ResetChecklistOnly();

                SetPhaseState(GamePhase.Memorize, PhotonNetwork.Time + memorizeDuration);
                yield return new WaitForSeconds(memorizeDuration);
            }

            SetCurrentRound(currentRound.Value + 1);
            SpawnRoundAnomalies();
            PrepareChecklistForRound();
            SetPhaseState(GamePhase.Investigation, 0d);

            yield return new WaitUntil(() =>
                gamePhase.Value == (int)GamePhase.RoundTransition ||
                gamePhase.Value == (int)GamePhase.Victory);

            if (gamePhase.Value == (int)GamePhase.Victory)
            {
                yield break;
            }

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

    public void SubmitChecklistServerRpc()
    {
        if (!PhotonNetwork.InRoom || gamePhase.Value != (int)GamePhase.Investigation)
        {
            return;
        }

        if (!HasLocalPlayerSubmitted())
        {
            SetLocalSubmissionState(true);
        }

        if (CanWriteState())
        {
            TryResolveWhenAllPlayersSubmitted();
            return;
        }

        PhotonNetwork.RaiseEvent(
            SubmitChecklistEventCode,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != SubmitChecklistEventCode || !CanWriteState())
        {
            return;
        }

        TryResolveWhenAllPlayersSubmitted();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        if (changedProps != null && changedProps.ContainsKey(PlayerSubmittedKey) && CanWriteState())
        {
            TryResolveWhenAllPlayersSubmitted();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (CanWriteState())
        {
            TryResolveWhenAllPlayersSubmitted();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (CanWriteState())
        {
            TryResolveWhenAllPlayersSubmitted();
        }
    }

    private void ResolveChecklistSubmission()
    {
        if (gamePhase.Value != (int)GamePhase.Investigation || checklistManager == null)
        {
            return;
        }

        var correct = checklistManager.EvaluateSubmission();

        if (correct)
        {
            SetCurrentScore(currentScore.Value + 1);
            lastRoundOutcome = RoundOutcome.Correct;

            if (currentScore.Value >= scoreToWin)
            {
                SetPhaseState(GamePhase.Victory, 0d);
                TryLoadVictoryScene();
                return;
            }
        }
        else
        {
            SetCurrentScore(0);
            SetCurrentRound(0);
            lastRoundOutcome = RoundOutcome.Wrong;
        }

        SetPhaseState(GamePhase.RoundTransition, 0d);
    }

    private void SetAllSpawnPointsToNormal()
    {
        if (checklistManager == null)
        {
            return;
        }

        var count = checklistManager.GetItemCount();
        for (var i = 0; i < count; i++)
        {
            var point = checklistManager.GetPoint(i);
            if (point != null)
            {
                point.SpawnNormal();
            }
        }
    }

    private void SpawnRoundAnomalies()
    {
        if (checklistManager == null)
        {
            return;
        }

        var count = checklistManager.GetItemCount();
        for (var i = 0; i < count; i++)
        {
            var point = checklistManager.GetPoint(i);
            if (point != null)
            {
                point.RollAndSpawn();
            }
        }
    }

    private void PrepareChecklistForRound()
    {
        if (checklistManager != null)
        {
            checklistManager.PrepareForNewRound();
        }
    }

    private void ResetChecklistOnly()
    {
        if (checklistManager != null)
        {
            checklistManager.ResetOnlySelections();
        }
    }

    public bool IsInvestigationPhase()
    {
        return gamePhase.Value == (int)GamePhase.Investigation;
    }

    public bool IsMemorizePhase()
    {
        return gamePhase.Value == (int)GamePhase.Memorize;
    }

    public int GetScoreToWin()
    {
        return scoreToWin;
    }

    public int GetMemorizeSecondsRemaining()
    {
        if (gamePhase.Value != (int)GamePhase.Memorize)
        {
            return 0;
        }

        var remainingTime = Math.Max(0d, currentPhaseEndTime.Value - PhotonNetwork.Time);
        return Mathf.CeilToInt((float)remainingTime);
    }

    public int GetSubmittedPlayerCount()
    {
        if (!PhotonNetwork.InRoom)
        {
            return 0;
        }

        var submittedCount = 0;
        var players = PhotonNetwork.PlayerList;
        for (var i = 0; i < players.Length; i++)
        {
            if (GetPlayerSubmitted(players[i]))
            {
                submittedCount++;
            }
        }

        return submittedCount;
    }

    public int GetExpectedSubmitterCount()
    {
        return PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Length : 0;
    }

    public bool HasLocalPlayerSubmitted()
    {
        return PhotonNetwork.LocalPlayer != null && GetPlayerSubmitted(PhotonNetwork.LocalPlayer);
    }

    public bool HasAnyPlayerSubmitted()
    {
        return GetSubmittedPlayerCount() > 0;
    }

    public void CancelChecklistSubmission()
    {
        if (!PhotonNetwork.InRoom || gamePhase.Value != (int)GamePhase.Investigation)
        {
            return;
        }

        SetLocalSubmissionState(false);
    }

    private void TryStartAsMaster()
    {
        if (!CanWriteState())
        {
            return;
        }

        EnsureRoomStateInitialized();
        StartGameLoop();
    }

    private static void EnsurePhotonSpawnManagerExists()
    {
        if (FindFirstObjectByType<PhotonScenePlayerSpawnManager>() != null)
        {
            return;
        }

        var managerObject = new GameObject("PhotonScenePlayerSpawnManager");
        managerObject.AddComponent<PhotonScenePlayerSpawnManager>();
    }

    private void StopGameLoop()
    {
        if (gameLoopRoutine != null)
        {
            StopCoroutine(gameLoopRoutine);
            gameLoopRoutine = null;
        }
    }

    private void EnsureRoomStateInitialized()
    {
        if (!PhotonNetwork.InRoom || !CanWriteState())
        {
            return;
        }

        var updates = new PhotonHashtable();

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ScoreKey))
        {
            updates[ScoreKey] = 0;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoundKey))
        {
            updates[RoundKey] = 0;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PhaseKey))
        {
            updates[PhaseKey] = (int)GamePhase.Memorize;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PhaseEndTimeKey))
        {
            updates[PhaseEndTimeKey] = 0d;
        }

        if (updates.Count > 0)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(updates);
        }
    }

    private void ApplyRoomState(PhotonHashtable properties)
    {
        if (properties == null)
        {
            return;
        }

        var previousPhase = gamePhase.Value;
        var previousRound = currentRound.Value;

        if (properties.ContainsKey(ScoreKey))
        {
            currentScore.Value = ReadInt(properties, ScoreKey, 0);
        }

        if (properties.ContainsKey(RoundKey))
        {
            currentRound.Value = ReadInt(properties, RoundKey, 0);
        }

        if (properties.ContainsKey(PhaseKey))
        {
            gamePhase.Value = ReadInt(properties, PhaseKey, (int)GamePhase.Memorize);
        }

        if (properties.ContainsKey(PhaseEndTimeKey))
        {
            currentPhaseEndTime.Value = ReadDouble(properties, PhaseEndTimeKey, 0d);
        }

        if (previousPhase != gamePhase.Value || previousRound != currentRound.Value)
        {
            SyncLocalSubmissionFlagForCurrentPhase();
        }
    }

    private void SetCurrentScore(int value)
    {
        currentScore.Value = value;
        PushRoomState(ScoreKey, value);
    }

    private void SetCurrentRound(int value)
    {
        currentRound.Value = value;
        PushRoomState(RoundKey, value);
    }

    private void SetPhaseState(GamePhase phase, double phaseEndTime)
    {
        gamePhase.Value = (int)phase;
        currentPhaseEndTime.Value = phaseEndTime;

        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { PhaseKey, (int)phase },
            { PhaseEndTimeKey, phaseEndTime }
        });
    }

    private void PushRoomState(string key, int value)
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { key, value }
        });
    }

    private static int ReadInt(PhotonHashtable properties, string key, int fallback)
    {
        if (properties.TryGetValue(key, out var value) && value is int intValue)
        {
            return intValue;
        }

        return fallback;
    }

    private static double ReadDouble(PhotonHashtable properties, string key, double fallback)
    {
        if (!properties.TryGetValue(key, out var value) || value == null)
        {
            return fallback;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return fallback;
    }

    private void SyncLocalSubmissionFlagForCurrentPhase()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        if (gamePhase.Value == (int)GamePhase.Investigation)
        {
            if (lastLocalSubmissionResetRound != currentRound.Value)
            {
                lastLocalSubmissionResetRound = currentRound.Value;
                SetLocalSubmissionState(false);
            }

            return;
        }

        lastLocalSubmissionResetRound = -1;
        SetLocalSubmissionState(false);
    }

    private void SetLocalSubmissionState(bool submitted)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        if (GetPlayerSubmitted(PhotonNetwork.LocalPlayer) == submitted)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { PlayerSubmittedKey, submitted }
        });
    }

    private bool GetPlayerSubmitted(Player player)
    {
        if (player == null || player.CustomProperties == null)
        {
            return false;
        }

        if (player.CustomProperties.TryGetValue(PlayerSubmittedKey, out var submittedValue) && submittedValue is bool submitted)
        {
            return submitted;
        }

        return false;
    }

    private void TryResolveWhenAllPlayersSubmitted()
    {
        if (!PhotonNetwork.InRoom || !CanWriteState())
        {
            return;
        }

        if (gamePhase.Value != (int)GamePhase.Investigation)
        {
            return;
        }

        var expectedSubmitters = GetExpectedSubmitterCount();
        if (expectedSubmitters <= 0)
        {
            return;
        }

        if (GetSubmittedPlayerCount() < expectedSubmitters)
        {
            return;
        }

        ResolveChecklistSubmission();
    }

    private static bool CanWriteState()
    {
        return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    }

    private void TryLoadVictoryScene()
    {
        var targetSceneName = victoryTargetSceneName;
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            if (Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                PhotonNetwork.LoadLevel(targetSceneName);
                return;
            }

            Debug.LogWarning($"[GameRoundManager] Scene '{targetSceneName}' is not in Build Profiles, so the room will stay in the current scene.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
            return;
        }

#if UNITY_EDITOR
        var targetScenePath = victoryTargetSceneAsset != null
            ? UnityEditor.AssetDatabase.GetAssetPath(victoryTargetSceneAsset)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(targetScenePath))
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                targetScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif

        Debug.LogWarning($"[GameRoundManager] No loadable target scene was found for '{targetSceneName}', so the game will remain in the current scene.");
    }
}

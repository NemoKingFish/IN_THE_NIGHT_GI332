using System;
using System.Collections;
using System.Collections.Generic;
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
    private const string ProgressionPhaseKey = "Game_ProgressionPhase";
    private const string PhaseEndTimeKey = "Game_PhaseEndTime";
    private const string MemorizeAccessOpenKey = "Game_MemorizeAccessOpen";
    private const string RoundOutcomeKey = "Game_RoundOutcome";
    private const string PlayerSubmittedKey = "PlayerSubmitted";
    private const byte SubmitChecklistEventCode = 42;

    public enum GamePhase
    {
        Memorize = 0,
        SpawnLockdown = 1,
        Investigation = 2,
        RoundTransition = 3,
        Victory = 4
    }

    private enum RoundOutcome
    {
        None = 0,
        Correct = 1,
        Wrong = 2
    }

    [Serializable]
    private class ProgressionPhaseAnomalySpawnSettings
    {
        [Min(0)] public int minAnomalyCount = 1;
        [Min(0)] public int maxAnomalyCount = 2;
        [Range(0f, 100f)] public float emptyRoomChance = 0f;
    }

    [Header("References")]
    [SerializeField] private ChecklistManager checklistManager;

    [Header("Round Rules")]
    [SerializeField] private int totalRounds = 7;
    [SerializeField] private int phaseTwoStartRound = 4;
    [SerializeField] private int phaseThreeStartRound = 6;
    [SerializeField] private bool useMemorizeTimer;
    [SerializeField] private float memorizeDuration = 8f;
    [SerializeField] private float spawnLockdownDuration = 2f;
    [SerializeField] private float transitionDelay = 2f;
    [SerializeField] private float phaseOneExplorationDuration = 120f;
    [SerializeField] private float phaseTwoExplorationDuration = 180f;
    [SerializeField] private float phaseThreeExplorationDuration = 240f;

    [Header("Anomaly Spawn Rules")]
    [SerializeField] private ProgressionPhaseAnomalySpawnSettings phaseOneAnomalySpawnSettings = new ProgressionPhaseAnomalySpawnSettings();
    [SerializeField] private ProgressionPhaseAnomalySpawnSettings phaseTwoAnomalySpawnSettings = new ProgressionPhaseAnomalySpawnSettings();
    [SerializeField] private ProgressionPhaseAnomalySpawnSettings phaseThreeAnomalySpawnSettings = new ProgressionPhaseAnomalySpawnSettings();
    [SerializeField] private bool debugLogSpawnedAnomalies = true;

    [Header("Scene Flow")]
    [SerializeField] private string victoryTargetSceneName = "";
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset victoryTargetSceneAsset;
#endif

    public ObservableValue<int> currentScore = new ObservableValue<int>(0);
    public ObservableValue<int> currentRound = new ObservableValue<int>(0);
    public ObservableValue<int> gamePhase = new ObservableValue<int>((int)GamePhase.Memorize);
    public ObservableValue<int> currentProgressionPhase = new ObservableValue<int>(1);
    public ObservableValue<double> currentPhaseEndTime = new ObservableValue<double>(0d);
    public ObservableValue<int> memorizeAccessOpen = new ObservableValue<int>(0);

    private Coroutine phaseRoutine;
    private RoundOutcome lastRoundOutcome = RoundOutcome.None;
    private int lastLocalSubmissionResetRound = -1;
    private bool offlineSubmitted;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (victoryTargetSceneAsset != null)
        {
            victoryTargetSceneName = victoryTargetSceneAsset.name;
        }

        totalRounds = Mathf.Max(1, totalRounds);
        phaseTwoStartRound = Mathf.Clamp(phaseTwoStartRound, 2, totalRounds);
        phaseThreeStartRound = Mathf.Clamp(phaseThreeStartRound, phaseTwoStartRound + 1, totalRounds);
        spawnLockdownDuration = Mathf.Max(0f, spawnLockdownDuration);
        transitionDelay = Mathf.Max(0f, transitionDelay);
        phaseOneExplorationDuration = Mathf.Max(1f, phaseOneExplorationDuration);
        phaseTwoExplorationDuration = Mathf.Max(1f, phaseTwoExplorationDuration);
        phaseThreeExplorationDuration = Mathf.Max(1f, phaseThreeExplorationDuration);
        memorizeDuration = Mathf.Max(0f, memorizeDuration);
        SanitizeAnomalySpawnSettings(phaseOneAnomalySpawnSettings);
        SanitizeAnomalySpawnSettings(phaseTwoAnomalySpawnSettings);
        SanitizeAnomalySpawnSettings(phaseThreeAnomalySpawnSettings);
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
        ResolveReferences();

        if (PhotonNetwork.InRoom)
        {
            ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
            SyncLocalSubmissionFlagForCurrentPhase();
        }

        TryStartAsMaster();
    }

    public override void OnJoinedRoom()
    {
        EnsurePhotonSpawnManagerExists();
        ResolveReferences();
        ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
        SyncLocalSubmissionFlagForCurrentPhase();
        TryStartAsMaster();
    }

    public override void OnLeftRoom()
    {
        StopPhaseRoutine();
        currentPhaseEndTime.Value = 0d;
        lastLocalSubmissionResetRound = -1;
        offlineSubmitted = false;
        lastRoundOutcome = RoundOutcome.None;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (newMasterClient == PhotonNetwork.LocalPlayer)
        {
            TryStartAsMaster();
        }
        else
        {
            StopPhaseRoutine();
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

        ResolveReferences();
        BeginMemorizeRound(1);
    }

    public void AdvanceMemorizePhase()
    {
        if (!CanWriteState() || gamePhase.Value != (int)GamePhase.Memorize || !IsRememberStarted())
        {
            return;
        }

        StopPhaseRoutine();
        TeleportLocalPlayerToAssignedSpawnPad();
        SetPhaseState(GamePhase.SpawnLockdown, GetCurrentTime() + spawnLockdownDuration);
        phaseRoutine = StartCoroutine(SpawnLockdownRoutine());
    }

    public void StartRememberPhase()
    {
        if (!CanWriteState() || gamePhase.Value != (int)GamePhase.Memorize || IsRememberStarted())
        {
            return;
        }

        SetMemorizeAccessOpen(true);

        var phaseEndTime = useMemorizeTimer && memorizeDuration > 0f
            ? GetCurrentTime() + memorizeDuration
            : 0d;

        currentPhaseEndTime.Value = phaseEndTime;
        PushRoomState(PhaseEndTimeKey, phaseEndTime);
        ScheduleMemorizeAdvanceIfNeeded();
    }

    public void SubmitChecklistServerRpc()
    {
        if (gamePhase.Value != (int)GamePhase.Investigation)
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

    public bool IsInvestigationPhase()
    {
        return gamePhase.Value == (int)GamePhase.Investigation;
    }

    public bool IsMemorizePhase()
    {
        return gamePhase.Value == (int)GamePhase.Memorize;
    }

    public bool IsRememberStarted()
    {
        return memorizeAccessOpen.Value != 0;
    }

    public bool IsSpawnLockdownPhase()
    {
        return gamePhase.Value == (int)GamePhase.SpawnLockdown;
    }

    public int GetScoreToWin()
    {
        return totalRounds;
    }

    public int GetTotalRounds()
    {
        return totalRounds;
    }

    public int GetCurrentProgressionPhase()
    {
        return currentProgressionPhase.Value;
    }

    public int GetMemorizeSecondsRemaining()
    {
        if (gamePhase.Value != (int)GamePhase.Memorize || currentPhaseEndTime.Value <= 0d)
        {
            return 0;
        }

        var remainingTime = Math.Max(0d, currentPhaseEndTime.Value - GetCurrentTime());
        return Mathf.CeilToInt((float)remainingTime);
    }

    public bool HasMemorizeTimer()
    {
        return gamePhase.Value == (int)GamePhase.Memorize && currentPhaseEndTime.Value > 0d;
    }

    public int GetInvestigationSecondsRemaining()
    {
        if (gamePhase.Value != (int)GamePhase.Investigation || currentPhaseEndTime.Value <= 0d)
        {
            return 0;
        }

        var remainingTime = Math.Max(0d, currentPhaseEndTime.Value - GetCurrentTime());
        return Mathf.CeilToInt((float)remainingTime);
    }

    public int GetSubmittedPlayerCount()
    {
        if (!PhotonNetwork.InRoom)
        {
            return offlineSubmitted ? 1 : 0;
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
        return PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Length : 1;
    }

    public bool HasLocalPlayerSubmitted()
    {
        if (!PhotonNetwork.InRoom)
        {
            return offlineSubmitted;
        }

        return PhotonNetwork.LocalPlayer != null && GetPlayerSubmitted(PhotonNetwork.LocalPlayer);
    }

    public bool HasAnyPlayerSubmitted()
    {
        return GetSubmittedPlayerCount() > 0;
    }

    public void CancelChecklistSubmission()
    {
        if (gamePhase.Value != (int)GamePhase.Investigation)
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
        ResumeOrStartMasterFlow();
    }

    private void ResolveReferences()
    {
        if (checklistManager == null)
        {
            checklistManager = FindFirstObjectByType<ChecklistManager>();
        }
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

    private void ResumeOrStartMasterFlow()
    {
        StopPhaseRoutine();

        if (currentRound.Value <= 0)
        {
            BeginMemorizeRound(1);
            return;
        }

        switch ((GamePhase)gamePhase.Value)
        {
            case GamePhase.Memorize:
                ScheduleMemorizeAdvanceIfNeeded();
                break;
            case GamePhase.SpawnLockdown:
                phaseRoutine = StartCoroutine(SpawnLockdownRoutine());
                break;
            case GamePhase.Investigation:
                phaseRoutine = StartCoroutine(InvestigationRoutine());
                break;
            case GamePhase.RoundTransition:
                phaseRoutine = StartCoroutine(RoundTransitionRoutine());
                break;
            case GamePhase.Victory:
                break;
            default:
                BeginMemorizeRound(1);
                break;
        }
    }

    private void BeginMemorizeRound(int roundNumber)
    {
        StopPhaseRoutine();

        var clampedRound = Mathf.Clamp(roundNumber, 1, totalRounds);
        SetCurrentScore(0);
        SetCurrentRound(clampedRound);
        SetProgressionPhase(GetProgressionPhaseForRound(clampedRound));
        SetLastRoundOutcome(RoundOutcome.None);
        SetMemorizeAccessOpen(false);
        SetAllSpawnPointsToNormal();
        ResetChecklistOnly();
        SetPhaseState(GamePhase.Memorize, 0d);
        TeleportLocalPlayerToAssignedSpawnPad();
        StopPhaseRoutine();
    }

    private void ScheduleMemorizeAdvanceIfNeeded()
    {
        StopPhaseRoutine();

        if (gamePhase.Value != (int)GamePhase.Memorize || !CanWriteState() || !IsRememberStarted())
        {
            return;
        }

        if (currentPhaseEndTime.Value <= 0d)
        {
            return;
        }

        phaseRoutine = StartCoroutine(AutoAdvanceMemorizeRoutine());
    }

    private IEnumerator AutoAdvanceMemorizeRoutine()
    {
        while (gamePhase.Value == (int)GamePhase.Memorize)
        {
            var remaining = currentPhaseEndTime.Value - GetCurrentTime();
            if (remaining <= 0d)
            {
                AdvanceMemorizePhase();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator SpawnLockdownRoutine()
    {
        while (gamePhase.Value == (int)GamePhase.SpawnLockdown)
        {
            var remaining = currentPhaseEndTime.Value - GetCurrentTime();
            if (remaining <= 0d)
            {
                StartInvestigationPhase();
                yield break;
            }

            yield return null;
        }
    }

    private void StartInvestigationPhase()
    {
        if (!CanWriteState())
        {
            return;
        }

        StopPhaseRoutine();
        SpawnRoundAnomalies(currentProgressionPhase.Value);
        PrepareChecklistForRound();

        var investigationDuration = GetExplorationDurationForCurrentPhase();
        SetPhaseState(GamePhase.Investigation, GetCurrentTime() + investigationDuration);
        phaseRoutine = StartCoroutine(InvestigationRoutine());
    }

    private IEnumerator InvestigationRoutine()
    {
        while (gamePhase.Value == (int)GamePhase.Investigation)
        {
            var remaining = currentPhaseEndTime.Value - GetCurrentTime();
            if (remaining <= 0d)
            {
                ResolveChecklistSubmission();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RoundTransitionRoutine()
    {
        while (gamePhase.Value == (int)GamePhase.RoundTransition)
        {
            var remaining = currentPhaseEndTime.Value - GetCurrentTime();
            if (remaining <= 0d)
            {
                AdvanceAfterRoundTransition();
                yield break;
            }

            yield return null;
        }
    }

    private void ResolveChecklistSubmission()
    {
        if (gamePhase.Value != (int)GamePhase.Investigation || checklistManager == null)
        {
            return;
        }

        StopPhaseRoutine();

        var correct = checklistManager.EvaluateSubmission();
        SetLastRoundOutcome(correct ? RoundOutcome.Correct : RoundOutcome.Wrong);

        if (correct && currentRound.Value >= totalRounds)
        {
            SetPhaseState(GamePhase.Victory, 0d);
            return;
        }

        SetPhaseState(GamePhase.RoundTransition, GetCurrentTime() + transitionDelay);
        phaseRoutine = StartCoroutine(RoundTransitionRoutine());
    }

    private void AdvanceAfterRoundTransition()
    {
        if (!CanWriteState())
        {
            return;
        }

        var nextRound = lastRoundOutcome == RoundOutcome.Correct
            ? currentRound.Value + 1
            : GetPhaseStartRound(GetProgressionPhaseForRound(currentRound.Value));

        BeginMemorizeRound(nextRound);
    }

    private void SetAllSpawnPointsToNormal()
    {
        if (checklistManager == null)
        {
            return;
        }

        var count = checklistManager.GetPointCount();
        for (var i = 0; i < count; i++)
        {
            var point = checklistManager.GetPoint(i);
            if (point != null)
            {
                point.SpawnNormal();
            }
        }
    }

    private void SpawnRoundAnomalies(int progressionPhase)
    {
        if (checklistManager == null)
        {
            return;
        }

        var eligiblePoints = new List<AnomalySpawnPoint>();
        var count = checklistManager.GetPointCount();
        for (var i = 0; i < count; i++)
        {
            var point = checklistManager.GetPoint(i);
            if (point == null)
            {
                continue;
            }

            point.SpawnNormal();

            if (point.CanSpawnAnomalyInProgressionPhase(progressionPhase))
            {
                eligiblePoints.Add(point);
            }
        }

        if (eligiblePoints.Count == 0)
        {
            LogSpawnedAnomalies(progressionPhase, null, "No eligible anomaly points were available for this phase.");
            return;
        }

        var settings = GetAnomalySpawnSettingsForProgressionPhase(progressionPhase);
        if (ShouldSpawnNoAnomalies(settings))
        {
            LogSpawnedAnomalies(progressionPhase, null, "Empty room roll succeeded.");
            return;
        }

        var anomalyCount = GetRandomAnomalyCount(settings, eligiblePoints.Count);
        if (anomalyCount <= 0)
        {
            LogSpawnedAnomalies(progressionPhase, null, "Resolved anomaly count was 0.");
            return;
        }

        var candidatePoints = BuildSpawnCandidateOrder(eligiblePoints);
        var spawnedPoints = new List<AnomalySpawnPoint>(anomalyCount);

        for (var index = 0; index < anomalyCount && index < candidatePoints.Count; index++)
        {
            var point = candidatePoints[index];
            point.SpawnAnomaly(progressionPhase);

            if (!point.HasAnomaly())
            {
                continue;
            }

            if (point.IsUniqueAnomaly())
            {
                ResetSpawnedPointsToNormal(spawnedPoints);
                spawnedPoints.Clear();
                spawnedPoints.Add(point);
                LogSpawnedAnomalies(progressionPhase, spawnedPoints, "Unique anomaly triggered and suppressed other anomalies for this round.");
                return;
            }

            spawnedPoints.Add(point);
        }

        LogSpawnedAnomalies(progressionPhase, spawnedPoints, null);
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

    private void StopPhaseRoutine()
    {
        if (phaseRoutine != null)
        {
            StopCoroutine(phaseRoutine);
            phaseRoutine = null;
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

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ProgressionPhaseKey))
        {
            updates[ProgressionPhaseKey] = 1;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PhaseEndTimeKey))
        {
            updates[PhaseEndTimeKey] = 0d;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MemorizeAccessOpenKey))
        {
            updates[MemorizeAccessOpenKey] = 0;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoundOutcomeKey))
        {
            updates[RoundOutcomeKey] = (int)RoundOutcome.None;
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

        if (properties.ContainsKey(ProgressionPhaseKey))
        {
            currentProgressionPhase.Value = ReadInt(properties, ProgressionPhaseKey, 1);
        }

        if (properties.ContainsKey(PhaseEndTimeKey))
        {
            currentPhaseEndTime.Value = ReadDouble(properties, PhaseEndTimeKey, 0d);
        }

        if (properties.ContainsKey(MemorizeAccessOpenKey))
        {
            memorizeAccessOpen.Value = ReadInt(properties, MemorizeAccessOpenKey, 0);
        }

        if (properties.ContainsKey(RoundOutcomeKey))
        {
            lastRoundOutcome = (RoundOutcome)ReadInt(properties, RoundOutcomeKey, (int)RoundOutcome.None);
        }

        if (previousPhase != gamePhase.Value || previousRound != currentRound.Value)
        {
            HandleLocalPhaseChange(previousPhase, gamePhase.Value);
            SyncLocalSubmissionFlagForCurrentPhase();
        }
    }

    private void HandleLocalPhaseChange(int previousPhaseValue, int nextPhaseValue)
    {
        if (previousPhaseValue == nextPhaseValue)
        {
            return;
        }

        var nextPhase = (GamePhase)nextPhaseValue;
        if (nextPhase == GamePhase.Memorize || nextPhase == GamePhase.SpawnLockdown)
        {
            var spawnManager = FindFirstObjectByType<PhotonScenePlayerSpawnManager>();
            if (spawnManager != null)
            {
                spawnManager.TeleportLocalPlayerToAssignedSpawnPad();
            }
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

    private void SetProgressionPhase(int value)
    {
        currentProgressionPhase.Value = Mathf.Clamp(value, 1, 3);
        PushRoomState(ProgressionPhaseKey, currentProgressionPhase.Value);
    }

    private void SetMemorizeAccessOpen(bool isOpen)
    {
        memorizeAccessOpen.Value = isOpen ? 1 : 0;
        PushRoomState(MemorizeAccessOpenKey, memorizeAccessOpen.Value);
    }

    private void SetLastRoundOutcome(RoundOutcome outcome)
    {
        lastRoundOutcome = outcome;
        PushRoomState(RoundOutcomeKey, (int)outcome);
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

    private static void TeleportLocalPlayerToAssignedSpawnPad()
    {
        var spawnManager = FindFirstObjectByType<PhotonScenePlayerSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.TeleportLocalPlayerToAssignedSpawnPad();
        }
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

    private void PushRoomState(string key, double value)
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

    private int GetExplorationDurationForCurrentPhase()
    {
        return GetExplorationDurationForProgressionPhase(currentProgressionPhase.Value);
    }

    private ProgressionPhaseAnomalySpawnSettings GetAnomalySpawnSettingsForProgressionPhase(int progressionPhase)
    {
        return progressionPhase switch
        {
            1 => phaseOneAnomalySpawnSettings,
            2 => phaseTwoAnomalySpawnSettings,
            _ => phaseThreeAnomalySpawnSettings
        };
    }

    private static void SanitizeAnomalySpawnSettings(ProgressionPhaseAnomalySpawnSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.minAnomalyCount = Mathf.Max(0, settings.minAnomalyCount);
        settings.maxAnomalyCount = Mathf.Max(settings.minAnomalyCount, settings.maxAnomalyCount);
        settings.emptyRoomChance = Mathf.Clamp(settings.emptyRoomChance, 0f, 100f);
    }

    private static bool ShouldSpawnNoAnomalies(ProgressionPhaseAnomalySpawnSettings settings)
    {
        if (settings == null)
        {
            return false;
        }

        return UnityEngine.Random.Range(0f, 100f) < Mathf.Clamp(settings.emptyRoomChance, 0f, 100f);
    }

    private static int GetRandomAnomalyCount(ProgressionPhaseAnomalySpawnSettings settings, int eligibleCount)
    {
        if (settings == null || eligibleCount <= 0)
        {
            return 0;
        }

        var minCount = Mathf.Clamp(settings.minAnomalyCount, 1, eligibleCount);
        var maxCount = Mathf.Clamp(settings.maxAnomalyCount, minCount, eligibleCount);
        return UnityEngine.Random.Range(minCount, maxCount + 1);
    }

    private static void ShuffleEligiblePoints(List<AnomalySpawnPoint> eligiblePoints)
    {
        if (eligiblePoints == null)
        {
            return;
        }

        for (var index = eligiblePoints.Count - 1; index > 0; index--)
        {
            var swapIndex = UnityEngine.Random.Range(0, index + 1);
            (eligiblePoints[index], eligiblePoints[swapIndex]) = (eligiblePoints[swapIndex], eligiblePoints[index]);
        }
    }

    private static List<AnomalySpawnPoint> BuildSpawnCandidateOrder(List<AnomalySpawnPoint> eligiblePoints)
    {
        var uniquePoints = new List<AnomalySpawnPoint>();
        var normalPoints = new List<AnomalySpawnPoint>();

        for (var index = 0; index < eligiblePoints.Count; index++)
        {
            var point = eligiblePoints[index];
            if (point == null)
            {
                continue;
            }

            if (point.IsUniqueAnomaly())
            {
                uniquePoints.Add(point);
            }
            else
            {
                normalPoints.Add(point);
            }
        }

        ShuffleEligiblePoints(uniquePoints);
        ShuffleEligiblePoints(normalPoints);

        var orderedPoints = new List<AnomalySpawnPoint>(eligiblePoints.Count);
        orderedPoints.AddRange(uniquePoints);
        orderedPoints.AddRange(normalPoints);
        return orderedPoints;
    }

    private static void ResetSpawnedPointsToNormal(List<AnomalySpawnPoint> spawnedPoints)
    {
        if (spawnedPoints == null)
        {
            return;
        }

        for (var index = 0; index < spawnedPoints.Count; index++)
        {
            var point = spawnedPoints[index];
            if (point != null)
            {
                point.SpawnNormal();
            }
        }
    }

    private void LogSpawnedAnomalies(int progressionPhase, List<AnomalySpawnPoint> spawnedPoints, string reason)
    {
        if (!debugLogSpawnedAnomalies)
        {
            return;
        }

        if (spawnedPoints == null || spawnedPoints.Count == 0)
        {
            var emptyReason = string.IsNullOrWhiteSpace(reason) ? "No anomalies spawned." : reason;
            Debug.Log($"[GameRoundManager] Phase {progressionPhase} spawned no anomalies. {emptyReason}", this);
            return;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append($"[GameRoundManager] Phase {progressionPhase} spawned {spawnedPoints.Count} anomaly");
        if (spawnedPoints.Count != 1)
        {
            builder.Append("ies");
        }
        else
        {
            builder.Append("y");
        }

        builder.AppendLine(":");

        for (var index = 0; index < spawnedPoints.Count; index++)
        {
            var point = spawnedPoints[index];
            if (point == null)
            {
                continue;
            }

            builder.Append("- ID: ");
            builder.Append(point.GetAnomalyID());
            builder.Append(" | Name: ");
            builder.Append(point.GetAnomalyName());
            builder.Append(" | Type: ");
            builder.Append(point.GetCurrentAnomalyType());
            builder.AppendLine();
        }

        Debug.Log(builder.ToString().TrimEnd(), this);
    }

    private int GetExplorationDurationForProgressionPhase(int progressionPhase)
    {
        return progressionPhase switch
        {
            1 => Mathf.CeilToInt(phaseOneExplorationDuration),
            2 => Mathf.CeilToInt(phaseTwoExplorationDuration),
            _ => Mathf.CeilToInt(phaseThreeExplorationDuration)
        };
    }

    private int GetProgressionPhaseForRound(int roundNumber)
    {
        if (roundNumber >= phaseThreeStartRound)
        {
            return 3;
        }

        if (roundNumber >= phaseTwoStartRound)
        {
            return 2;
        }

        return 1;
    }

    private int GetPhaseStartRound(int progressionPhase)
    {
        return progressionPhase switch
        {
            1 => 1,
            2 => phaseTwoStartRound,
            3 => phaseThreeStartRound,
            _ => 1
        };
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
        if (!PhotonNetwork.InRoom)
        {
            if (gamePhase.Value != (int)GamePhase.Investigation)
            {
                offlineSubmitted = false;
            }

            return;
        }

        if (PhotonNetwork.LocalPlayer == null)
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
        if (!PhotonNetwork.InRoom)
        {
            offlineSubmitted = submitted;
            TryResolveWhenAllPlayersSubmitted();
            return;
        }

        if (PhotonNetwork.LocalPlayer == null)
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
        if (!CanWriteState())
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

    private static double GetCurrentTime()
    {
        return PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
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

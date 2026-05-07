using UnityEngine;

public class PhaseAccessManager : MonoBehaviour
{
    [System.Serializable]
    private class PhaseAudioSettings
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private PhaseDoorController spawnRoomDoor;
    [SerializeField] private PhaseDoorController[] phaseTwoDoors;
    [SerializeField] private PhaseDoorController[] phaseThreeDoors;
    [SerializeField] private AudioSource phaseChangeAudioSource;
    [SerializeField] private PhaseAudioSettings memorizePhaseAudio = new PhaseAudioSettings();
    [SerializeField] private PhaseAudioSettings spawnLockdownPhaseAudio = new PhaseAudioSettings();
    [SerializeField] private PhaseAudioSettings investigationPhaseAudio = new PhaseAudioSettings();
    [SerializeField] private PhaseAudioSettings roundTransitionPhaseAudio = new PhaseAudioSettings();
    [SerializeField] private PhaseAudioSettings victoryPhaseAudio = new PhaseAudioSettings();

    private bool subscribed;
    private bool hasInitializedObservedPhase;
    private int lastObservedPhaseValue;

    private void Start()
    {
        ResolveReferences();
        TrySubscribe();
        CacheCurrentPhaseState();
        RefreshDoors();
        SyncProgressionMusic();
    }

    private void Update()
    {
        if (gameRoundManager == null)
        {
            ResolveReferences();
            TrySubscribe();
            CacheCurrentPhaseState();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (gameRoundManager == null)
        {
            gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        }

        if (phaseChangeAudioSource == null)
        {
            phaseChangeAudioSource = GetComponent<AudioSource>();
        }

        if (phaseChangeAudioSource == null)
        {
            phaseChangeAudioSource = gameObject.AddComponent<AudioSource>();
        }

        phaseChangeAudioSource.playOnAwake = false;
        phaseChangeAudioSource.loop = false;
        phaseChangeAudioSource.spatialBlend = 0f;
        var soundEmitter = SoundCategoryEmitter.Ensure(phaseChangeAudioSource, SoundCategory.Sfx);
        if (soundEmitter != null)
        {
            soundEmitter.CaptureCurrentVolumeAsBase();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed || gameRoundManager == null)
        {
            return;
        }

        gameRoundManager.currentProgressionPhase.OnValueChanged += OnProgressionPhaseChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || gameRoundManager == null)
        {
            return;
        }

        gameRoundManager.currentProgressionPhase.OnValueChanged -= OnProgressionPhaseChanged;
        gameRoundManager.gamePhase.OnValueChanged -= OnGamePhaseChanged;
        subscribed = false;
    }

    private void OnProgressionPhaseChanged(int oldValue, int newValue)
    {
        RefreshDoors();
        SyncProgressionMusic();
    }

    private void OnGamePhaseChanged(int oldValue, int newValue)
    {
        PlayPhaseChangedAudioIfNeeded(newValue);
        RefreshDoors();
    }

    private void CacheCurrentPhaseState()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        lastObservedPhaseValue = gameRoundManager.gamePhase.Value;
        hasInitializedObservedPhase = true;
    }

    private void PlayPhaseChangedAudioIfNeeded(int nextPhaseValue)
    {
        if (!hasInitializedObservedPhase)
        {
            lastObservedPhaseValue = nextPhaseValue;
            hasInitializedObservedPhase = true;
            return;
        }

        if (nextPhaseValue == lastObservedPhaseValue)
        {
            return;
        }

        lastObservedPhaseValue = nextPhaseValue;

        if (phaseChangeAudioSource == null)
        {
            return;
        }

        var settings = GetPhaseAudioSettings((GameRoundManager.GamePhase)nextPhaseValue);
        if (settings == null || settings.clip == null)
        {
            return;
        }

        phaseChangeAudioSource.PlayOneShot(settings.clip, Mathf.Clamp01(settings.volume));
    }

    private PhaseAudioSettings GetPhaseAudioSettings(GameRoundManager.GamePhase phase)
    {
        return phase switch
        {
            GameRoundManager.GamePhase.Memorize => memorizePhaseAudio,
            GameRoundManager.GamePhase.SpawnLockdown => spawnLockdownPhaseAudio,
            GameRoundManager.GamePhase.Investigation => investigationPhaseAudio,
            GameRoundManager.GamePhase.RoundTransition => roundTransitionPhaseAudio,
            GameRoundManager.GamePhase.Victory => victoryPhaseAudio,
            _ => null
        };
    }

    private void RefreshDoors()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        var progressionPhase = gameRoundManager.GetCurrentProgressionPhase();
        SetDoorsOpen(phaseTwoDoors, progressionPhase >= 2);
        SetDoorsOpen(phaseThreeDoors, progressionPhase >= 3);

        if (spawnRoomDoor != null)
        {
            var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;
            var shouldOpenSpawnDoor = phase == GameRoundManager.GamePhase.Memorize ||
                                      phase == GameRoundManager.GamePhase.Investigation;
            spawnRoomDoor.SetOpen(shouldOpenSpawnDoor);
        }
    }

    private static void SetDoorsOpen(PhaseDoorController[] doors, bool shouldOpen)
    {
        if (doors == null)
        {
            return;
        }

        for (var i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
            {
                doors[i].SetOpen(shouldOpen);
            }
        }
    }

    private void SyncProgressionMusic()
    {
        if (gameRoundManager == null || SoundManager.Instance == null)
        {
            return;
        }

        var progressionPhase = Mathf.Clamp(gameRoundManager.GetCurrentProgressionPhase(), 1, 3);
        SoundManager.Instance.PlayMusicTrackById($"phase{progressionPhase}");
    }
}

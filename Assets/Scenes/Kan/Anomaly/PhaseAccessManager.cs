using UnityEngine;

public class PhaseAccessManager : MonoBehaviour
{
    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private PhaseDoorController spawnRoomDoor;
    [SerializeField] private PhaseDoorController[] phaseTwoDoors;
    [SerializeField] private PhaseDoorController[] phaseThreeDoors;

    private bool subscribed;

    private void Start()
    {
        ResolveReferences();
        TrySubscribe();
        RefreshDoors();
    }

    private void Update()
    {
        if (gameRoundManager == null)
        {
            ResolveReferences();
            TrySubscribe();
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
    }

    private void OnGamePhaseChanged(int oldValue, int newValue)
    {
        RefreshDoors();
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
}

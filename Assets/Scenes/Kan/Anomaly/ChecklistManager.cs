using Unity.Netcode;
using UnityEngine;

public class ChecklistManager : NetworkBehaviour
{
    public enum MatchResult
    {
        Playing = 0,
        Win = 1,
        Lose = 2
    }

    [Header("Anomaly Group From Hierarchy")]
    [SerializeField] private Transform anomalyGroup;

    [Header("Auto Filled")]
    [SerializeField] private AnomalySpawnPoint[] anomalyPoints;

    public NetworkVariable<ulong> checkedMask = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> matchResult = new NetworkVariable<int>(
        (int)MatchResult.Playing,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private ulong correctMask;

    private void Awake()
    {
        RefreshAnomalyPoints();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshAnomalyPoints();
    }
#endif

    [ContextMenu("Refresh Anomaly Points")]
    public void RefreshAnomalyPoints()
    {
        if (anomalyGroup == null)
        {
            anomalyPoints = new AnomalySpawnPoint[0];
            return;
        }

        anomalyPoints = anomalyGroup.GetComponentsInChildren<AnomalySpawnPoint>(true);
    }

    public int GetItemCount()
    {
        return anomalyPoints != null ? anomalyPoints.Length : 0;
    }

    public AnomalySpawnPoint GetPoint(int index)
    {
        if (anomalyPoints == null) return null;
        if (index < 0 || index >= anomalyPoints.Length) return null;
        return anomalyPoints[index];
    }

    public string GetItemName(int index)
    {
        AnomalySpawnPoint point = GetPoint(index);
        if (point == null) return $"Anomaly {index}";
        return point.GetAnomalyName();
    }

    public bool IsItemChecked(int index)
    {
        ulong bit = 1UL << index;
        return (checkedMask.Value & bit) != 0;
    }

    public bool IsPlaying()
    {
        return matchResult.Value == (int)MatchResult.Playing;
    }

    public void PrepareForNewRound()
    {
        if (!IsServer) return;

        BuildCorrectMask();
        checkedMask.Value = 0;
        matchResult.Value = (int)MatchResult.Playing;
    }

    public void ResetOnlySelections()
    {
        if (!IsServer) return;

        checkedMask.Value = 0;
        matchResult.Value = (int)MatchResult.Playing;
    }

    private void BuildCorrectMask()
    {
        correctMask = 0;

        if (anomalyPoints == null) return;

        for (int i = 0; i < anomalyPoints.Length; i++)
        {
            if (anomalyPoints[i] != null && anomalyPoints[i].IsAnomaly())
            {
                correctMask |= (1UL << i);
            }
        }
    }

    public bool EvaluateSubmission()
    {
        if (!IsServer) return false;

        bool correct = checkedMask.Value == correctMask;
        matchResult.Value = correct ? (int)MatchResult.Win : (int)MatchResult.Lose;
        return correct;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleChecklistItemServerRpc(int index, bool isChecked)
    {
        if (matchResult.Value != (int)MatchResult.Playing) return;
        if (anomalyPoints == null) return;
        if (index < 0 || index >= anomalyPoints.Length) return;

        ulong bit = 1UL << index;
        ulong current = checkedMask.Value;

        if (isChecked)
            current |= bit;
        else
            current &= ~bit;

        checkedMask.Value = current;
    }
}
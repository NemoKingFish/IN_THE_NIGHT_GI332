using System;
using System.Collections.Generic;
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

    [Header("Auto Filled Spawn Points")]
    [SerializeField] private AnomalySpawnPoint[] anomalyPoints;

    [Header("Auto Filled Type List")]
    [SerializeField] private AnomalyType[] checklistTypes;

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
        RefreshAnomalyData();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshAnomalyData();
    }
#endif

    [ContextMenu("Refresh Anomaly Data")]
    public void RefreshAnomalyData()
    {
        RefreshAnomalyPoints();
        RefreshChecklistTypesFromEnum();
    }

    private void RefreshAnomalyPoints()
    {
        if (anomalyGroup == null)
        {
            anomalyPoints = new AnomalySpawnPoint[0];
            return;
        }

        anomalyPoints = anomalyGroup.GetComponentsInChildren<AnomalySpawnPoint>(true);
    }

    private void RefreshChecklistTypesFromEnum()
    {
        List<AnomalyType> allTypes = new List<AnomalyType>();
        AnomalyType[] enumValues = (AnomalyType[])Enum.GetValues(typeof(AnomalyType));

        for (int i = 0; i < enumValues.Length; i++)
        {
            if (enumValues[i] == AnomalyType.None)
                continue;

            allTypes.Add(enumValues[i]);
        }

        checklistTypes = allTypes.ToArray();
    }

    public int GetItemCount()
    {
        return checklistTypes != null ? checklistTypes.Length : 0;
    }

    public string GetItemName(int index)
    {
        if (checklistTypes == null || index < 0 || index >= checklistTypes.Length)
            return $"AnomalyType {index}";

        return checklistTypes[index].ToString();
    }

    public AnomalyType GetChecklistType(int index)
    {
        if (checklistTypes == null || index < 0 || index >= checklistTypes.Length)
            return AnomalyType.None;

        return checklistTypes[index];
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

        if (checklistTypes == null || anomalyPoints == null) return;

        for (int typeIndex = 0; typeIndex < checklistTypes.Length; typeIndex++)
        {
            AnomalyType checklistType = checklistTypes[typeIndex];
            bool typeExistsThisRound = false;

            for (int pointIndex = 0; pointIndex < anomalyPoints.Length; pointIndex++)
            {
                if (anomalyPoints[pointIndex] == null) continue;

                if (anomalyPoints[pointIndex].GetCurrentAnomalyType() == checklistType)
                {
                    typeExistsThisRound = true;
                    break;
                }
            }

            if (typeExistsThisRound)
            {
                correctMask |= (1UL << typeIndex);
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
        if (checklistTypes == null) return;
        if (index < 0 || index >= checklistTypes.Length) return;

        ulong bit = 1UL << index;
        ulong current = checkedMask.Value;

        if (isChecked)
            current |= bit;
        else
            current &= ~bit;

        checkedMask.Value = current;
    }

    public AnomalySpawnPoint GetPoint(int index)
    {
        if (anomalyPoints == null) return null;
        if (index < 0 || index >= anomalyPoints.Length) return null;
        return anomalyPoints[index];
    }
}
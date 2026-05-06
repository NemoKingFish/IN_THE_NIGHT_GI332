using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChecklistManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const string CheckedMaskKey = "Checklist_CheckedMask";
    private const string MatchResultKey = "Checklist_MatchResult";
    private const byte ToggleChecklistEventCode = 41;

    public enum MatchResult
    {
        Playing = 0,
        Win = 1,
        Lose = 2
    }

    [Header("Auto Filled Spawn Points")]
    [SerializeField] private AnomalySpawnPoint[] anomalyPoints;

    [Header("Auto Filled Type List")]
    [SerializeField] private AnomalyType[] checklistTypes;

    public ObservableValue<ulong> checkedMask = new ObservableValue<ulong>(0);
    public ObservableValue<int> matchResult = new ObservableValue<int>((int)MatchResult.Playing);
    public event Action AnomalyDataChanged;

    private ulong correctMask;
    private int dataRevision;

    public int DataRevision => dataRevision;

    private void Awake()
    {
        RefreshAnomalyData();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
#if UNITY_EDITOR
        EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
        EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
#endif
    }

    public override void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
#endif
        PhotonNetwork.RemoveCallbackTarget(this);
        base.OnDisable();
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            checkedMask.SetValue(0);
            matchResult.SetValue((int)MatchResult.Playing);
            return;
        }

        ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
        EnsureRoomStateInitialized();
    }

    public override void OnJoinedRoom()
    {
        ApplyRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
        EnsureRoomStateInitialized();
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        ApplyRoomState(propertiesThatChanged);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshAnomalyData();
    }
#endif

    [ContextMenu("Refresh Anomaly Data")]
    public bool RefreshAnomalyData()
    {
        var nextPoints = CollectAnomalyPoints();
        var nextTypes = BuildChecklistTypesFromEnum();
        var pointsChanged = !AreSamePoints(anomalyPoints, nextPoints);
        var typesChanged = !AreSameTypes(checklistTypes, nextTypes);

        if (!pointsChanged && !typesChanged)
        {
            return false;
        }

        anomalyPoints = nextPoints;
        checklistTypes = nextTypes;
        dataRevision++;
        AnomalyDataChanged?.Invoke();
        return true;
    }

    private AnomalySpawnPoint[] CollectAnomalyPoints()
    {
        var scenePoints = FindObjectsByType<AnomalySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var filteredPoints = new List<AnomalySpawnPoint>();

        for (var i = 0; i < scenePoints.Length; i++)
        {
            var point = scenePoints[i];
            if (point == null)
            {
                continue;
            }

            if (point.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            filteredPoints.Add(point);
        }

        filteredPoints.Sort(CompareAnomalyPoints);
        return filteredPoints.ToArray();
    }

    private AnomalyType[] BuildChecklistTypesFromEnum()
    {
        var allTypes = new List<AnomalyType>();
        var enumValues = (AnomalyType[])Enum.GetValues(typeof(AnomalyType));

        for (var i = 0; i < enumValues.Length; i++)
        {
            if (enumValues[i] == AnomalyType.None)
            {
                continue;
            }

            allTypes.Add(enumValues[i]);
        }

        return allTypes.ToArray();
    }

    private static bool AreSamePoints(AnomalySpawnPoint[] current, AnomalySpawnPoint[] next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        if (current == null || next == null || current.Length != next.Length)
        {
            return false;
        }

        for (var i = 0; i < current.Length; i++)
        {
            if (current[i] != next[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSameTypes(AnomalyType[] current, AnomalyType[] next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        if (current == null || next == null || current.Length != next.Length)
        {
            return false;
        }

        for (var i = 0; i < current.Length; i++)
        {
            if (current[i] != next[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareAnomalyPoints(AnomalySpawnPoint left, AnomalySpawnPoint right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        var leftId = left.GetAnomalyID();
        var rightId = right.GetAnomalyID();
        if (leftId > 0 && rightId > 0 && leftId != rightId)
        {
            return leftId.CompareTo(rightId);
        }

        return string.CompareOrdinal(GetHierarchyPath(left.transform), GetHierarchyPath(right.transform));
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        var hierarchyPath = target.GetSiblingIndex().ToString("D4");
        var current = target.parent;

        while (current != null)
        {
            hierarchyPath = $"{current.GetSiblingIndex():D4}/{hierarchyPath}";
            current = current.parent;
        }

        return hierarchyPath;
    }

#if UNITY_EDITOR
    private void OnEditorHierarchyChanged()
    {
        if (this == null || gameObject == null || Application.isPlaying)
        {
            return;
        }

        if (RefreshAnomalyData())
        {
            EditorUtility.SetDirty(this);
        }
    }
#endif

    public int GetItemCount()
    {
        return checklistTypes != null ? checklistTypes.Length : 0;
    }

    public int GetPointCount()
    {
        return anomalyPoints != null ? anomalyPoints.Length : 0;
    }

    public string GetItemName(int index)
    {
        if (checklistTypes == null || index < 0 || index >= checklistTypes.Length)
        {
            return $"AnomalyType {index}";
        }

        return checklistTypes[index].ToString();
    }

    public AnomalyType GetChecklistType(int index)
    {
        if (checklistTypes == null || index < 0 || index >= checklistTypes.Length)
        {
            return AnomalyType.None;
        }

        return checklistTypes[index];
    }

    public bool IsItemChecked(int index)
    {
        var bit = 1UL << index;
        return (checkedMask.Value & bit) != 0;
    }

    public bool IsPlaying()
    {
        return matchResult.Value == (int)MatchResult.Playing;
    }

    public void PrepareForNewRound()
    {
        if (!CanWriteState())
        {
            return;
        }

        BuildCorrectMask();
        SetCheckedMask(0);
        SetMatchResult((int)MatchResult.Playing);
    }

    public void ResetOnlySelections()
    {
        if (!CanWriteState())
        {
            return;
        }

        SetCheckedMask(0);
        SetMatchResult((int)MatchResult.Playing);
    }

    private void BuildCorrectMask()
    {
        correctMask = 0;

        if (checklistTypes == null || anomalyPoints == null)
        {
            return;
        }

        for (var typeIndex = 0; typeIndex < checklistTypes.Length; typeIndex++)
        {
            var checklistType = checklistTypes[typeIndex];
            var typeExistsThisRound = false;

            for (var pointIndex = 0; pointIndex < anomalyPoints.Length; pointIndex++)
            {
                if (anomalyPoints[pointIndex] == null)
                {
                    continue;
                }

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
        if (!CanWriteState())
        {
            return false;
        }

        var correct = checkedMask.Value == correctMask;
        SetMatchResult(correct ? (int)MatchResult.Win : (int)MatchResult.Lose);
        return correct;
    }

    public void ToggleChecklistItemServerRpc(int index, bool isChecked)
    {
        if (matchResult.Value != (int)MatchResult.Playing || checklistTypes == null)
        {
            return;
        }

        if (index < 0 || index >= checklistTypes.Length)
        {
            return;
        }

        if (CanWriteState())
        {
            ApplyToggle(index, isChecked);
            return;
        }

        PhotonNetwork.RaiseEvent(
            ToggleChecklistEventCode,
            new object[] { index, isChecked },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != ToggleChecklistEventCode || !CanWriteState())
        {
            return;
        }

        if (photonEvent.CustomData is not object[] payload || payload.Length < 2)
        {
            return;
        }

        ApplyToggle((int)payload[0], (bool)payload[1]);
    }

    private void ApplyToggle(int index, bool isChecked)
    {
        var bit = 1UL << index;
        var current = checkedMask.Value;

        if (isChecked)
        {
            current |= bit;
        }
        else
        {
            current &= ~bit;
        }

        SetCheckedMask(current);
    }

    public AnomalySpawnPoint GetPoint(int index)
    {
        if (anomalyPoints == null || index < 0 || index >= anomalyPoints.Length)
        {
            return null;
        }

        return anomalyPoints[index];
    }

    private void EnsureRoomStateInitialized()
    {
        if (!CanWriteState() || !PhotonNetwork.InRoom)
        {
            return;
        }

        var updates = new PhotonHashtable();

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CheckedMaskKey))
        {
            updates[CheckedMaskKey] = "0";
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MatchResultKey))
        {
            updates[MatchResultKey] = (int)MatchResult.Playing;
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

        if (properties.ContainsKey(CheckedMaskKey))
        {
            checkedMask.Value = ReadULong(properties, CheckedMaskKey, 0);
        }

        if (properties.ContainsKey(MatchResultKey))
        {
            matchResult.Value = ReadInt(properties, MatchResultKey, (int)MatchResult.Playing);
        }
    }

    private void SetCheckedMask(ulong newMask)
    {
        checkedMask.Value = newMask;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { CheckedMaskKey, newMask.ToString() }
            });
        }
    }

    private void SetMatchResult(int result)
    {
        matchResult.Value = result;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { MatchResultKey, result }
            });
        }
    }

    private static int ReadInt(PhotonHashtable properties, string key, int fallback)
    {
        if (properties.TryGetValue(key, out var value) && value is int intValue)
        {
            return intValue;
        }

        return fallback;
    }

    private static ulong ReadULong(PhotonHashtable properties, string key, ulong fallback)
    {
        if (!properties.TryGetValue(key, out var value) || value == null)
        {
            return fallback;
        }

        if (value is string textValue && ulong.TryParse(textValue, out var parsed))
        {
            return parsed;
        }

        if (value is long longValue)
        {
            return (ulong)longValue;
        }

        if (value is int intValue)
        {
            return (ulong)intValue;
        }

        return fallback;
    }

    private static bool CanWriteState()
    {
        return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    }
}

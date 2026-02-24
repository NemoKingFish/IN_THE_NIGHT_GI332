using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [Header("Room Data")]
    [SerializeField] private GameObject roomEntrance;
    [SerializeField] private GameObject roomExit;
    [SerializeField] private bool isAnomalyRoom;

    [Header("Anomaly Score")]
    [SerializeField] private int passScore;
    [SerializeField] private int passScoreThreshold;

}

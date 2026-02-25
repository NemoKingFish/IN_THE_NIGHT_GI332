using System;
using UnityEngine;

public class AnomalyManager : MonoBehaviour
{
    [Header("Anomaly")]
    [SerializeField] private GameObject anomalyObjectSpawn;
    [SerializeField][Range(0,100)] private float chanceAnomalySpawn;
    public bool isSpawn;
    [SerializeField] private int ID;
    [SerializeField] private string anomalyName;

    private void Start()
    {
        float randomValue = UnityEngine.Random.Range(0f, 100f);
        if(chanceAnomalySpawn >= randomValue)
        {
            Instantiate(anomalyObjectSpawn, transform.position, Quaternion.identity);
            isSpawn = true;
            Debug.Log($"Anomaly ID: {ID}/Anomaly Name: {anomalyName}/Anomaly chance: {chanceAnomalySpawn}/This round value: {randomValue}/Spawned: {isSpawn}");
        }
        else
        {
            isSpawn = false;
            Debug.Log($"Anomaly ID: {ID}/Anomaly Name: {anomalyName}/Anomaly chance: {chanceAnomalySpawn}/This round value: {randomValue}/Spawned: {isSpawn}");
        }
    }
}

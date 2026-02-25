using System;
using UnityEngine;

public class AnomalyManager : MonoBehaviour
{
    [Header("Anomaly")]
    [SerializeField] private GameObject anomalyObjectSpawn;
    [SerializeField][Range(0,100)] private float chanceAnomalySpawn;
    [SerializeField] private bool isSafe;
    [SerializeField] private int ID;
    [SerializeField] private string anomalyName;

    private void Start()
    {
        float randomValue = UnityEngine.Random.Range(0f, 100f);
        if(randomValue <= chanceAnomalySpawn)
        {
            Instantiate(anomalyObjectSpawn, transform.position, Quaternion.identity);
        }
    }
}

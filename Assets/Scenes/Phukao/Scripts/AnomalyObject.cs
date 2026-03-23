using UnityEngine;

public class AnomalyObject : MonoBehaviour
{
    [Header("References")]
    public GameObject normalModel;
    public GameObject anomalyModel;

    [Header("Settings")]
    public bool isAnomaly;
    [Range(0f, 1f)]
    public float anomalyChance = 0.2f;

    void Start()
    {
        RandomizeState();
        ApplyState();
    }

    public void RandomizeState()
    {
        isAnomaly = Random.value < anomalyChance;
    }

    public void SetState(bool state)
    {
        isAnomaly = state;
        ApplyState();
    }

    void ApplyState()
    {
        if (normalModel != null)
            normalModel.SetActive(!isAnomaly);

        if (anomalyModel != null)
            anomalyModel.SetActive(isAnomaly);
    }
}
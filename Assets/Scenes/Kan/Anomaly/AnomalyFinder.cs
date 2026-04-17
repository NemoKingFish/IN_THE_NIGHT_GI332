using UnityEngine;

[ExecuteAlways]
public class AnomalyFinder : MonoBehaviour
{
    [Header("Search Input")]
    [SerializeField] private string searchIdInput = "";
    [SerializeField] private string searchNameInput = "";

    [Header("Search Result")]
    [SerializeField] private string foundType = "";
    [SerializeField, TextArea(3, 5)] private string resultMessage = "";

    [Header("State")]
    [SerializeField] private bool fieldsLocked;
    [SerializeField] private bool foundMatch;

    private AnomalySpawnPoint foundPoint;

    public string SearchIdInput
    {
        get => searchIdInput;
        set => searchIdInput = value ?? string.Empty;
    }

    public string SearchNameInput
    {
        get => searchNameInput;
        set => searchNameInput = value ?? string.Empty;
    }

    public string FoundType => foundType;
    public string ResultMessage => resultMessage;
    public bool FieldsLocked => fieldsLocked;
    public bool FoundMatch => foundMatch && foundPoint != null;
    public AnomalySpawnPoint FoundPoint => foundPoint;

    public bool CanSearch()
    {
        return !fieldsLocked && (!string.IsNullOrWhiteSpace(searchIdInput) || !string.IsNullOrWhiteSpace(searchNameInput));
    }

    public bool CanGo()
    {
        return fieldsLocked && foundMatch && foundPoint != null;
    }

    public void SetSearchLocked(bool locked)
    {
        fieldsLocked = locked;
    }

    public void SetSearchResult(AnomalySpawnPoint point)
    {
        foundPoint = point;
        foundMatch = point != null;

        if (point == null)
        {
            foundType = string.Empty;
            resultMessage = "Not Found";
            return;
        }

        foundType = point.GetAnomalyTypeName();
        resultMessage = $"Found\nID: {point.GetAnomalyID()}\nName: {point.GetAnomalyName()}\nType: {point.GetAnomalyTypeName()}";
    }

    public void ClearSearch()
    {
        searchIdInput = string.Empty;
        searchNameInput = string.Empty;
        foundType = string.Empty;
        resultMessage = string.Empty;
        fieldsLocked = false;
        foundMatch = false;
        foundPoint = null;
    }
}

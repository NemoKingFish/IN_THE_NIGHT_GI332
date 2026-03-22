using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChecklistUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChecklistManager checklistManager;
    [SerializeField] private GameRoundManager gameRoundManager;

    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private Transform contentParent;
    [SerializeField] private ChecklistItemUI itemPrefab;

    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI resultText;

    private readonly List<ChecklistItemUI> spawnedItems = new List<ChecklistItemUI>();

    private void Start()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        BuildUI();

        if (submitButton != null)
            submitButton.onClick.AddListener(OnClickSubmit);

        if (checklistManager != null)
        {
            checklistManager.checkedMask.OnValueChanged += OnCheckedMaskChanged;
            checklistManager.matchResult.OnValueChanged += OnMatchResultChanged;
        }

        if (gameRoundManager != null)
        {
            gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;
        }

        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
    }

    private void OnDestroy()
    {
        if (checklistManager != null)
        {
            checklistManager.checkedMask.OnValueChanged -= OnCheckedMaskChanged;
            checklistManager.matchResult.OnValueChanged -= OnMatchResultChanged;
        }

        if (gameRoundManager != null)
        {
            gameRoundManager.gamePhase.OnValueChanged -= OnGamePhaseChanged;
        }

        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnClickSubmit);
    }

    private void BuildUI()
    {
        if (contentParent == null)
        {
            Debug.LogError("ChecklistUI: Content Parent is missing.");
            return;
        }

        if (itemPrefab == null)
        {
            Debug.LogError("ChecklistUI: Item Prefab is missing.");
            return;
        }

        if (checklistManager == null)
        {
            Debug.LogError("ChecklistUI: ChecklistManager is missing.");
            return;
        }

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        spawnedItems.Clear();

        int count = checklistManager.GetItemCount();

        for (int i = 0; i < count; i++)
        {
            ChecklistItemUI item = Instantiate(itemPrefab, contentParent);
            item.Setup(this, i, checklistManager.GetItemName(i));
            spawnedItems.Add(item);
        }
    }

    private void RefreshAllItems()
    {
        if (checklistManager == null) return;

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            bool checkedState = checklistManager.IsItemChecked(i);
            spawnedItems[i].SetCheckedWithoutNotify(checkedState);
        }
    }

    private void RefreshUIState()
    {
        bool canInteract = CanInteractChecklist();

        foreach (var item in spawnedItems)
        {
            item.SetInteractable(canInteract);
        }

        if (submitButton != null)
            submitButton.interactable = canInteract;
    }

    private void RefreshResultText()
    {
        if (resultText == null) return;
        if (checklistManager == null || gameRoundManager == null)
        {
            resultText.text = "Missing References";
            return;
        }

        var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;
        var result = (ChecklistManager.MatchResult)checklistManager.matchResult.Value;

        switch (phase)
        {
            case GameRoundManager.GamePhase.Memorize:
                resultText.text = "Memorize the room...";
                break;

            case GameRoundManager.GamePhase.Investigation:
                resultText.text = "Find the anomalies.";
                break;

            case GameRoundManager.GamePhase.RoundTransition:
                if (result == ChecklistManager.MatchResult.Win)
                    resultText.text = "Correct! Next round...";
                else if (result == ChecklistManager.MatchResult.Lose)
                    resultText.text = "Wrong! Restarting...";
                else
                    resultText.text = "Preparing next round...";
                break;

            case GameRoundManager.GamePhase.Victory:
                resultText.text = "You Win";
                break;
        }
    }

    private bool CanInteractChecklist()
    {
        if (checklistManager == null || gameRoundManager == null)
            return false;

        if (!checklistManager.IsPlaying())
            return false;

        return gameRoundManager.IsInvestigationPhase();
    }

    private void OnCheckedMaskChanged(ulong oldValue, ulong newValue)
    {
        RefreshAllItems();
    }

    private void OnMatchResultChanged(int oldValue, int newValue)
    {
        RefreshUIState();
        RefreshResultText();
    }

    private void OnGamePhaseChanged(int oldValue, int newValue)
    {
        RefreshUIState();
        RefreshResultText();
    }

    public void OnToggleChanged(int index, bool value)
    {
        if (!CanInteractChecklist()) return;
        if (checklistManager == null) return;

        checklistManager.ToggleChecklistItemServerRpc(index, value);
    }

    private void OnClickSubmit()
    {
        if (!CanInteractChecklist()) return;
        if (gameRoundManager == null) return;

        gameRoundManager.SubmitChecklistServerRpc();
    }

    public void OpenWindow()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(true);
    }

    public void CloseWindow()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);
    }

    public void ToggleWindow()
    {
        if (checklistWindow == null) return;
        checklistWindow.SetActive(!checklistWindow.activeSelf);
    }
}
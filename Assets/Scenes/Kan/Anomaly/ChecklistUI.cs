using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChecklistUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private ChecklistManager checklistManager;
    [SerializeField] private GameRoundManager gameRoundManager;

    [Header("UI References")]
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private Transform contentParent;
    [SerializeField] private ChecklistItemUI itemPrefab;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private readonly List<ChecklistItemUI> spawnedItems = new List<ChecklistItemUI>();
    private bool initialized;

    private IEnumerator Start()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        // ก่อน Host / Join: เมาส์ไม่ล็อก
        UnlockCursor();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        while (checklistManager == null || gameRoundManager == null)
        {
            if (checklistManager == null)
                checklistManager = FindFirstObjectByType<ChecklistManager>();

            if (gameRoundManager == null)
                gameRoundManager = FindFirstObjectByType<GameRoundManager>();

            yield return null;
        }

        BuildUI();

        if (submitButton != null)
            submitButton.onClick.AddListener(OnClickSubmit);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseWindow);

        checklistManager.checkedMask.OnValueChanged += OnCheckedMaskChanged;
        checklistManager.matchResult.OnValueChanged += OnMatchResultChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;

        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
        RefreshCursorState();

        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        // ยังไม่ Host / Join -> ห้ามเปิด Tab
        if (!IsConnectedToSession()) return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleWindow();
        }
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

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseWindow);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        UnlockCursor();
    }

    private void OnClientConnected(ulong clientId)
    {
        RefreshCursorState();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        RefreshCursorState();
    }

    private bool IsConnectedToSession()
    {
        if (NetworkManager.Singleton == null) return false;

        return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient;
    }

    private void RefreshCursorState()
    {
        // ก่อน Host / Join -> เมาส์ไม่ล็อก
        if (!IsConnectedToSession())
        {
            UnlockCursor();
            return;
        }

        // หลัง Host / Join:
        // ถ้าเมนูเปิด -> ไม่ล็อก
        // ถ้าเมนูปิด -> ล็อก
        if (checklistWindow != null && checklistWindow.activeSelf)
            UnlockCursor();
        else
            LockCursor();
    }

    private void BuildUI()
    {
        if (contentParent == null || itemPrefab == null || checklistManager == null)
        {
            Debug.LogError("ChecklistUI: Missing references.");
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
        if (resultText == null || checklistManager == null || gameRoundManager == null)
            return;

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
        if (!IsConnectedToSession()) return false;
        if (checklistManager == null || gameRoundManager == null) return false;
        if (!checklistManager.IsPlaying()) return false;

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
        if (!IsConnectedToSession()) return;
        if (checklistWindow == null) return;

        checklistWindow.SetActive(true);
        UnlockCursor();
        RefreshUIState();
    }

    public void CloseWindow()
    {
        if (checklistWindow == null) return;

        checklistWindow.SetActive(false);
        RefreshCursorState();
    }

    public void ToggleWindow()
    {
        if (!IsConnectedToSession()) return;
        if (checklistWindow == null) return;

        if (checklistWindow.activeSelf)
            CloseWindow();
        else
            OpenWindow();
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
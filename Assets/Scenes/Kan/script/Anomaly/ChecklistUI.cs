using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChecklistUI : MonoBehaviourPunCallbacks
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
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI submitButtonText;
    [SerializeField] private ScrollRect checklistScrollRect;
    [SerializeField] private Button cancelSubmitButton;
    [SerializeField] private TextMeshProUGUI cancelSubmitButtonText;

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

        while (checklistManager == null || gameRoundManager == null)
        {
            if (checklistManager == null)
                checklistManager = FindFirstObjectByType<ChecklistManager>();

            if (gameRoundManager == null)
                gameRoundManager = FindFirstObjectByType<GameRoundManager>();

            yield return null;
        }

        ResolveOptionalReferences();
        ConfigureWindowLayout();
        BuildUI();

        if (submitButton != null)
            submitButton.onClick.AddListener(OnClickSubmit);

        if (cancelSubmitButton != null)
            cancelSubmitButton.onClick.AddListener(OnClickCancelSubmit);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseWindow);

        checklistManager.checkedMask.OnValueChanged += OnCheckedMaskChanged;
        checklistManager.matchResult.OnValueChanged += OnMatchResultChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;

        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
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

        RefreshResultText();
        RefreshSubmitPresentation();
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

        if (cancelSubmitButton != null)
            cancelSubmitButton.onClick.RemoveListener(OnClickCancelSubmit);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseWindow);
    }

    public override void OnJoinedRoom()
    {
        RefreshCursorState();
        RefreshUIState();
        RefreshSubmitPresentation();
    }

    public override void OnLeftRoom()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        RefreshCursorState();
    }

    private bool IsConnectedToSession()
    {
        return PhotonNetwork.InRoom;
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

    private void ResolveOptionalReferences()
    {
        if (checklistWindow != null && titleText == null)
        {
            titleText = FindNamedChildComponent<TextMeshProUGUI>(checklistWindow.transform, "TitleText");
        }

        if (submitButton != null && submitButtonText == null)
        {
            submitButtonText = submitButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (contentParent != null && checklistScrollRect == null)
        {
            checklistScrollRect = contentParent.GetComponentInParent<ScrollRect>();
        }

        if (cancelSubmitButton == null && submitButton != null)
        {
            cancelSubmitButton = CreateCancelButton(submitButton);
        }

        if (cancelSubmitButton != null && cancelSubmitButtonText == null)
        {
            cancelSubmitButtonText = cancelSubmitButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void ConfigureWindowLayout()
    {
        if (titleText != null)
        {
            titleText.text = "Checklist";
            titleText.fontSize = 58f;
            titleText.alignment = TextAlignmentOptions.Center;

            if (titleText.rectTransform != null)
            {
                titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
                titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
                titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
        }

        if (checklistScrollRect != null)
        {
            checklistScrollRect.horizontal = false;
            checklistScrollRect.vertical = true;
            checklistScrollRect.movementType = ScrollRect.MovementType.Clamped;
            checklistScrollRect.scrollSensitivity = 28f;
        }

        if (contentParent is RectTransform contentRect)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
        }

        var layoutGroup = contentParent != null ? contentParent.GetComponent<VerticalLayoutGroup>() : null;
        if (layoutGroup != null)
        {
            layoutGroup.padding.left = 18;
            layoutGroup.padding.right = 18;
            layoutGroup.padding.top = 14;
            layoutGroup.padding.bottom = 14;
            layoutGroup.spacing = 14f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
        }

        ConfigureActionButton(submitButton, 74f);
        ConfigureActionButton(cancelSubmitButton, 18f);

        if (submitButtonText != null)
        {
            submitButtonText.fontSize = 24f;
            submitButtonText.alignment = TextAlignmentOptions.Center;
            submitButtonText.text = "submit";
        }

        if (cancelSubmitButtonText != null)
        {
            cancelSubmitButtonText.fontSize = 24f;
            cancelSubmitButtonText.alignment = TextAlignmentOptions.Center;
            cancelSubmitButtonText.text = "cancel";
        }

        if (resultText != null)
        {
            resultText.fontSize = 34f;
            resultText.alignment = TextAlignmentOptions.Center;
        }
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
                resultText.text = $"Memorize ({gameRoundManager.GetMemorizeSecondsRemaining()})";
                break;

            case GameRoundManager.GamePhase.Investigation:
                var submittedCount = gameRoundManager.GetSubmittedPlayerCount();
                var expectedPlayers = gameRoundManager.GetExpectedSubmitterCount();
                resultText.text = submittedCount > 0
                    ? $"wait.... ({submittedCount}/{expectedPlayers})"
                    : "Choose anomalies";
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
        if (gameRoundManager.HasLocalPlayerSubmitted()) return false;

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
        RefreshSubmitPresentation();
    }

    private void OnGamePhaseChanged(int oldValue, int newValue)
    {
        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
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
        RefreshUIState();
        RefreshSubmitPresentation();
    }

    private void OnClickCancelSubmit()
    {
        if (gameRoundManager == null) return;

        gameRoundManager.CancelChecklistSubmission();
        RefreshUIState();
        RefreshSubmitPresentation();
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

    private void RefreshSubmitPresentation()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        if (submitButton != null)
        {
            submitButton.interactable = CanInteractChecklist();
        }

        if (cancelSubmitButton != null)
        {
            cancelSubmitButton.gameObject.SetActive(gameRoundManager.IsInvestigationPhase() && gameRoundManager.HasLocalPlayerSubmitted());
        }

        if (submitButtonText == null)
        {
            return;
        }

        var submittedCount = gameRoundManager.GetSubmittedPlayerCount();
        var expectedPlayers = gameRoundManager.GetExpectedSubmitterCount();
        var isWaitingForPlayers = gameRoundManager.IsInvestigationPhase() && gameRoundManager.HasAnyPlayerSubmitted();

        submitButtonText.text = isWaitingForPlayers
            ? $"wait.... ({submittedCount}/{expectedPlayers})"
            : "submit";

        if (cancelSubmitButtonText != null)
        {
            cancelSubmitButtonText.text = "cancel";
        }
    }

    private static T FindNamedChildComponent<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.GetComponent<T>();
            }
        }

        return null;
    }

    private static void ConfigureActionButton(Button button, float bottomOffset)
    {
        if (button == null || button.transform is not RectTransform rectTransform)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.anchoredPosition = new Vector2(-28f, bottomOffset);
        rectTransform.sizeDelta = new Vector2(190f, 48f);
    }

    private static Button CreateCancelButton(Button submitSourceButton)
    {
        if (submitSourceButton == null || submitSourceButton.transform.parent == null)
        {
            return null;
        }

        var cancelObject = Instantiate(submitSourceButton.gameObject, submitSourceButton.transform.parent);
        cancelObject.name = "CancelSubmitButton";
        cancelObject.SetActive(false);

        var cancelButton = cancelObject.GetComponent<Button>();
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
        }

        return cancelButton;
    }
}

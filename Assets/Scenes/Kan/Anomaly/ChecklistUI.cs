using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private GameObject checklistSelectionArea;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private readonly List<ChecklistItemUI> spawnedItems = new List<ChecklistItemUI>();
    private bool initialized;
    private bool localSubmitLock;

    private IEnumerator Start()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        UnlockCursor();

        while (checklistManager == null || gameRoundManager == null)
        {
            if (checklistManager == null)
            {
                checklistManager = FindFirstObjectByType<ChecklistManager>();
            }

            if (gameRoundManager == null)
            {
                gameRoundManager = FindFirstObjectByType<GameRoundManager>();
            }

            yield return null;
        }

        ResolveOptionalReferences();
        ConfigureWindowLayout();
        BuildUI();

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnClickSubmit);
        }

        if (cancelSubmitButton != null)
        {
            cancelSubmitButton.onClick.AddListener(OnClickCancelSubmit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseWindow);
        }

        checklistManager.checkedMask.OnValueChanged += OnCheckedMaskChanged;
        checklistManager.matchResult.OnValueChanged += OnMatchResultChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;

        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
        RefreshCursorState();

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !IsConnectedToSession())
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleWindow();
        }

        RefreshResultText();
        RefreshUIState();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
        EnforceVerticalOnlyScroll();

        if (!gameRoundManager.IsInvestigationPhase())
        {
            localSubmitLock = false;
        }
        else if (!gameRoundManager.HasLocalPlayerSubmitted() && localSubmitLock && !gameRoundManager.HasAnyPlayerSubmitted())
        {
            localSubmitLock = false;
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
        {
            submitButton.onClick.RemoveListener(OnClickSubmit);
        }

        if (cancelSubmitButton != null)
        {
            cancelSubmitButton.onClick.RemoveListener(OnClickCancelSubmit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWindow);
        }
    }

    public override void OnJoinedRoom()
    {
        localSubmitLock = false;
        RefreshCursorState();
        RefreshUIState();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
    }

    public override void OnLeftRoom()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }

        localSubmitLock = false;
        RefreshCursorState();
    }

    private static bool IsConnectedToSession()
    {
        return PhotonNetwork.InRoom;
    }

    private void RefreshCursorState()
    {
        if (!IsConnectedToSession())
        {
            UnlockCursor();
            return;
        }

        if (checklistWindow != null && checklistWindow.activeSelf)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
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

        if (checklistSelectionArea == null)
        {
            if (checklistScrollRect != null)
            {
                checklistSelectionArea = checklistScrollRect.gameObject;
            }
            else if (contentParent != null)
            {
                checklistSelectionArea = contentParent.gameObject;
            }
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
            titleText.text = "Check List";
            titleText.fontSize = 58f;
            titleText.alignment = TextAlignmentOptions.Center;

            if (titleText.rectTransform != null)
            {
                titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
                titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
                titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
        }

        if (resultText != null)
        {
            resultText.fontSize = 34f;
            resultText.alignment = TextAlignmentOptions.Center;

            if (resultText.rectTransform != null)
            {
                resultText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                resultText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                resultText.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
        }

        if (checklistScrollRect != null)
        {
            checklistScrollRect.horizontal = false;
            checklistScrollRect.vertical = true;
            checklistScrollRect.movementType = ScrollRect.MovementType.Clamped;
            checklistScrollRect.scrollSensitivity = 28f;
            checklistScrollRect.horizontalNormalizedPosition = 0f;
            checklistScrollRect.verticalNormalizedPosition = 1f;

            if (checklistScrollRect.horizontalScrollbar != null)
            {
                checklistScrollRect.horizontalScrollbar.gameObject.SetActive(false);
                checklistScrollRect.horizontalScrollbar = null;
            }

            if (checklistScrollRect.verticalScrollbar != null)
            {
                checklistScrollRect.verticalScrollbar.gameObject.SetActive(false);
                checklistScrollRect.verticalScrollbar = null;
            }

            DisableEmbeddedScrollbarVisuals();
        }

        if (contentParent is RectTransform contentRect)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
            contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
            contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);
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

        EnforceVerticalOnlyScroll();
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

        var count = checklistManager.GetItemCount();
        for (var i = 0; i < count; i++)
        {
            var item = Instantiate(itemPrefab, contentParent);
            item.Setup(this, i, checklistManager.GetItemName(i));
            spawnedItems.Add(item);
        }

        EnforceVerticalOnlyScroll();
    }

    private void EnforceVerticalOnlyScroll()
    {
        if (checklistScrollRect != null)
        {
            checklistScrollRect.horizontal = false;
            checklistScrollRect.vertical = true;
            checklistScrollRect.horizontalNormalizedPosition = 0f;

            if (checklistScrollRect.horizontalScrollbar != null)
            {
                checklistScrollRect.horizontalScrollbar.gameObject.SetActive(false);
                checklistScrollRect.horizontalScrollbar = null;
            }

            if (checklistScrollRect.verticalScrollbar != null)
            {
                checklistScrollRect.verticalScrollbar.gameObject.SetActive(false);
                checklistScrollRect.verticalScrollbar = null;
            }

            var scrollbars = checklistScrollRect.GetComponentsInChildren<Scrollbar>(true);
            for (var i = 0; i < scrollbars.Length; i++)
            {
                if (scrollbars[i] == null)
                {
                    continue;
                }

                scrollbars[i].gameObject.SetActive(false);
            }

            DisableEmbeddedScrollbarVisuals();
        }

        if (contentParent is RectTransform contentRect)
        {
            var anchoredPosition = contentRect.anchoredPosition;
            if (Mathf.Abs(anchoredPosition.x) > 0.01f)
            {
                anchoredPosition.x = 0f;
                contentRect.anchoredPosition = anchoredPosition;
            }

            var offsetMin = contentRect.offsetMin;
            var offsetMax = contentRect.offsetMax;
            if (Mathf.Abs(offsetMin.x) > 0.01f || Mathf.Abs(offsetMax.x) > 0.01f)
            {
                contentRect.offsetMin = new Vector2(0f, offsetMin.y);
                contentRect.offsetMax = new Vector2(0f, offsetMax.y);
            }
        }
    }

    private void RefreshAllItems()
    {
        if (checklistManager == null)
        {
            return;
        }

        for (var i = 0; i < spawnedItems.Count; i++)
        {
            spawnedItems[i].SetCheckedWithoutNotify(checklistManager.IsItemChecked(i));
        }
    }

    private void RefreshUIState()
    {
        var canInteract = CanInteractChecklist();

        foreach (var item in spawnedItems)
        {
            item.SetInteractable(canInteract);
        }

        if (submitButton != null)
        {
            submitButton.interactable = canInteract;
        }

        RefreshSelectionPresentation();
    }

    private void RefreshResultText()
    {
        if (resultText == null || checklistManager == null || gameRoundManager == null)
        {
            return;
        }

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
                    : "Choose\nanomalies";
                break;

            case GameRoundManager.GamePhase.RoundTransition:
                if (result == ChecklistManager.MatchResult.Win)
                {
                    resultText.text = "Correct! Next round...";
                }
                else if (result == ChecklistManager.MatchResult.Lose)
                {
                    resultText.text = "Wrong! Restarting...";
                }
                else
                {
                    resultText.text = "Preparing next round...";
                }
                break;

            case GameRoundManager.GamePhase.Victory:
                resultText.text = "You Win";
                break;
        }
    }

    private bool CanInteractChecklist()
    {
        if (!IsConnectedToSession() || checklistManager == null || gameRoundManager == null)
        {
            return false;
        }

        if (!checklistManager.IsPlaying() || !gameRoundManager.IsInvestigationPhase())
        {
            return false;
        }

        return !localSubmitLock && !gameRoundManager.HasLocalPlayerSubmitted();
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
        RefreshSelectionPresentation();
    }

    private void OnGamePhaseChanged(int oldValue, int newValue)
    {
        if ((GameRoundManager.GamePhase)newValue != GameRoundManager.GamePhase.Investigation)
        {
            localSubmitLock = false;
        }

        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
    }

    public void OnToggleChanged(int index, bool value)
    {
        if (!CanInteractChecklist() || checklistManager == null)
        {
            return;
        }

        checklistManager.ToggleChecklistItemServerRpc(index, value);
    }

    private void OnClickSubmit()
    {
        if (!CanInteractChecklist() || gameRoundManager == null)
        {
            return;
        }

        localSubmitLock = true;
        gameRoundManager.SubmitChecklistServerRpc();
        RefreshUIState();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
    }

    private void OnClickCancelSubmit()
    {
        if (gameRoundManager == null)
        {
            return;
        }

        localSubmitLock = false;
        gameRoundManager.CancelChecklistSubmission();
        RefreshUIState();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
    }

    public void OpenWindow()
    {
        if (!IsConnectedToSession() || checklistWindow == null)
        {
            return;
        }

        checklistWindow.SetActive(true);
        UnlockCursor();
        RefreshUIState();
        RefreshSelectionPresentation();
    }

    public void CloseWindow()
    {
        if (checklistWindow == null)
        {
            return;
        }

        checklistWindow.SetActive(false);
        RefreshCursorState();
    }

    public void ToggleWindow()
    {
        if (!IsConnectedToSession() || checklistWindow == null)
        {
            return;
        }

        if (checklistWindow.activeSelf)
        {
            CloseWindow();
        }
        else
        {
            OpenWindow();
        }
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void LockCursor()
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

        if (submitButtonText != null)
        {
            var submittedCount = gameRoundManager.GetSubmittedPlayerCount();
            var expectedPlayers = gameRoundManager.GetExpectedSubmitterCount();
            var isWaitingForPlayers = gameRoundManager.IsInvestigationPhase() && gameRoundManager.HasAnyPlayerSubmitted();

            submitButtonText.text = isWaitingForPlayers
                ? $"wait.... ({submittedCount}/{expectedPlayers})"
                : "submit";
        }

        if (cancelSubmitButtonText != null)
        {
            cancelSubmitButtonText.text = "cancel";
        }
    }

    private void RefreshSelectionPresentation()
    {
        if (checklistSelectionArea == null || gameRoundManager == null)
        {
            return;
        }

        var shouldShowSelectionArea = gameRoundManager.IsInvestigationPhase() && !gameRoundManager.HasLocalPlayerSubmitted();
        if (checklistSelectionArea.activeSelf != shouldShowSelectionArea)
        {
            checklistSelectionArea.SetActive(shouldShowSelectionArea);
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

    private void DisableEmbeddedScrollbarVisuals()
    {
        if (checklistScrollRect == null)
        {
            return;
        }

        foreach (var scrollbar in checklistScrollRect.GetComponentsInChildren<Scrollbar>(true))
        {
            if (scrollbar == null)
            {
                continue;
            }

            scrollbar.enabled = false;

            var selectable = scrollbar as Selectable;
            if (selectable != null)
            {
                selectable.interactable = false;
            }

            var raycastHandler = scrollbar.GetComponent<GraphicRaycaster>();
            if (raycastHandler != null)
            {
                raycastHandler.enabled = false;
            }

            foreach (var graphic in scrollbar.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                {
                    continue;
                }

                graphic.enabled = false;
                graphic.raycastTarget = false;
            }

            foreach (var trigger in scrollbar.GetComponentsInChildren<EventTrigger>(true))
            {
                if (trigger != null)
                {
                    trigger.enabled = false;
                }
            }

            scrollbar.gameObject.SetActive(false);
        }
    }
}

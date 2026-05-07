using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChecklistUI : MonoBehaviourPunCallbacks
{
    private const string FallbackChecklistItemPrefabPath = "Assets/Scenes/Kan/CheckItem.prefab";
    private const float ChecklistItemHeight = 104f;
    private const float ChecklistItemSpacing = 14f;

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
    private int observedChecklistRevision = -1;

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
        EnsureEventSystemExists();
        ConfigureChecklistListLayout();
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
        checklistManager.AnomalyDataChanged += OnAnomalyDataChanged;
        gameRoundManager.gamePhase.OnValueChanged += OnGamePhaseChanged;

        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
        RefreshCursorState();
        HideCenterStatusText();

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !IsSessionReady())
        {
            return;
        }

        if (checklistManager != null && observedChecklistRevision != checklistManager.DataRevision)
        {
            RebuildChecklistItems();
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (GameplayPauseMenu.IsLocalPauseMenuOpen)
            {
                return;
            }

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
            checklistManager.AnomalyDataChanged -= OnAnomalyDataChanged;
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

    private bool IsSessionReady()
    {
        return PhotonNetwork.InRoom || gameRoundManager != null;
    }

    private void RefreshCursorState()
    {
        if (!IsSessionReady())
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
        if (checklistScrollRect == null)
        {
            if (checklistWindow != null)
            {
                checklistScrollRect = checklistWindow.GetComponentInChildren<ScrollRect>(true);
            }

            if (checklistScrollRect == null)
            {
                checklistScrollRect = GetComponentInChildren<ScrollRect>(true);
            }
        }

        if (contentParent == null && checklistScrollRect != null)
        {
            contentParent = checklistScrollRect.content;
        }

        if (contentParent == null)
        {
            var searchRoot = checklistWindow != null ? checklistWindow.transform : transform;
            contentParent = FindNamedChildComponent<RectTransform>(searchRoot, "Content");
        }

        if (itemPrefab == null)
        {
            itemPrefab = FindChecklistItemTemplateInScene();
        }

        if (itemPrefab == null)
        {
            itemPrefab = LoadFallbackChecklistItemPrefab();
        }

        if (itemPrefab == null)
        {
            itemPrefab = CreateRuntimeChecklistItemTemplate();
        }

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

    private void ConfigureChecklistListLayout()
    {
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
            ConfigureScrollViewBounds();
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
            layoutGroup.spacing = ChecklistItemSpacing;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
        }

    }

    private void BuildUI()
    {
        if (contentParent == null || itemPrefab == null || checklistManager == null)
        {
            Debug.LogError($"ChecklistUI: Missing references. contentParent={contentParent != null}, itemPrefab={itemPrefab != null}, checklistManager={checklistManager != null}");
            return;
        }

        checklistManager.RefreshAnomalyData();

        var templateTransform = itemPrefab.transform;
        var templateIsSceneChild = templateTransform != null && templateTransform.IsChildOf(contentParent);

        foreach (Transform child in contentParent)
        {
            if (templateIsSceneChild && child == templateTransform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }

        spawnedItems.Clear();

        var count = checklistManager.GetItemCount();
        ResizeContentForItemCount(count);

        for (var i = 0; i < count; i++)
        {
            var item = Instantiate(itemPrefab, contentParent);
            item.gameObject.SetActive(true);
            item.Setup(this, i, checklistManager.GetItemName(i));
            spawnedItems.Add(item);
        }

        EnforceVerticalOnlyScroll();
        observedChecklistRevision = checklistManager.DataRevision;
    }

    private void RebuildChecklistItems()
    {
        BuildUI();
        RefreshAllItems();
        RefreshUIState();
        RefreshResultText();
        RefreshSubmitPresentation();
        RefreshSelectionPresentation();
    }

    private void ConfigureScrollViewBounds()
    {
        if (checklistScrollRect == null || checklistScrollRect.transform is not RectTransform scrollRectTransform)
        {
            return;
        }

        scrollRectTransform.anchorMin = new Vector2(0.18f, 0.24f);
        scrollRectTransform.anchorMax = new Vector2(0.82f, 0.74f);
        scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollRectTransform.anchoredPosition = Vector2.zero;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        if (checklistScrollRect.viewport != null)
        {
            var viewportRect = checklistScrollRect.viewport;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = Vector2.zero;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.sizeDelta = Vector2.zero;
        }

        if (checklistScrollRect.content == null && contentParent is RectTransform contentRect)
        {
            checklistScrollRect.content = contentRect;
        }
    }

    private void ResizeContentForItemCount(int itemCount)
    {
        if (contentParent is not RectTransform contentRect)
        {
            return;
        }

        var verticalLayout = contentParent.GetComponent<VerticalLayoutGroup>();
        var topPadding = verticalLayout != null ? verticalLayout.padding.top : 14;
        var bottomPadding = verticalLayout != null ? verticalLayout.padding.bottom : 14;
        var spacing = verticalLayout != null ? verticalLayout.spacing : ChecklistItemSpacing;
        var desiredHeight = topPadding + bottomPadding + (itemCount * ChecklistItemHeight);

        if (itemCount > 1)
        {
            desiredHeight += (itemCount - 1) * spacing;
        }

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
        contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);
        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(desiredHeight, ChecklistItemHeight));
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
                resultText.text = gameRoundManager.HasMemorizeTimer()
                    ? $"Memorize ({gameRoundManager.GetMemorizeSecondsRemaining()})"
                    : "Memorize";
                break;

            case GameRoundManager.GamePhase.SpawnLockdown:
                resultText.text = "Return to spawn...";
                break;

            case GameRoundManager.GamePhase.Investigation:
                var submittedCount = gameRoundManager.GetSubmittedPlayerCount();
                var expectedPlayers = gameRoundManager.GetExpectedSubmitterCount();
                resultText.text = submittedCount > 0
                    ? $"wait.... ({submittedCount}/{expectedPlayers})"
                    : $"Choose\nanomalies ({gameRoundManager.GetInvestigationSecondsRemaining()})";
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

    private void HideCenterStatusText()
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }
    }

    private bool CanInteractChecklist()
    {
        if (!IsSessionReady() || checklistManager == null || gameRoundManager == null)
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
        AutoCloseWindowWhenRoundResolved();
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
        AutoCloseWindowWhenRoundResolved();
    }

    private void OnAnomalyDataChanged()
    {
        if (!initialized)
        {
            return;
        }

        RebuildChecklistItems();
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
        if (!IsSessionReady() || checklistWindow == null)
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
        if (!IsSessionReady() || checklistWindow == null)
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

    private void AutoCloseWindowWhenRoundResolved()
    {
        if (checklistWindow == null || !checklistWindow.activeSelf || checklistManager == null || gameRoundManager == null)
        {
            return;
        }

        var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;
        var result = (ChecklistManager.MatchResult)checklistManager.matchResult.Value;
        var shouldCloseForResult = (phase == GameRoundManager.GamePhase.RoundTransition || phase == GameRoundManager.GamePhase.Victory)
            && result != ChecklistManager.MatchResult.Playing;

        if (shouldCloseForResult)
        {
            CloseWindow();
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

        var phase = (GameRoundManager.GamePhase)gameRoundManager.gamePhase.Value;
        var shouldShowSelectionArea = phase == GameRoundManager.GamePhase.Memorize
            || phase == GameRoundManager.GamePhase.Investigation;

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

    private static void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private ChecklistItemUI FindChecklistItemTemplateInScene()
    {
        var searchRoot = checklistWindow != null ? checklistWindow.transform : transform;
        var localItems = searchRoot.GetComponentsInChildren<ChecklistItemUI>(true);
        for (var i = 0; i < localItems.Length; i++)
        {
            if (localItems[i] != null)
            {
                return localItems[i];
            }
        }

        var sceneItems = FindObjectsByType<ChecklistItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < sceneItems.Length; i++)
        {
            if (sceneItems[i] != null)
            {
                return sceneItems[i];
            }
        }

        return null;
    }

    private static ChecklistItemUI LoadFallbackChecklistItemPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<ChecklistItemUI>(FallbackChecklistItemPrefabPath);
#else
        return null;
#endif
    }

    private ChecklistItemUI CreateRuntimeChecklistItemTemplate()
    {
        if (contentParent == null)
        {
            return null;
        }

        var templateObject = new GameObject(
            "RuntimeChecklistItemTemplate",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Toggle),
            typeof(Outline),
            typeof(LayoutElement),
            typeof(ChecklistItemUI));

        templateObject.transform.SetParent(contentParent, false);
        templateObject.SetActive(false);

        var rectTransform = templateObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(0f, 104f);

        var image = templateObject.GetComponent<Image>();
        image.color = new Color(0.76f, 0.76f, 0.76f, 0.96f);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(templateObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(22f, 14f);
        labelRect.offsetMax = new Vector2(-22f, -14f);

        var labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = "Anomaly";
        labelText.fontSize = 34f;
        labelText.color = Color.black;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        return templateObject.GetComponent<ChecklistItemUI>();
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

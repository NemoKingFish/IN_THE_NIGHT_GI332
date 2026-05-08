using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChecklistItemUI : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Outline selectionOutline;

    private ChecklistUI ownerUI;
    private int itemIndex;
    private bool suppressCallback;
    private bool visualsCached;
    private Color defaultOutlineColor = Color.black;

    public void Setup(ChecklistUI ui, int index, string label)
    {
        ownerUI = ui;
        itemIndex = index;
        EnsurePresentation();

        if (labelText != null)
        {
            labelText.text = FormatLabel(label);
            EnsureReadableLabelPresentation();
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonPressed);
        }

        RefreshVisualState(toggle != null && toggle.isOn);
    }

    public void SetCheckedWithoutNotify(bool value)
    {
        if (toggle == null)
        {
            RefreshVisualState(value);
            return;
        }

        suppressCallback = true;
        toggle.isOn = value;
        suppressCallback = false;
        RefreshVisualState(value);
    }

    public void SetInteractable(bool value)
    {
        if (toggle != null)
        {
            toggle.interactable = value;
        }

        if (button != null)
        {
            button.interactable = value;
        }

        RefreshVisualState(toggle != null && toggle.isOn);
    }

    private void OnToggleValueChanged(bool value)
    {
        RefreshVisualState(value);

        if (suppressCallback) return;
        if (ownerUI == null) return;

        ownerUI.OnToggleChanged(itemIndex, value);
    }

    private void OnButtonPressed()
    {
        if (toggle == null || !toggle.interactable)
        {
            return;
        }

        toggle.isOn = !toggle.isOn;
    }

    private void EnsurePresentation()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }
        }

        if (selectionOutline == null)
        {
            selectionOutline = GetComponent<Outline>();
            if (selectionOutline == null)
            {
                selectionOutline = gameObject.AddComponent<Outline>();
            }
        }

        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        CacheDefaultVisuals();

        var layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = 104f;
            layoutElement.minHeight = 96f;
            layoutElement.flexibleHeight = 0f;
        }

        if (transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(0f, 104f);
        }

        if (labelText != null)
        {
            labelText.raycastTarget = false;
            labelText.enableWordWrapping = true;
            labelText.enabled = true;
        }

        if (toggle != null)
        {
            if (toggle.targetGraphic == null && backgroundImage != null)
            {
                toggle.targetGraphic = backgroundImage;
            }
        }

        if (button.targetGraphic == null && backgroundImage != null)
        {
            button.targetGraphic = backgroundImage;
        }

        selectionOutline.effectDistance = new Vector2(3f, -3f);
        selectionOutline.useGraphicAlpha = false;
    }

    private void CacheDefaultVisuals()
    {
        if (visualsCached)
        {
            return;
        }

        if (selectionOutline != null)
        {
            defaultOutlineColor = selectionOutline.effectColor;
        }

        visualsCached = true;
    }

    private void EnsureReadableLabelPresentation()
    {
        if (labelText == null)
        {
            return;
        }

        if (labelText.color.a <= 0f)
        {
            labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        }
    }

    private void RefreshVisualState(bool isSelected)
    {
        if (selectionOutline != null)
        {
            selectionOutline.effectColor = isSelected
                ? new Color(0.08f, 0.92f, 0.18f, 1f)
                : defaultOutlineColor;
        }
    }

    private static string FormatLabel(string rawLabel)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return "Unknown";
        }

        var builder = new System.Text.StringBuilder(rawLabel.Length + 8);
        for (var i = 0; i < rawLabel.Length; i++)
        {
            var current = rawLabel[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(rawLabel[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}

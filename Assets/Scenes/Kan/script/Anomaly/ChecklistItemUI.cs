using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LegacyText = UnityEngine.UI.Text;

public class ChecklistItemUI : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Graphic checkmarkGraphic;
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private HorizontalLayoutGroup layoutGroup;
    [SerializeField] private Outline outline;
    [SerializeField] private LegacyText legacyLabelText;
    [SerializeField] private Graphic legacyToggleBackground;

    private ChecklistUI ownerUI;
    private int itemIndex;
    private bool suppressCallback;
    private bool isInteractable = true;

    private static readonly Color NormalBackground = new Color(0.76f, 0.76f, 0.76f, 0.98f);
    private static readonly Color SelectedBackground = new Color(0.82f, 0.9f, 0.82f, 0.98f);
    private static readonly Color DisabledBackground = new Color(0.55f, 0.58f, 0.63f, 0.2f);
    private static readonly Color NormalText = new Color(0.09f, 0.09f, 0.1f, 1f);
    private static readonly Color SelectedText = new Color(0.09f, 0.09f, 0.1f, 1f);
    private static readonly Color DisabledText = new Color(0.8f, 0.8f, 0.82f, 0.6f);
    private static readonly Color NormalOutline = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color SelectedOutline = new Color(0.1f, 0.95f, 0.15f, 1f);

    public void Setup(ChecklistUI ui, int index, string label)
    {
        ownerUI = ui;
        itemIndex = index;
        EnsureReferences();
        ApplyButtonLayout();

        if (labelText != null)
            labelText.text = label;

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        ApplyVisualState(toggle != null && toggle.isOn);
    }

    public void SetCheckedWithoutNotify(bool value)
    {
        if (toggle == null) return;

        suppressCallback = true;
        toggle.isOn = value;
        suppressCallback = false;
        ApplyVisualState(value);
    }

    public void SetInteractable(bool value)
    {
        isInteractable = value;

        if (toggle != null)
            toggle.interactable = value;

        ApplyVisualState(toggle != null && toggle.isOn);
    }

    private void OnToggleValueChanged(bool value)
    {
        if (suppressCallback) return;
        if (ownerUI == null) return;

        ApplyVisualState(value);
        ownerUI.OnToggleChanged(itemIndex, value);
    }

    private void EnsureReferences()
    {
        if (toggle == null)
        {
            toggle = GetComponentInChildren<Toggle>(true);
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (legacyToggleBackground == null && toggle != null)
        {
            legacyToggleBackground = toggle.targetGraphic;
        }

        if (backgroundImage == null && legacyToggleBackground is Image toggleBackgroundImage)
        {
            backgroundImage = toggleBackgroundImage;
        }

        if (backgroundImage == null && toggle != null)
        {
            backgroundImage = toggle.GetComponentInChildren<Image>(true);
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }

        if (checkmarkGraphic == null && toggle != null)
        {
            checkmarkGraphic = toggle.graphic;
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }

        if (layoutGroup == null)
        {
            layoutGroup = GetComponent<HorizontalLayoutGroup>();
        }

        if (legacyLabelText == null)
        {
            legacyLabelText = GetComponentInChildren<LegacyText>(true);
        }

        if (outline == null)
        {
            outline = backgroundImage != null
                ? backgroundImage.GetComponent<Outline>()
                : GetComponent<Outline>();

            if (outline == null)
            {
                outline = (backgroundImage != null ? backgroundImage.gameObject : gameObject).AddComponent<Outline>();
            }
        }
    }

    private void ApplyButtonLayout()
    {
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = 86f;
            layoutElement.flexibleWidth = 1f;
        }

        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
            layoutGroup.spacing = 0f;
            layoutGroup.padding.left = 0;
            layoutGroup.padding.right = 0;
            layoutGroup.padding.top = 0;
            layoutGroup.padding.bottom = 0;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        }

        if (toggle != null && toggle.transform is RectTransform toggleRect)
        {
            toggleRect.anchorMin = Vector2.zero;
            toggleRect.anchorMax = Vector2.one;
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
            toggleRect.SetAsFirstSibling();

            toggle.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };

            toggle.targetGraphic = backgroundImage;
            toggle.transition = Selectable.Transition.None;
        }

        if (backgroundImage != null)
        {
            var backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            backgroundImage.raycastTarget = true;
            backgroundImage.gameObject.SetActive(true);
        }

        if (checkmarkGraphic != null)
        {
            checkmarkGraphic.gameObject.SetActive(false);
        }

        if (legacyToggleBackground != null && legacyToggleBackground != backgroundImage)
        {
            legacyToggleBackground.gameObject.SetActive(false);
        }

        if (legacyLabelText != null)
        {
            legacyLabelText.gameObject.SetActive(false);
        }

        if (labelText != null)
        {
            var labelParent = backgroundImage != null ? backgroundImage.transform : toggle.transform;
            if (labelText.transform.parent != labelParent)
            {
                labelText.transform.SetParent(labelParent, false);
            }

            labelText.fontSize = 34f;
            labelText.enableAutoSizing = false;
            labelText.alignment = TextAlignmentOptions.CenterGeoAligned;
            labelText.enableWordWrapping = false;
            labelText.raycastTarget = false;

            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(28f, 14f);
            labelRect.offsetMax = new Vector2(-28f, -14f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
        }

        if (outline != null)
        {
            if (outline.gameObject != backgroundImage.gameObject)
            {
                Destroy(outline);
                outline = backgroundImage.gameObject.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = backgroundImage.gameObject.AddComponent<Outline>();
                }
            }

            outline.effectDistance = new Vector2(6f, 6f);
            outline.useGraphicAlpha = false;
        }
    }

    private void ApplyVisualState(bool isSelected)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = !isInteractable
                ? DisabledBackground
                : (isSelected ? SelectedBackground : NormalBackground);
        }

        if (labelText != null)
        {
            labelText.color = !isInteractable
                ? DisabledText
                : (isSelected ? SelectedText : NormalText);
        }

        if (outline != null)
        {
            outline.effectColor = !isInteractable
                ? DisabledText
                : (isSelected ? SelectedOutline : NormalOutline);
        }
    }
}

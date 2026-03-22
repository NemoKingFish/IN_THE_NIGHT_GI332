using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChecklistItemUI : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TextMeshProUGUI labelText;

    private ChecklistUI ownerUI;
    private int itemIndex;
    private bool suppressCallback;

    public void Setup(ChecklistUI ui, int index, string label)
    {
        ownerUI = ui;
        itemIndex = index;

        if (labelText != null)
            labelText.text = label;

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    public void SetCheckedWithoutNotify(bool value)
    {
        if (toggle == null) return;

        suppressCallback = true;
        toggle.isOn = value;
        suppressCallback = false;
    }

    public void SetInteractable(bool value)
    {
        if (toggle != null)
            toggle.interactable = value;
    }

    private void OnToggleValueChanged(bool value)
    {
        if (suppressCallback) return;
        if (ownerUI == null) return;

        ownerUI.OnToggleChanged(itemIndex, value);
    }
}
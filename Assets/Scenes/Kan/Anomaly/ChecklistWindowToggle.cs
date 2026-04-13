using UnityEngine;

public class ChecklistWindowToggle : MonoBehaviour
{
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private void Start()
    {
        if (checklistWindow != null)
        {
            checklistWindow.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        if (checklistWindow == null) return;

        checklistWindow.SetActive(!checklistWindow.activeSelf);
    }

    public void OpenWindow()
    {
        if (checklistWindow == null) return;

        checklistWindow.SetActive(true);
    }

    public void CloseWindow()
    {
        if (checklistWindow == null) return;

        checklistWindow.SetActive(false);
    }
}
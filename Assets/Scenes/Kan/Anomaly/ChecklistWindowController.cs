using UnityEngine;

public class ChecklistWindowController : MonoBehaviour
{
    [SerializeField] private GameObject checklistWindow;
    [SerializeField] private GameObject openChecklistButton;

    private void Start()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(true);
    }

    public void OpenWindow()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(true);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(false);
    }

    public void CloseWindow()
    {
        if (checklistWindow != null)
            checklistWindow.SetActive(false);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(true);
    }

    public void ToggleWindow()
    {
        if (checklistWindow == null) return;

        bool isOpening = !checklistWindow.activeSelf;
        checklistWindow.SetActive(isOpening);

        if (openChecklistButton != null)
            openChecklistButton.SetActive(!isOpening);
    }
}
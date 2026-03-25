using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;      
    public GameObject startPanel;     
    public GameObject joinGamePanel;  
    public GameObject settingsPanel;
    public GameObject tutorialPanel;

    void Start()
    {
        menuPanel.SetActive(true);
        startPanel.SetActive(false);
        joinGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    public void OnStartClick()
    {
        menuPanel.SetActive(false);
        startPanel.SetActive(true);
    }

    public void OnJoinGameClick()
    {
        menuPanel.SetActive(false);
        joinGamePanel.SetActive(true);
    }

    public void OnSettingsClick()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
    public void OnTUTORIALClick()
    {
        menuPanel.SetActive(false);
        tutorialPanel.SetActive(true);
    }

    public void OnBackClick()
    {
        menuPanel.SetActive(true);
        startPanel.SetActive(false);
        joinGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    public void OnQuitClick()
    {
        Application.Quit();
    }
}

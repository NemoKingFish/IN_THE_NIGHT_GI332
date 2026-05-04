using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject startPanel;
    public GameObject joinGamePanel;
    public GameObject settingsPanel;
    public GameObject tutorialPanel;
    public GameObject BacktolobbyPanel;
    public GameObject mainmenuPanel;

    void Start()
    {
        menuPanel.SetActive(true);
        startPanel.SetActive(false);
        joinGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        tutorialPanel.SetActive(false);
        BacktolobbyPanel.SetActive(false);
        mainmenuPanel.SetActive(false);
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

    public void OnBacktolobbyClick()
    {
        menuPanel.SetActive(false);
        BacktolobbyPanel.SetActive(true);
    }

    public void OnMainMenuClick()
    {
        startPanel.SetActive(false);
        joinGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        tutorialPanel.SetActive(false);
        BacktolobbyPanel.SetActive(false);
        mainmenuPanel.SetActive(false);

        menuPanel.SetActive(true);
    }

    public void OnQuitClick()
    {
        Application.Quit();
    }
}

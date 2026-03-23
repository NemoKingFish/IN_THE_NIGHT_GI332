using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject settingsPanel;

    public void StartGame()
    {
        SceneManager.LoadScene("START");
    }

    public void JoinGame()
    {
        SceneManager.LoadScene("JOIN GAME");
    }

    public void Tutorial()
    {
        SceneManager.LoadScene("TUTORIAL");
    }

    public void Setting()
    {
        SceneManager.LoadScene("Setting");
    }
}

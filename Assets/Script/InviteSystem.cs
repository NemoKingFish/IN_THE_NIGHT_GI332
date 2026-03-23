using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class InviteSystem : MonoBehaviour
{
    [Header("UI Setup")]
    public GameObject playerPrefab;   // Prefab ของ UI ผู้เล่น
    public Transform parent;          // Panel ที่จะวาง

    [Header("Settings")]
    public int maxPlayers = 4;

    private int currentPlayers = 1; // เริ่มมีตัวเองแล้ว

    // ปุ่ม + (ไปหน้า INVITE)
    public void OpenInviteScene()
    {
        SceneManager.LoadScene("INVITE");
    }

    // ใช้ในหน้า INVITE (เพิ่มผู้เล่น)
    public void InvitePlayer()
    {
        if (currentPlayers >= maxPlayers)
        {
            Debug.Log("Room Full!");
            return;
        }

        GameObject newPlayer = Instantiate(playerPrefab, parent);

        TMP_Text nameText = newPlayer.GetComponentInChildren<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = "Player " + (currentPlayers + 1);
        }

        currentPlayers++;
    }
}

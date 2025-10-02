
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void StartButton()
    {
        SceneManager.LoadScene("GameplayScene");
        Time.timeScale = 1f;
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

        // if in the editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void SettingsButton()
    {
        SceneManager.LoadScene("SettingsMenu");
        Time.timeScale = 1f;
    }

    public void MainMenuButton()
    {
        SceneManager.LoadScene("MainMenuScene");
        Time.timeScale = 1f;
    }
}


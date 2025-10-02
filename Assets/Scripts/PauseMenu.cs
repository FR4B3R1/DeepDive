using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject playerInventory;
    [SerializeField] private GameObject firstPauseButton;
    [SerializeField] private GameObject minimap;

    private bool isPaused;
    private bool isOverlayVisible = true;
    private bool isMinimapVisible = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7))
            TogglePause();
    }


    public void TogglePause()
    {
        isPaused = !isPaused;
        isOverlayVisible = !isOverlayVisible;
        isMinimapVisible = !isMinimapVisible;
        playerInventory?.SetActive(isOverlayVisible);
        minimap?.SetActive(isMinimapVisible);
        ApplyPause(isPaused);
    }

    public void ResumeFromButton()
    {
        TogglePause();
    }

    private void ApplyPause(bool pause)
    {
        Time.timeScale = pause ? 0f : 1f;

        if (pauseMenu != null)
            pauseMenu.SetActive(pause);

        if (pause)
        {
            // Seleziona il primo pulsante per navigazione con gamepad
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstPauseButton);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene");

    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameplayScene");
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

    private void OnDisable()
    {

        if (isPaused)
        {
            Time.timeScale = 1f;

        }
    }


}



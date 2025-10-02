using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerLives : MonoBehaviour
{
    [Header("Vite & Respawn")]
    [SerializeField] private int maxLives = 3;                 // quante volte pu� respawnare
    [SerializeField] private bool loadGameOverScene = true;
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    public int LivesLeft { get; private set; }

    private PlayerHealth health;
    private Rigidbody2D rb;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        rb = GetComponent<Rigidbody2D>();

        LivesLeft = maxLives;
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDeath;
    }

    private void HandleDeath()
    {

        if (LivesLeft > 0)
        {
            LivesLeft--;
        }
        else
        {
            // Game Over
            if (loadGameOverScene && !string.IsNullOrEmpty(gameOverSceneName))
            {
                SceneManager.LoadScene(gameOverSceneName);
            }
            else
            {
                // fallback: disattiva il player
                gameObject.SetActive(false);
            }
        }
    }

}
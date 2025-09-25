using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerLives : MonoBehaviour
{
    [Header("Vite & Respawn")]
    [SerializeField] private int maxLives = 3;                 // quante volte pu� respawnare
    [SerializeField] private Transform spawnPoint;             // se nullo ? usa posizione iniziale
    [SerializeField] private float respawnDelay = 0.75f;       // tempo prima del respawn
    [SerializeField] private float invulnAfterRespawn = 1.5f;  // finestra di invulnerabilit�
    [SerializeField] private bool loadGameOverScene = true;
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    public int LivesLeft { get; private set; }

    private PlayerHealth health;
    private Rigidbody2D rb;
    private Vector3 savedSpawnPos;
    private Quaternion savedSpawnRot;
    private bool isRespawning;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        rb = GetComponent<Rigidbody2D>();

        LivesLeft = maxLives;
        savedSpawnPos = (spawnPoint ? spawnPoint.position : transform.position);
        savedSpawnRot = (spawnPoint ? spawnPoint.rotation : transform.rotation);
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
        if (isRespawning) return;

        if (LivesLeft > 0)
        {
            LivesLeft--;
            StartCoroutine(RespawnRoutine());
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

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        // 1) Disabilita input/movimento se necessario (facoltativo)
        var input = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (input) input.enabled = false;

        // 2) Ferma la fisica
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // (Facoltativo) animazione morte ? attesa
        yield return new WaitForSeconds(respawnDelay);

        // 3) Teletrasporta al checkpoint (o posizione iniziale)
        transform.SetPositionAndRotation(
            spawnPoint ? spawnPoint.position : savedSpawnPos,
            spawnPoint ? spawnPoint.rotation : savedSpawnRot
        );

        // 5) Riabilita input
        if (input) input.enabled = true;

        isRespawning = false;
    }
}
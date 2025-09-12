using UnityEngine;

public class SellZoneTrigger : MonoBehaviour
{
    public GameObject sellMenuUI; // Assegna il Canvas del menu di vendita da Inspector
    private bool playerInZone = false;

    void Start()
    {
        if (sellMenuUI != null)
            sellMenuUI.SetActive(false); // Assicurati che il menu sia nascosto all'inizio
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            Debug.Log("Il giocatore è nella zona di vendita.");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            if (sellMenuUI != null)
                sellMenuUI.SetActive(false); // Chiudi il menu se il giocatore esce
        }
    }

    void Update()
    {
        if (playerInZone && Input.GetKeyDown(KeyCode.M)) // M per aprire il menu
        {
            if (sellMenuUI != null)
                sellMenuUI.SetActive(!sellMenuUI.activeSelf); // Toggle menu
        }
    }
}

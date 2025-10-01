using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SellMenu : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private GameObject menuRoot;     // Assegna qui il pannello/menu da mostrare
    [SerializeField] private PlayerBehaviour player;   // Riferimento allo script del giocatore
    [SerializeField] private GameObject playerInventory;
    [SerializeField] private GameObject minimap;
    

    [Header("Impostazioni")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode openKey = KeyCode.I;
    [SerializeField] private bool toggleWithSameKey = true;  // Premi I per aprire/chiudere
    [SerializeField] private bool closeWhenExitZone = true;  // Chiudi il menu quando esci dalla zona

    private bool playerInside;
    public bool isOpen;
   

    private void Reset()
    {
        // Assicura che il collider sia un trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;

            if (closeWhenExitZone && isOpen)
                CloseMenu();
        }
    }

    private void Update()
    {
        if (!playerInside) return;

        if (Input.GetKeyDown(openKey))
        {
            if (toggleWithSameKey)
            {
                if (isOpen)
                {
                    
                    CloseMenu();
                    minimap?.SetActive(true);

                }
                else 
                {
                    OpenMenu();
                    minimap?.SetActive(false);
                }
               
            }
            else
            {
                if (!isOpen) OpenMenu();
            }
        }
    }

    public void OpenMenu()
    {
        isOpen = true;
        if (menuRoot != null) menuRoot.SetActive(true);

        playerInventory?.SetActive(false);

        Time.timeScale = 0f;
    }

    public void CloseMenu()
    {
        Time.timeScale = 1f;
        isOpen = false;
        if (menuRoot != null) menuRoot.SetActive(false);

        playerInventory?.SetActive(true);

        
    }
    
}

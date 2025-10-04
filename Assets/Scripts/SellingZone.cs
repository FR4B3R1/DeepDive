using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class SellMenu : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private GameObject menuRoot;     // Assegna qui il pannello/menu da mostrare
    [SerializeField] private PlayerBehaviour player;   // Riferimento allo script del giocatore
    [SerializeField] private GameObject gameUI;
    [SerializeField] private GameObject minimap;
    [SerializeField] private GameObject firstSellButton; // primo pulsante da selezionare
    [SerializeField] private PlayerInput PlayerInput;


    [Header("Impostazioni")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode openKey = KeyCode.I;
    [SerializeField] private bool toggleWithSameKey = true;  // Premi I per aprire/chiudere
    [SerializeField] private bool closeWhenExitZone = true;  // Chiudi il menu quando esci dalla zona

    private bool playerInside;
    public bool isOpen;

   
    private InputAction openSellMenuAction;

    public void OnOpenSellMenu(InputAction.CallbackContext context)
    {
        if (!playerInside) return;

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

    public void OpenMenu()
    {
        isOpen = true;
        if (menuRoot != null) menuRoot.SetActive(true);

        gameUI?.SetActive(false);
        minimap?.SetActive(false);

        Time.timeScale = 0f;

        // Seleziona il primo pulsante per navigazione con gamepad
        if (firstSellButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSellButton);
        }
    }

    public void CloseMenu()
    {

        Time.timeScale = 1f;
        isOpen = false;
        if (menuRoot != null) menuRoot.SetActive(false);

        gameUI?.SetActive(true);
        minimap?.SetActive(true);

        
    }

    private void OnEnable()
    {
        var input = GetComponent<PlayerInput>();
        if (input != null)
        {
            openSellMenuAction = input.actions["OpenSellMenu"];
            openSellMenuAction.performed += OnOpenSellMenu;
            openSellMenuAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (openSellMenuAction == null) return;
        openSellMenuAction.performed -= OnOpenSellMenu;
        openSellMenuAction.Disable();
    }
}

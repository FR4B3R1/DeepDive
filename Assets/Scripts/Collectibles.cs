using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Collectible : MonoBehaviour
{
    [SerializeField] private float weight;
    [SerializeField] public double value = 0;

    private bool isPlayerNearby = false;
    private Animator animator;
    private bool isCollected = false;
    [SerializeField] private Sprite destroyedCrystal; // Assegna lo sprite finale del cristallo da Inspector

    PlayerInventory playerInventory;
   


    void Awake()
    {
        animator = GetComponent<Animator>();
        playerInventory = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerInventory>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            isPlayerNearby = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            isPlayerNearby = false;
    }

    void Update()
    {
        if (isPlayerNearby && !isCollected && Keyboard.current.eKey.wasPressedThisFrame)
        {
            isCollected = true;

            // Avvia l'animazione di raccolta
            if (animator != null && this.CompareTag("Cristallo"))
            {
               
                animator.SetTrigger("break");
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null && destroyedCrystal != null)
                {
                    sr.sprite = destroyedCrystal;
                }
                if (animator != null)
                    animator.enabled = false;

            }
            else
            {
                // Se non c'è un animatore, distruggi immediatamente l'oggetto
                Destroy(gameObject);
            }

            // Aggiungi peso e valore al player
            PlayerBehaviour player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerBehaviour>();
            PlayerInventory inventory = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerInventory>();
            if (player != null && inventory != null)
            {
                player.AddWeight(weight);
                inventory.AddMoney(this.GetComponent<Collider2D>());
            }
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        // Specify the type argument for ReadValue<TValue>()
        // For a Button, you typically want to read a bool value (pressed or not)
        bool isPressed = context.ReadValue<bool>();
        // You can use isPressed to trigger interaction logic if needed
        // Example: if (isPressed) { /* handle interaction */ }
    }

    public double GetValue()
    {
        double target = playerInventory.targetMoney;

        if (this.CompareTag("Forziere"))
            value = target * 0.4;
        else if (this.CompareTag("Cristallo"))
            value = target * 0.19;
        else if (this.CompareTag("Spada"))
            value = target * 0.75;
        else if (this.CompareTag("Legno"))
            value = target * 0.02; 
        return value;
    }
}

using UnityEngine;

public class Collectible : MonoBehaviour
{
    [SerializeField] private float weight;
    [SerializeField] public int value;

    private bool isPlayerNearby = false;
    private Animator animator;
    private bool isCollected = false;
    [SerializeField] private Sprite destroyedCrystal; // Assegna lo sprite finale del cristallo da Inspector


    void Awake()
    {
        animator = GetComponent<Animator>();
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
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E) && !isCollected)
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
}

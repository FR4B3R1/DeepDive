using UnityEngine;

public class Collectible : MonoBehaviour
{
    [SerializeField] private float weight;
    [SerializeField] public int value;

    private bool isPlayerNearby = false;

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
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            PlayerBehaviour player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerBehaviour>();
            PlayerInventory inventory = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerInventory>();
            if (player != null && inventory != null)
            {
                player.AddWeight(weight);
                inventory.AddMoney(this.GetComponent<Collider2D>());
                Destroy(gameObject);
            }
        }
    }
}

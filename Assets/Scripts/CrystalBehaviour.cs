using UnityEngine;

public class CrystalBehaviour : MonoBehaviour
{
    [SerializeField] private int hitsRequired = 4;
    [SerializeField] private GameObject destructionEffectPrefab;

    private int currentHits = 0;
    private Animator animator;
    private bool isDestroyed = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Harpoon") && !isDestroyed)
        {
            HitWithHarpoon();
        }
    }

    public void HitWithHarpoon()
    {
        currentHits++;

        if (currentHits >= hitsRequired && !isDestroyed)
        {
            isDestroyed = true;
            TriggerDestruction();
        }
    }

    private void TriggerDestruction()
    {
        if (animator != null)
        {
            animator.SetTrigger("Break");
        }

        if (destructionEffectPrefab != null)
        {
            Instantiate(destructionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Puoi anche distruggere l'oggetto dopo un delay se vuoi che l'animazione si completi
        Destroy(gameObject, 1.5f); // tempo sufficiente per l'animazione
    }
}

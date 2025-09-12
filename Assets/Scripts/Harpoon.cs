using UnityEngine;

public class Harpoon : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float gravityScaleOutOfWater = 1f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Water"))
        {
            rb.gravityScale = 0f; // In acqua, niente gravità
        }

        CrystalBehaviour crystal = other.GetComponent<CrystalBehaviour>();
        if (crystal != null)
        {
            crystal.HitWithHarpoon();
            Destroy(gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Water"))
        {
            rb.gravityScale = gravityScaleOutOfWater; // Fuori dall’acqua, attiva gravità
        }
    }
}

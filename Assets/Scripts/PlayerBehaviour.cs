using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBehaviour : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float swimSpeed = 2f;
    [SerializeField] private float accelInWater = 10f;

    [Header("Inventario")]
    [SerializeField] private float carriedWeight = 0f;
    [SerializeField] private float weightImpactFactor = 1f; // Quanto il peso influisce sulla velocità

    [Header("Fiocina")]
    [SerializeField] private GameObject harpoonPrefab;
    [SerializeField] private Transform harpoonSpawnPoint;
    [SerializeField] private float harpoonSpeed = 10f;
    [SerializeField] private float harpoonCooldown = 1f;
    private float lastHarpoonTime = -Mathf.Infinity;


    [Header("Fisica acqua")]
    [SerializeField] private float waterDrag = 3f;
    [SerializeField] private float normalDrag = 0f;

    [Tooltip("Forza target di galleggiamento (coefficiente di molla). Più alto = risale più forte verso la superficie.")]
    [SerializeField] private float buoyancyK = 8f;

    [Tooltip("Smorzamento verticale in acqua. Più alto = meno rimbalzi.")]
    [SerializeField] private float buoyancyDamping = 3f;

    [Tooltip("Forza massima applicabile come spinta di galleggiamento (per evitare picchi).")]
    [SerializeField] private float maxBuoyancyForce = 12f;

    [Header("Affondamento iniziale")]
    [SerializeField] private float sinkDuration = 0.6f;
    [SerializeField] private float initialSinkForce = 2.0f;
    [SerializeField] private float buoyancyRampTime = 0.8f;

    [Header("Effetti")]
    [SerializeField] private GameObject splashEffectPrefab;

    private Vector2 moveInput;
    private bool isInWater = false;
    private bool isSinking = false;
    private float sinkTimer = 0f;
    private float buoyancyT = 0f;
    private float waterSurfaceY = Mathf.NegativeInfinity;
    private Collider2D currentWaterTrigger;

    private Rigidbody2D rb;

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        // DEBUG (facoltativo)
        // if (context.performed) Debug.Log($"Move: {moveInput}");
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Evita ribaltamenti
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void FixedUpdate()
    {
        if (isInWater)
        {
            rb.gravityScale = 0f;
            rb.linearDamping = waterDrag;

            // Movimento in acqua su X e Y (ammorbidito)
            // Vector2 targetVel = moveInput * swimSpeed;
            

            float adjustedSwimSpeed = Mathf.Max(0.5f, swimSpeed - carriedWeight * weightImpactFactor);
            Vector2 targetVel = moveInput * adjustedSwimSpeed;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVel, accelInWater * Time.fixedDeltaTime);


            // --- Affondamento -> galleggiamento ---
            if (isSinking)
            {
                sinkTimer += Time.fixedDeltaTime;

                float downward = initialSinkForce;
                buoyancyT = Mathf.Clamp01(sinkTimer / buoyancyRampTime);

                if (sinkTimer >= sinkDuration)
                    isSinking = false;

                rb.AddForce(Vector2.down * downward, ForceMode2D.Force);
            }
            else
            {
                buoyancyT = Mathf.MoveTowards(buoyancyT, 1f, Time.fixedDeltaTime / Mathf.Max(0.0001f, buoyancyRampTime));
            }

            // --- Galleggiamento tipo molla verso la superficie ---
            if (currentWaterTrigger != null)
            {
                waterSurfaceY = currentWaterTrigger.bounds.max.y;

                float depth = waterSurfaceY - rb.position.y; // > 0: sotto la superficie
                float k = buoyancyK * buoyancyT;

                float spring = depth > 0f ? depth * k : 0f;
                float damping = -rb.linearVelocity.y * buoyancyDamping;

                float F = Mathf.Clamp(spring + damping, -maxBuoyancyForce, maxBuoyancyForce);

                rb.AddForce(Vector2.up * F, ForceMode2D.Force);
            }
        }
        else
        {
            // Fuori dall’acqua
            rb.gravityScale = 1f;
            rb.linearDamping = normalDrag;

            // Input orizzontale
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
        }
    }

    public void AddWeight(float amount)
    {
        carriedWeight += amount;
        Debug.Log($"Peso totale trasportato: {carriedWeight}");
    }


    [System.Obsolete]
    private void FireHarpoon()
    {
        if (Time.time - lastHarpoonTime < harpoonCooldown)
            return; // Ancora in cooldown

        lastHarpoonTime = Time.time;

        if (harpoonPrefab != null && harpoonSpawnPoint != null)
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 direction = (mouseWorldPos - harpoonSpawnPoint.position).normalized;

            GameObject harpoon = Instantiate(harpoonPrefab, harpoonSpawnPoint.position, Quaternion.identity);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            harpoon.transform.rotation = Quaternion.Euler(0, 0, angle);

            Rigidbody2D rb = harpoon.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * harpoonSpeed;
            }
        }
    }




    public void OnFireHarpoon(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            FireHarpoon();
        }
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Water"))
        {
            isInWater = true;
            isSinking = true;
            sinkTimer = 0f;
            buoyancyT = 0f;
            currentWaterTrigger = other;

            if (splashEffectPrefab != null)
                Instantiate(splashEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == currentWaterTrigger)
        {
            isInWater = false;
            isSinking = false;
            currentWaterTrigger = null;
        }
    }
}

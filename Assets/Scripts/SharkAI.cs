using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SharkChaseAI2D : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack }

    [Header("Riferimenti")]
    [SerializeField] private Transform player;      // se nullo → cercato per Tag "Player"
    [SerializeField] private Transform eye;         // punto di vista e bocca (raycast e morso)

    [Header("Percezione (vista)")]
    [SerializeField] private float viewRadius = 8f;
    [SerializeField, Range(0, 360)] private float viewAngle = 120f;
    [SerializeField] private LayerMask obstacleMask = 0; // non includere Player/Water
    [SerializeField] private float scanInterval = 0.1f;

    [Header("Movimento")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float patrolSpeed = 2.2f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float rotateSmooth = 10f;
    [SerializeField] private float waterDrag = 1.5f;
    [SerializeField] private float velocityEps = 0.01f;

    [Header("Patrol lineare (A ↔ B)")]
    [SerializeField] private bool useGeneratedLine = true;
    [SerializeField] private Vector2 lineDirection = Vector2.right;
    [SerializeField] private float lineLength = 8f;
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float stopDistance = 0.25f;
    [SerializeField] private bool flipOnCollision = false;

    [Header("Confinamento in acqua")]
    [SerializeField] private Collider2D waterArea;   // IsTrigger=ON
    [SerializeField] private float waterPadding = 0.12f;

    [Header("Attacco (morso)")]
    [Tooltip("Distanza massima dalla bocca per poter mordere.")]
    [SerializeField] private float biteRange = 0.85f;
    [Tooltip("Danno inflitto al player.")]
    [SerializeField] private float biteDamage = 10f;
    [Tooltip("Forza del knockback applicato al player.")]
    [SerializeField] private float biteKnockback = 6f;
    [Tooltip("Cooldown tra un morso e il successivo (s).")]
    [SerializeField] private float attackCooldown = 1.25f;
    [Tooltip("Tempo di windup prima dello scatto (s).")]
    [SerializeField] private float biteWindup = 0.12f;
    [Tooltip("Durata della finestra attiva (scatto) in cui il morso può colpire (s).")]
    [SerializeField] private float biteActiveTime = 0.18f;
    [Tooltip("Velocità dello scatto durante il morso.")]
    [SerializeField] private float biteLungeSpeed = 5.5f;
    [Tooltip("Tempo di recover dopo il morso (s).")]
    [SerializeField] private float biteRecover = 0.22f;
    [Tooltip("Layer che verrà colpito dal morso (metti il layer del player).")]
    [SerializeField] private LayerMask playerHitMask = ~0; // per default colpisce tutto (regolalo in Editor)
    [Tooltip("Attacca solo se il player è dentro i bounds dell'acqua.")]
    [SerializeField] private bool requirePlayerInWater = true;

    // --- runtime ---
    private Rigidbody2D rb;
    private State state = State.Patrol;
    private Vector2 facingDir = Vector2.right;
    private float nextScanTime = 0f;
    private bool seesPlayer = false;

    // Patrol
    private Vector2 patrolA, patrolB;
    private bool goingToB = true;

    // Attack
    private float nextAttackTime = 0f;
    private bool hasDealtDamageThisBite = false;
    private Coroutine attackCo;

    // -------------------------------------------------------
    // Compatibilità API (Unity 6 vs versioni precedenti)
    // -------------------------------------------------------
    private Vector2 CurrentVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
    }
    private float LinearDamping
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearDamping;
#else
            return rb.drag;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = value;
#else
            rb.drag = value;
#endif
        }
    }
    private float AngularDamping
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return rb.angularDamping;
#else
            return rb.angularDrag;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            rb.angularDamping = value;
#else
            rb.angularDrag = value;
#endif
        }
    }

    // -------------------- Unity lifecycle --------------------

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        LinearDamping = waterDrag;
        AngularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (eye == null) eye = transform;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (waterArea == null)
        {
            Debug.LogWarning($"{name}: 'Water Area' non assegnato. Trascina qui il Collider2D dell'acqua (IsTrigger=ON).");
        }
        SetupPatrolLine();
    }

    void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            seesPlayer = CanSeePlayer();
            if (state != State.Attack) // durante l'attacco non cambiamo stato qui
                state = seesPlayer ? State.Chase : State.Patrol;
        }
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Patrol:
                PatrolBehaviour();
                break;

            case State.Chase:
                ChaseBehaviour();
                break;

            case State.Attack:
                // la logica è nella Coroutine; qui solo confinamento/rotazione
                break;
        }

        RotateTowardsVelocity();
        EnforceWaterBounds();
    }

    // -------------------- Behaviours --------------------

    private void PatrolBehaviour()
    {
        Vector2 target = goingToB ? patrolB : patrolA;
        if (waterArea != null)
            target = ClampToWater(target);

        Vector2 to = target - rb.position;
        if (to.magnitude <= stopDistance)
        {
            goingToB = !goingToB;
            return;
        }

        Vector2 desiredVel = to.normalized * patrolSpeed;
        CurrentVelocity = Vector2.MoveTowards(CurrentVelocity, desiredVel, acceleration * Time.fixedDeltaTime);
    }

    private void ChaseBehaviour()
    {
        if (player == null)
        {
            state = State.Patrol;
            return;
        }

        // Attacco: se entro range, cooldown ok e (opzionale) player in acqua
        float distToPlayer = Vector2.Distance(rb.position, player.position);
        bool playerInsideWater = !requirePlayerInWater || (waterArea != null && ShrunkWaterBounds().Contains(player.position));

        if (distToPlayer <= biteRange && Time.time >= nextAttackTime && playerInsideWater)
        {
            if (attackCo == null)
                attackCo = StartCoroutine(BiteAttack());
            return;
        }

        // Inseguimento
        Vector2 targetPos = player.position;
        if (waterArea != null) targetPos = ClampToWater(targetPos);

        Vector2 to = targetPos - rb.position;
        Vector2 desiredVel = (to.sqrMagnitude > 0.0001f) ? to.normalized * chaseSpeed : Vector2.zero;

        CurrentVelocity = Vector2.MoveTowards(CurrentVelocity, desiredVel, acceleration * Time.fixedDeltaTime);
    }

    // -------------------- Attack (Morso) --------------------

    private IEnumerator BiteAttack()
    {
        state = State.Attack;
        hasDealtDamageThisBite = false;

        // 1) WINDUP: fermati e “prendi la mira”
        CurrentVelocity = Vector2.zero;
        if (player != null)
        {
            Vector2 dir = ((Vector2)player.position - rb.position);
            if (dir.sqrMagnitude > 0.0001f) facingDir = dir.normalized;
        }
        if (biteWindup > 0f) yield return new WaitForSeconds(biteWindup);

        // 2) ACTIVE (LUNGE): scatto in avanti con finestra di impatto
        Vector2 lungeDir = facingDir;
        float t = 0f;
        while (t < biteActiveTime)
        {
            t += Time.fixedDeltaTime;

            // spingi in avanti
            CurrentVelocity = Vector2.MoveTowards(CurrentVelocity, lungeDir * biteLungeSpeed, acceleration * Time.fixedDeltaTime);

            // controllo hit
            TryBiteHit();

            yield return new WaitForFixedUpdate();
        }

        // 3) RECOVER: fermati un attimo
        CurrentVelocity = Vector2.zero;
        if (biteRecover > 0f) yield return new WaitForSeconds(biteRecover);

        nextAttackTime = Time.time + attackCooldown;
        attackCo = null;

        // Torna allo stato coerente
        state = (seesPlayer ? State.Chase : State.Patrol);
    }

    private void TryBiteHit()
    {
        if (hasDealtDamageThisBite) return;

        Vector2 center = (eye != null) ? (Vector2)eye.position : rb.position;

        // OverlapCircle: controlla i collider del layer playerHitMask
        Collider2D hit = Physics2D.OverlapCircle(center, biteRange, playerHitMask);
        if (hit == null) return;

        // filtra proprio il player (se disponibile)
        if (player != null && !hit.transform.IsChildOf(player) && hit.transform != player)
        {
            // Se vuoi essere permissivo, rimuovi questo check
        }

        // Applica danno/knockback (IDamageable se disponibile, altrimenti PlayerHealth semplice)
        Vector2 dir = ((Vector2)hit.bounds.center - rb.position).normalized;

        // 1) Interfaccia generica (consigliata)
        var dmg = hit.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(biteDamage, dir, biteKnockback);
        }
        else
        {
            // 2) Implementazione di esempio: PlayerHealth (vedi sotto)
            var health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null) health.TakeDamage(biteDamage);

            // 3) Applica knockback se c'è un Rigidbody2D
            var prb = hit.GetComponentInParent<Rigidbody2D>();
            if (prb != null)
            {
#if UNITY_6000_0_OR_NEWER
                prb.linearVelocity += dir * biteKnockback;
#else
                prb.velocity += dir * biteKnockback;
#endif
            }
        }

        hasDealtDamageThisBite = true;
    }

    // -------------------- Visione --------------------

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 eyePos = (eye != null) ? (Vector2)eye.position : rb.position;
        Vector2 toPlayer = (Vector2)player.position - eyePos;
        float dist = toPlayer.magnitude;

        if (dist > viewRadius) return false;

        Vector2 forward = (facingDir.sqrMagnitude > 0.0001f) ? facingDir : (Vector2)transform.right;
        float angle = Vector2.Angle(forward, toPlayer);
        if (angle > viewAngle * 0.5f) return false;

        if (obstacleMask != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask);
            if (hit.collider != null) return false;
        }

        // opzionale: ignora se player è fuori dall'acqua
        // if (requirePlayerInWater && waterArea != null && !ShrunkWaterBounds().Contains(player.position)) return false;

        return true;
    }

    // -------------------- Rotazione --------------------

    private void RotateTowardsVelocity()
    {
        Vector2 v = CurrentVelocity;
        if (v.sqrMagnitude > velocityEps * velocityEps)
        {
            Vector2 targetDir = v.normalized;
            facingDir = Vector2.Lerp(facingDir, targetDir, rotateSmooth * Time.fixedDeltaTime);

            float targetAngle = Mathf.Atan2(facingDir.y, facingDir.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotateSmooth * Time.fixedDeltaTime);
            rb.MoveRotation(newAngle);
        }
    }

    // -------------------- Confinamento in acqua --------------------

    private Bounds ShrunkWaterBounds()
    {
        Bounds b = waterArea.bounds;
        b.Expand(-2f * waterPadding);
        return b;
    }

    private Vector2 ClampToWater(Vector2 pos)
    {
        if (waterArea == null) return pos;
        Bounds b = ShrunkWaterBounds();
        return new Vector2(
            Mathf.Clamp(pos.x, b.min.x, b.max.x),
            Mathf.Clamp(pos.y, b.min.y, b.max.y)
        );
    }

    private void EnforceWaterBounds()
    {
        if (waterArea == null) return;

        Bounds b = ShrunkWaterBounds();
        Vector2 pos = rb.position;

        if (!b.Contains(pos))
        {
            Vector2 clamped = ClampToWater(pos);
            Vector2 v = CurrentVelocity;

            if (pos.x <= b.min.x && v.x < 0f) v.x = 0f;
            if (pos.x >= b.max.x && v.x > 0f) v.x = 0f;
            if (pos.y <= b.min.y && v.y < 0f) v.y = 0f;
            if (pos.y >= b.max.y && v.y > 0f) v.y = 0f;

            CurrentVelocity = v;
            rb.position = clamped;
        }
    }

    // -------------------- Patrol setup & collision flip --------------------

    private void SetupPatrolLine()
    {
        if (useGeneratedLine)
        {
            Vector2 A = transform.position;
            Vector2 dir = (lineDirection.sqrMagnitude < 0.0001f) ? Vector2.right : lineDirection.normalized;
            Vector2 B = A + dir * Mathf.Max(0.01f, lineLength);

            patrolA = A;
            patrolB = B;
        }
        else
        {
            if (patrolPointA == null || patrolPointB == null)
            {
                Debug.LogWarning($"{name}: UseGeneratedLine=false ma PatrolPointA/B non assegnati. Passo alla generazione automatica.");
                useGeneratedLine = true;
                SetupPatrolLine();
                return;
            }
            patrolA = patrolPointA.position;
            patrolB = patrolPointB.position;
        }

        if (waterArea != null)
        {
            patrolA = ClampToWater(patrolA);
            patrolB = ClampToWater(patrolB);
        }

        goingToB = (Vector2.Distance(rb.position, patrolA) <= Vector2.Distance(rb.position, patrolB));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!flipOnCollision) return;
        if (state == State.Patrol && collision.collider != null)
        {
            goingToB = !goingToB;
        }
    }

    // -------------------- Gizmos --------------------

    void OnDrawGizmosSelected()
    {
        // vista
        Transform eyeT = (eye != null) ? eye : transform;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(eyeT.position, viewRadius);

        Vector2 fwd = Application.isPlaying
            ? (facingDir.sqrMagnitude > 0.0001f ? facingDir : (Vector2)transform.right)
            : (Vector2)transform.right;

        float half = viewAngle * 0.5f * Mathf.Deg2Rad;
        Vector2 left = new Vector2(
            fwd.x * Mathf.Cos(half) - fwd.y * Mathf.Sin(half),
            fwd.x * Mathf.Sin(half) + fwd.y * Mathf.Cos(half)
        );
        Vector2 right = new Vector2(
            fwd.x * Mathf.Cos(-half) - fwd.y * Mathf.Sin(-half),
            fwd.x * Mathf.Sin(-half) + fwd.y * Mathf.Cos(-half)
        );
        Gizmos.DrawLine(eyeT.position, (Vector2)eyeT.position + left * viewRadius);
        Gizmos.DrawLine(eyeT.position, (Vector2)eyeT.position + right * viewRadius);

        // bite range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(eyeT.position, biteRange);

        // acqua
        if (waterArea != null)
        {
            Bounds b = ShrunkWaterBounds();
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawCube(b.center, b.size);
        }

        // patrol line
        Gizmos.color = Color.yellow;
        Vector2 A, B;
        if (useGeneratedLine)
        {
            Vector2 pos = (Vector2)transform.position;
            Vector2 dir = (lineDirection.sqrMagnitude < 0.0001f) ? Vector2.right : lineDirection.normalized;
            A = pos; B = pos + dir * Mathf.Max(0.01f, lineLength);
        }
        else
        {
            A = patrolPointA ? (Vector2)patrolPointA.position : (Vector2)transform.position;
            B = patrolPointB ? (Vector2)patrolPointB.position : A + Vector2.right * 3f;
        }
        Gizmos.DrawSphere(A, 0.1f);
        Gizmos.DrawSphere(B, 0.1f);
        Gizmos.DrawLine(A, B);
    }
}

/* ====== Opzionale: interfaccia danno generica ====== */
public interface IDamageable
{
    void TakeDamage(float amount, Vector2 hitDirection, float knockback);
}

/* ====== Opzionale: implementazione semplice di vita del player ======
 * Se non hai già un tuo sistema, puoi usarla come base.
 */
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float iFrames = 0.5f; // invulnerabilità dopo il danno
    private float currentHP;
    private float nextHittableTime = 0f;
    private Rigidbody2D rb;

    void Awake()
    {
        currentHP = maxHP;
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(float amount, Vector2 hitDir, float knockback)
    {
        if (Time.time < nextHittableTime) return;
        nextHittableTime = Time.time + iFrames;

        currentHP -= amount;
        if (currentHP <= 0f)
        {
            // TODO: morte player
            Debug.Log("Player morto");
        }

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity += hitDir.normalized * knockback;
#else
            rb.velocity += hitDir.normalized * knockback;
#endif
        }
    }

    // Overload se vuoi chiamarla senza interfaccia
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero, 0f);
    }
}



#if UNITY_EDITOR
using UnityEditor;
#endif
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

    [Tooltip("Attacca solo se il player è dentro i bounds dell'acqua.")]
    [SerializeField] private bool requirePlayerInWater = true;

    [Header("Danno a Contatto")]
    [SerializeField] private float contactDamage = 20f;
    [SerializeField] private float contactCooldown = 1f;
    [SerializeField] private bool damageOnlyWhenChasing = false; // se true, danneggia solo quando in Chase
    [SerializeField] private bool debugContactLogs = false;

    [Header("Evitamento ostacoli / inversione")]
    [SerializeField] private LayerMask turnOnLayers;      // Seleziona qui i Layer: Obstacle + Shark
    [SerializeField] private float sensorDistance = 0.6f; // distanza del sensore
    [SerializeField] private float sensorRadius = 0.2f;   // raggio del sensore circolare
    [SerializeField] private float turnCooldown = 0.25f;  // evita flip-flop
    [SerializeField] private SpriteRenderer sr;           // se nullo, lo trovo in Awake
    [SerializeField] private float pushBackDistance = 0.1f; // piccola spinta all'indietro
    [SerializeField] private bool spriteFacesRight = true; // true se lo sprite "di base" guarda a destra

    private float _lastTurnTime = -999f;

    // cache
    private Collider2D selfCollider;

    private float nextContactTime = 0f;


    // --- runtime ---
    private Rigidbody2D rb;
    private State state = State.Patrol;
    private Vector2 facingDir = Vector2.right;
    private float nextScanTime = 0f;
    private bool seesPlayer = false;

    // Patrol
    private Vector2 patrolA, patrolB;
    private bool goingToB = true;

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
        selfCollider = GetComponent<Collider2D>();             // <-- nuovo

        rb.gravityScale = 0f;
        LinearDamping = waterDrag;
        AngularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.None;

        if (eye == null) eye = transform;
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(); // <-- già aggiunto da te


        // Mantieni la scala positiva; usa flipX per il verso
        var ls = transform.localScale;
        if (ls.x < 0f) { ls.x = Mathf.Abs(ls.x); transform.localScale = ls; }

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

        SyncInitialFacingAndFlip();

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
            case State.Patrol: PatrolBehaviour(); break;
            case State.Chase: ChaseBehaviour(); break;
            case State.Attack: break;
        }

        RotateTowardsVelocity();   
        EnforceWaterBounds();
    }


    // -------------------- Behaviours --------------------

    private void PatrolBehaviour()
    {
        // Se c'è un ostacolo davanti → TurnBack()
        if (DetectHitAhead(out var hitNormal))
        {
            Vector2 away = hitNormal.sqrMagnitude > 0.0001f ? hitNormal : -facingDir;
            TurnBack(away);
            return;
        }

        Vector2 target = goingToB ? patrolB : patrolA;
        if (waterArea != null)
            target = ClampToWater(target);

        Vector2 to = target - rb.position;

        // ✅ Se siamo arrivati alla fine della linea → TurnBack()
        if (to.magnitude <= stopDistance)
        {
            // awayDir = direzione opposta a quella attuale
            Vector2 away = -facingDir;
            TurnBack(away);
            return;
        }

        // Movimento verso il target
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

        // Inseguimento
        Vector2 targetPos = player.position;
        if (waterArea != null) targetPos = ClampToWater(targetPos);

        Vector2 to = targetPos - rb.position;
        Vector2 desiredVel = (to.sqrMagnitude > 0.0001f) ? to.normalized * chaseSpeed : Vector2.zero;
        
        // aggiusto la rotazion dello squalo quando insegue SE girato verso sinistra
                if (desiredVel.x < 0 && facingDir.x > 0)
                {
                    facingDir = -facingDir; // Inverti la direzione di facing
                    ApplyFlipFromFacing();  // Aggiorna il flip visivo
        }
                    

        CurrentVelocity = Vector2.MoveTowards(CurrentVelocity, desiredVel, acceleration * Time.fixedDeltaTime);

        // Controllo attacco
        ProcessContact(eye.GetComponent<Collider2D>());

    }

    // -------------------- Visione --------------------

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 eyePos = (eye != null) ? (Vector2)eye.position : rb.position;
        Vector2 toPlayer = (Vector2)player.position - eyePos;
        float dist = toPlayer.magnitude;
        if (dist > viewRadius) return false;

        Vector2 forward = GetForward2D(); // <-- non usare transform.right
        float angle = Vector2.Angle(forward, toPlayer);
        if (angle > viewAngle * 0.5f) return false;

        if (obstacleMask != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask);
            if (hit.collider != null) return false;
        }
        return true;
    }

    // -------------------- Rotazione --------------------




    private void RotateTowardsVelocity()
    {
        Vector2 v = CurrentVelocity;
        bool moving = v.sqrMagnitude > velocityEps * velocityEps;

        // Se mi muovo, allineo al vettore velocità; da fermo uso facingDir (già sincronizzato allo start)
        Vector2 targetDir = moving
            ? v.normalized
            : (facingDir.sqrMagnitude > 0.0001f ? facingDir.normalized : (Vector2)transform.right);

        // Smooth soltanto quando mi sto muovendo
        if (moving)
            facingDir = Vector2.Lerp(facingDir, targetDir, rotateSmooth * Time.fixedDeltaTime).normalized;
        else
            facingDir = targetDir;

        // Angolo grezzo [-180°, +180°]
        float rawAngle = Mathf.Atan2(facingDir.y, facingDir.x) * Mathf.Rad2Deg;
        rawAngle = Mathf.DeltaAngle(0f, rawAngle);

        // Rimappa in [-90°, +90°] preservando il segno e decidi il flip
        bool flip;
        float visualAngle = MapAngleAndFlip(rawAngle, out flip);

        // Applica sempre il flip (anche da fermo), considerando l'orientamento base dello sprite
        if (sr != null)
            sr.flipX = spriteFacesRight ? flip : !flip;

        // Ruota il rigidbody verso l’angolo visivo (solo se c’è movimento)
        if (moving)
        {
            float newAngle = Mathf.LerpAngle(rb.rotation, visualAngle, rotateSmooth * Time.fixedDeltaTime);
            rb.MoveRotation(newAngle);
        }
    }

    /// <summary>
    /// Porta l'angolo in [-90°, +90°] **preservando il segno** del tilt;
    /// indica se è necessario flippare orizzontalmente.
    /// </summary>
    private static float MapAngleAndFlip(float angle, out bool flip)
    {
        flip = false;

        if (angle > 90f)
        {
            // Esempio: 150° -> -30° (flip=true)
            angle = angle - 180f;
            flip = true;
        }
        else if (angle < -90f)
        {
            // Esempio: -150° -> +30° (flip=true)
            angle = angle + 180f;
            flip = true;
        }

        // Ora angle è in [-90, +90] con segno coerente
        return angle;
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

        bool outLeft = pos.x < b.min.x;
        bool outRight = pos.x > b.max.x;
        bool outBottom = pos.y < b.min.y;
        bool outTop = pos.y > b.max.y;

        if (!(outLeft || outRight || outBottom || outTop))
            return;

        // 1) Clampa posizione dentro i bounds (con padding già gestito da ShrunkWaterBounds)
        Vector2 clamped = new Vector2(
            Mathf.Clamp(pos.x, b.min.x, b.max.x),
            Mathf.Clamp(pos.y, b.min.y, b.max.y)
        );

        // Se vuoi "teleport" pulito:
        rb.position = clamped;
        // In alternativa, per interp fisica:
        // rb.MovePosition(clamped);

        // 2) Riflette solo la componente che spinge fuori
        Vector2 v = CurrentVelocity;

        if (outLeft && v.x < 0f) v.x = -v.x;
        if (outRight && v.x > 0f) v.x = -v.x;
        if (outBottom && v.y < 0f) v.y = -v.y;
        if (outTop && v.y > 0f) v.y = -v.y;

        CurrentVelocity = v;

        // 3) (Opzionale) Se in Patrol e colpisci lato SX/DX, esegui la tua logica di inversione
        if (state == State.Patrol && (outLeft || outRight))
        {
            // AwayDir orizzontale coerente con il lato colpito
            Vector2 away = outLeft ? Vector2.right : Vector2.left;
            TurnBack(away); // ha già cooldown interno; non flippa qui, solo direzione/velocità
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

    // -------------------- Gizmos --------------------


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (eye == null) return;

        Vector2 origin = eye.position;
        // In Play usa facingDir; in Edit usa fallback corretto col flip
        Vector2 fwd = Application.isPlaying ? GetForward2D() : GetEditorForward2D();

        float half = viewAngle * 0.5f;
        Quaternion qL = Quaternion.Euler(0, 0, +half);
        Quaternion qR = Quaternion.Euler(0, 0, -half);

        Vector2 left = qL * fwd;
        Vector2 right = qR * fwd;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + left * viewRadius);
        Gizmos.DrawLine(origin, origin + right * viewRadius);

        // Arco pieno (richiede UnityEditor.Handles)
        Handles.color = new Color(0, 1, 1, 0.15f);
        Handles.DrawSolidArc(origin, Vector3.forward, right, viewAngle, viewRadius);
    }

    private Vector2 GetEditorForward2D()
    {
        // In editor, se sr/flip sono disponibili, ricava la "destra visiva"
        if (sr != null)
        {
            bool flippedMeansLeft = spriteFacesRight ? sr.flipX : !sr.flipX;
            float sign = flippedMeansLeft ? -1f : 1f;
            return (Vector2)transform.right * sign;
        }

        // Fallback dai dati di patrol
        if (patrolPointA != null && patrolPointB != null)
        {
            Vector2 dir = (goingToB ? (patrolB - patrolA) : (patrolA - patrolB));
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }
        if (lineDirection.sqrMagnitude > 0.0001f)
            return (goingToB ? lineDirection : -lineDirection).normalized;

        return Vector2.right;
    }
#endif


    // Chiamato da tutti gli handler di contatto (trigger/collisione)
    private void ProcessContact(Collider2D other)
    {
        // Cooldown
        if (Time.time < nextContactTime) return;

        // Stato (opzionale)
        if (damageOnlyWhenChasing && state != State.Chase && state != State.Attack)
            return;

        // Solo il Player: verifica tramite riferimento Transform
        bool isPlayerHit = false;
        if (player != null)
        {
            // colpo su root o child del player
            isPlayerHit = (other.transform == player) || other.transform.IsChildOf(player);
        }
        else
        {
            // fallback: se non hai il riferimento, prova col Tag "Player"
            isPlayerHit = other.CompareTag("Player") || (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player"));
        }

        if (!isPlayerHit) return;

        // (Opzionale) richiedi che il player sia dentro l'acqua
        if (requirePlayerInWater && waterArea != null)
        {
            if (!waterArea.OverlapPoint(other.bounds.center))
            {
                if (debugContactLogs) Debug.Log("[Shark] Player fuori dall'acqua → niente danno a contatto.");
                return;
            }
        }

        // Trova qualcosa di danneggiabile sul player (IDamageable preferito; fallback PlayerHealth)
        IDamageable damageable =
              other.GetComponentInParent<IDamageable>()
           ?? other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(contactDamage);
            if (debugContactLogs) Debug.Log($"[Shark] Danno a contatto (IDamageable): {contactDamage}");
            nextContactTime = Time.time + contactCooldown;
            return;
        }

        // Fallback: PlayerHealth semplice (se non implementa IDamageable)
        var ph = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(contactDamage);
            if (debugContactLogs) Debug.Log($"[Shark] Danno a contatto (PlayerHealth fallback): {contactDamage}");
            nextContactTime = Time.time + contactCooldown;
            return;
        }

        if (debugContactLogs) Debug.LogWarning("[Shark] Nessun componente IDamageable/PlayerHealth trovato sul Player colpito.");
    }

    // ----- HANDLER per TRIGGER e COLLISIONI -----
    // Usa entrambi così funziona sia con collider di tipo Trigger che non-Trigger.

    private void OnTriggerEnter2D(Collider2D other) { ProcessContact(other); }
    private void OnTriggerStay2D(Collider2D other) { ProcessContact(other); }
    private void OnCollisionEnter2D(Collision2D col) { ProcessContact(col.collider); }
    private void OnCollisionStay2D(Collision2D col) { ProcessContact(col.collider); }


    /// <summary>
    /// Rileva se c'è qualcosa davanti allo squalo su 'turnOnLayers'.
    /// Ignora il proprio collider. Restituisce anche la normale d'impatto (se serve).
    /// </summary>
    private bool DetectHitAhead(out Vector2 hitNormal)
    {
        hitNormal = Vector2.zero;

        if (turnOnLayers == 0) return false;

        Vector2 dir =
            (CurrentVelocity.sqrMagnitude > 0.0001f) ? CurrentVelocity.normalized :
            (facingDir.sqrMagnitude > 0.0001f) ? facingDir.normalized :
            (Vector2)transform.right;

        // Origine del cast leggermente davanti
        Vector2 origin = rb.position + dir * Mathf.Max(0.01f, sensorRadius);

        // Usiamo CircleCastAll per poter saltare il self-collider
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, sensorRadius, dir, sensorDistance, turnOnLayers);
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider == selfCollider) continue;                       // ignora se stesso
            if (h.rigidbody != null && h.rigidbody == rb) continue;         // ignora se stesso (rigidbody)
                                                                            // trovato qualcosa di valido
            hitNormal = h.normal;
            return true;
        }
        return false;
    }



    /// <summary>
    /// Esegue il "turn back": flip visivo, inversione direzione/facing e imposta una velocità iniziale
    /// lontano dall'ostacolo. Applica anche un piccolo spostamento per non restare incastrato.
    /// </summary>
    private void TurnBack(Vector2 awayDir)
    {

        if (Time.time - _lastTurnTime < turnCooldown)
            return;

        _lastTurnTime = Time.time;

        // ❌ Non toccare sr.flipX qui: lo gestisce RotateTowardsVelocity()
        awayDir = awayDir.sqrMagnitude > 0.0001f ? awayDir.normalized
                                                 : (facingDir.sqrMagnitude > 0.0001f ? -facingDir
                                                                                      : -(Vector2)transform.right);

        // Aggiorna facing e dai una spinta iniziale sufficiente
        facingDir = awayDir;
        CurrentVelocity = awayDir * Mathf.Max(patrolSpeed * 0.8f, 1.0f);

        // Allontanati un pochino per uscire da overlap
        rb.position += awayDir * pushBackDistance;

        // Inverti il target della patrol line
        goingToB = !goingToB;
    }


    private Vector2 GetForward2D()
    {
        if (facingDir.sqrMagnitude > 0.0001f)
            return facingDir.normalized;

        // Fallback: usa transform.right ma correggilo col flip e con spriteFacesRight
        float sign = 1f;
        if (sr != null)
        {
            // Se lo sprite base guarda a destra, flipX=true significa "guarda a sinistra" => sign=-1
            // Se lo sprite base guarda a sinistra, flipX=false significa "sinistra" => sign=-1
            bool flippedMeansLeft = spriteFacesRight ? sr.flipX : !sr.flipX;
            sign = flippedMeansLeft ? -1f : 1f;
        }
        return (Vector2)transform.right * sign;
    }


  
    /// <summary>
    /// Allinea facingDir e il flip visivo allo stato iniziale della patrol line,
    /// così gli squali che partono verso sinistra non sono in "retromarcia".
    /// Chiamalo dopo SetupPatrolLine() in Start().
    /// </summary>
    private void SyncInitialFacingAndFlip()
    {
        // 1) Direzione iniziale desiderata verso il primo target di patrol
        Vector2 dir = GetForward2D();

        if (patrolPointA != null && patrolPointB != null)
        {
            Vector2 target = goingToB ? patrolB : patrolA;
            dir = (target - (Vector2)transform.position);
        }
        else if (lineDirection.sqrMagnitude > 0.0001f)
        {
            dir = goingToB ? lineDirection : -lineDirection;
        }

        if (dir.sqrMagnitude > 0.0001f)
            facingDir = dir.normalized;

        // 2) Forza subito il flip coerente con facingDir (anche se la velocità è zero)
        ApplyFlipFromFacing();
    }

    /// <summary>
    /// Imposta sr.flipX in modo deterministico a partire da facingDir,
    /// usando la stessa mappatura angolare di RotateTowardsVelocity.
    /// </summary>
    /// 
    private void ApplyFlipFromFacing()
    {
        if (sr == null) return;

        float rawAngle = Mathf.Atan2(facingDir.y, facingDir.x) * Mathf.Rad2Deg;
        rawAngle = Mathf.DeltaAngle(0f, rawAngle);

        bool flip;
        MapAngleAndFlip(rawAngle, out flip); // usa il tuo helper già presente

        sr.flipX = spriteFacesRight ? flip : !flip;
    }
}


using UnityEngine;

/// <summary>
/// FSM AD HOC DU BANDIT (GameObject « Capsule ») — Chapitre 3, diapos 6 / 8 / 9 / 10.
///
/// C'est LE script de la méthode ad hoc : il décide, il ne cherche pas de chemin.
///
/// Rôles dans la scène :
///   - « Capsule » = le BANDIT : il poursuit l'Agent, l'attaque, et l'empêche
///                   d'atteindre l'objectif ;
///   - « Agent »   = celui qui cherche à rejoindre l'objectif (feu de camp),
///                   déplacé par A* sur la grille.
///
/// Le bandit se déplace en LIGNE DROITE vers sa cible (pas de grille, pas de A*) :
/// c'est le cours d'IA ad hoc, la décision compte plus que le chemin.
///
/// États du cours : Wait (W) / Move (M) / Follow (F) / Attack (A) / RunAway (R)
/// (+ Search, extension quand la cible est injoignable).
///
/// Événements (diapo 10), convertis en unités Unity :
///   Event 1 : dist > 15          Event 2 : 8 < dist <= 15
///   Event 3 : 3 < dist <= 8      Event 4 : 0 < dist <= 3
///   Event 5 : dist <= 0          Event 6 : PP_attacking = true
/// </summary>
public class GuardFSM : MonoBehaviour
{
    public enum State
    {
        Wait,
        Move,
        Follow,
        Attack,
        RunAway,
        Search           // extension : cible injoignable / perdue
    }

    [Header("Références")]
    [Tooltip("L'Agent : la cible à empêcher d'arriver à l'objectif.")]
    public Transform player;
    [Tooltip("Option : contrôleur clavier de l'Agent (PP_attacking par Espace).")]
    public BanditController playerController;
    [Tooltip("Contrôleur automatique de l'Agent (lit aussi PP_attacking).")]
    public GoalSeeker playerSeeker;
    [Tooltip("Allers/Retours (Move) : points de patrouille du bandit.")]
    public Transform[] patrolWaypoints;

    [Header("Déplacement (ligne droite)")]
    [Tooltip("Vitesse de marche du bandit.")]
    public float moveSpeed = 5f;
    [Tooltip("Vitesse de rotation (deg/s).")]
    public float rotationSpeed = 8f;
    [Tooltip("Distance à laquelle il considère être arrivé à son point.")]
    public float arrivalDistance = 0.6f;

    [Header("Événements (unité de distance)")]
    public float distFar = 15f;      // Event 1 : distance  > distFar
    public float distMedium = 8f;    // Event 2 : distMedium < d <= distFar
    public float distClose = 3f;     // Event 3 : distClose < d <= distMedium
                                     // Event 4 : d <= distClose

    [Header("Comportement")]
    public float waitDuration = 3f;
    public float attackDuration = 2f;        // temps passé à attaquer avant de reprendre
    [Tooltip("Délai minimum entre deux attaques (évite la boucle Attack/Follow).")]
    public float attackCooldown = 4f;
    [Tooltip("Après une attaque, le bandit reprend sa patrouille pendant ce temps (diapo 8).")]
    public float attackRecovery = 2f;
    [Tooltip("Après une attaque, retour à la patrouille en marchant (diapo 8).")]
    public bool returnToPatrolAfterAttack = true;
    public float searchDuration = 4f;        // « regarde autour » avant d'abandonner
    public float searchCooldown = 6f;        // pause avant de retenter
    [Tooltip("Rayon de patrouille autour de l'objectif quand aucun waypoint n'est fourni.")]
    public float patrolRadius = 8f;
    public float runAwayMaxDuration = 3f;    // garde-fou si la fuite est impossible
    public float runAwayDistance = 10f;      // distance du point de fuite
    public float stateCooldown = 0.4f;       // anti-clignotement entre deux transitions

    [Header("Débogage")]
    public bool logTransitions = true;

    State currentState = State.Wait;
    float waitTimer;
    float attackTimer;
    float searchTimer;
    float cooldownTimer;
    float runAwayTimer;
    float attackCooldownTimer;
    float reactionPauseTimer;
    float searchCooldownTimer;
    float ignorePlayerTimer;
    int currentWaypointIndex;
    Vector3 moveTarget;          // destination courante (waypoint, point de fuite...)
    bool hasMoveTarget;
    Vector3 fleeTarget;

    public State CurrentState => currentState;

    void Start()
    {
        if (player == null)
        {
            // L'Agent s'appelle « Agent » dans la scène.
            GameObject go = GameObject.Find("Agent");
            if (go != null) player = go.transform;
        }
        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<BanditController>();
        }
        if (playerSeeker == null && player != null)
        {
            playerSeeker = player.GetComponent<GoalSeeker>();
        }

        if (player == null)
        {
            Debug.LogError("GuardFSM : aucune cible 'Player' (l'Agent) assignée !");
            enabled = false;
            return;
        }

        if (patrolWaypoints == null) patrolWaypoints = new Transform[0];

        PickNextPatrolPoint();
        EnterState(State.Wait, true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cooldownTimer -= dt;
        attackCooldownTimer -= dt;
        reactionPauseTimer -= dt;
        searchCooldownTimer -= dt;
        ignorePlayerTimer -= dt;

        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // Event 6 : PP_attacking vient soit du clavier, soit de la riposte de l'Agent.
        bool playerIsAttacking =
            (playerController != null && playerController.isAttacking) ||
            (playerSeeker != null && playerSeeker.isAttacking);

        // ---------------- TRANSITIONS ----------------
        switch (currentState)
        {
            case State.Wait:
                waitTimer += dt;
                if (waitTimer >= waitDuration && cooldownTimer <= 0f)
                {
                    EnterState(State.Move);
                }
                else if (distance <= distMedium && reactionPauseTimer <= 0f && ignorePlayerTimer <= 0f)
                {
                    EnterState(State.Follow);           // Event 2
                }
                break;

            case State.Move:
                if (distance <= distClose && CanAttack())
                {
                    EnterState(State.Attack);           // Event 3 / 4
                }
                else if (distance <= distMedium && reactionPauseTimer <= 0f && ignorePlayerTimer <= 0f)
                {
                    EnterState(State.Follow);           // Event 2
                }
                else if (hasMoveTarget &&
                         Vector3.Distance(transform.position, moveTarget) <= arrivalDistance)
                {
                    EnterState(State.Wait);             // point atteint
                }
                break;

            case State.Follow:
                if (ignorePlayerTimer > 0f && cooldownTimer <= 0f)
                {
                    EnterState(State.Move);             // il a « perdu » l'Agent
                }
                else if (distance > distFar)
                {
                    EnterState(State.Move);             // Event 1
                }
                else if (distance <= distClose && CanAttack())
                {
                    EnterState(State.Attack);           // Event 4
                }
                break;

            case State.Attack:
                attackTimer += dt;
                if (playerIsAttacking)
                {
                    EnterState(State.RunAway);          // Event 6
                }
                else if (attackTimer >= attackDuration)
                {
                    // Diapo 8 : une fois qu'il a touché le PP, le PNJ revient
                    // à l'état de déplacement (allers/retours).
                    attackCooldownTimer = attackCooldown;
                    reactionPauseTimer = attackRecovery;

                    if (returnToPatrolAfterAttack || distance > distMedium)
                        EnterState(State.Move);
                    else
                        EnterState(State.Follow);
                }
                break;

            case State.RunAway:
                runAwayTimer += dt;
                if (distance > distFar && cooldownTimer <= 0f)
                {
                    EnterState(State.Move);             // Event 1
                }
                else if (runAwayTimer >= runAwayMaxDuration)
                {
                    EnterState(distance > distMedium ? State.Move : State.Follow);
                }
                break;

            case State.Search:
                searchTimer += dt;
                if (searchTimer >= searchDuration)
                {
                    searchCooldownTimer = searchCooldown;
                    ignorePlayerTimer = searchCooldown;
                    EnterState(State.Move);
                }
                break;
        }

        ExecuteBehavior(dt);
    }

    /// <summary>Change d'état, remet les timers, prépare la destination.</summary>
    void EnterState(State newState, bool force = false)
    {
        if (!force && currentState == newState) return;

        if (logTransitions && currentState != newState)
        {
            Debug.Log($"[PNJ] {currentState} ---> {newState}  (t={Time.time:F2})");
        }

        State previous = currentState;
        currentState = newState;
        waitTimer = 0f;
        attackTimer = 0f;
        searchTimer = 0f;
        runAwayTimer = 0f;
        cooldownTimer = stateCooldown;

        if (newState == State.Move && previous != State.Move)
        {
            // Allers/retours : nouveau point à chaque entrée dans Move.
            PickNextPatrolPoint();
        }

        if (newState == State.RunAway)
        {
            // Point de fuite : à l'opposé de l'Agent.
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = transform.forward;
            fleeTarget = transform.position + away.normalized * runAwayDistance;
        }
    }

    // ---------------- COMPORTEMENT (déplacement en ligne droite) ----------------
    void ExecuteBehavior(float dt)
    {
        switch (currentState)
        {
            case State.Wait:
                // immobile : le PNJ attend dans son environnement (diapo 8)
                break;

            case State.Move:
                if (hasMoveTarget) MoveTowards(moveTarget, dt);
                break;

            case State.Follow:
                MoveTowards(player.position, dt);          // poursuite
                break;

            case State.Attack:
                FaceTarget(player.position, dt);           // il reste sur place et attaque
                break;

            case State.RunAway:
                MoveTowards(fleeTarget, dt);               // fuite
                if (Vector3.Distance(transform.position, fleeTarget) <= arrivalDistance)
                {
                    // Coin de map : on arrête de fuir plutôt que de vibrer.
                    EnterState(State.Move);
                }
                break;

            case State.Search:
                transform.Rotate(Vector3.up, 90f * dt, Space.World);   // regarde autour
                break;
        }
    }

    /// <summary>Avance en ligne droite vers une position (aucun pathfinding).</summary>
    void MoveTowards(Vector3 destination, float dt)
    {
        Vector3 flat = destination - transform.position;
        flat.y = 0f;

        if (flat.magnitude <= arrivalDistance)
        {
            FaceTarget(destination, dt);
            return;
        }

        Vector3 step = flat.normalized * moveSpeed * dt;
        transform.position += step;
        FaceTarget(destination, dt);
    }

    void FaceTarget(Vector3 destination, float dt)
    {
        Vector3 flat = destination - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * dt);
    }

    // ---------------- Patrouille (allers/retours) ----------------
    void PickNextPatrolPoint()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            Transform wp = patrolWaypoints[currentWaypointIndex];
            if (wp != null)
            {
                moveTarget = wp.position;
                hasMoveTarget = true;
                return;
            }
        }

        moveTarget = PickRandomPointAround();
        hasMoveTarget = true;
    }

    bool ReachedMoveTarget()
    {
        return hasMoveTarget && Vector3.Distance(transform.position, moveTarget) <= arrivalDistance;
    }

    /// <summary>
    /// Point aléatoire autour de l'objectif (feu de camp) : le bandit fait des
    /// allers/retours devant l'objectif, c'est comme ça qu'il bloque l'Agent.
    /// </summary>
    Vector3 PickRandomPointAround()
    {
        Vector3 center = transform.position;

        // On se centre sur l'objectif si la scène en contient un.
        GameObject campfire = GameObject.Find("Campfire");
        if (campfire != null) center = campfire.transform.position;

        Vector2 offset = Random.insideUnitCircle * patrolRadius;
        Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
        point.y = transform.position.y;
        return point;
    }

    bool CanAttack()
    {
        return attackCooldownTimer <= 0f && reactionPauseTimer <= 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distFar);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distMedium);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distClose);

        if (patrolWaypoints == null) return;
        Gizmos.color = Color.blue;
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] == null) continue;
            Gizmos.DrawSphere(patrolWaypoints[i].position, 0.25f);
            if (i > 0 && patrolWaypoints[i - 1] != null)
            {
                Gizmos.DrawLine(patrolWaypoints[i - 1].position, patrolWaypoints[i].position);
            }
        }
    }
}

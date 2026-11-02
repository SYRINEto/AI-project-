using UnityEngine;

/// <summary>
/// L'AGENT va vers l'OBJECTIF en suivant la grille (A*).
///
/// Rôles dans la scène :
///   - « Agent »   = celui qui cherche à rejoindre l'objectif (feu de camp) ;
///   - « Capsule » = le bandit (GuardFSM) : il le poursuit, l'attaque et
///                   l'empêche d'arriver ;
///   - « MCTSChaser » = le chasseur du chapitre 4 : il lui coupe la route.
///
/// Ce script ne contient AUCUN algorithme : il dit simplement à
/// l'AStarPathfinding « va vers l'objectif », et il réagit aux deux ennemis :
///   - le bandit s'approche trop  -> l'Agent fait un détour (il ne passe pas) ;
///   - le bandit l'attaque        -> l'Agent riposte (PP_attacking, Event 6) ;
///   - le chasseur le touche      -> l'Agent est bloqué 1,5 s.
///
/// L'A* peut être sur ce GameObject OU sur « PathfindingManager » : dans les
/// deux cas, c'est l'Agent qui est déplacé (Agent = ce transform).
/// </summary>
public class GoalSeeker : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("L'A* qui déplace l'Agent (celui de PathfindingManager). Vide : cherché automatiquement.")]
    public AStarPathfinding pathfinding;
    [Tooltip("Le point à rejoindre (feu de camp).")]
    public Transform objective;

    [Header("Face au bandit")]
    [Tooltip("Si le bandit est plus près que ça, l'Agent fait un détour au lieu d'avancer.")]
    public float dangerDistance = 4f;
    [Tooltip("Durée d'un détour.")]
    public float detourDuration = 2.5f;
    [Tooltip("Longueur du détour (point à l'opposé du bandit, sur la grille).")]
    public float detourDistance = 8f;
    [Tooltip("Temps d'attente avant de repartir vers l'objectif.")]
    public float recoverDelay = 1.5f;
    [Tooltip("Distance à laquelle l'objectif est considéré comme atteint.")]
    public float arriveDistance = 2f;

    [Header("Face au chasseur MCTS")]
    [Tooltip("Il se bloque quand le chasseur MCTS l'attrape (sinon le MCTS n'a aucun effet).")]
    public bool blockedByChaser = true;
    [Tooltip("Détour quand le chasseur approche. Plus petit que pour le bandit : sinon il ne le touche jamais.")]
    public float chaserDangerDistance = 2.2f;
    [Tooltip("Durée du blocage quand le chasseur le touche.")]
    public float caughtDuration = 1.5f;
    [Tooltip("Temps mini entre deux blocages (sinon il est bloqué en permanence).")]
    public float catchCooldown = 4f;

    [Header("Riposte (Event 6)")]
    [Tooltip("L'Agent riposte quand le bandit l'attaque (=> Event 6 : le bandit fuit).")]
    public bool fightBack = true;
    [Tooltip("Durée de la riposte.")]
    public float fightBackDuration = 1.5f;
    [Tooltip("Pause entre deux ripostes (sinon le bandit fuit en permanence).")]
    public float fightBackCooldown = 6f;

    [Header("Débogage")]
    public bool logTransitions = true;

    public enum Mode
    {
        ToObjective,   // il marche vers l'objectif
        Detour,        // il s'écarte du bandit / du chasseur
        Recover        // il attend, puis repart
    }

    public Mode CurrentMode { get; private set; } = Mode.ToObjective;
    public bool HasArrived { get; private set; }

    /// <summary>PP_attacking — lu par GuardFSM pour l'Event 6.</summary>
    public bool isAttacking { get; private set; }

    Vector3 detourTarget;
    float detourTimer;
    float recoverTimer;
    float attackTimer;
    float attackCooldown;
    float caughtTimer;
    float catchCooldownTimer;
    MCTSChaser chaser;

    // Initialisation : lie l'Agent à A* et lance le premier chemin vers l'objectif (feu de camp).
    void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = FindFirstObjectByType<AStarPathfinding>();

        if (pathfinding == null)
        {
            Debug.LogError("GoalSeeker : aucun AStarPathfinding dans la scène.");
            enabled = false;
            return;
        }

        // ⚠️ C'est l'AGENT qui suit ce chemin.
        pathfinding.agent = transform;

        if (objective == null && pathfinding.defaultTarget != null)
        {
            objective = pathfinding.defaultTarget;
        }
        if (objective == null)
        {
            Debug.LogError("GoalSeeker : aucun objectif assigné (feu de camp).");
            enabled = false;
            return;
        }

        chaser = FindFirstObjectByType<MCTSChaser>();

        pathfinding.MoveTo(objective.position);
        Debug.Log("[AGENT] en route vers l'objectif. Il suit la grille (A*).");
    }

    // Boucle principale : gère la riposte (Event 6), le blocage par le chasseur,
    // l'arrivée, puis les modes (Objectif / Détour / Récupération).
    void Update()
    {
        if (pathfinding == null || objective == null) return;

        float dt = Time.deltaTime;

        // Le bandit = la Capsule, qui porte GuardFSM.
        GuardFSM bandit = FindFirstObjectByType<GuardFSM>();
        Transform banditT = bandit != null ? bandit.transform : null;
        float distanceToBandit = banditT != null
            ? Vector3.Distance(transform.position, banditT.position)
            : float.MaxValue;

        // ---- riposte (Event 6) ----
        if (attackCooldown > 0f) attackCooldown -= dt;
        if (fightBack && banditT != null)
        {
            bool banditAttacks = bandit != null && bandit.CurrentState == GuardFSM.State.Attack;
            if (banditAttacks && distanceToBandit <= dangerDistance
                && attackTimer <= 0f && attackCooldown <= 0f)
            {
                attackTimer = fightBackDuration;
                attackCooldown = fightBackCooldown;
                Debug.Log("[AGENT] riposte -> PP_attacking = true (Event 6)");
            }
        }
        if (attackTimer > 0f) attackTimer -= dt;
        isAttacking = attackTimer > 0f;

        // ---- le chasseur MCTS l'a attrapé : il est bloqué un instant ----
        if (blockedByChaser && chaser != null)
        {
            float dChaser = Vector3.Distance(transform.position, chaser.transform.position);
            if (dChaser <= chaser.catchDistance && caughtTimer <= 0f && catchCooldownTimer <= 0f)
            {
                caughtTimer = caughtDuration;
                catchCooldownTimer = catchCooldown;
                Debug.Log("[AGENT] attrapé par le chasseur MCTS -> bloqué 1,5 s.");
            }
            catchCooldownTimer -= dt;
        }
        if (caughtTimer > 0f)
        {
            caughtTimer -= dt;
            pathfinding.Stop();
            return;
        }

        // ---- objectif atteint ----
        if (!HasArrived && Vector3.Distance(transform.position, objective.position) <= arriveDistance)
        {
            HasArrived = true;
            pathfinding.Stop();
            if (logTransitions) Debug.Log($"[AGENT] objectif atteint (t={Time.time:F1}s) !");
            return;
        }
        if (HasArrived) return;

        // ---------------- TRANSITIONS ----------------
        switch (CurrentMode)
        {
            case Mode.ToObjective:
                if (banditT != null && distanceToBandit <= dangerDistance)
                {
                    StartDetour(banditT);
                }
                else if (chaser != null &&
                         Vector3.Distance(transform.position, chaser.transform.position) <= chaserDangerDistance)
                {
                    StartDetour(chaser.transform);
                }
                break;

            case Mode.Detour:
                detourTimer -= dt;
                if (detourTimer <= 0f || (banditT != null && distanceToBandit > dangerDistance * 2f))
                {
                    EnterRecover();
                }
                break;

            case Mode.Recover:
                recoverTimer -= dt;
                if (recoverTimer <= 0f)
                {
                    CurrentMode = Mode.ToObjective;
                    pathfinding.MoveTo(objective.position);
                    if (logTransitions) Debug.Log("[AGENT] il repart vers l'objectif.");
                }
                break;
        }

        // ---------------- COMPORTEMENT ----------------
        if (CurrentMode == Mode.ToObjective)
        {
            pathfinding.MoveTo(objective.position);
        }
        else if (CurrentMode == Mode.Detour)
        {
            pathfinding.MoveTo(detourTarget);
        }
        else
        {
            pathfinding.Stop();
        }
    }

    /// <summary>Point à l'opposé de l'ennemi, ramené sur une case marchable de la grille.</summary>
    void StartDetour(Transform ennemi)
    {
        Vector3 away = transform.position - ennemi.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        Vector3 target = transform.position + away.normalized * detourDistance;

        if (pathfinding.grid != null)
        {
            GridGenerator g = pathfinding.grid;

            // On reste dans la grille.
            Vector3 center = g.transform.position;
            float halfX = g.gridSizeX / 2f - 1f;
            float halfZ = g.gridSizeZ / 2f - 1f;
            target.x = Mathf.Clamp(target.x, center.x - halfX, center.x + halfX);
            target.z = Mathf.Clamp(target.z, center.z - halfZ, center.z + halfZ);

            // Et sur une case marchable.
            Node node = g.NodeFromWorldPoint(target);
            if (node != null) target = node.worldPosition;
        }

        detourTarget = target;
        detourTimer = detourDuration;
        CurrentMode = Mode.Detour;

        if (logTransitions) Debug.Log("[AGENT] ennemi trop proche -> détour.");
    }

    void EnterRecover()
    {
        CurrentMode = Mode.Recover;
        recoverTimer = recoverDelay;
        pathfinding.Stop();
        if (logTransitions) Debug.Log("[AGENT] il reprend son souffle.");
    }

    void OnDrawGizmosSelected()
    {
        if (objective != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, objective.position);
            Gizmos.DrawWireSphere(objective.position, arriveDistance);
        }
        Gizmos.color = isAttacking ? Color.red : new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, dangerDistance);
    }
}
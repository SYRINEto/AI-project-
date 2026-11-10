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

    [Header("Mesures (soutenance)")]
    [Tooltip("Temps du dernier vrai recalcul A* (ms) — lu par ComparisonHUD.")]
    public float lastDecisionMs;
    [Tooltip("Nombre de recalculs A* depuis le début.")]
    public int decisionCount;

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

    // Boucle principale : riposte (Event 6), blocage par le chasseur, arrivée, puis les modes.
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
        // On ne chronomètre que le VRAI recalcul A* (MoveTo ne recalcule que si
        // la destination change) — sinon on mesurerait une comparaison de Vector3.
        int before = pathfinding.searchCount;

        if (CurrentMode == Mode.ToObjective)
        {
            var chrono = System.Diagnostics.Stopwatch.StartNew();
            pathfinding.MoveTo(objective.position);
            chrono.Stop();
            if (pathfinding.searchCount != before)
            {
                lastDecisionMs = (float)chrono.Elapsed.TotalMilliseconds;
                decisionCount++;
            }
        }
        else if (CurrentMode == Mode.Detour)
        {
            var chrono = System.Diagnostics.Stopwatch.StartNew();
            pathfinding.MoveTo(detourTarget);
            chrono.Stop();
            if (pathfinding.searchCount != before)
            {
                lastDecisionMs = (float)chrono.Elapsed.TotalMilliseconds;
                decisionCount++;
            }
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
        }

        detourTarget = FindWalkableDetour(target);
        detourTimer = detourDuration;
        CurrentMode = Mode.Detour;

        if (logTransitions) Debug.Log("[AGENT] ennemi trop proche -> détour.");
    }

    /// <summary>
    /// Cherche la case marchable la plus proche autour du point voulu.
    /// Sans ça, le détour peut viser un arbre : A* répond « cible inatteignable »
    /// puis se replie sur n'importe quelle case, et l'Agent part au hasard.
    /// </summary>
    Vector3 FindWalkableDetour(Vector3 wanted)
    {
        if (pathfinding.grid == null) return wanted;

        GridGenerator g = pathfinding.grid;
        Node start = g.NodeFromWorldPoint(transform.position);
        Node best = null;
        float bestScore = float.MaxValue;

        // On regarde les cases autour du point voulu (rayon de 5 cases).
        for (int dx = -5; dx <= 5; dx++)
        {
            for (int dz = -5; dz <= 5; dz++)
            {
                Node n = g.NodeFromWorldPoint(wanted + new Vector3(dx, 0f, dz));
                if (n == null || !n.isWalkable) continue;

                float score = Vector3.Distance(n.worldPosition, wanted);
                if (start != null && start == n) score += 2f;   // on évite de rester sur place
                if (score < bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
        }

        return best != null ? best.worldPosition : transform.position;
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
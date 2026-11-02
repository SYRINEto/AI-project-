using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MONTE CARLO TREE SEARCH (chapitre 4) — le « chasseur ».
///
/// Rôles dans la scène :
///   - Le chasseur ne suit AUCUNE règle écrite (pas de « si distance < 3 alors attaque »).
///     À chaque décision il SIMULE des centaines de parties au hasard dans sa tête,
///     puis joue le coup qui a le meilleur taux de victoire.
///   - Contrairement au bandit (GuardFSM, chapitre 3), il n'attaque personne :
///     son objectif est de bloquer le chemin de l'Agent vers le feu de camp.
///   - Il se déplace sur LA MÊME grille que l'Agent (GridGenerator / Node).
///
/// Les 4 étapes du cours, dans l'ordre :
///   1. Sélection          -> on descend dans l'arbre avec la formule UCB
///   2. Expansion          -> on ajoute un enfant non visité (marqué 0/0)
///   3. Simulation         -> on joue une partie jusqu'à la fin
///   4. Rétropropagation   -> on remonte le résultat jusqu'à la racine
///
/// La formule UCB du diapo :
///   UCB = (w / n) + c * sqrt( ln(t) / n )
///   w = victoires du nœud · n = visites du nœud · t = visites du parent · c = constante
///   - w/n            = EXPLOITATION (les branches qui ont déjà gagné)
///   - c*sqrt(...)    = EXPLORATION  (les branches encore peu visitées)
/// </summary>
[DisallowMultipleComponent]
public class MCTSChaser : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("La personne à bloquer (l'Agent).")]
    public Transform target;
    [Tooltip("Le point à défendre (feu de camp).")]
    public Transform objective;
    [Tooltip("La grille commune.")]
    public GridGenerator grid;

    [Header("Recherche MCTS")]
    [Tooltip("Nombre de parties simulées à chaque décision (plus = plus intelligent, plus lent).")]
    public int simulationsPerDecision = 250;
    [Tooltip("Constante c de la formule UCB : plus grand = plus curieux (exploration).")]
    public float explorationC = 1.414f;
    [Tooltip("Nombre de coups d'avance simulés dans une partie.")]
    public int rolloutDepth = 14;
    [Tooltip("Cadence des décisions (secondes). Entre deux décisions, il marche vers la case choisie.")]
    public float decisionInterval = 0.2f;
    [Tooltip("Si le coup choisi est la case où il est déjà, il attend ce délai avant de rejouer MCTS (économie CPU).")]
    public float idleRebackoff = 0.6f;
    [Tooltip("Nombre de cases voisines explorées à chaque étape (4 = grille du cours).")]
    public int branching = 4;

    [Header("Déplacement")]
    public float moveSpeed = 5.2f;
    public float rotationSpeed = 8f;
    [Tooltip("Arrivé à cette distance de la case visée, il s'arrête.")]
    public float arrivalDistance = 0.25f;

    [Header("Récompense (ce que le chasseur cherche à gagner)")]
    [Tooltip("Distance à laquelle on considère qu'il a attrapé l'Agent.")]
    public float catchDistance = 2.5f;

    [Header("Débogage")]
    public bool logDecisions = true;
    public bool drawGizmos = true;

    // résultat de la dernière décision (affiché dans l'Inspector / Gizmos)
    [Header("État (lecture seule)")]
    public int lastIterations;
    public int lastRootVisits;
    public float lastWinRate;
    public string lastChosenAction;
    [Tooltip("Position de la case que le chasseur a décidé de rejoindre.")]
    public Vector3 lastChosenTarget;
    public int totalSimulations;

    MCTSNode root;
    float decisionTimer;
    Vector3 currentTarget;
    bool hasTarget;

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<GridGenerator>();

        if (target == null)
        {
            GameObject go = GameObject.Find("Agent");
            if (go != null) target = go.transform;
        }

        if (objective == null)
        {
            GameObject go = GameObject.Find("Campfire");
            if (go != null) objective = go.transform;
        }

        if (grid == null || target == null || objective == null)
        {
            Debug.LogError("MCTSChaser : il manque la grille, l'Agent ou l'objectif.");
            enabled = false;
            return;
        }

        // La racine = la position actuelle du chasseur.
        root = new MCTSNode(null, transform.position, transform.position, LegalActions(transform.position));
        root.visits = 1;

        Decision();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        decisionTimer -= dt;
        if (decisionTimer <= 0f)
        {
            Decision();
            // S'il a décidé de rester sur place, on ne recalcule pas 5 fois par
            // seconde pour rien : on attend un peu (le CPU respire).
            decisionTimer = hasTarget && !IsCurrentTargetThisCell()
                ? decisionInterval
                : idleRebackoff;
        }

        if (hasTarget) MoveTowardsGridCell(currentTarget, dt);
    }

    // =====================================================================
    // LA DÉCISION : MCTS complet
    // =====================================================================
    void Decision()
    {
        // On repart de la case courante : l'arbre d'avant n'est plus valable
        // (l'Agent a bougé). C'est le compromis vitesse / qualité.
        root = new MCTSNode(null, transform.position, transform.position, LegalActions(transform.position));
        root.visits = 1;

        int done = 0;
        for (int i = 0; i < simulationsPerDecision; i++)
        {
            RunOneIteration(0);
            done++;
        }

        lastIterations = done;
        totalSimulations += done;
        lastRootVisits = root.visits;

        // On joue le fils le plus visité (= le coup le plus sûr).
        MCTSNode best = null;
        int bestVisits = -1;
        foreach (MCTSNode child in root.children)
        {
            if (child.visits > bestVisits)
            {
                bestVisits = child.visits;
                best = child;
            }
        }

        if (best != null && best.visits > 0)
        {
            currentTarget = best.action;
            hasTarget = true;
            lastWinRate = best.WinRate;
            lastChosenTarget = best.action;
            lastChosenAction = $"{best.action.x:F0},{best.action.z:F0}  (w/n = {best.wins:F0}/{best.visits}, {best.WinRate * 100f:F0}%)";

            if (logDecisions)
            {
                Debug.Log($"[MCTS] {done} simulations · coup choisi {lastChosenAction}");
            }
        }
    }

    /// <summary>Une itération = les 4 étapes du cours.</summary>
    void RunOneIteration(int depth)
    {
        MCTSNode node = root;

        // ---------- ÉTAPE 1 : SÉLECTION (UCB) ----------
        // On descend tant que le nœud est complètement développé.
        while (node.IsFullyExpanded && node.children.Count > 0)
        {
            node = BestChildUCB(node);
        }

        // ---------- ÉTAPE 2 : EXPANSION ----------
        // On développe UN enfant non visité, marqué 0/0 comme sur le diapo.
        if (!node.IsFullyExpanded)
        {
            Vector3 action = node.untriedActions[0];
            node.untriedActions.RemoveAt(0);

            Vector3 childState = MoveByOneCell(node.state, action);
            MCTSNode child = new MCTSNode(node, childState, action, LegalActions(childState));
            node.children.Add(child);
            node = child;
        }

        // ---------- ÉTAPE 3 : SIMULATION ----------
        // On joue une partie jusqu'à la configuration finale.
        // result vaut +1 (attrapé) ou -1 (l'Agent s'échappe) : on le ramène
        // dans [0,1] pour que w/n reste un taux de victoire, comme sur le diapo.
        float result = (Rollout(node.state) + 1f) * 0.5f;

        // ---------- ÉTAPE 4 : RÉTROPROPAGATION ----------
        // On remonte le résultat vers la racine : n+1 partout, et w+1 si on a gagné.
        while (node != null)
        {
            node.visits++;
            node.wins += result;
            node = node.parent;
        }
    }

    /// <summary>
    /// Formule UCB du diapo :
    /// UCB = (w / n) + c * sqrt( ln(t) / n )
    /// </summary>
    MCTSNode BestChildUCB(MCTSNode node)
    {
        MCTSNode best = null;
        float bestScore = float.MinValue;

        float lnParent = Mathf.Log(Mathf.Max(1f, node.visits));   // ln(t)

        foreach (MCTSNode child in node.children)
        {
            float n = Mathf.Max(1, child.visits);

            float exploitation = child.wins / n;                   // w / n
            float exploration = explorationC * Mathf.Sqrt(lnParent / n);  // c * sqrt(ln(t)/n)

            float ucb = exploitation + exploration;

            if (ucb > bestScore)
            {
                bestScore = ucb;
                best = child;
            }
        }

        return best ?? node;
    }

    // =====================================================================
    // ÉTAPE 3 — la simulation
    // =====================================================================
    /// <summary>
    /// Joue une partie depuis la position courante : l'Agent avance d'une case
    /// vers le feu de camp, le chasseur répond (80 % du temps il COUPE la route,
    /// 20 % du temps il joue au hasard = la part d'aléatoire du cours).
    /// Résultat : +1 s'il l'attrape, -1 si l'Agent touche le feu, 0 si match nul.
    /// </summary>
    float Rollout(Vector3 startPos)
    {
        Vector3 chaser = startPos;
        Vector3 agent = target.position;
        Vector3 goal = objective.position;

        for (int step = 0; step < rolloutDepth; step++)
        {
            // 1) Où sera l'Agent au prochain coup ? Il avance d'une case vers l'objectif.
            Vector3 predicted = StepTowards(agent, goal);

            // 2) Le chasseur joue : la plupart du temps il COUPE la route de l'Agent,
            //    parfois il se déplace au hasard (c'est la part d'aléatoire du cours).
            if (Random.value < 0.8f)
            {
                chaser = StepTowards(chaser, predicted);        // interception
            }
            else
            {
                List<Vector3> options = LegalActions(chaser);
                if (options.Count > 0) chaser = options[Random.Range(0, options.Count)];
            }

            // 3) L'Agent joue son coup
            agent = predicted;

            // 4) Attrapé ? -> victoire
            if (Vector3.Distance(chaser, agent) <= catchDistance) return 1f;

            // 5) L'Agent touche l'objectif ? -> défaite
            if (Vector3.Distance(agent, goal) <= 1.5f) return -1f;
        }

        // Ni l'un ni l'autre au bout des coups : match nul.
        return 0f;
    }

    // =====================================================================
    // Le plateau : la grille du chapitre 2
    // =====================================================================
    /// <summary>Les 4 cases voisines marchables (voisinage 4 directions de GridGenerator).</summary>
    List<Vector3> LegalActions(Vector3 position)
    {
        List<Vector3> actions = new List<Vector3>(branching);
        if (grid == null || grid.grid == null) return actions;

        Node here = grid.NodeFromWorldPoint(position);
        if (here == null) return actions;

        foreach (Node neighbour in grid.GetNeighbors(here))
        {
            if (neighbour != null && neighbour.isWalkable) actions.Add(neighbour.worldPosition);
        }

        return actions;
    }

    Vector3 MoveByOneCell(Vector3 from, Vector3 action)
    {
        Vector3 p = action;
        p.y = from.y;
        return p;
    }

    /// <summary>L'Agent simulé avance d'une case vers l'objectif.</summary>
    Vector3 StepTowards(Vector3 from, Vector3 to)
    {
        if (grid == null || grid.grid == null) return from;

        Node here = grid.NodeFromWorldPoint(from);
        Node goal = grid.NodeFromWorldPoint(to);
        if (here == null || goal == null) return from;

        Node best = here;
        int bestScore = int.MaxValue;

        foreach (Node neighbour in grid.GetNeighbors(here))
        {
            if (neighbour == null || !neighbour.isWalkable) continue;

            // distance de Manhattan sur la grille (comme dans A*)
            int score = Mathf.Abs(neighbour.gridX - goal.gridX)
                      + Mathf.Abs(neighbour.gridZ - goal.gridZ);

            if (score < bestScore)
            {
                bestScore = score;
                best = neighbour;
            }
        }

        Vector3 p = best.worldPosition;
        p.y = from.y;
        return p;
    }

    // =====================================================================
    // Déplacement réel
    // =====================================================================
    /// <summary>Le coup choisi est-il la case où il se trouve déjà ?</summary>
    bool IsCurrentTargetThisCell()
    {
        return Vector3.Distance(currentTarget, transform.position) <= arrivalDistance;
    }

    void MoveTowardsGridCell(Vector3 destination, float dt)
    {
        Vector3 flat = destination - transform.position;
        flat.y = 0f;

        if (flat.magnitude <= arrivalDistance)
        {
            hasTarget = false;
            return;
        }

        Vector3 step = flat.normalized * moveSpeed * dt;
        transform.position += step;

        Quaternion look = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * dt);
    }

    // =====================================================================
    // Gizmos : voir la décision
    // =====================================================================
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.9f);

        if (hasTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget);
            Gizmos.DrawWireCube(currentTarget, Vector3.one * 0.8f);
        }

        if (objective != null && target != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawLine(target.position, objective.position);
        }
    }
}
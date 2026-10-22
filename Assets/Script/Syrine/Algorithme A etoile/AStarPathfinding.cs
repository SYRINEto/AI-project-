using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Résultat d'une recherche A* (utile pour la FSM : savoir s'il faut renoncer).
/// </summary>
public enum PathStatus
{
    Complete,    // chemin trouvé jusqu'à la cible exacte
    Partial,     // cible non atteignable -> chemin vers la case marchable la plus proche
    Arrived,     // déjà sur la cible
    Unreachable  // aucune case atteignable, même proche de la cible
}

/// <summary>
/// Script de DÉPLACEMENT (Pathfinding) : il ne décide de rien.
/// C'est la couche ad hoc (GuardFSM / CapsuleFSM) qui choisit la destination
/// et le moment de la changer ; ici on ne fait que suivre le chemin sur la grille.
///
/// Corrections par rapport à la version du cours :
///  1) les coûts G/H/Parent sont RÉINITIALISÉS avant chaque recherche ;
///  2) la cible est « snapée » sur la case marchable la plus proche
///     (le bandit peut se cacher dans un buisson / contre une barrière) ;
///  3) si la cible est totalement inaccessible, on renvoie le chemin vers
///     la case ATTEIGNABLE la plus proche (Partial) au lieu de ne rien faire ;
///  4) le recalcul est limité dans le temps (repathInterval) et déclenché
///     seulement si la cible a bougé de targetMoveThreshold -> un bandit
///     dynamique ne coûte pas 60 recherches A* par seconde ;
///  5) détection de blocage : si l'agent ne progresse plus, on recalcule.
/// </summary>
public class AStarPathfinding : MonoBehaviour
{
    [Header("Références")]
    public GridGenerator grid;
    public Transform agent;              // si vide : le transform de ce GameObject
    public Transform defaultTarget;      // cible de départ (ex : feu de camp)

    [Header("Déplacement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float arrivalDistance = 0.5f;

    [Header("Recalcul dynamique")]
    [Tooltip("Intervalle minimal entre deux recherches A*.")]
    public float repathInterval = 0.25f;
    [Tooltip("La cible doit bouger de cette distance pour forcer un nouveau calcul.")]
    public float targetMoveThreshold = 0.6f;
    [Tooltip("Temps sans progression avant de considérer l'agent bloqué.")]
    public float stuckTimeout = 1.5f;

    [Header("Audio et Effets")]
    public AudioSource arrivalSound;
    public ParticleSystem arrivalParticles;

    [Header("Visualisation A*")]
    public bool showOpenSet = true;
    public bool showClosedSet = true;
    public bool showFinalPath = true;
    [Tooltip("Garde-fou : nombre max de cubes dessinés par liste (perf en Play mode).")]
    public int maxGizmoNodes = 400;

    // --- état interne ---
    private List<Node> currentPath;
    private int currentPathIndex;
    private bool hasArrived;
    private Vector3 currentDestination;
    private Vector3 lastComputedDestination = new Vector3(float.MaxValue, 0f, 0f);
    private float repathTimer;
    private float stuckTimer;
    private Vector3 lastAgentPosition;
    private PathStatus lastSearchStatus = PathStatus.Unreachable;
    [HideInInspector] public int searchCount;   // nombre de recherches A* effectuées

    // --- visualisation ---
    public List<Node> openSetVisual = new List<Node>();
    public List<Node> closedSetVisual = new List<Node>();

    public PathStatus LastSearchStatus => lastSearchStatus;
    public bool IsFollowingPath => currentPath != null && currentPath.Count > 0 && !hasArrived;
    public Vector3 CurrentDestination => currentDestination;

    private Transform Agent => agent != null ? agent : transform;

    void Start()
    {
        if (grid == null || grid.grid == null)
        {
            Debug.LogError("A* : la grille n'est pas initialisée (référence GridGenerator manquante).");
            enabled = false;
            return;
        }

        lastAgentPosition = Agent.position;

        if (defaultTarget != null)
        {
            MoveTo(defaultTarget.position);
        }
    }

    void Update()
    {
        if (currentPath != null && currentPath.Count > 0 && !hasArrived)
        {
            MoveAlongPath();
            CheckStuck();
        }
    }

    // =============================================================
    // API appelée par la FSM
    // =============================================================

    /// <summary>
    /// Demande d'aller vers une position. Appelable à chaque frame :
    /// la recherche A* n'est refaite que si nécessaire (cible qui bouge).
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        if (grid == null || grid.grid == null) return;

        bool destinationChanged = (destination - lastComputedDestination).sqrMagnitude
                                  > targetMoveThreshold * targetMoveThreshold;

        repathTimer += Time.deltaTime;

        bool pathConsumed = currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count;

        // Un chemin partiel est toujours « utilisable » (on s'approche).
        // Un chemin vide n'est réessayé que si la dernière recherche a échoué.
        bool noUsablePath = pathConsumed && lastSearchStatus != PathStatus.Partial;

        if (destinationChanged || (noUsablePath && repathTimer >= repathInterval))
        {
            ComputePath(destination);
        }

        // Si A* a recalculé, ComputePath a déjà remis hasArrived à false.
        // Si on ne recalcule pas et que l'agent est arrivé, il ne faut PAS
        // relancer un mouvement vers la même destination (sinon vibration).
        if (destinationChanged && !hasArrived)
        {
            hasArrived = false;
        }

        currentDestination = destination;
    }

    /// <summary>Arrête net le suivi de chemin (utilisé par l'état Attack / Wait).</summary>
    public void Stop()
    {
        currentPath = new List<Node>();
        currentPathIndex = 0;
        hasArrived = true;
    }

    /// <summary>Force un recalcul immédiat (obstacle dynamique apparu sur le chemin).</summary>
    public void RecalculatePath()
    {
        if (hasArrived) return;
        ComputePath(currentDestination);
    }

    /// <summary>La FSM demande : « suis-je arrivé à ma destination ? »</summary>
    public bool HasReachedDestination()
    {
        return hasArrived;
    }

    /// <summary>La cible est-elle totalement injoignable ? (pour que la FSM renonce)</summary>
    public bool IsTargetUnreachable()
    {
        return lastSearchStatus == PathStatus.Unreachable;
    }

    /// <summary>La dernière recherche n'a pu s'approcher que partiellement de la cible.</summary>
    public bool IsPartialPath()
    {
        return lastSearchStatus == PathStatus.Partial;
    }

    /// <summary>
    /// La cible n'est pas joignable proprement : soit aucune case atteignable,
    /// soit seulement un chemin partiel (le bandit est « derrière un mur »).
    /// C'est ce test que la FSM utilise pour passer en état Search.
    /// </summary>
    public bool IsTargetBlocked()
    {
        return lastSearchStatus == PathStatus.Partial || lastSearchStatus == PathStatus.Unreachable;
    }

    // =============================================================
    // Cœur de l'algorithme A*
    // =============================================================

    private void ComputePath(Vector3 destination)
    {
        searchCount++;
        currentDestination = destination;
        lastComputedDestination = destination;
        repathTimer = 0f;
        currentPathIndex = 0;
        hasArrived = false;

        List<Node> path;
        lastSearchStatus = FindPath(Agent.position, destination, out path);

        currentPath = path ?? new List<Node>();

        if (lastSearchStatus == PathStatus.Arrived)
        {
            hasArrived = true;
            OnArrival(destination);
        }
    }

    /// <summary>
    /// Recherche A*. Renvoie le statut et le chemin (jamais null).
    /// Signature « compatible cours » : FindPath(startPos, targetPos).
    /// </summary>
    public void FindPath(Vector3 startPos, Vector3 targetPos)
    {
        List<Node> path;
        lastSearchStatus = FindPath(startPos, targetPos, out path);
        currentPath = path;
        currentPathIndex = 0;
        hasArrived = lastSearchStatus == PathStatus.Arrived;
    }

    public PathStatus FindPath(Vector3 startPos, Vector3 targetPos, out List<Node> path)
    {
        path = new List<Node>();

        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        if (startNode == null || targetNode == null)
        {
            return PathStatus.Unreachable;
        }

        // --- 1) La cible tombe sur un obstacle : on prend la case marchable la plus proche ---
        Node goalNode = NearestWalkableNode(targetNode, 4);

        // --- 2) Réinitialisation des coûts (indispensable entre deux recherches) ---
        ResetAllNodes();

        // Si l'agent est posé sur une case « obstacle », on l'autorise à en sortir.
        bool startWasWalkable = startNode.isWalkable;
        startNode.isWalkable = true;

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, goalNode);
        openSet.Add(startNode);

        // Pour le repli « case atteignable la plus proche de la cible »
        Node closestReachable = startNode;
        float closestWorldDistance = Vector3.Distance(startNode.worldPosition, targetPos);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost ||
                    (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == goalNode)
            {
                RetracePath(startNode, goalNode, path);
                startNode.isWalkable = startWasWalkable;
                SaveVisualisation(openSet, closedSet);

                if (path.Count == 0) return PathStatus.Arrived;

                // Le goal était-il exactement la case demandée ?
                return (goalNode == targetNode) ? PathStatus.Complete : PathStatus.Partial;
            }

            // suivi de la case atteignable la plus proche de la position réelle visée
            float worldDist = Vector3.Distance(currentNode.worldPosition, targetPos);
            if (currentNode.isWalkable && worldDist < closestWorldDistance - 0.01f)
            {
                closestWorldDistance = worldDist;
                closestReachable = currentNode;
            }

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                int newMovementCost = currentNode.gCost + GetDistance(currentNode, neighbor);
                if (newMovementCost < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCost;
                    neighbor.hCost = GetDistance(neighbor, goalNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        // --- 3) Échec : on renvoie le meilleur chemin partiel trouvé ---
        RetracePath(startNode, closestReachable, path);
        startNode.isWalkable = startWasWalkable;
        SaveVisualisation(openSet, closedSet);

        if (path.Count == 0 && closestReachable != startNode)
        {
            path.Add(closestReachable);
        }

        if (closestReachable != startNode)
        {
            Debug.LogWarning($"A* : cible inatteignable, repli sur la case la plus proche ({closestWorldDistance:F1} u).");
            return PathStatus.Partial;
        }

        if (goalNode == startNode) return PathStatus.Arrived;
        return PathStatus.Unreachable;
    }

    private void ResetAllNodes()
    {
        for (int x = 0; x < grid.gridSizeXCount; x++)
        {
            for (int z = 0; z < grid.gridSizeZCount; z++)
            {
                grid.grid[x, z].ResetCosts();
            }
        }
    }

    /// <summary>Cherche la case marchable la plus proche en spirale (rayon max donné).</summary>
    private Node NearestWalkableNode(Node node, int maxRadius)
    {
        if (node.isWalkable) return node;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            Node best = null;
            float bestDist = float.MaxValue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;

                    int x = node.gridX + dx;
                    int z = node.gridZ + dz;
                    if (x < 0 || x >= grid.gridSizeXCount || z < 0 || z >= grid.gridSizeZCount) continue;

                    Node candidate = grid.grid[x, z];
                    if (!candidate.isWalkable) continue;

                    float d = (candidate.worldPosition - node.worldPosition).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = candidate;
                    }
                }
            }

            if (best != null)
            {
                Debug.Log($"A* : la cible est sur un obstacle -> voisin marchable à {Mathf.Sqrt(bestDist):F1} u.");
                return best;
            }
        }

        return node; // aucun voisin marchable : la recherche échouera proprement
    }

    private void RetracePath(Node startNode, Node endNode, List<Node> output)
    {
        output.Clear();

        if (endNode == null) return;

        Node currentNode = endNode;
        int guard = 0;

        while (currentNode != null && currentNode != startNode && guard++ < 100000)
        {
            output.Add(currentNode);
            currentNode = currentNode.parent;
        }

        output.Reverse();
    }

    /// <summary>
    /// Heuristique de Manhattan (4 voisins autorisés) : 10 par case.
    /// Si vous passez un jour aux 8 voisins, utilisez le couple 14 / 10 du cours.
    /// </summary>
    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstZ = Mathf.Abs(nodeA.gridZ - nodeB.gridZ);
        return 10 * (dstX + dstZ);
    }

    // =============================================================
    // Suivi du chemin
    // =============================================================

    private void MoveAlongPath()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            return;
        }

        Node targetNode = currentPath[currentPathIndex];
        Vector3 targetPosition = targetNode.worldPosition;
        targetPosition.y = Agent.position.y;

        Agent.position = Vector3.MoveTowards(Agent.position, targetPosition, moveSpeed * Time.deltaTime);

        Vector3 direction = targetPosition - Agent.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            Agent.rotation = Quaternion.Slerp(Agent.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // On valide une case dès qu'on l'a atteinte (rayon suffisant pour ne pas vibrer)
        if (Vector3.Distance(Agent.position, targetPosition) < 0.15f)
        {
            currentPathIndex++;

            if (currentPathIndex >= currentPath.Count)
            {
                CheckArrival();
            }
        }
    }

    private void CheckArrival()
    {
        if (hasArrived) return;

        float distanceToTarget = Vector3.Distance(Agent.position, currentDestination);
        if (distanceToTarget <= arrivalDistance || lastSearchStatus == PathStatus.Partial)
        {
            hasArrived = true;
            OnArrival(currentDestination);
        }
    }

    private void CheckStuck()
    {
        if (hasArrived) return;

        float moved = Vector3.Distance(Agent.position, lastAgentPosition);
        lastAgentPosition = Agent.position;

        if (moved < 0.005f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeout)
            {
                stuckTimer = 0f;
                Debug.Log("A* : agent bloqué -> recalcul forcé.");
                RecalculatePath();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void OnArrival(Vector3 destination)
    {
        // Effets uniquement à l'arrivée sur la cible par défaut, pour ne pas
        // jouer une fanfare à chaque waypoint de patrouille.
        if (defaultTarget != null &&
            Vector3.Distance(destination, defaultTarget.position) <= arrivalDistance * 2f)
        {
            if (arrivalSound != null) arrivalSound.Play();
            if (arrivalParticles != null) arrivalParticles.Play();
            Debug.Log("Le garde est arrivé au feu de camp !");
        }
    }

    private void SaveVisualisation(List<Node> openSet, HashSet<Node> closedSet)
    {
        if (!showOpenSet && !showClosedSet)
        {
            openSetVisual.Clear();
            closedSetVisual.Clear();
            return;
        }

        openSetVisual = new List<Node>(openSet);
        closedSetVisual = new List<Node>(closedSet);
    }

    // =============================================================
    // Gizmos
    // =============================================================
    void OnDrawGizmos()
    {
        if (grid == null || grid.grid == null) return;

        if (showOpenSet)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawCubes(openSetVisual, 0.3f);
        }

        if (showClosedSet)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            DrawCubes(closedSetVisual, 0.3f);
        }

        if (showFinalPath && currentPath != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < currentPath.Count; i++)
            {
                Gizmos.DrawCube(currentPath[i].worldPosition, Vector3.one * 0.4f);
                if (i > 0)
                {
                    Gizmos.DrawLine(currentPath[i - 1].worldPosition, currentPath[i].worldPosition);
                }
            }
        }
    }

    private void DrawCubes(List<Node> nodes, float size)
    {
        if (nodes == null) return;
        int count = Mathf.Min(nodes.Count, maxGizmoNodes);
        for (int i = 0; i < count; i++)
        {
            Gizmos.DrawCube(nodes[i].worldPosition, Vector3.one * size);
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public class DStarLitePathfinding : MonoBehaviour
{
    public GridGenerator grid;
    public Transform agent;
    public Transform target;

    public float moveSpeed = 5f;
    public float arrivalDistance = 0.5f;

    private List<Node> currentPath;
    private int currentPathIndex = 0;
    private bool hasArrived = false;

    // Variables D* Lite
    private float km = 0f;
    private List<Node> openList = new List<Node>();
    private Node startNode;
    private Node goalNode;

    void Start()
    {
        if (grid == null || grid.grid == null) return;
        InitializeDStarLite();
    }

    void Update()
    {
        if (!hasArrived && currentPath != null && currentPath.Count > 0)
        {
            MoveAlongPath();
        }
    }

    // 1. Initialisation
    void InitializeDStarLite()
    {
        km = 0f;
        startNode = grid.NodeFromWorldPoint(agent.position);
        goalNode = grid.NodeFromWorldPoint(target.position);

        // Réinitialiser tous les nœuds
        for (int x = 0; x < grid.gridSizeXCount; x++)
        {
            for (int z = 0; z < grid.gridSizeZCount; z++)
            {
                Node n = grid.grid[x, z];
                n.gCost = float.PositiveInfinity;
                n.rhs = float.PositiveInfinity;
                n.key = new List<float> { float.PositiveInfinity, float.PositiveInfinity };
            }
        }

        goalNode.rhs = 0;
        goalNode.key = CalculateKey(goalNode);
        Insert(goalNode);

        ComputeShortestPath();
        RebuildPathFromStart();
    }

    // 2. Calcul de la Key (Priorité)
    List<float> CalculateKey(Node s)
    {
        float min_g_rhs = Mathf.Min(s.gCost, s.rhs);
        float k1 = min_g_rhs + Heuristic(s, startNode) + km;
        float k2 = min_g_rhs;
        return new List<float> { k1, k2 };
    }

    // Heuristique (Distance Octile)
    float Heuristic(Node a, Node b)
    {
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstZ = Mathf.Abs(a.gridZ - b.gridZ);
        return (dstX > dstZ) ? (14 * dstZ + 10 * (dstX - dstZ)) : (14 * dstX + 10 * (dstZ - dstX));
    }

    // 3. Cœur de l'algorithme D* Lite (Logique corrigée)
    void ComputeShortestPath()
    {
        while (openList.Count > 0)
        {
            // Trier la liste (simule une Priority Queue)
            openList.Sort((a, b) => CompareKeys(a.key, b.key));

            Node u = openList[0];
            List<float> key_u_old = new List<float>(u.key);
            List<float> key_start = CalculateKey(startNode);

            // Condition d'arrêt : si la meilleure clé est >= celle du départ ET le départ est consistant
            if (CompareKeys(key_start, key_u_old) <= 0 && startNode.rhs == startNode.gCost)
            {
                break;
            }

            openList.RemoveAt(0); // Pop

            // Si la clé a changé (nœud obsolète)
            if (CompareKeys(key_u_old, CalculateKey(u)) < 0)
            {
                u.key = CalculateKey(u);
                Insert(u);
            }
            // Si le nœud est sur-estimé (g > rhs)
            else if (u.gCost > u.rhs)
            {
                u.gCost = u.rhs;
                foreach (Node s in GetPredecessors(u))
                {
                    UpdateVertex(s);
                }
            }
            // Si le nœud est sous-estimé (g <= rhs)
            else
            {
                u.gCost = float.PositiveInfinity;
                foreach (Node s in GetPredecessors(u))
                {
                    if (s != u) UpdateVertex(s);
                }
                UpdateVertex(u);
            }
        }
    }

    // 4. Mise à jour d'un sommet (quand l'environnement change)
    void UpdateVertex(Node u)
    {
        if (u != goalNode)
        {
            float minRhs = float.PositiveInfinity;
            foreach (Node s in GetSuccessors(u))
            {
                float cost = GetCost(u, s);
                float val = s.gCost + cost;
                if (val < minRhs)
                {
                    minRhs = val;
                }
            }
            u.rhs = minRhs;
        }

        openList.Remove(u);
        if (u.gCost != u.rhs)
        {
            u.key = CalculateKey(u);
            Insert(u);
        }
    }

    // Utilitaires
    void Insert(Node s)
    {
        if (!openList.Contains(s)) openList.Add(s);
    }

    int CompareKeys(List<float> k1, List<float> k2)
    {
        if (k1[0] < k2[0]) return -1;
        if (k1[0] > k2[0]) return 1;
        if (k1[1] < k2[1]) return -1;
        if (k1[1] > k2[1]) return 1;
        return 0;
    }

    List<Node> GetSuccessors(Node s)
    {
        List<Node> successors = new List<Node>();
        foreach (Node neighbor in grid.GetNeighbors(s))
        {
            if (neighbor.isWalkable) successors.Add(neighbor);
        }
        return successors;
    }

    List<Node> GetPredecessors(Node s)
    {
        return GetSuccessors(s); // Grille non-dirigée
    }

    float GetCost(Node a, Node b)
    {
        if (!b.isWalkable) return float.PositiveInfinity;
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstZ = Mathf.Abs(a.gridZ - b.gridZ);
        return (dstX > 0 && dstZ > 0) ? 14f : 10f;
    }

    // 5. Reconstruction du chemin
    void RebuildPathFromStart()
    {
        currentPath = new List<Node>();
        Node current = startNode;
        currentPath.Add(current);

        int maxSteps = 200;
        int steps = 0;

        while (current != goalNode && steps < maxSteps)
        {
            Node bestNext = null;
            float minCost = float.PositiveInfinity;

            foreach (Node successor in GetSuccessors(current))
            {
                float cost = successor.gCost + GetCost(current, successor);
                if (cost < minCost)
                {
                    minCost = cost;
                    bestNext = successor;
                }
            }

            if (bestNext == null || minCost == float.PositiveInfinity)
            {
                Debug.LogWarning("Aucun chemin trouvé !");
                break;
            }

            currentPath.Add(bestNext);
            current = bestNext;
            steps++;
        }
        currentPathIndex = 0;
    }

    // --- Gestion des changements dynamiques ---
    public void OnEnvironmentChanged()
    {
        Debug.Log("?? Environnement modifié : Recalcul D* Lite...");

        // Mise à jour du km (Key Modifier)
        km += Heuristic(startNode, grid.NodeFromWorldPoint(agent.position));
        startNode = grid.NodeFromWorldPoint(agent.position);

        // Mettre à jour les nœuds autour de l'obstacle (simplifié : on relance le calcul global pour être sûr)
        ComputeShortestPath();
        RebuildPathFromStart();
    }

    void MoveAlongPath()
    {
        if (currentPathIndex >= currentPath.Count) return;

        Node targetNode = currentPath[currentPathIndex];
        Vector3 targetPosition = targetNode.worldPosition;
        targetPosition.y = agent.position.y;

        agent.position = Vector3.MoveTowards(agent.position, targetPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(agent.position, targetPosition) < 0.1f)
        {
            currentPathIndex++;
        }

        if (currentPathIndex >= currentPath.Count)
        {
            hasArrived = true;
            Debug.Log("?? Cible atteinte !");
        }
    }
}
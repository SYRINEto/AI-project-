using UnityEngine;
using System.Collections.Generic;

public class AStarPathfinding : MonoBehaviour
{
    public GridGenerator grid;
    public Transform agent;
    public Transform target;

    [Header("Paramètres de déplacement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float arrivalDistance = 0.5f;

    [Header("Audio et Effets")]
    public AudioSource arrivalSound;
    public ParticleSystem arrivalParticles;

    [Header("Visualisation A*")]
    public bool showOpenSet = true;
    public bool showClosedSet = true;
    public bool showFinalPath = true;

    private List<Node> currentPath;
    private int currentPathIndex = 0;
    private bool hasArrived = false;

    // Pour la visualisation
    public List<Node> openSetVisual = new List<Node>();
    public List<Node> closedSetVisual = new List<Node>();

    void Start()
    {
        if (grid == null || grid.grid == null)
        {
            Debug.LogError("La grille n'est pas initialisée !");
            return;
        }
        FindPath(agent.position, target.position);
    }

    void Update()
    {
        if (currentPath != null && currentPath.Count > 0 && !hasArrived)
        {
            MoveAlongPath();
        }
    }

    // Fonction appelée par le gestionnaire d'obstacles dynamiques
    public void RecalculatePath()
    {
        hasArrived = false;
        currentPathIndex = 0;
        FindPath(agent.position, target.position);
        Debug.Log("?? A* a recalculé le chemin suite à un changement !");
    }

    public void FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        openSet.Add(startNode);

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

            if (currentNode == targetNode)
            {
                RetracePath(startNode, targetNode);
                return;
            }

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        // Sauvegarde pour visualisation
        openSetVisual = new List<Node>(openSet);
        closedSetVisual = new List<Node>(closedSet);
    }

    void RetracePath(Node startNode, Node endNode)
    {
        currentPath = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            currentPath.Add(currentNode);
            currentNode = currentNode.parent;
        }
        currentPath.Reverse();
        currentPathIndex = 0;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstZ = Mathf.Abs(nodeA.gridZ - nodeB.gridZ);
        if (dstX > dstZ)
        {
            return 14 * dstZ + 10 * (dstX - dstZ);
        }
        return 14 * dstX + 10 * (dstZ - dstX);
    }

    void MoveAlongPath()
    {
        if (currentPathIndex >= currentPath.Count) return;

        Node targetNode = currentPath[currentPathIndex];
        Vector3 targetPosition = targetNode.worldPosition;
        targetPosition.y = agent.position.y;

        agent.position = Vector3.MoveTowards(agent.position, targetPosition, moveSpeed * Time.deltaTime);

        Vector3 direction = (targetPosition - agent.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            agent.rotation = Quaternion.Slerp(agent.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(agent.position, targetPosition) < 0.1f)
        {
            currentPathIndex++;
        }

        CheckArrival();
    }

    void CheckArrival()
    {
        if (hasArrived) return;
        float distanceToTarget = Vector3.Distance(agent.position, target.position);
        if (distanceToTarget <= arrivalDistance)
        {
            hasArrived = true;
            Debug.Log("?? L'agent a atteint l'objectif !");
            OnArrival();
        }
    }

    void OnArrival()
    {
        if (arrivalSound != null) arrivalSound.Play();
        if (arrivalParticles != null) arrivalParticles.Play();
    }

    void OnDrawGizmos()
    {
        if (grid == null || grid.grid == null) return;

        if (showOpenSet)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            foreach (Node node in openSetVisual) Gizmos.DrawCube(node.worldPosition, Vector3.one * 0.3f);
        }
        if (showClosedSet)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            foreach (Node node in closedSetVisual) Gizmos.DrawCube(node.worldPosition, Vector3.one * 0.3f);
        }
        if (showFinalPath && currentPath != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < currentPath.Count; i++)
            {
                Gizmos.DrawCube(currentPath[i].worldPosition, Vector3.one * 0.4f);
                if (i > 0) Gizmos.DrawLine(currentPath[i - 1].worldPosition, currentPath[i].worldPosition);
            }
        }
    }
}
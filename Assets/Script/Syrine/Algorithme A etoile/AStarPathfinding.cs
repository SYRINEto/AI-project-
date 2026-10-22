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

    public float arrivalDistance = 0.5f;  // Distance pour considérer arrivé

    [Header("Audio et Effets")]
    public AudioSource arrivalSound;
    public ParticleSystem arrivalParticles;
    private List<Node> currentPath;
    private int currentPathIndex = 0;
    private bool hasArrived = false;

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

    public void FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {//La sélection du nœud avec le plus petit fCost  puis on le retire de la liste ouverte et on l'ajoute à la liste fermée
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

                //  int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                float newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
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
    //  la Distance Octile 
    //agent à la position (2, 3) et la cible (feu de camp) est à (7, 6). 
    //dstX = |7 - 2| = 5  (il faut avancer de 5 cases en X)
    //dstZ = |6 - 3| = 3  (il faut avancer de 3 cases en Z)
    // int GetDistance(Node nodeA, Node nodeB)
    // AVANT : int GetDistance(...)
    // APRÈS :
    float GetDistance(Node nodeA, Node nodeB)
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
        if (currentPathIndex >= currentPath.Count)
        {
            return;
        }

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

        //érifie si l'agent est arrivé à la cible finale
        CheckArrival();
    }
    public void RecalculatePath()
    {
        hasArrived = false;
        currentPathIndex = 0;

        // On relance le calcul du chemin depuis la position actuelle de l'agent
        FindPath(agent.position, target.position);
        Debug.Log("?? L'agent recalcule son chemin automatiquement !");
    }
    // Détecte l'arrivée et joue le son
    void CheckArrival()
    {
        if (hasArrived) return;

        float distanceToTarget = Vector3.Distance(agent.position, target.position);

        // ? AJOUTE CETTE LIGNE POUR VOIR LA DISTANCE EN TEMPS RÉEL :
        Debug.Log($"?? Distance au feu : {distanceToTarget:F2} mètres | Seuil : {arrivalDistance}");

        if (distanceToTarget <= arrivalDistance)
        {
            hasArrived = true;
            Debug.Log("?? L'agent a atteint l'objectif !");

            if (arrivalSound != null) arrivalSound.Play();
            OnArrival();
        }
    }

    // Actions à faire à l'arrivée
    void OnArrival()
    {
        Debug.Log(" Le garde est arrivé au feu de camp !");

        // Joue le son
        if (arrivalSound != null)
        {
            arrivalSound.Play();
        }
        // Joue les particules
        if (arrivalParticles != null)
        {
            arrivalParticles.Play();
        }
    }

    void OnDrawGizmos()
    {
        if (currentPath != null)
        {
            for (int i = 0; i < currentPath.Count; i++)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(currentPath[i].worldPosition, Vector3.one * 0.5f);
            }
        }
    }
}


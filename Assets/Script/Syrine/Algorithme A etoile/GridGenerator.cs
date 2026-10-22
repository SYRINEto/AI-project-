using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Génère la grille 2D (X/Z) utilisée par A*.
/// Version corrigée : chaque case connaît son statut dynamique (obstacle temporaire)
/// et le personnage A* peut être bloquée sans corrompre la grille de référence.
/// </summary>
public class GridGenerator : MonoBehaviour
{
    [Header("Paramètres de la grille")]
    public float gridSizeX = 40f;
    public float gridSizeZ = 40f;
    public float nodeRadius = 0.5f;      // cases de 1 x 1 unité
    public LayerMask obstacleMask;
    public float obstacleCheckHeight = 0.5f; // hauteur à laquelle on teste les obstacles

    [Header("Couleurs (Gizmos)")]
    public Color walkableColor = Color.green;
    public Color obstacleColor = Color.red;
    public Color blockedByAgentColor = Color.magenta;

    [Header("Affichage G/H/F")]
    public bool showGHFText = true;

    [Header("Débogage")]
    public bool showGridGizmos = true;

    [HideInInspector] public Node[,] grid;
    [HideInInspector] public int gridSizeXCount;
    [HideInInspector] public int gridSizeZCount;
    public float nodeDiameter;

    void Awake()
    {
        InitGrid();
    }

    public void InitGrid()
    {
        nodeDiameter = nodeRadius * 2f;
        gridSizeXCount = Mathf.RoundToInt(gridSizeX / nodeDiameter);
        gridSizeZCount = Mathf.RoundToInt(gridSizeZ / nodeDiameter);
        CreateGrid();
    }

    /// <summary>Reconstruit entièrement la grille (bouton contextuel / obstacle dynamique).</summary>
    public void CreateGrid()
    {
        grid = new Node[gridSizeXCount, gridSizeZCount];
        Vector3 worldBottomLeft = transform.position
                                  - Vector3.right * gridSizeX / 2f
                                  - Vector3.forward * gridSizeZ / 2f;

        for (int x = 0; x < gridSizeXCount; x++)
        {
            for (int z = 0; z < gridSizeZCount; z++)
            {
                Vector3 worldPoint = worldBottomLeft
                                     + Vector3.right * (x * nodeDiameter + nodeRadius)
                                     + Vector3.forward * (z * nodeDiameter + nodeRadius);
                grid[x, z] = new Node(worldPoint, x, z, true);
            }
        }

        RefreshObstacles();
    }

    /// <summary>
    /// Re-scanne les obstacles physiques. À appeler si vous déplacez un obstacle
    /// permanent (rocher, arbre, barrière) pendant la partie.
    /// </summary>
    public void RefreshObstacles()
    {
        for (int x = 0; x < gridSizeXCount; x++)
        {
            for (int z = 0; z < gridSizeZCount; z++)
            {
                Node node = grid[x, z];
                Vector3 checkPos = node.worldPosition + Vector3.up * obstacleCheckHeight;
                node.isWalkable = !Physics.CheckSphere(checkPos, nodeRadius * 0.95f, obstacleMask, QueryTriggerInteraction.Ignore);
            }
        }
    }

    // ------------------------------------------------------------------
    // Obstacles DYNAMIQUES (Tâche 1 du cours) : le joueur, une caisse...
    // Renvoie true si le statut de la case a changé -> A* devra recalculer.
    // ------------------------------------------------------------------
    public bool SetDynamicObstacle(Vector3 worldPosition, bool blocked)
    {
        Node node = NodeFromWorldPoint(worldPosition);
        if (node == null || node.isWalkable == !blocked) return false;

        node.isWalkable = !blocked;
        return true;
    }

    /// <summary>Alias conservé pour la compatibilité avec l'ancien script.</summary>
    public void UpdateNodeAt(Vector3 worldPosition)
    {
        Node node = NodeFromWorldPoint(worldPosition);
        if (node != null) node.isWalkable = !node.isWalkable;
    }

    // ------------------------------------------------------------------
    // Voisins : on utilise volontairement 4 voisins (pas de diagonales).
    // Avec des diagonales, A* coupe les coins des murs -> le garde traverse
    // le GrillManager et se coince. Le coût est plus simple à expliquer :
    // 10 par case orthogonale.
    // ------------------------------------------------------------------
    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>(4);

        TryAddNeighbor(neighbors, node.gridX - 1, node.gridZ);
        TryAddNeighbor(neighbors, node.gridX + 1, node.gridZ);
        TryAddNeighbor(neighbors, node.gridX, node.gridZ - 1);
        TryAddNeighbor(neighbors, node.gridX, node.gridZ + 1);

        return neighbors;
    }

    private void TryAddNeighbor(List<Node> neighbors, int x, int z)
    {
        if (x >= 0 && x < gridSizeXCount && z >= 0 && z < gridSizeZCount)
        {
            neighbors.Add(grid[x, z]);
        }
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        if (grid == null) return null;

        float percentX = (worldPosition.x - transform.position.x + gridSizeX / 2f) / gridSizeX;
        float percentZ = (worldPosition.z - transform.position.z + gridSizeZ / 2f) / gridSizeZ;
        percentX = Mathf.Clamp01(percentX);
        percentZ = Mathf.Clamp01(percentZ);

        int x = Mathf.RoundToInt((gridSizeXCount - 1) * percentX);
        int z = Mathf.RoundToInt((gridSizeZCount - 1) * percentZ);

        return grid[Mathf.Clamp(x, 0, gridSizeXCount - 1), Mathf.Clamp(z, 0, gridSizeZCount - 1)];
    }

    // ------------------------------------------------------------------
    // Gizmos
    // ------------------------------------------------------------------
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSizeX, 1f, gridSizeZ));

        if (!showGridGizmos || grid == null) return;

        for (int x = 0; x < gridSizeXCount; x += 2)
        {
            for (int z = 0; z < gridSizeZCount; z += 2)
            {
                Gizmos.color = grid[x, z].isWalkable ? new Color(walkableColor.r, walkableColor.g, walkableColor.b, 0.08f) : new Color(obstacleColor.r, obstacleColor.g, obstacleColor.b, 0.08f);
                Gizmos.DrawCube(grid[x, z].worldPosition, Vector3.one * 0.2f);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showGHFText || grid == null) return;

        for (int x = 0; x < gridSizeXCount; x++)
        {
            for (int z = 0; z < gridSizeZCount; z++)
            {
                Node node = grid[x, z];
                if (node.gCost >= Node.InfiniteCost && node.hCost == 0) continue;

                Vector3 textPosition = node.worldPosition + Vector3.up * 0.5f;
                Handles.color = Color.white;
                Handles.Label(textPosition, $"G:{node.gCost} H:{node.hCost} F:{node.fCost}");
            }
        }
    }
#endif
}

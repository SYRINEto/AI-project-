using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridGenerator : MonoBehaviour
{
    [Header("Paramètres de la grille")]
    public float gridSizeX = 30f;      // Augmenté pour bien couvrir la zone
    public float gridSizeZ = 30f;
    public float nodeRadius = 0.5f;    // Cases de 1x1 unité
    public LayerMask obstacleMask;

    [Header("Couleurs")]
    public Color walkableColor = Color.green;
    public Color obstacleColor = Color.red;

    [Header("Affichage G/H/F")]
    public bool showGHFText = true;
    public float textSize = 0.1f;

    public Node[,] grid;
    public float nodeDiameter;
    public int gridSizeXCount;
    public int gridSizeZCount;

    void Awake()
    {
        nodeDiameter = nodeRadius * 2f;
        gridSizeXCount = Mathf.RoundToInt(gridSizeX / nodeDiameter);
        gridSizeZCount = Mathf.RoundToInt(gridSizeZ / nodeDiameter);
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeXCount, gridSizeZCount];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridSizeX / 2f - Vector3.forward * gridSizeZ / 2f;

        for (int x = 0; x < gridSizeXCount; x++)
        {
            for (int z = 0; z < gridSizeZCount; z++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (z * nodeDiameter + nodeRadius);

                // Détection d'obstacle
                bool walkable = !Physics.CheckSphere(worldPoint, nodeRadius, obstacleMask);
                grid[x, z] = new Node(worldPoint, x, z, walkable);

                // Création visuelle (optionnel, peut être lourd si la grille est grande)
                // CreateVisualNode(worldPoint, walkable); 
            }
        }
    }

    // Fonction pour mettre à jour un nœud dynamiquement (pour la Tâche 1)
    public void UpdateNodeAt(Vector3 worldPosition)
    {
        Node node = NodeFromWorldPoint(worldPosition);
        if (node != null)
        {
            node.isWalkable = !node.isWalkable;
        }
    }

    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;

                int checkX = node.gridX + x;
                int checkZ = node.gridZ + z;

                if (checkX >= 0 && checkX < gridSizeXCount && checkZ >= 0 && checkZ < gridSizeZCount)
                {
                    neighbors.Add(grid[checkX, checkZ]);
                }
            }
        }
        return neighbors;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + gridSizeX / 2f) / gridSizeX;
        float percentZ = (worldPosition.z + gridSizeZ / 2f) / gridSizeZ;
        percentX = Mathf.Clamp01(percentX);
        percentZ = Mathf.Clamp01(percentZ);

        int x = Mathf.RoundToInt((gridSizeXCount - 1) * percentX);
        int z = Mathf.RoundToInt((gridSizeZCount - 1) * percentZ);

        return grid[x, z];
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSizeX, 1f, gridSizeZ));
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
                if (node.gCost == 0 && node.hCost == 0) continue;

                Vector3 textPosition = node.worldPosition + Vector3.up * 0.5f;
                string ghfText = $"G:{node.gCost} H:{node.hCost} F:{node.fCost}";

                Handles.color = Color.white;
                Handles.Label(textPosition, ghfText);
            }
        }
    }
#endif
}
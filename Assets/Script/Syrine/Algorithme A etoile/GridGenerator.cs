using Mono.Cecil;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class GridGenerator : MonoBehaviour
{
    [Header("Paramètres de la grille")]
    public float gridSizeX = 20f;      // Largeur de la grille en unités
    public float gridSizeZ = 20f;      // Profondeur de la grille en unités
    public float nodeRadius = 0.5f;      // Rayon de chaque case (diamètre = 2)
    public LayerMask obstacleMask;     // Layer des obstacles (arbres, maison, ....)

    [Header("Couleurs")]
    public Color walkableColor = Color.green;
    public Color obstacleColor = Color.red;

    public Node[,] grid;               // La grille 2D de noeuds
    public float nodeDiameter;         // Diamètre calculé automatiquement
    public int gridSizeXCount;         // Nombre de cases en X
    public int gridSizeZCount;         // Nombre de cases en Z

    void Awake()  // verif grille existe avant que A* essaie de l'utiliser
    {
        nodeDiameter = nodeRadius * 2f;
        gridSizeXCount = Mathf.RoundToInt(gridSizeX / nodeDiameter);
        gridSizeZCount = Mathf.RoundToInt(gridSizeZ / nodeDiameter);

        CreateGrid();
    }
    //Pour chaque case, on utilise un CheckSphere avec un LayerMask pour détecter auto les obstacles
    //Si un obstacle est détecté, la case devient rouge (non-marchable), sinon elle reste verte." 
    void CreateGrid()
    {
        grid = new Node[gridSizeXCount, gridSizeZCount];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridSizeX / 2f - Vector3.forward * gridSizeZ / 2f;

        for (int x = 0; x < gridSizeXCount; x++)
        {
            for (int z = 0; z < gridSizeZCount; z++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (z * nodeDiameter + nodeRadius);

                // Détection d'obstacle avec Raycast
                bool walkable = !Physics.CheckSphere(worldPoint, nodeRadius, obstacleMask);

                grid[x, z] = new Node(worldPoint, x, z, walkable);

                // Création visuelle du cube
                CreateVisualNode(worldPoint, walkable);
            }
        }
    }

    void CreateVisualNode(Vector3 position, bool walkable)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = position;
        cube.transform.localScale = new Vector3(nodeDiameter, 0.1f, nodeDiameter);

        // Couleur selon marchable ou non
        Renderer renderer = cube.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = walkable ? walkableColor : obstacleColor;
        renderer.material = mat;
        Destroy(cube.GetComponent<BoxCollider>());
    }

    //  pour obtenir les voisins d'un noeud (utilisée par A*)
   // où il peut aller.Elle retourne les 8 cases adjacentes (horizontales, verticales et diagonales)
    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue; // Skip le noeud lui-même

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

    //  pour convertir une position monde en coordonnées de grille
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

    // Dessine la grille dans l'éditeur pour visualiser
    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSizeX, 1f, gridSizeZ));
    }


    // Ajoutez ceci dans GridGenerator.cs
    public void UpdateNodeAt(Vector3 worldPosition)
    {
        Node node = NodeFromWorldPoint(worldPosition);
        if (node != null)
        {
            // Inverse l'état marchable
            node.isWalkable = !node.isWalkable;

            // Met à jour la couleur visuelle pour le débogage
            Debug.Log($"Case en {worldPosition} est maintenant {(node.isWalkable ? "Marchable" : "Obstacle")}");
        }
    } 
}

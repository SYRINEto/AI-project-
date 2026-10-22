/*using UnityEngine;
using UnityEngine.Rendering;

public class Node
{
    public Vector3 worldPosition; // Position réelle dans Unity
    public int gridX;             // Position X dans la grille
    public int gridZ;             // Position Z dans la grille
    public bool isWalkable;       // True = marchable, False = obstacle
    public int gCost;             // Coût depuis le départ
    public int hCost;             // Coût heuristique vers la cible
    public Node parent;           // Noeud parent (pour reconstruire le chemin)

    public int fCost => gCost + hCost; // F = G + H
                                       //gCost : G Cost  Coût réel depuis le départ
                                       //hCost : H Cost  Heuristique(estimation) vers la cible


    public Node(Vector3 worldPos, int x, int z, bool walkable)
    {
        worldPosition = worldPos;
        gridX = x;
        gridZ = z;
        isWalkable = walkable;
    }
}

*/

using UnityEngine;

public class Node
{
    public Vector3 worldPosition; // Position réelle dans Unity
    public int gridX;             // Position X dans la grille
    public int gridZ;             // Position Z dans la grille
    public bool isWalkable;       // True = marchable, False = obstacle

    // ? CHANGÉ : int -> float pour supporter l'infini et D* Lite
    public float gCost;           // Coût depuis le départ
    public float hCost;           // Coût heuristique vers la cible
    public Node parent;           // Noeud parent (pour reconstruire le chemin)
    public float fCost => gCost + hCost; // F = G + H

    // Pour D* Lite
    public float rhs = float.PositiveInfinity;
    public System.Collections.Generic.List<float> key = new System.Collections.Generic.List<float>(2);

    public Node(Vector3 worldPos, int x, int z, bool walkable)
    {
        worldPosition = worldPos;
        gridX = x;
        gridZ = z;
        isWalkable = walkable;
    }
}
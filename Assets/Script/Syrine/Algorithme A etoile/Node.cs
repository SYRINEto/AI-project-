using UnityEngine;
using System.Collections.Generic;

public class Node
{
    public Vector3 worldPosition; // Position réelle dans Unity
    public int gridX;             // Position X dans la grille
    public int gridZ;             // Position Z dans la grille
    public bool isWalkable;       // True = marchable, False = obstacle

    // Coûts pour A* (en int pour éviter les problèmes d'infini)
    public int gCost;             // Coût réel depuis le départ
    public int hCost;             // Coût heuristique vers la cible
    public Node parent;           // Noeud parent (pour reconstruire le chemin)
    public int fCost => gCost + hCost; // F = G + H

    public Node(Vector3 worldPos, int x, int z, bool walkable)
    {
        worldPosition = worldPos;
        gridX = x;
        gridZ = z;
        isWalkable = walkable;
    }
}
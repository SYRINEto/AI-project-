using UnityEngine;

/// <summary>
/// Une case (nœud) de la grille utilisée par A*.
/// Chapitre 3 – la grille n'est qu'un support : ce sont les méthodes ad hoc (FSM)
/// qui décident QUAND demander un nouveau chemin.
/// </summary>
public class Node
{
    /// <summary>
    /// "Infini" sûr : on évite int.MaxValue pour ne pas déborder
    /// quand on fait gCost + hCost ou gCost + coût du voisin.
    /// </summary>
    public const int InfiniteCost = 1_000_000;

    public Vector3 worldPosition;   // position réelle dans Unity
    public int gridX;               // colonne dans la grille
    public int gridZ;               // ligne dans la grille
    public bool isWalkable;         // true = marchable, false = obstacle

    // Coûts A* (entiers, comme dans le cours)
    public int gCost = InfiniteCost; // coût réel depuis le départ
    public int hCost;                // coût heuristique vers la cible
    public Node parent;              // nœud parent (pour reconstruire le chemin)

    public int fCost => gCost + hCost; // F = G + H

    public Node(Vector3 worldPosition, int gridX, int gridZ, bool isWalkable)
    {
        this.worldPosition = worldPosition;
        this.gridX = gridX;
        this.gridZ = gridZ;
        this.isWalkable = isWalkable;
    }

    /// <summary>
    /// Remet le nœud à zéro avant chaque nouvelle recherche.
    /// (Sans ça, les coûts de la recherche précédente polluent la suivante :
    /// c'est l'un des bugs silencieux les plus fréquents des implémentations A*.)
    /// </summary>
    public void ResetCosts()
    {
        gCost = InfiniteCost;
        hCost = 0;
        parent = null;
    }
}

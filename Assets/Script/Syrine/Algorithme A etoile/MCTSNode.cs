using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un nœud de l'arbre MCTS (chapitre 4).
///
/// Chaque nœud retient ce que le cours demande :
///   - l'action qui a mené ici (une case voisine de la grille) ;
///   - le nœud parent (pour la rétropropagation) ;
///   - les enfants (l'arbre grossit au fil des simulations) ;
///   - victoires / visites, notés « w/n » comme sur le diapo 4 étapes
///     (ex : 3/3 devient 3/4 si la partie est perdue).
/// </summary>
public class MCTSNode
{
    public MCTSNode parent;
    public List<MCTSNode> children = new List<MCTSNode>();

    /// <summary>Case de la grille jouée pour arriver sur ce nœud.</summary>
    public Vector3 action;

    /// <summary>Position du chasseur dans cet état du jeu.</summary>
    public Vector3 state;

    /// <summary>Actions pas encore développées (Expansion).</summary>
    public List<Vector3> untriedActions;

    public int visits;      // n
    public float wins;      // w

    /// <summary>Vrai si on a découvert un enfant (il ne vaut plus rien d'être simulé).</summary>
    public bool expanded;

    public MCTSNode(MCTSNode parent, Vector3 state, Vector3 action, List<Vector3> untriedActions)
    {
        this.parent = parent;
        this.state = state;
        this.action = action;
        this.untriedActions = untriedActions ?? new List<Vector3>();
        expanded = this.untriedActions.Count > 0;
    }

    public bool IsFullyExpanded => untriedActions.Count == 0;

    /// <summary>Score d'exploitation seul : w / n (le taux de victoire).</summary>
    public float WinRate => visits > 0 ? wins / visits : 0f;
}

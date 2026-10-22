using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Câblage automatique de la scène (à poser sur n'importe quel GameObject vide,
/// ou sur « Capsule »).
///
/// Rôles dans le projet :
///   - « Agent »   = celui qui va vers l'OBJECTIF (A* sur la grille) ;
///                   + BanditController si tu veux aussi le piloter au clavier ;
///   - « Capsule » = le BANDIT, celui qui a l'IA ad hoc (GuardFSM) et qui
///                   l'empêche d'arriver.
///
/// Au démarrage, ce script ajoute les composants manquants et remplit les
/// références vides pour que la démo fonctionne sans configuration.
/// Ce n'est PAS une méthode d'IA : c'est juste du confort.
/// Le cœur du chapitre 3 reste GuardFSM (les règles ad hoc) + la grille
/// (GridGenerator / Node) et AStarPathfinding pour le déplacement.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AdHocSetup : MonoBehaviour
{
    [Header("Objets de la scène")]
    [Tooltip("L'Agent : celui qui va vers l'objectif.")]
    public Transform player;
    [Tooltip("Le bandit : le GameObject « Capsule ».")]
    public Transform bandit;
    public Transform gridManager;
    [Tooltip("Allers/Retours (état Move) du bandit.")]
    public Transform[] patrolWaypoints = new Transform[0];
    [Tooltip("L'objectif autour duquel le bandit patrouille (feu de camp).")]
    public Transform campfireTarget;

    [Header("Options")]
    public bool autoAddComponents = true;

    void Awake()
    {
        ResolveMissingReferences();

        if (autoAddComponents) EnsureComponents();

        WireEverything();
    }

    void ResolveMissingReferences()
    {
        if (player == null) player = FindTransform("Agent");
        if (bandit == null) bandit = FindTransform("Capsule");
        if (gridManager == null) gridManager = FindTransform("GridManager");

        if (bandit == null)
        {
            var fsm = FindFirstObjectByType<GuardFSM>();
            if (fsm != null) bandit = fsm.transform;
        }

        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
        {
            var list = new List<Transform>();
            for (int i = 1; i <= 8; i++)
            {
                Transform wp = FindTransform("Waypoint" + i);
                if (wp != null) list.Add(wp);
            }
            patrolWaypoints = list.ToArray();
        }
    }

    Transform FindTransform(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    void EnsureComponents()
    {
        // ---------------- L'AGENT : il va vers l'objectif (A* sur la grille) ----------------
        if (player != null)
        {
            // L'A* de l'Agent : celui de « PathfindingManager » s'il existe
            // (comme dans la scène), sinon un A* sur l'Agent lui-même.
            if (FindFirstObjectByType<AStarPathfinding>() == null)
            {
                player.gameObject.AddComponent<AStarPathfinding>();
            }

            if (player.GetComponent<GoalSeeker>() == null)
            {
                player.gameObject.AddComponent<GoalSeeker>();
            }
        }

        // ---------------- Le bandit (Capsule) : IA ad hoc + déplacement ----------------
        if (bandit != null)
        {
            // Le bandit ne fait PAS de pathfinding : il se déplace en ligne droite
            // (c'est la méthode ad hoc du cours : la décision, pas le chemin).

            // La FSM ad hoc : c'est elle qui décide (Wait/Move/Follow/Attack/RunAway).
            if (bandit.GetComponent<GuardFSM>() == null)
            {
                bandit.gameObject.AddComponent<GuardFSM>();
            }
        }
    }

    void WireEverything()
    {
        if (bandit == null) return;

        GuardFSM fsm = bandit.GetComponent<GuardFSM>();
        BanditController ctrl = player != null ? player.GetComponent<BanditController>() : null;

        // ---------------- L'AGENT va vers l'objectif ----------------
        if (player != null)
        {
            // L'A* de l'Agent = celui de PathfindingManager (sinon celui de l'Agent).
            AStarPathfinding agentPath = player.GetComponent<AStarPathfinding>();
            if (agentPath == null)
            {
                AStarPathfinding candidate = FindFirstObjectByType<AStarPathfinding>();
                // On exclut l'A* du bandit (celui qui a « agent » = la capsule).
                if (candidate != null && candidate.agent != bandit) agentPath = candidate;
            }

            GoalSeeker seeker = player.GetComponent<GoalSeeker>();

            if (agentPath != null)
            {
                agentPath.agent = player;       // ⚠️ c'est l'AGENT qui suit ce chemin
                if (agentPath.grid == null && gridManager != null)
                {
                    agentPath.grid = gridManager.GetComponent<GridGenerator>();
                }
                if (agentPath.defaultTarget == null) agentPath.defaultTarget = campfireTarget;
            }

            if (seeker != null)
            {
                if (seeker.pathfinding == null) seeker.pathfinding = agentPath;
                if (seeker.objective == null) seeker.objective = campfireTarget;
                if (seeker.objective == null && agentPath != null) seeker.objective = agentPath.defaultTarget;
            }
        }

        if (fsm != null)
        {
            if (fsm.player == null) fsm.player = player;
            if (fsm.playerController == null) fsm.playerController = ctrl;
            if (fsm.playerSeeker == null && player != null) fsm.playerSeeker = player.GetComponent<GoalSeeker>();
            if ((fsm.patrolWaypoints == null || fsm.patrolWaypoints.Length == 0) && patrolWaypoints.Length > 0)
            {
                fsm.patrolWaypoints = patrolWaypoints;
            }
        }
    }
}

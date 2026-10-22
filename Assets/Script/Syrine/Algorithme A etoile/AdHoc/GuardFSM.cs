using UnityEngine;

/// <summary>
/// =============================================================================
/// CLASSE GuardFSM : Machine à États Finis pour le comportement du Garde
/// =============================================================================
/// 
/// Conforme au Chapitre 3 du cours "Le comportement Ad-Hoc".
/// 
/// UNE FSM EST CONSTRUITE À PARTIR DE 3 ÉLÉMENTS DE BASE (Diapo 6) :
/// 1. Des ÉTATS : description de la configuration du système (Wait, Move, etc.)
/// 2. Des TRANSITIONS : passage instantané d'un état à un autre
/// 3. Des ÉVÈNEMENTS : conditions déclenchant les transitions (distances)
/// 
/// QUESTION PROF : "Pourquoi une FSM et pas un Behavior Tree ?"
/// RÉPONSE : 
/// - FSM : simple, déterministe, rapide, contrôle absolu ? parfaite pour 
///   des comportements discrets et bien définis comme un garde
/// - Behavior Tree : plus flexible pour des comportements complexes, mais 
///   plus difficile à déboguer et à visualiser
/// Pour notre scénario (5 états clairs), la FSM est le choix optimal.
/// 
/// SCÉNARIO (adapté de la Diapo 8 du cours) :
/// "Un Garde patrouille dans son environnement. Quand le Bandit (PP) entre 
/// dans son champ de vision, le Garde le poursuit. Quand ils sont proches, 
/// le Garde attaque. Si le Bandit attaque à son tour, le Garde fuit. 
/// Quand le Bandit s'éloigne, le Garde reprend sa patrouille."
/// =============================================================================
public class GuardFSM : MonoBehaviour
{
    // =========================================================================
    // RÉFÉRENCES EXTERNES
    // =========================================================================

    /// <summary>
    /// Le Bandit (Personnage Principal - PP dans le cours).
    /// C'est le stimulus qui déclenche les transitions de la FSM.
    /// </summary>
    public Transform bandit;

    /// <summary>
    /// Référence au système de pathfinding A*.
    /// La FSM décide OÙ aller, A* décide COMMENT y aller.
    /// </summary>
    public AStarPathfinding pathfinding;

    /// <summary>
    /// Points de patrouille (waypoints) pour l'état Move.
    /// Le Garde les visite en boucle.
    /// </summary>
    public Transform[] patrolWaypoints;

    // =========================================================================
    // PARAMÈTRES DES ÉVÈNEMENTS (adaptés des Diapos 9-10 du cours)
    // =========================================================================
    // Le cours utilise 600, 300, 100 unités. Nous adaptons à Unity (mètres) :
    // 600 ? 15m, 300 ? 8m, 100 ? 3m

    /// <summary>
    /// Event1 : distance > distFar (15m) ? Bandit trop loin, retour à Patrouille
    /// </summary>
    public float distFar = 15f;

    /// <summary>
    /// Event2 : distClose < distance ? distFar (8m < d ? 15m) 
    /// ? Bandit dans le champ de vision, déclenche Poursuite
    /// </summary>
    public float distMedium = 8f;

    /// <summary>
    /// Event3/4 : distance ? distClose (3m) 
    /// ? Bandit à portée d'attaque, déclenche Attaque ou Fuite
    /// </summary>
    public float distClose = 3f;

    // =========================================================================
    // DÉFINITION DES ÉTATS (Diapo 9 du cours)
    // =========================================================================
    /// <summary>
    /// Les 5 états du PNJ selon le cours :
    /// - Wait (W) : Garde immobile, en attente
    /// - Move (M) : Garde en patrouille entre les waypoints
    /// - Follow (F) : Garde poursuit le Bandit
    /// - Attack (A) : Garde attaque le Bandit
    /// - RunAway (R) : Garde fuit le Bandit
    /// </summary>
    private enum State { Wait, Move, Follow, Attack, RunAway }
    private State currentState = State.Wait; // État initial

    // =========================================================================
    // VARIABLES INTERNES
    // =========================================================================
    private int currentWaypointIndex = 0; // Index du waypoint actuel
    private float waitTimer = 0f;         // Timer pour l'état Wait
    public float waitDuration = 3f;       // Durée d'attente avant de repartir

    // =========================================================================
    // CYCLE DE VIE
    // =========================================================================
    void Start()
    {
        currentState = State.Wait; // État initial selon le cours
    }

    // =========================================================================
    // UPDATE : Boucle principale de la FSM (exécutée à chaque frame)
    // =========================================================================
    /// <summary>
    /// La FSM suit ce cycle à chaque frame :
    /// 1. Calculer les stimuli (distance au Bandit)
    /// 2. Évaluer les transitions (événements) selon l'état actuel
    /// 3. Exécuter le comportement de l'état actuel
    /// </summary>
    void Update()
    {
        // ÉTAPE 1 : Calculer les stimuli
        float distanceToBandit = Vector3.Distance(transform.position, bandit.position);
        bool banditIsAttacking = bandit.GetComponent<BanditController>().isAttacking;

        // ÉTAPE 2 : GESTION DES TRANSITIONS (Évènements du cours)
        // Chaque état a ses propres conditions de transition
        switch (currentState)
        {
            case State.Wait:
                // Transition Wait ? Move : temps d'attente écoulé
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitDuration)
                {
                    TransitionTo(State.Move);
                }
                // Transition Wait ? Follow : Bandit détecté (Event2)
                else if (distanceToBandit <= distMedium)
                {
                    TransitionTo(State.Follow);
                }
                break;

            case State.Move: // Patrouille
                // Transition Move ? Follow : Bandit dans le champ de vision (Event2)
                if (distanceToBandit <= distMedium && distanceToBandit > distClose)
                {
                    TransitionTo(State.Follow);
                }
                // Transition Move ? Attack : Bandit très proche (Event3)
                else if (distanceToBandit <= distClose)
                {
                    TransitionTo(State.Attack);
                }
                break;

            case State.Follow: // Poursuite
                // Transition Follow ? Move : Bandit perdu de vue (Event1)
                if (distanceToBandit > distFar)
                {
                    TransitionTo(State.Move);
                }
                // Transition Follow ? Attack : Bandit à portée (Event3)
                else if (distanceToBandit <= distClose)
                {
                    TransitionTo(State.Attack);
                }
                break;

            case State.Attack: // Attaque
                // Transition Attack ? RunAway : Bandit contre-attaque (Event6)
                if (banditIsAttacking || distanceToBandit <= distClose)
                {
                    TransitionTo(State.RunAway);
                }
                // Transition Attack ? Follow : Bandit s'éloigne (Event2)
                else if (distanceToBandit > distMedium)
                {
                    TransitionTo(State.Follow);
                }
                break;

            case State.RunAway: // Fuite
                // Transition RunAway ? Move : Hors de danger (Event1)
                if (distanceToBandit > distFar)
                {
                    TransitionTo(State.Move);
                }
                break;
        }

        // ÉTAPE 3 : Exécuter le comportement de l'état actuel
        ExecuteStateBehavior();
    }

    // =========================================================================
    // TRANSITION : Changement d'état
    // =========================================================================
    /// <summary>
    /// QUESTION PROF : "Comment gérez-vous les transitions ?"
    /// RÉPONSE : La méthode TransitionTo centralise tous les changements d'état.
    /// Elle log la transition (débogage), met à jour currentState, et réinitialise 
    /// les timers. La condition "if (currentState == newState) return" évite les 
    /// transitions inutiles (optimisation).
    /// </summary>
    private void TransitionTo(State newState)
    {
        if (currentState == newState) return; // Évite les transitions inutiles

        Debug.Log($" TRANSITION FSM : {currentState}  --->  {newState}");
        currentState = newState;
        waitTimer = 0f; // Réinitialise le timer
    }

    // =========================================================================
    // COMPORTEMENTS DES ÉTATS
    // =========================================================================
    /// <summary>
    /// Exécute l'action associée à l'état actuel.
    /// Chaque état délègue le mouvement à A* via pathfinding.MoveTo().
    /// </summary>
    private void ExecuteStateBehavior()
    {
        switch (currentState)
        {
            case State.Wait:
                // Le garde ne fait rien, attend le timer ou un stimulus
                break;

            case State.Move: // Patrouille entre les waypoints
                if (patrolWaypoints.Length > 0)
                {
                    // Utilise A* pour aller au prochain waypoint
                    pathfinding.MoveTo(patrolWaypoints[currentWaypointIndex].position);

                    // Si arrivé au waypoint, passe au suivant (boucle)
                    if (pathfinding.HasReachedDestination())
                    {
                        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                        TransitionTo(State.Wait); // Petite pause entre chaque waypoint
                    }
                }
                break;

            case State.Follow: // Poursuit le Bandit
                // Utilise A* pour suivre la position actuelle du Bandit
                // Le chemin est recalculé à chaque frame (suivi temps réel)
                pathfinding.MoveTo(bandit.position);
                break;

            case State.Attack: // Attaque le Bandit
                // Le garde s'arrête (pas de MoveTo)
                // Ici on pourrait déclencher une animation d'attaque
                break;

            case State.RunAway: // Fuit le Bandit
                // Calcule un point de fuite à l'opposé du Bandit
                Vector3 fleeDirection = (transform.position - bandit.position).normalized;
                Vector3 fleeTarget = transform.position + (fleeDirection * 10f); // Fuit de 10m
                pathfinding.MoveTo(fleeTarget);
                break;
        }
    }
}
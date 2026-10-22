using UnityEngine;

public class GuardFSM : MonoBehaviour
{
    // --- RÉFÉRENCES ---
    public Transform bandit;                  // La cible (PP dans le cours)
    public AStarPathfinding pathfinding;      // Pour utiliser A* dans les états
    public Transform[] patrolWaypoints;       // Points de patrouille

    // --- PARAMÈTRES DES ÉVÈNEMENTS (Adaptés en unités Unity) ---
    // Event 1: distance > 15
    public float distFar = 15f;
    // Event 2: 8 < distance <= 15
    public float distMedium = 8f;
    // Event 3: 3 < distance <= 8
    public float distClose = 3f;
    // Event 4/6: distance <= 3 OU PP_attacking

    // --- ÉTATS DE LA FSM (Diapo 9) ---
    private enum State { Wait, Move, Follow, Attack, RunAway }
    private State currentState = State.Wait;

    // Variables internes
    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;
    public float waitDuration = 3f;

    void Start()
    {
        // Démarrage en attente
        currentState = State.Wait;
    }

    void Update()
    {
        // 1. Calcul de la distance (Condition de base pour les événements)
        float distanceToBandit = Vector3.Distance(transform.position, bandit.position);
        bool banditIsAttacking = bandit.GetComponent<BanditController>().isAttacking;

        // 2. GESTION DES TRANSITIONS (Évènements du cours)
        switch (currentState)
        {
            case State.Wait:
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitDuration)
                {
                    TransitionTo(State.Move);
                }
                else if (distanceToBandit <= distMedium) // Event 2
                {
                    TransitionTo(State.Follow);
                }
                break;

            case State.Move: // Patrouille
                if (distanceToBandit <= distMedium && distanceToBandit > distClose) // Event 2
                {
                    TransitionTo(State.Follow);
                }
                else if (distanceToBandit <= distClose) // Event 3
                {
                    TransitionTo(State.Attack);
                }
                break;

            case State.Follow: // Poursuite
                if (distanceToBandit > distFar) // Event 1
                {
                    TransitionTo(State.Move);
                }
                else if (distanceToBandit <= distClose) // Event 3
                {
                    TransitionTo(State.Attack);
                }
                break;

            case State.Attack: // Attaque
                if (banditIsAttacking || distanceToBandit <= distClose) // Event 6 ou 4
                {
                    TransitionTo(State.RunAway);
                }
                else if (distanceToBandit > distMedium) // Event 2 (le bandit s'est enfui)
                {
                    TransitionTo(State.Follow);
                }
                break;

            case State.RunAway: // Fuite
                if (distanceToBandit > distFar) // Event 1 (Hors de danger)
                {
                    TransitionTo(State.Move);
                }
                break;
        }

        // 3. EXÉCUTION DU COMPORTEMENT DE L'ÉTAT ACTUEL
        ExecuteStateBehavior();
    }

    // --- GESTION DES TRANSITIONS ---
    private void TransitionTo(State newState)
    {
        if (currentState == newState) return;

        Debug.Log($"?? TRANSITION: {currentState}  --->  {newState}");
        currentState = newState;
        waitTimer = 0f; // Reset du timer si on change d'état
    }

    // --- COMPORTEMENTS DES ÉTATS ---
    private void ExecuteStateBehavior()
    {
        switch (currentState)
        {
            case State.Wait:
                // Le garde ne fait rien, A* n'est pas sollicité
                break;

            case State.Move:
                // Aller au prochain waypoint
                if (patrolWaypoints.Length > 0)
                {
                    pathfinding.MoveTo(patrolWaypoints[currentWaypointIndex].position);

                    // Si on est arrivé au waypoint, passer au suivant
                    if (pathfinding.HasReachedDestination())
                    {
                        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                        TransitionTo(State.Wait); // Petite pause entre chaque waypoint (optionnel)
                    }
                }
                break;

            case State.Follow:
                // Suivre la position actuelle du bandit
                pathfinding.MoveTo(bandit.position);
                break;

            case State.Attack:
                // S'arrêter et simuler une attaque (pas de déplacement A*)
                // On pourrait déclencher une animation ici
                break;

            case State.RunAway:
                // Calculer un point de fuite à l'opposé du bandit
                Vector3 fleeDirection = (transform.position - bandit.position).normalized;
                Vector3 fleeTarget = transform.position + (fleeDirection * 10f); // Fuir de 10 unités
                pathfinding.MoveTo(fleeTarget);
                break;
        }
    }
}
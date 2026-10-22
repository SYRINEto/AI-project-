using UnityEngine;

public class GuardFSM : MonoBehaviour
{
    // Références
    public Transform bandit;
    public GridGenerator grid;
    public AStarPathfinding pathfinding; // Pour utiliser A* dans chaque état

    // Paramètres de détection
    public float viewDistance = 10f;
    public float hearDistance = 5f;
    public float dangerDistance = 2f;
    public float viewAngle = 90f;

    // États
    private enum GuardState { Patrol, Investigate, Chase, Flee }
    private GuardState currentState = GuardState.Patrol;

    // Waypoints de patrouille
    public Transform[] patrolPoints;
    private int currentPatrolIndex = 0;

    // Timer pour l'état Investigation
    private float investigateTimer = 0f;
    public float investigateDuration = 5f;
    private Vector3 lastKnownBanditPosition;

    void Update()
    {
        // 1. Détecter les stimuli
        float distanceToBandit = Vector3.Distance(transform.position, bandit.position);
        bool canSeeBandit = CanSeeTarget(bandit.position);

        // 2. Machine à états (Logique de transition)
        switch (currentState)
        {
            case GuardState.Patrol:
                if (canSeeBandit && distanceToBandit < viewDistance)
                {
                    TransitionTo(GuardState.Chase);
                }
                else if (distanceToBandit < hearDistance)
                {
                    lastKnownBanditPosition = bandit.position;
                    TransitionTo(GuardState.Investigate);
                }
                break;

            case GuardState.Investigate:
                investigateTimer += Time.deltaTime;
                if (canSeeBandit && distanceToBandit < viewDistance)
                {
                    TransitionTo(GuardState.Chase);
                }
                else if (investigateTimer > investigateDuration)
                {
                    TransitionTo(GuardState.Patrol);
                }
                break;

            case GuardState.Chase:
                if (distanceToBandit < dangerDistance)
                {
                    TransitionTo(GuardState.Flee);
                }
                else if (distanceToBandit > viewDistance * 1.5f)
                {
                    TransitionTo(GuardState.Patrol);
                }
                break;

            case GuardState.Flee:
                if (distanceToBandit > viewDistance)
                {
                    TransitionTo(GuardState.Patrol);
                }
                break;
        }

        // 3. Exécuter le comportement de l'état actuel
        ExecuteStateBehavior();
    }

    bool CanSeeTarget(Vector3 targetPos)
    {
        Vector3 directionToTarget = (targetPos - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToTarget);

        if (angle < viewAngle / 2f)
        {
            // Raycast pour vérifier s'il y a un obstacle entre le garde et le bandit
            if (!Physics.Raycast(transform.position, directionToTarget, Vector3.Distance(transform.position, targetPos)))
            {
                return true;
            }
        }
        return false;
    }

    void TransitionTo(GuardState newState)
    {
        Debug.Log($"?? Garde passe de {currentState} à {newState}");
        currentState = newState;
        investigateTimer = 0f;

        // Ici, on pourra appeler pathfinding.FindPath() pour chaque état
    }

    void ExecuteStateBehavior()
    {
        switch (currentState)
        {
            case GuardState.Patrol:
                // Aller vers le prochain waypoint
                MoveTo(patrolPoints[currentPatrolIndex].position);
                if (Vector3.Distance(transform.position, patrolPoints[currentPatrolIndex].position) < 1f)
                {
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                }
                break;

            case GuardState.Investigate:
                MoveTo(lastKnownBanditPosition);
                break;

            case GuardState.Chase:
                MoveTo(bandit.position);
                break;

            case GuardState.Flee:
                // Fuir dans la direction opposée au bandit
                Vector3 fleeDirection = (transform.position - bandit.position).normalized;
                Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                MoveTo(fleeTarget);
                break;
        }
    }

    void MoveTo(Vector3 targetPosition)
    {
        // Utiliser A* pour se déplacer vers la cible
        // (On appellera pathfinding.FindPath(transform.position, targetPosition))
    }
}
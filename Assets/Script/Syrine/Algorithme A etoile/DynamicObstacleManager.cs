using UnityEngine;

public class DynamicObstacleManager : MonoBehaviour
{
    [Header("Références")]
    public GridGenerator grid;
    public AStarPathfinding pathfinding; // Pour dire à l'agent de recalculer
    public Transform agent;
    public GameObject obstaclePrefab;

    [Header("Scénario Automatique")]
    public float timeToSpawn = 3f;   // L'obstacle apparaît après 3 secondes
    public float timeToMove = 8f;    // L'obstacle bouge après 8 secondes

    private GameObject currentObstacle;
    private float timer = 0f;
    private int phase = 0; // 0: attente, 1: apparu, 2: déplacé

    void Update()
    {
        timer += Time.deltaTime;

        // PHASE 1 : Faire apparaître l'obstacle automatiquement
        if (phase == 0 && timer >= timeToSpawn)
        {
            SpawnObstacle();
            phase = 1;
            timer = 0f;
        }
        // PHASE 2 : Déplacer l'obstacle automatiquement
        else if (phase == 1 && timer >= timeToMove)
        {
            MoveObstacle();
            phase = 2;
            timer = 0f;
        }
    }

    void SpawnObstacle()
    {
        if (currentObstacle != null) Destroy(currentObstacle);

        // L'obstacle apparaît à 4 cases devant l'agent
        Vector3 spawnPos = agent.position + agent.forward * 4f;
        spawnPos.y = 0.5f; // Ajuste la hauteur pour qu'il soit au sol

        currentObstacle = Instantiate(obstaclePrefab, spawnPos, Quaternion.identity);
        currentObstacle.layer = LayerMask.NameToLayer("Obstacles");

        // Mettre à jour la grille et le chemin
        grid.UpdateNodeAt(spawnPos);
        pathfinding.RecalculatePath();

        Debug.Log($"?? [{timer:F1}s] Obstacle apparu automatiquement devant l'agent !");
    }

    void MoveObstacle()
    {
        if (currentObstacle == null) return;

        // 1. Libérer l'ancienne case (elle redevient verte)
        Vector3 oldPos = currentObstacle.transform.position;
        grid.UpdateNodeAt(oldPos);

        // 2. Déplacer l'obstacle sur le côté (comme un patrouilleur)
        Vector3 newPos = oldPos + Vector3.right * 3f;
        newPos.y = 0.5f;
        currentObstacle.transform.position = newPos;

        // 3. Bloquer la nouvelle case et recalculer
        grid.UpdateNodeAt(newPos);
        pathfinding.RecalculatePath();

        Debug.Log($"?? [{timeToMove + timeToSpawn:F1}s] Obstacle déplacé dynamiquement !");
    }
}
using UnityEngine;

/// <summary>
/// =============================================================================
/// CLASSE BanditController : Contrôle le Bandit (Personnage Principal - PP)
/// =============================================================================
/// 
/// Rôle : Servir de stimulus pour la FSM du Garde.
/// Le Bandit peut être contrôlé manuellement (ZQSD) pour tester les réactions 
/// du Garde, ou avoir un comportement automatique.
/// 
/// QUESTION PROF : "Quel est le rôle du Bandit dans votre FSM ?"
/// RÉPONSE : Le Bandit est le "Personnage Principal" (PP) du cours. Sa position 
/// et son état (isAttacking) sont les stimuli qui déclenchent les événements 
/// de la FSM du Garde (Event1 à Event6). Sans Bandit, la FSM resterait bloquée 
/// dans l'état Wait/Move.
/// =============================================================================
public class BanditController : MonoBehaviour
{
    public float moveSpeed = 3f;

    /// <summary>
    /// Indique si le Bandit attaque le Garde.
    /// Déclenche l'Event6 du cours : PP_attacking = true ? Garde passe en Fuite.
    /// Activé avec la touche ESPACE.
    /// </summary>
    public bool isAttacking = false;

    void Update()
    {
        // Contrôle manuel avec ZQSD pour tester les réactions du Garde
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(h, 0, v).normalized;

        if (direction != Vector3.zero)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Simulation d'attaque avec la touche Espace (Event6 du cours)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isAttacking = !isAttacking;
            Debug.Log(isAttacking ? " Bandit ATTAQUE ! (Event6 déclenché)" : " Bandit arrête d'attaquer.");
        }
    }
}
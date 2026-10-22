using UnityEngine;

public class BanditController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public bool isAttacking = false; // Pour l'Event 6 du cours

    void Update()
    {
        // Déplacement manuel avec ZQSD pour tester les réactions du garde
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(h, 0, v).normalized;

        if (direction != Vector3.zero)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Simulation d'attaque avec la touche Espace
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isAttacking = !isAttacking;
            Debug.Log(isAttacking ? "?? Bandit ATTAQUE !" : "??? Bandit arrête d'attaquer.");
        }
    }
}
using UnityEngine;

public class Bandit : MonoBehaviour
{
    public float moveSpeed = 3f;

    void Update()
    {
        // Contrôle manuel pour tester (ZQSD)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(h, 0, v).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;

        // Rotation du bandit
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
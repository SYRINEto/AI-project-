using UnityEngine;

/// <summary>
/// Contrôleur du personnage jouable — GameObject « Agent » (le PP).
///
/// Aucune IA : c'est le joueur qui le pilote (ZQSD + Espace).
/// Son seul rôle dans le chapitre 3 est de déclencher les événements de la FSM
/// du bandit : distance (Event 1 à 5) et PP_attacking (Event 6).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BanditController : MonoBehaviour
{
    [Header("Déplacement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.8f;
    public float acceleration = 20f;
    public float rotationSpeed = 12f;

    [Header("Physique")]
    public float gravity = -20f;

    [Header("Combat")]
    [Tooltip("Touche qui déclenche PP_attacking (Event 6 de la FSM du bandit).")]
    public KeyCode attackKey = KeyCode.Space;
    [Tooltip("Durée pendant laquelle PP_attacking reste vrai.")]
    public bool useTimedAttack = false;
    public float attackDuration = 1.5f;

    [Header("Sécurité anti-chute")]
    public bool respawnIfFalling = true;
    public float fallLimitY = -5f;

    [Header("Débogage")]
    public bool drawStateGizmo = true;

    /// <summary>Lue par BanditFSM (Event 6 : PP_attacking).</summary>
    public bool isAttacking { get; private set; }

    public float CurrentSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
    public bool IsMoving => CurrentSpeed > 0.2f;

    CharacterController controller;
    Vector3 spawnPosition;
    Vector3 velocity;
    float verticalVelocity;
    float attackTimer;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (respawnIfFalling && transform.position.y < fallLimitY)
        {
            Respawn();
            return;
        }

        Vector3 desiredDirection = ReadKeyboard();
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude > 1f) desiredDirection.Normalize();

        Move(desiredDirection);
        HandleAttack();
    }

    Vector3 ReadKeyboard()
    {
        // « Horizontal » / « Vertical » couvrent ZQSD si l'Input Manager est en AZERTY.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return new Vector3(h, 0f, v);
    }

    void Move(Vector3 desiredDirection)
    {
        float targetSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) targetSpeed *= sprintMultiplier;

        Vector3 targetVelocity = desiredDirection * targetSpeed;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, acceleration * Time.deltaTime);

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -25f);
        }

        Vector3 motion = velocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        if (velocity.sqrMagnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void HandleAttack()
    {
        if (Input.GetKeyDown(attackKey))
        {
            isAttacking = !isAttacking;
            attackTimer = 0f;
            Debug.Log(isAttacking ? "PP ATTAQUE !" : "PP arrête d'attaquer.");
        }

        if (useTimedAttack && isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackDuration) isAttacking = false;
        }
    }

    /// <summary>Renvoie le PP à son point de départ (chute hors de la map).</summary>
    public void Respawn()
    {
        if (controller != null) controller.enabled = false;

        velocity = Vector3.zero;
        verticalVelocity = 0f;
        transform.position = spawnPosition + Vector3.up * 0.1f;

        if (controller != null) controller.enabled = true;

        Debug.LogWarning("[PP] Chute détectée -> retour au départ. Vérifie que le sol a un collider.");
    }

    /// <summary>API pour la simulation : force PP_attacking (pas utilisé en jeu).</summary>
    public void SetAttackingExternally(bool attacking)
    {
        isAttacking = attacking;
        attackTimer = 0f;
    }

    void OnDrawGizmos()
    {
        if (!drawStateGizmo) return;

        Gizmos.color = isAttacking ? Color.red : new Color(0.2f, 0.6f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.9f);
    }
}

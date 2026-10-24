using UnityEngine;

public class BoxerPunchPattern : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Animator animator;
    [SerializeField] Transform target;          // CenterEyeAnchor del jugador VR
    [SerializeField] Collider fistCollider;     // Opcional: collider del puño

    [Header("Patrón de golpes")]
    [SerializeField] float jabInterval = 5f;
    [SerializeField] float uppercutInterval = 21f;

    [Header("Rotación")]
    [SerializeField] float turnSpeed = 8f;

    float jabTimer;
    float uppercutTimer;

    static readonly int JabHash = Animator.StringToHash("Jab");
    static readonly int UppercutHash = Animator.StringToHash("Uppercut");

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (fistCollider) fistCollider.enabled = false;
    }

    void Update()
    {
        FaceTarget();

        jabTimer += Time.deltaTime;
        uppercutTimer += Time.deltaTime;

        // El uppercut tiene prioridad si coinciden (pasa cada 105 s)
        if (uppercutTimer >= uppercutInterval)
        {
            uppercutTimer = 0f;
            if (jabTimer >= jabInterval) jabTimer = 0f;
            animator.SetTrigger(UppercutHash);
        }
        else if (jabTimer >= jabInterval)
        {
            jabTimer = 0f;
            animator.SetTrigger(JabHash);
        }
    }

    void FaceTarget()
    {
        if (!target) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    // Se llaman desde Animation Events en los clips de golpe
    public void EnableFist()
    {
        if (fistCollider) fistCollider.enabled = true;
    }

    public void DisableFist()
    {
        if (fistCollider) fistCollider.enabled = false;
    }
}
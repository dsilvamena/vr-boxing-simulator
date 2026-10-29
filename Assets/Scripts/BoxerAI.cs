using UnityEngine;

public class BoxerAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Animator animator;
    [SerializeField] Transform target;        // CenterEyeAnchor
    [SerializeField] Collider fistCollider;   // Opcional

    [Header("Patrón de ataque")]
    [SerializeField] float fastInterval = 10f;
    [SerializeField] float slowInterval = 14f;
    [SerializeField] int swapEveryAttacks = 4;
    [SerializeField] float jitter = 1f;

    [Header("Reacciones a golpes")]
    [SerializeField] int hitsForReaction = 3;
    [SerializeField] int hitsForStun = 60;
    [SerializeField] float stunDuration = 6f;
    [SerializeField] float hitCooldown = 0.25f;

    [Header("Rotación")]
    [SerializeField] float turnSpeed = 8f;

    static readonly int PunchComboHash = Animator.StringToHash("PunchCombo");
    static readonly int CrossPunchHash = Animator.StringToHash("CrossPunch");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int StunnedHash = Animator.StringToHash("Stunned");

    bool comboIsFast = true;
    float comboTimer, comboWait;
    float crossTimer, crossWait;
    int attackCount;

    int totalHits, hitsSinceReaction;
    float lastHitTime = -10f;

    bool stunned;
    float stunTimer;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (fistCollider) fistCollider.enabled = false;
    }

    void Start()
    {
        ScheduleCombo();
        ScheduleCross();
    }

    void Update()
    {
        if (stunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) EndStun();
            return;
        }

        FaceTarget();

        comboTimer += Time.deltaTime;
        crossTimer += Time.deltaTime;

        if (!IsIdle()) return;

        float comboOver = comboTimer - comboWait;
        float crossOver = crossTimer - crossWait;

        if (comboOver < 0f && crossOver < 0f) return;

        if (comboOver >= crossOver) LaunchCombo();
        else LaunchCross();
    }

    void LaunchCombo()
    {
        animator.SetTrigger(PunchComboHash);
        ScheduleCombo();
        CountAttack();
    }

    void LaunchCross()
    {
        animator.SetTrigger(CrossPunchHash);
        ScheduleCross();
        CountAttack();
    }

    void CountAttack()
    {
        attackCount++;
        if (attackCount % swapEveryAttacks == 0)
            comboIsFast = !comboIsFast;
    }

    void ScheduleCombo()
    {
        comboTimer = 0f;
        comboWait = Interval(comboIsFast);
    }

    void ScheduleCross()
    {
        crossTimer = 0f;
        crossWait = Interval(!comboIsFast);
    }

    float Interval(bool fast)
    {
        float baseTime = fast ? fastInterval : slowInterval;
        return Mathf.Max(1f, baseTime + Random.Range(-jitter, jitter));
    }

    bool IsIdle()
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")
               && !animator.IsInTransition(0);
    }

    public void RegisterHit()
    {
        if (stunned) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;

        totalHits++;
        hitsSinceReaction++;

        if (totalHits >= hitsForStun)
        {
            StartStun();
            return;
        }

        if (hitsSinceReaction >= hitsForReaction)
        {
            hitsSinceReaction = 0;
            DisableFist();
            animator.SetTrigger(HitHash);
        }
    }

    void StartStun()
    {
        stunned = true;
        stunTimer = stunDuration;
        DisableFist();
        animator.SetBool(StunnedHash, true);
    }

    void EndStun()
    {
        stunned = false;
        totalHits = 0;
        hitsSinceReaction = 0;
        animator.SetBool(StunnedHash, false);
        ScheduleCombo();
        ScheduleCross();
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

    public void EnableFist()  { if (fistCollider) fistCollider.enabled = true; }
    public void DisableFist() { if (fistCollider) fistCollider.enabled = false; }

    [ContextMenu("Test Hit")] void TestHit() => RegisterHit();
}
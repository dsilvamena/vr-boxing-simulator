using System.Collections.Generic;
using UnityEngine;

public class BoxerAI : MonoBehaviour
{
    [System.Serializable]
    public class Attack
    {
        public string trigger = "PunchCombo";   // nombre del Trigger en el Animator
        [Min(0f)] public float weight = 1f;     // probabilidad relativa
        public float cooldown = 5f;             // segundos mínimos antes de repetirlo
        [HideInInspector] public float lastUsed = -999f;

        public Attack() { }
        public Attack(string t, float w, float c) { trigger = t; weight = w; cooldown = c; }
    }

    enum Mood { Calm, Normal, Aggressive }

    [Header("Referencias")]
    [SerializeField] Animator animator;
    [SerializeField] Transform target;          // CenterEyeAnchor
    [SerializeField] Collider fistCollider;     // Opcional

    [Header("Ataques disponibles")]
    [SerializeField] List<Attack> attacks = new List<Attack>
    {
        new Attack("PunchCombo", 1f, 5f),
        new Attack("CrossPunch", 1f, 4f)
    };
    [SerializeField] int maxRepeatsInRow = 2;

    [Header("Tiempo entre ataques (segundos en guardia)")]
    [SerializeField] float minWait = 4f;
    [SerializeField] float maxWait = 12f;

    [Header("Variaciones")]
    [Range(0f, 1f)] [SerializeField] float burstChance = 0.2f;   // golpe seguido casi enseguida
    [SerializeField] float burstWaitMin = 1.2f;
    [SerializeField] float burstWaitMax = 2.5f;
    [Range(0f, 1f)] [SerializeField] float restChance = 0.15f;   // pausa larga
    [SerializeField] float restMin = 3f;
    [SerializeField] float restMax = 7f;

    [Header("Estados de ánimo")]
    [SerializeField] float moodMinTime = 15f;
    [SerializeField] float moodMaxTime = 30f;
    [SerializeField] float calmMultiplier = 1.5f;        // espera más
    [SerializeField] float aggressiveMultiplier = 0.6f;  // espera menos

    [Header("Reacciones a golpes")]
    [SerializeField] int hitsForReaction = 3;
    [SerializeField] int hitsForStun = 60;
    [SerializeField] float stunDuration = 6f;
    [SerializeField] float hitCooldown = 0.25f;
    [Range(0f, 1f)] [SerializeField] float counterChance = 0.4f;  // contraataca rápido tras reaccionar

    [Header("Rotación")]
    [SerializeField] float turnSpeed = 8f;

    [Header("Debug")]
    [SerializeField] bool showDebug = true;

    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int StunnedHash = Animator.StringToHash("Stunned");

    float nextAttackTimer;
    int lastAttackIndex = -1;
    int repeatCount;

    Mood mood = Mood.Normal;
    float moodTimer;

    int totalHits, hitsSinceReaction;
    float lastHitTime = -10f;

    bool stunned;
    float stunTimer;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (fistCollider) fistCollider.enabled = false;

        if (attacks == null || attacks.Count == 0)
        {
            attacks = new List<Attack>
            {
                new Attack("PunchCombo", 1f, 5f),
                new Attack("CrossPunch", 1f, 4f)
            };
        }
    }

    void Start()
    {
        ChangeMood();
        ScheduleNext();
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

        moodTimer -= Time.deltaTime;
        if (moodTimer <= 0f) ChangeMood();

        if (!IsIdle()) return;          // solo cuenta la espera en guardia

        nextAttackTimer -= Time.deltaTime;
        if (nextAttackTimer > 0f) return;

        int index = PickAttack();
        if (index < 0)
        {
            nextAttackTimer = 0.5f;     // todos en cooldown, reintenta pronto
            return;
        }

        Launch(index);
        ScheduleNext();
    }

    // ---------- ELECCIÓN DE ATAQUE ----------
    int PickAttack()
    {
        List<int> candidates = new List<int>();
        float total = 0f;

        for (int i = 0; i < attacks.Count; i++)
        {
            Attack a = attacks[i];
            if (a.weight <= 0f) continue;
            if (Time.time - a.lastUsed < a.cooldown) continue;
            if (i == lastAttackIndex && repeatCount >= maxRepeatsInRow) continue;

            candidates.Add(i);
            total += a.weight;
        }

        if (candidates.Count == 0) return -1;

        float roll = Random.Range(0f, total);
        foreach (int i in candidates)
        {
            roll -= attacks[i].weight;
            if (roll <= 0f) return i;
        }
        return candidates[candidates.Count - 1];
    }

    void Launch(int index)
    {
        Attack a = attacks[index];
        animator.SetTrigger(a.trigger);
        a.lastUsed = Time.time;

        if (index == lastAttackIndex) repeatCount++;
        else { lastAttackIndex = index; repeatCount = 1; }

        if (showDebug) Debug.Log($"[Boxer] Ataque: {a.trigger} | Ánimo: {mood}");
    }

    // ---------- TIEMPOS ----------
    void ScheduleNext()
    {
        float wait = Random.Range(minWait, maxWait) * MoodMultiplier();

        if (mood != Mood.Calm && Random.value < burstChance)
            wait = Random.Range(burstWaitMin, burstWaitMax);
        else if (Random.value < restChance)
            wait += Random.Range(restMin, restMax);

        nextAttackTimer = Mathf.Max(0.8f, wait);

        if (showDebug) Debug.Log($"[Boxer] Próximo ataque en {nextAttackTimer:F1}s");
    }

    float MoodMultiplier()
    {
        switch (mood)
        {
            case Mood.Calm: return calmMultiplier;
            case Mood.Aggressive: return aggressiveMultiplier;
            default: return 1f;
        }
    }

    void ChangeMood()
    {
        float r = Random.value;
        mood = r < 0.25f ? Mood.Calm : (r < 0.75f ? Mood.Normal : Mood.Aggressive);
        moodTimer = Random.Range(moodMinTime, moodMaxTime);

        if (showDebug) Debug.Log($"[Boxer] Nuevo ánimo: {mood} ({moodTimer:F0}s)");
    }

    bool IsIdle()
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")
               && !animator.IsInTransition(0);
    }

    // ---------- GOLPES RECIBIDOS ----------
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

            // Después de reaccionar: o contraataca rápido, o se recupera un momento
            nextAttackTimer = Random.value < counterChance
                ? Random.Range(0.8f, 1.5f)
                : Random.Range(2f, 4f);
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
        mood = Mood.Aggressive;                     // vuelve enojado
        moodTimer = Random.Range(moodMinTime, moodMaxTime);
        nextAttackTimer = Random.Range(1.5f, 3f);
    }

    // ---------- UTILIDADES ----------
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
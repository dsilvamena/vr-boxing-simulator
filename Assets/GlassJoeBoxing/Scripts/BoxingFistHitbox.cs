using UnityEngine;

/// <summary>Put this on a hand/punch transform. A trigger sphere is added automatically if missing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class BoxingFistHitbox : MonoBehaviour
{
    [Min(0.01f)] public float radius = 0.11f;
    [Min(0f)] public float minimumPunchSpeed = 0.25f;
    [Min(0f)] public float damage = 10f;
    [Tooltip("If false, the fist must be moving fast enough to count as a punch.")]
    public bool countAnyContact;
    public bool useSweepForFastPunches = true;

    SphereCollider trigger;
    Rigidbody body;
    Vector3 previousPosition;
    Vector3 velocity;
    float lastHitTime = -999f;

    void Awake()
    {
        trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = radius;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        previousPosition = transform.position;
    }

    void OnValidate()
    {
        SphereCollider c = GetComponent<SphereCollider>();
        if (c != null) { c.isTrigger = true; c.radius = radius; }
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        velocity = (transform.position - previousPosition) / dt;
        if (useSweepForFastPunches && velocity.magnitude >= minimumPunchSpeed)
        {
            Vector3 delta = transform.position - previousPosition;
            if (delta.sqrMagnitude > 0.000001f && Physics.SphereCast(previousPosition, radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z), delta.normalized, out RaycastHit hit, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                TryHit(hit.collider);
        }
        previousPosition = transform.position;
    }

    void OnTriggerEnter(Collider other) { TryHit(other); }
    void OnTriggerStay(Collider other) { if (countAnyContact || velocity.magnitude >= minimumPunchSpeed) TryHit(other); }

    void TryHit(Collider other)
    {
        if (other == null || Time.time - lastHitTime < 0.12f) return;
        GlassJoeTarget target = other.GetComponentInParent<GlassJoeTarget>();
        if (target == null) return;
        if (!countAnyContact && velocity.magnitude < minimumPunchSpeed) return;
        Vector3 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
        if (target.ReceivePunch(transform.position, direction, damage, GetInstanceID())) lastHitTime = Time.time;
    }
}
